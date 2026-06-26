using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight, scene-spanning store of XP and loot the player has gained during the current
/// session. The data is consumed by <see cref="TrackerWindowUI"/> (and any other reporters)
/// and reset when the player presses the tracker window's Reset button.
///
/// XP is collected automatically by listening to <see cref="SkillsManager.OnXpGained"/>.
/// Loot must be reported explicitly by gameplay systems via <see cref="RegisterLootGain"/>
/// because the inventory layer does not know which gameplay event added the item
/// (gather node vs. enemy drop vs. shop purchase).
/// </summary>
[DisallowMultipleComponent]
public class SessionTrackerData : MonoBehaviour
{
    public static SessionTrackerData Instance { get; private set; }

    /// <summary>Aggregated XP gains for one labelled source (e.g. "Splitwood Tree").</summary>
    public class XpSourceEntry
    {
        public string source;
        public SkillType lastSkill;
        public int totalXp;
        public int gainCount;
        public int lastGainAmount;
    }

    /// <summary>Aggregated loot gains for one labelled source (e.g. "Splitwood Tree" or "Spider").</summary>
    public class LootSourceEntry
    {
        public string source;
        public int totalValue;
        public int totalItemCount;
        /// <summary>Per-item totals so we can render "Splitwood Log x12, Bark x3, Birds Nest x1".</summary>
        public readonly Dictionary<string, int> itemAmounts = new Dictionary<string, int>(StringComparer.Ordinal);
        /// <summary>First-seen ordering so older items stay at the front of the list display.</summary>
        public readonly List<string> orderedItemIds = new List<string>();
        public int killCount;
    }

    public event Action OnDataChanged;
    public event Action OnSessionReset;

    /// <summary>
    /// Unscaled time captured the first time the player gains XP or loot after session start / reset.
    /// The timer intentionally stays paused at 0 until the first action so opening the tracker window
    /// while idle does not pollute per-hour rates with idle time.
    /// </summary>
    public float SessionStartUnscaledTime { get; private set; }
    /// <summary>True once the first XP or loot gain has been recorded for this session.</summary>
    public bool HasStartedTimer { get; private set; }
    public float ElapsedSeconds => HasStartedTimer ? Mathf.Max(0f, Time.unscaledTime - SessionStartUnscaledTime) : 0f;

    public int TotalXp { get; private set; }
    public int TotalLootValue { get; private set; }
    public int TotalLootItemCount { get; private set; }

    private readonly List<XpSourceEntry> _xpOrdered = new();
    private readonly Dictionary<string, XpSourceEntry> _xpBySource = new(StringComparer.Ordinal);
    private readonly List<LootSourceEntry> _lootOrdered = new();
    private readonly Dictionary<string, LootSourceEntry> _lootBySource = new(StringComparer.Ordinal);

    public IReadOnlyList<XpSourceEntry> XpEntries => _xpOrdered;
    public IReadOnlyList<LootSourceEntry> LootEntries => _lootOrdered;

    private SkillsManager _subscribedSkills;
    private Inventory _cachedInventoryForValueLookup;
    private ItemDatabase _cachedItemDatabaseForValueLookup;

    /// <summary>Returns the existing instance or creates a hidden persistent one on the fly.</summary>
    public static SessionTrackerData EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        SessionTrackerData found = FindFirstObjectByType<SessionTrackerData>(FindObjectsInactive.Include);
        if (found != null)
        {
            Instance = found;
            return Instance;
        }

        var go = new GameObject("[SessionTrackerData]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<SessionTrackerData>();
        return Instance;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureTrackerExistsForClosedWindow()
    {
        EnsureInstance();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        SessionStartUnscaledTime = Time.unscaledTime;
        HasStartedTimer = false;
    }

    private void OnEnable()
    {
        TrySubscribeSkills();
    }

    private void OnDisable()
    {
        TryUnsubscribeSkills();
    }

    private void OnDestroy()
    {
        TryUnsubscribeSkills();
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        // SkillsManager may not exist yet on first scene load.
        if (_subscribedSkills == null)
            TrySubscribeSkills();
    }

    private void TrySubscribeSkills()
    {
        if (_subscribedSkills != null)
            return;

        SkillsManager sm = SkillsManager.Instance;
        if (sm == null)
            return;

        sm.OnXpGained += HandleXpGained;
        _subscribedSkills = sm;
    }

    private void TryUnsubscribeSkills()
    {
        if (_subscribedSkills == null)
            return;

        _subscribedSkills.OnXpGained -= HandleXpGained;
        _subscribedSkills = null;
    }

    private void HandleXpGained(SkillType skill, int amount, string source)
    {
        if (amount <= 0)
            return;

        EnsureTimerStarted();

        // Combat skills share generic source strings ("Combat", "Defence") so we key by the skill name itself
        // ("Ranged", "Endurance"). Gathering skills carry per-node sources ("Splitwood Tree") and stay grouped by source.
        string key = ResolveXpDisplayName(skill, source);
        if (!_xpBySource.TryGetValue(key, out XpSourceEntry entry))
        {
            entry = new XpSourceEntry { source = key };
            _xpBySource[key] = entry;
            _xpOrdered.Add(entry);
        }

        entry.lastSkill = skill;
        entry.totalXp += amount;
        entry.gainCount += 1;
        entry.lastGainAmount = amount;
        TotalXp += amount;

        OnDataChanged?.Invoke();
    }

    /// <summary>Returns true for skills trained from combat damage (own attacks or incoming damage).</summary>
    public static bool IsCombatStyleSkill(SkillType skill)
    {
        return skill == SkillType.Melee
            || skill == SkillType.Ranged
            || skill == SkillType.Magic
            || skill == SkillType.Endurance;
    }

    /// <summary>Resolves the row label for an XP gain — uses the skill name for combat skills, source string otherwise.</summary>
    public static string ResolveXpDisplayName(SkillType skill, string source)
    {
        if (IsCombatStyleSkill(skill))
            return skill.ToString();
        if (string.IsNullOrWhiteSpace(source))
            return skill.ToString();
        return source.Trim();
    }

    /// <summary>
    /// Records loot picked up from a known source. Safe to call from gather ticks or the world-pickup path.
    /// Silently ignores empty sources/items so unsourced inventory adds (purchases, equipment swaps) do not
    /// pollute the tracker.
    /// </summary>
    public void RegisterLootGain(string source, string itemId, int amount)
    {
        if (amount <= 0)
            return;

        RegisterLootChange(source, itemId, amount);
    }

    /// <summary>
    /// Records a signed loot/value change (positive gains or negative losses such as furnace ore deposits).
    /// </summary>
    public void RegisterLootChange(string source, string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(itemId) || amount == 0)
            return;

        EnsureTimerStarted();

        string key = NormaliseSource(source);
        if (!_lootBySource.TryGetValue(key, out LootSourceEntry entry))
        {
            entry = new LootSourceEntry { source = key };
            _lootBySource[key] = entry;
            _lootOrdered.Add(entry);
        }

        if (!entry.itemAmounts.ContainsKey(itemId))
        {
            entry.itemAmounts[itemId] = 0;
            entry.orderedItemIds.Add(itemId);
        }
        entry.itemAmounts[itemId] += amount;
        entry.totalItemCount += amount;

        int unitValue = ResolveItemValue(itemId);
        int delta = unitValue * amount;
        entry.totalValue += delta;
        TotalLootValue += delta;
        TotalLootItemCount += amount;

        OnDataChanged?.Invoke();
    }

    /// <summary>Records an enemy kill for a loot tracker source label (e.g. "Spider").</summary>
    public void RegisterEnemyKill(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return;

        string key = NormaliseSource(source);
        if (!_lootBySource.TryGetValue(key, out LootSourceEntry entry))
        {
            entry = new LootSourceEntry { source = key };
            _lootBySource[key] = entry;
            _lootOrdered.Add(entry);
        }

        entry.killCount += 1;
        OnDataChanged?.Invoke();
    }

    /// <summary>Clears all accumulated XP/loot and pauses the elapsed-time clock until the next XP/loot gain.</summary>
    public void ResetSession()
    {
        _xpBySource.Clear();
        _xpOrdered.Clear();
        _lootBySource.Clear();
        _lootOrdered.Clear();
        TotalXp = 0;
        TotalLootValue = 0;
        TotalLootItemCount = 0;
        SessionStartUnscaledTime = Time.unscaledTime;
        HasStartedTimer = false;

        OnSessionReset?.Invoke();
        OnDataChanged?.Invoke();
    }

    /// <summary>Captures the unscaled-time anchor on the first XP or loot gain so the timer only counts active play.</summary>
    private void EnsureTimerStarted()
    {
        if (HasStartedTimer)
            return;
        SessionStartUnscaledTime = Time.unscaledTime;
        HasStartedTimer = true;
    }

    /// <summary>Resolves the per-unit gold value of an item using <see cref="Inventory.GetItemValue"/> when possible, falling back to <see cref="ItemDatabase"/>.</summary>
    private int ResolveItemValue(string itemId)
    {
        if (_cachedInventoryForValueLookup == null)
            _cachedInventoryForValueLookup = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

        if (_cachedInventoryForValueLookup != null)
            return _cachedInventoryForValueLookup.GetItemValue(itemId);

        if (_cachedItemDatabaseForValueLookup == null)
            _cachedItemDatabaseForValueLookup = FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);

        if (_cachedItemDatabaseForValueLookup != null)
        {
            var def = _cachedItemDatabaseForValueLookup.Get(itemId);
            if (def != null)
                return Mathf.Max(0, def.value);
        }

        return 0;
    }

    /// <summary>Computes the per-hour rate (extrapolated) for a running total over the elapsed session.</summary>
    public float ComputePerHour(int total)
    {
        float elapsed = ElapsedSeconds;
        if (elapsed < 0.001f || total <= 0)
            return 0f;
        return total * 3600f / elapsed;
    }

    private static string NormaliseSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return "Unknown";
        return source.Trim();
    }
}
