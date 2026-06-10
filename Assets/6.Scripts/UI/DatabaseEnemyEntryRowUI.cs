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

        List<ItemDefinition> lootItems = CollectUniqueLootItems(enemy);
        for (int i = 0; i < lootItems.Count; i++)
        {
            ItemDefinition item = lootItems[i];
            if (item == null)
                continue;

            GameObject backgroundGo = Instantiate(lootEntryBackgroundTemplate, lootRow);
            backgroundGo.SetActive(true);
            backgroundGo.name = $"LootTableBackground_{item.itemId}";
            ConfigureLootBackground(backgroundGo.transform as RectTransform);

            Transform iconTransform = backgroundGo.transform.Find("LootTableEntryItem");
            if (!iconTransform)
                continue;

            iconTransform.gameObject.SetActive(true);

            if (!iconTransform.TryGetComponent(out DatabaseLootTableEntryUI entryUi))
                entryUi = iconTransform.gameObject.AddComponent<DatabaseLootTableEntryUI>();

            entryUi.Bind(item, tooltip);
        }

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

    private static List<ItemDefinition> CollectUniqueLootItems(EnemyDefinition enemy)
    {
        var unique = new List<ItemDefinition>();
        var seenIds = new HashSet<string>();

        AppendLootItems(enemy.loot, unique, seenIds);
        AppendLootItems(enemy.eliteLoot, unique, seenIds);
        return unique;
    }

    private static void AppendLootItems(
        List<EnemyLootEntry> entries,
        List<ItemDefinition> unique,
        HashSet<string> seenIds)
    {
        if (entries == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            EnemyLootEntry entry = entries[i];
            ItemDefinition item = entry != null ? entry.item : null;
            if (item == null || string.IsNullOrWhiteSpace(item.itemId))
                continue;

            string itemId = item.itemId.Trim();
            if (!seenIds.Add(itemId))
                continue;

            unique.Add(item);
        }
    }
}
