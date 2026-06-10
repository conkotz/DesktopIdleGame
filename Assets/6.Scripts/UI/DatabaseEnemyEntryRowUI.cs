using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>One enemy row in <see cref="DatabasePageUI"/>.</summary>
[DisallowMultipleComponent]
public sealed class DatabaseEnemyEntryRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Transform lootRow;
    [Tooltip("Optional. Auto-finds LootRow/LootTableBackground — cloned once per loot item.")]
    [SerializeField] private GameObject lootEntryBackgroundTemplate;

    private void Awake()
    {
        ResolveReferences();
    }

    public void Bind(EnemyDefinition enemy, SharedTooltipUI tooltip)
    {
        ResolveReferences();

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
