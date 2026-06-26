using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Persistent merchant stock quantities (DontDestroyOnLoad). Scene <see cref="Merchant"/> components bind here so
/// finite stock survives map reloads without racing <see cref="LevelSpawnDirector"/>'s deferred spawn coroutine.
/// </summary>
[DisallowMultipleComponent]
public sealed class MerchantStockRuntime : MonoBehaviour, ISaveable
{
    private static MerchantStockRuntime _instance;

    private readonly Dictionary<string, int[]> _quantitiesByMerchantId =
        new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);

    public static MerchantStockRuntime Instance => _instance;

    public static MerchantStockRuntime EnsureInstance()
    {
        if (_instance != null)
            return _instance;

        var existing = FindFirstObjectByType<MerchantStockRuntime>(FindObjectsInactive.Include);
        if (existing != null)
        {
            _instance = existing;
            return _instance;
        }

        var host = new GameObject(nameof(MerchantStockRuntime));
        _instance = host.AddComponent<MerchantStockRuntime>();
        DontDestroyOnLoad(host);
        return _instance;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _instance = null;

    public bool HasAnyStocks => _quantitiesByMerchantId.Count > 0;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    public int GetQuantity(string merchantId, MerchantStock stock, int entryIndex, Transform sceneTransform = null)
    {
        if (stock?.Items == null || entryIndex < 0 || entryIndex >= stock.Items.Count)
            return 0;

        int[] quantities = GetOrCreateQuantities(merchantId, stock, sceneTransform);
        if (entryIndex >= quantities.Length)
            return Mathf.Max(-1, stock.Items[entryIndex]?.defaultQuantity ?? 0);

        return quantities[entryIndex];
    }

    public void SetQuantity(string merchantId, MerchantStock stock, int entryIndex, int quantity, Transform sceneTransform = null)
    {
        if (stock?.Items == null || entryIndex < 0 || entryIndex >= stock.Items.Count)
            return;

        int[] quantities = GetOrCreateQuantities(merchantId, stock, sceneTransform);
        int clamped = Mathf.Max(-1, quantity);
        if (quantities[entryIndex] == clamped)
            return;

        quantities[entryIndex] = clamped;
    }

    /// <summary>Returns the live quantity array for this merchant (creates from defaults when missing).</summary>
    public int[] GetOrCreateQuantities(string merchantId, MerchantStock stock, Transform sceneTransform = null)
    {
        if (stock?.Items == null)
            return Array.Empty<int>();

        string canonicalId = NormalizeMerchantId(merchantId, stock);
        if (_quantitiesByMerchantId.TryGetValue(canonicalId, out int[] existing) && existing != null)
        {
            EnsureCapacity(canonicalId, stock);
            return _quantitiesByMerchantId[canonicalId];
        }

        int[] migrated = TryMigrateLegacyQuantities(
            canonicalId,
            stock,
            BuildLookupKeys(merchantId, stock, sceneTransform));
        if (migrated != null)
        {
            _quantitiesByMerchantId[canonicalId] = migrated;
            return migrated;
        }

        int[] seeded = SeedFromDefaults(stock);
        _quantitiesByMerchantId[canonicalId] = seeded;
        return seeded;
    }

    public void ResetToDefaults(string merchantId, MerchantStock stock)
    {
        if (stock?.Items == null)
            return;

        string canonicalId = NormalizeMerchantId(merchantId, stock);
        _quantitiesByMerchantId[canonicalId] = SeedFromDefaults(stock);
    }

    public void ResetToDefaultsForStockSaveKey(string stockSaveKey, MerchantStock stock)
    {
        if (stock == null || string.IsNullOrWhiteSpace(stockSaveKey))
            return;

        string merchantId = $"merchantStock:{stock.StockSaveKey}";
        ResetToDefaults(merchantId, stock);
    }

    public void NotifyAllMerchantsStockChanged()
    {
        Merchant[] merchants = FindObjectsByType<Merchant>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < merchants.Length; i++)
            merchants[i]?.NotifyStockChanged();
    }

    public void SaveInto(SaveData data)
    {
        if (data == null || !HasAnyStocks)
            return;

        data.merchantStocks ??= new List<SaveData.MerchantStockSave>();
        data.merchantStocks.Clear();

        foreach (KeyValuePair<string, int[]> kv in _quantitiesByMerchantId)
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || kv.Value == null || kv.Value.Length == 0)
                continue;

            data.merchantStocks.Add(new SaveData.MerchantStockSave
            {
                merchantId = kv.Key,
                quantities = (int[])kv.Value.Clone()
            });
        }
    }

    public void LoadFrom(SaveData data)
    {
        _quantitiesByMerchantId.Clear();

        if (data?.merchantStocks == null || data.merchantStocks.Count == 0)
            return;

        for (int i = 0; i < data.merchantStocks.Count; i++)
        {
            SaveData.MerchantStockSave row = data.merchantStocks[i];
            if (row == null || string.IsNullOrWhiteSpace(row.merchantId) ||
                row.quantities == null || row.quantities.Length == 0)
                continue;

            string id = row.merchantId.Trim();
            int[] copy = new int[row.quantities.Length];
            for (int q = 0; q < row.quantities.Length; q++)
                copy[q] = Mathf.Max(-1, row.quantities[q]);

            _quantitiesByMerchantId[id] = copy;
        }
    }

    /// <summary>Loads save rows only when the runtime has no stock yet (scene rehydrate safety net).</summary>
    public void LoadFromSaveIfEmpty(SaveData data)
    {
        if (HasAnyStocks)
            return;

        LoadFrom(data);
    }

    private int[] TryMigrateLegacyQuantities(string canonicalId, MerchantStock stock, List<string> lookupKeys)
    {
        if (lookupKeys == null)
            return null;

        for (int i = 0; i < lookupKeys.Count; i++)
        {
            string key = lookupKeys[i];
            if (string.IsNullOrWhiteSpace(key) ||
                string.Equals(key, canonicalId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!_quantitiesByMerchantId.TryGetValue(key, out int[] legacy) || legacy == null)
                continue;

            int[] migrated = new int[legacy.Length];
            Array.Copy(legacy, migrated, legacy.Length);
            _quantitiesByMerchantId[canonicalId] = migrated;
            _quantitiesByMerchantId.Remove(key);
            EnsureCapacity(canonicalId, stock);
            return _quantitiesByMerchantId[canonicalId];
        }

        return null;
    }

    private static int[] SeedFromDefaults(MerchantStock stock)
    {
        int count = stock.Items.Count;
        var quantities = new int[count];
        for (int i = 0; i < count; i++)
        {
            MerchantStock.Entry entry = stock.Items[i];
            quantities[i] = entry != null ? Mathf.Max(-1, entry.defaultQuantity) : 0;
        }

        return quantities;
    }

    private void EnsureCapacity(string merchantId, MerchantStock stock)
    {
        if (stock?.Items == null ||
            !_quantitiesByMerchantId.TryGetValue(merchantId, out int[] quantities) ||
            quantities == null)
            return;

        int want = stock.Items.Count;
        if (quantities.Length >= want)
            return;

        int[] resized = new int[want];
        Array.Copy(quantities, resized, quantities.Length);
        for (int i = quantities.Length; i < want; i++)
        {
            MerchantStock.Entry entry = stock.Items[i];
            resized[i] = entry != null ? Mathf.Max(-1, entry.defaultQuantity) : 0;
        }

        _quantitiesByMerchantId[merchantId] = resized;
    }

    private static string NormalizeMerchantId(string merchantId, MerchantStock stock)
    {
        if (!string.IsNullOrWhiteSpace(merchantId))
            return merchantId.Trim();

        if (stock != null)
            return $"merchantStock:{stock.StockSaveKey}";

        return "merchantStock:unknown";
    }

    internal static List<string> BuildLookupKeys(string merchantId, MerchantStock stock, Transform sceneTransform)
    {
        var keys = new List<string>();

        if (!string.IsNullOrWhiteSpace(merchantId))
            keys.Add(merchantId.Trim());

        if (stock != null)
            keys.Add($"merchantStock:{stock.StockSaveKey}");

        if (sceneTransform != null)
            keys.Add($"{sceneTransform.gameObject.scene.name}:{BuildPath(sceneTransform)}");

        return keys;
    }

    private static string BuildPath(Transform t)
    {
        var sb = new StringBuilder(t.name);
        Transform p = t.parent;
        while (p != null)
        {
            sb.Insert(0, '/');
            sb.Insert(0, p.name);
            p = p.parent;
        }

        return sb.ToString();
    }
}
