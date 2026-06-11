using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One enemy row in <see cref="DatabasePageUI"/>.</summary>
[DisallowMultipleComponent]
public sealed class DatabaseEnemyEntryRowUI : MonoBehaviour
{
    private const float NameBandHeight = 50f;
    private const float LootBandHeight = 50f;
    private const float RowHeight = NameBandHeight + LootBandHeight;

    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Transform lootRow;
    [Tooltip("Optional. Auto-finds LootRow/LootTableBackground — cloned once per loot item.")]
    [SerializeField] private GameObject lootEntryBackgroundTemplate;

    private void Awake()
    {
        ResolveReferences();
        EnsureRowLayout();
    }

    public void Bind(EnemyDefinition enemy, SharedTooltipUI tooltip)
    {
        ResolveReferences();
        EnsureRowLayout();

        if (nameText)
            nameText.text = enemy != null ? enemy.displayName : string.Empty;

        ClearSpawnedLootEntries();

        if (enemy == null || lootRow == null || lootEntryBackgroundTemplate == null)
            return;

        SpawnGoldLootEntry(enemy, tooltip);

        List<LootDisplayEntry> lootEntries = CollectLootDisplayEntries(enemy);
        for (int i = 0; i < lootEntries.Count; i++)
        {
            LootDisplayEntry lootEntry = lootEntries[i];
            ItemDefinition item = lootEntry.item;
            if (item == null)
                continue;

            if (!TrySpawnLootEntryBackground($"LootTableBackground_{item.itemId}_{i}", out Transform iconTransform))
                continue;

            if (!iconTransform.TryGetComponent(out DatabaseLootTableEntryUI entryUi))
                entryUi = iconTransform.gameObject.AddComponent<DatabaseLootTableEntryUI>();

            entryUi.Bind(
                item,
                tooltip,
                lootEntry.dropChance,
                lootEntry.isEliteDrop,
                lootEntry.amountMin,
                lootEntry.amountMax);
        }

        EnsureGoldLootEntryIsFirst();
        lootEntryBackgroundTemplate.transform.SetAsLastSibling();
    }

    private void EnsureRowLayout()
    {
        RectTransform row = transform as RectTransform;
        if (row)
        {
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(0f, RowHeight);
        }

        if (TryGetComponent(out VerticalLayoutGroup rowLayout))
            rowLayout.enabled = false;

        LayoutElement rowElement = GetComponent<LayoutElement>();
        if (!rowElement)
            rowElement = gameObject.AddComponent<LayoutElement>();
        rowElement.minHeight = RowHeight;
        rowElement.preferredHeight = RowHeight;
        rowElement.flexibleWidth = 1f;

        ConfigureTopBand(nameText ? nameText.rectTransform : null, 0f, NameBandHeight);
        ConfigureTopBand(lootRow as RectTransform, NameBandHeight, LootBandHeight);
    }

    private static void ConfigureLootBackground(RectTransform background)
    {
        if (!background)
            return;

        background.anchorMin = new Vector2(0f, 0.5f);
        background.anchorMax = new Vector2(0f, 0.5f);
        background.pivot = new Vector2(0.5f, 0.5f);
        background.anchoredPosition = Vector2.zero;
        background.sizeDelta = new Vector2(50f, 50f);
    }

    private static void ConfigureTopBand(RectTransform band, float yOffsetFromTop, float height)
    {
        if (!band)
            return;

        band.anchorMin = new Vector2(0f, 1f);
        band.anchorMax = new Vector2(1f, 1f);
        band.pivot = new Vector2(0f, 1f);
        band.anchoredPosition = new Vector2(0f, -yOffsetFromTop);
        band.sizeDelta = new Vector2(0f, height);
    }

    private void ResolveReferences()
    {
        if (!nameText)
            nameText = transform.Find("NameText")?.GetComponent<TMP_Text>();
        if (!lootRow)
            lootRow = transform.Find("LootRow");
        if (!lootEntryBackgroundTemplate && lootRow != null)
            lootEntryBackgroundTemplate = lootRow.Find("LootTableBackground")?.gameObject;
    }

    private void SpawnGoldLootEntry(EnemyDefinition enemy, SharedTooltipUI tooltip)
    {
        if (!TrySpawnLootEntryBackground("LootTableBackground_Gold", out Transform iconTransform))
            return;

        if (!iconTransform.TryGetComponent(out DatabaseLootTableEntryUI entryUi))
            entryUi = iconTransform.gameObject.AddComponent<DatabaseLootTableEntryUI>();

        entryUi.BindGold(
            DatabaseLootTableEntryUI.ResolveGoldIconSprite(),
            tooltip,
            enemy.goldMin,
            enemy.goldMax);

        EnsureGoldLootEntryIsFirst();
    }

    private void EnsureGoldLootEntryIsFirst()
    {
        if (!lootRow)
            return;

        Transform goldBackground = lootRow.Find("LootTableBackground_Gold");
        if (!goldBackground)
            return;

        Transform label = lootRow.Find("LootTableLabel");
        int targetIndex = label != null ? label.GetSiblingIndex() + 1 : 0;
        if (goldBackground.GetSiblingIndex() != targetIndex)
            goldBackground.SetSiblingIndex(targetIndex);
    }

    private bool TrySpawnLootEntryBackground(string backgroundName, out Transform iconTransform)
    {
        iconTransform = null;
        if (!lootRow || !lootEntryBackgroundTemplate)
            return false;

        GameObject backgroundGo = Instantiate(lootEntryBackgroundTemplate, lootRow);
        backgroundGo.SetActive(true);
        backgroundGo.name = backgroundName;
        ConfigureLootBackground(backgroundGo.transform as RectTransform);

        iconTransform = backgroundGo.transform.Find("LootTableEntryItem");
        if (!iconTransform)
            return false;

        iconTransform.gameObject.SetActive(true);
        return true;
    }

    private void ClearSpawnedLootEntries()
    {
        if (!lootRow || !lootEntryBackgroundTemplate)
            return;

        Transform label = lootRow.Find("LootTableLabel");
        Transform template = lootEntryBackgroundTemplate.transform;

        for (int i = lootRow.childCount - 1; i >= 0; i--)
        {
            Transform child = lootRow.GetChild(i);
            if (child == null || child == label || child == template)
                continue;

            Destroy(child.gameObject);
        }

        lootEntryBackgroundTemplate.SetActive(false);
    }

    private readonly struct LootDisplayEntry
    {
        public readonly ItemDefinition item;
        public readonly float dropChance;
        public readonly bool isEliteDrop;
        public readonly int amountMin;
        public readonly int amountMax;

        public LootDisplayEntry(
            ItemDefinition item,
            float dropChance,
            bool isEliteDrop,
            int amountMin,
            int amountMax)
        {
            this.item = item;
            this.dropChance = dropChance;
            this.isEliteDrop = isEliteDrop;
            this.amountMin = amountMin;
            this.amountMax = amountMax;
        }
    }

    private static List<LootDisplayEntry> CollectLootDisplayEntries(EnemyDefinition enemy)
    {
        var entries = new List<LootDisplayEntry>();

        AppendLootEntries(enemy.loot, entries, isEliteTable: false);
        AppendLootEntries(enemy.eliteLoot, entries, isEliteTable: true);
        return entries;
    }

    private static void AppendLootEntries(
        List<EnemyLootEntry> lootTable,
        List<LootDisplayEntry> entries,
        bool isEliteTable)
    {
        if (lootTable == null)
            return;

        for (int i = 0; i < lootTable.Count; i++)
        {
            EnemyLootEntry entry = lootTable[i];
            ItemDefinition item = entry != null ? entry.item : null;
            if (item == null || string.IsNullOrWhiteSpace(item.itemId))
                continue;

            float chance = entry != null ? Mathf.Clamp01(entry.dropChance) : 0f;
            int amountMin = entry != null ? Mathf.Max(1, entry.amountMin) : 1;
            int amountMax = entry != null ? Mathf.Max(amountMin, entry.amountMax) : amountMin;
            entries.Add(new LootDisplayEntry(item, chance, isEliteTable, amountMin, amountMax));
        }
    }
}
