using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One enemy row in <see cref="DatabasePageUI"/>.</summary>
[DisallowMultipleComponent]
public sealed class DatabaseEnemyEntryRowUI : MonoBehaviour
{
    private const float IconColumnWidth = 200f;
    private const float IconFrameSize = 200f;
    private const float IconInsetTotal = 60f;
    private const float RowLabelWidth = 200f;
    private const float RowContentSpacing = 6f;
    private const float LootIconSize = 50f;
    private const float LootDropTextHeight = 18f;
    private const float NameBandHeight = 50f;
    private const float LootBandHeight = LootIconSize + LootDropTextHeight;
    private const float LocationsBandHeight = 50f;
    private const float AbilityBandHeight = 50f;
    private const string BossNamePrefixRichText = "<size=60%><color=#FF2B2B>- Boss -</color></size> ";

    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text combatProfileLabel;
    [SerializeField] private Image enemyIcon;
    [SerializeField] private Transform lootRow;
    [SerializeField] private Transform locationsRow;
    [SerializeField] private TMP_Text locationsText;
    [SerializeField] private Transform abilityRow;
    [Tooltip("Optional. Auto-finds LootRow/LootTableBackground — cloned once per loot item.")]
    [SerializeField] private GameObject lootEntryBackgroundTemplate;
    [Tooltip("Optional. Auto-finds AbilityRow/AbilityBackground — cloned once per enemy ability.")]
    [SerializeField] private GameObject abilityEntryBackgroundTemplate;

    private void Awake()
    {
        ResolveReferences();
        EnsureRowLayout(showAbilityRow: false);
    }

    public void Bind(
        EnemyDefinition enemy,
        SharedTooltipUI tooltip,
        WorldMapDefinition worldMap = null,
        RegionDefinition regionFilter = null)
    {
        ResolveReferences();

        List<EnemyAbilityDefinition> abilities = CollectAbilityDefinitions(enemy);
        bool showAbilityRow = abilities.Count > 0;
        EnsureRowLayout(showAbilityRow);

        if (nameText)
        {
            if (enemy == null)
                nameText.text = string.Empty;
            else
                nameText.text = enemy.isBossEnemy
                    ? $"{BossNamePrefixRichText}{enemy.displayName}"
                    : enemy.displayName;
        }

        ApplyEnemyIcon(enemy);
        WireEnemyIconTooltip(enemy, tooltip);
        ApplyCombatProfileLabel(enemy);

        if (locationsText)
        {
            string enemyId = enemy != null && !string.IsNullOrWhiteSpace(enemy.enemyId)
                ? enemy.enemyId.Trim()
                : string.Empty;
            locationsText.text = string.IsNullOrEmpty(enemyId)
                ? string.Empty
                : DatabaseRegionEnemyCatalog.FormatMapLocationsForEnemy(enemyId, worldMap, regionFilter);
        }

        ClearSpawnedLootEntries();
        ClearSpawnedAbilityEntries();
        SpawnAbilityEntries(abilities, tooltip, showAbilityRow);

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
        LayoutLootRowIcons();
        LayoutAbilityRowIcons();

        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }

    private static float ComputeRowHeight(bool showAbilityRow)
    {
        float contentHeight = NameBandHeight + LootBandHeight + LocationsBandHeight;
        if (showAbilityRow)
            contentHeight += AbilityBandHeight;
        return Mathf.Max(IconColumnWidth, contentHeight);
    }

    private void ApplyEnemyIcon(EnemyDefinition enemy)
    {
        if (!enemyIcon)
            return;

        Sprite sprite = DatabaseEnemyAvatarLookup.ResolveAvatarSprite(enemy);
        if (!sprite && enemy != null && enemy.icon)
            sprite = enemy.icon;

        enemyIcon.sprite = sprite;
        enemyIcon.type = Image.Type.Simple;
        enemyIcon.preserveAspect = true;
        enemyIcon.color = Color.white;
        enemyIcon.enabled = sprite != null;
        enemyIcon.gameObject.SetActive(true);
        ConfigureEnemyIconRect();
    }

    private void WireEnemyIconTooltip(EnemyDefinition enemy, SharedTooltipUI tooltip)
    {
        if (!enemyIcon)
            return;

        if (!enemyIcon.TryGetComponent(out DatabaseEnemyIconTooltipUI tooltipUi))
            tooltipUi = enemyIcon.gameObject.AddComponent<DatabaseEnemyIconTooltipUI>();

        tooltipUi.Bind(enemy, tooltip);
    }

    private void ApplyCombatProfileLabel(EnemyDefinition enemy)
    {
        if (!combatProfileLabel)
            return;

        if (enemy == null)
        {
            combatProfileLabel.text = string.Empty;
            return;
        }

        string label = EnduranceTrialUIHelpers.GetEnemyCombatProfileLabel(enemy);
        combatProfileLabel.text = label;
        combatProfileLabel.color = CombatProfileDisplay.GetColorForDisplayLabel(label);
        combatProfileLabel.raycastTarget = false;
    }

    private void EnsureRowLayout(bool showAbilityRow)
    {
        float rowHeight = ComputeRowHeight(showAbilityRow);

        RectTransform row = transform as RectTransform;
        if (row)
        {
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(0f, rowHeight);
        }

        if (TryGetComponent(out VerticalLayoutGroup rowLayout))
            rowLayout.enabled = false;

        LayoutElement rowElement = GetComponent<LayoutElement>();
        if (!rowElement)
            rowElement = gameObject.AddComponent<LayoutElement>();
        rowElement.minHeight = rowHeight;
        rowElement.preferredHeight = rowHeight;
        rowElement.flexibleWidth = 1f;

        RectTransform iconBorder = transform.Find("IconBorder") as RectTransform;
        if (iconBorder)
        {
            iconBorder.anchorMin = new Vector2(0f, 1f);
            iconBorder.anchorMax = new Vector2(0f, 1f);
            iconBorder.pivot = new Vector2(0f, 1f);
            iconBorder.anchoredPosition = Vector2.zero;
            iconBorder.sizeDelta = new Vector2(IconFrameSize, IconFrameSize);
        }

        RectTransform rightArea = transform.Find("RightArea") as RectTransform;
        if (rightArea)
        {
            rightArea.anchorMin = new Vector2(0f, 1f);
            rightArea.anchorMax = new Vector2(1f, 1f);
            rightArea.pivot = new Vector2(0f, 1f);
            rightArea.anchoredPosition = new Vector2(IconColumnWidth, 0f);
            rightArea.sizeDelta = new Vector2(-IconColumnWidth, rowHeight);
        }

        ConfigureTopBand(nameText ? nameText.rectTransform : null, 0f, NameBandHeight);
        ConfigureTopBand(lootRow as RectTransform, NameBandHeight, LootBandHeight);
        ConfigureTopBand(locationsRow as RectTransform, NameBandHeight + LootBandHeight, LocationsBandHeight);

        if (abilityRow)
        {
            abilityRow.gameObject.SetActive(showAbilityRow);
            if (showAbilityRow)
                ConfigureTopBand(abilityRow as RectTransform, NameBandHeight + LootBandHeight + LocationsBandHeight, AbilityBandHeight);
        }

        ConfigureEnemyIconRect();
        ConfigureLabeledRow(locationsRow as RectTransform, LocationsBandHeight);
        ConfigureLabeledRow(lootRow as RectTransform, LootBandHeight);
        if (showAbilityRow)
            ConfigureLabeledRow(abilityRow as RectTransform, AbilityBandHeight);

        if (locationsText)
        {
            locationsText.textWrappingMode = TextWrappingModes.Normal;
            locationsText.overflowMode = TextOverflowModes.Overflow;
            locationsText.horizontalAlignment = HorizontalAlignmentOptions.Left;
        }
    }

    private void ConfigureEnemyIconRect()
    {
        if (!enemyIcon)
            return;

        RectTransform iconRect = enemyIcon.rectTransform;
        float iconSize = Mathf.Max(1f, IconFrameSize - IconInsetTotal);

        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(iconSize, iconSize);
    }

    private static void DisableRowLayoutGroup(RectTransform row)
    {
        if (row != null && row.TryGetComponent(out HorizontalLayoutGroup layoutGroup))
            layoutGroup.enabled = false;
    }

    private static void ConfigureLabeledRow(RectTransform row, float bandHeight)
    {
        if (!row)
            return;

        DisableRowLayoutGroup(row);

        for (int i = 0; i < row.childCount; i++)
        {
            Transform child = row.GetChild(i);
            if (child == null || !child.gameObject.activeSelf)
                continue;

            RectTransform childRect = child as RectTransform;
            if (!childRect)
                continue;

            string childName = child.name;
            if (childName.StartsWith("LootTableBackground", System.StringComparison.Ordinal) ||
                childName.StartsWith("AbilityBackground", System.StringComparison.Ordinal))
            {
                continue;
            }

            if (childName.EndsWith("Label", System.StringComparison.OrdinalIgnoreCase))
            {
                ConfigureRowLabel(childRect, bandHeight);
                continue;
            }

            if (childName.EndsWith("Text", System.StringComparison.OrdinalIgnoreCase))
                ConfigureRowValueText(childRect, bandHeight);
        }
    }

    private static void ConfigureRowLabel(RectTransform labelRect, float bandHeight)
    {
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(RowLabelWidth, 0f);

        if (!labelRect.TryGetComponent(out LayoutElement layoutElement))
            layoutElement = labelRect.gameObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = RowLabelWidth;
        layoutElement.preferredWidth = RowLabelWidth;
        layoutElement.minHeight = bandHeight;
        layoutElement.preferredHeight = bandHeight;
    }

    private static void ConfigureRowValueText(RectTransform textRect, float bandHeight)
    {
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;
        textRect.offsetMin = new Vector2(RowLabelWidth + RowContentSpacing, 0f);
        textRect.offsetMax = Vector2.zero;

        if (textRect.TryGetComponent(out ContentSizeFitter fitter))
            fitter.enabled = false;

        if (textRect.TryGetComponent(out LayoutElement layoutElement))
        {
            layoutElement.minWidth = -1f;
            layoutElement.preferredWidth = -1f;
            layoutElement.flexibleWidth = 1f;
        }
    }

    private void LayoutLootRowIcons()
    {
        if (!lootRow)
            return;

        float x = RowLabelWidth + RowContentSpacing;
        Transform label = lootRow.Find("LootTableLabel");
        int startIndex = label != null ? label.GetSiblingIndex() + 1 : 0;

        for (int i = startIndex; i < lootRow.childCount; i++)
        {
            Transform child = lootRow.GetChild(i);
            if (child == null || !child.gameObject.activeSelf)
                continue;
            if (!child.name.StartsWith("LootTableBackground", System.StringComparison.Ordinal))
                continue;

            ConfigureLootIconSlot(child as RectTransform, x);
            x += LootIconSize + RowContentSpacing;
        }
    }

    private void LayoutAbilityRowIcons()
    {
        if (!abilityRow || !abilityRow.gameObject.activeSelf)
            return;

        float x = RowLabelWidth + RowContentSpacing;
        Transform label = abilityRow.Find("LootTableLabel");
        int startIndex = label != null ? label.GetSiblingIndex() + 1 : 0;

        for (int i = startIndex; i < abilityRow.childCount; i++)
        {
            Transform child = abilityRow.GetChild(i);
            if (child == null || !child.gameObject.activeSelf)
                continue;
            if (!child.name.StartsWith("AbilityBackground", System.StringComparison.Ordinal))
                continue;

            ConfigureIconSlot(child as RectTransform, x, AbilityBandHeight, LootIconSize);
            x += LootIconSize + RowContentSpacing;
        }
    }

    private static void ConfigureLootSlotDropChance(Transform lootBackground)
    {
        if (!lootBackground)
            return;

        Transform dropChance = lootBackground.Find("DropChanceText");
        if (!dropChance)
        {
            Transform entryItem = lootBackground.Find("LootTableEntryItem");
            if (entryItem)
                dropChance = entryItem.Find("DropChanceText");
        }

        if (!dropChance)
            return;

        if (dropChance.parent != lootBackground)
            dropChance.SetParent(lootBackground, false);

        RectTransform dropRect = dropChance as RectTransform;
        if (!dropRect)
            return;

        dropRect.anchorMin = new Vector2(0.5f, 1f);
        dropRect.anchorMax = new Vector2(0.5f, 1f);
        dropRect.pivot = new Vector2(0.5f, 1f);
        dropRect.anchoredPosition = Vector2.zero;
        dropRect.sizeDelta = new Vector2(LootIconSize, LootDropTextHeight);
        dropChance.SetAsLastSibling();
    }

    private static void ConfigureIconSlot(RectTransform slot, float x, float bandHeight, float size)
    {
        if (!slot)
            return;

        slot.anchorMin = new Vector2(0f, 0f);
        slot.anchorMax = new Vector2(0f, 0f);
        slot.pivot = new Vector2(0.5f, 0f);
        slot.anchoredPosition = new Vector2(x + size * 0.5f, 0f);
        slot.sizeDelta = new Vector2(size, size);

        if (!slot.TryGetComponent(out LayoutElement layoutElement))
            layoutElement = slot.gameObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = size;
        layoutElement.preferredWidth = size;
        layoutElement.minHeight = size;
        layoutElement.preferredHeight = size;
    }

    private static void ConfigureLootIconSlot(RectTransform slot, float x)
    {
        if (!slot)
            return;

        slot.anchorMin = new Vector2(0f, 0f);
        slot.anchorMax = new Vector2(0f, 0f);
        slot.pivot = new Vector2(0.5f, 0f);
        slot.anchoredPosition = new Vector2(x + LootIconSize * 0.5f, 0f);
        slot.sizeDelta = new Vector2(LootIconSize, LootBandHeight);

        if (!slot.TryGetComponent(out LayoutElement layoutElement))
            layoutElement = slot.gameObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = LootIconSize;
        layoutElement.preferredWidth = LootIconSize;
        layoutElement.minHeight = LootBandHeight;
        layoutElement.preferredHeight = LootBandHeight;

        ConfigureLootIconBackgroundFill(slot);

        Transform entryItem = slot.Find("LootTableEntryItem");
        if (entryItem is RectTransform entryRect)
        {
            ConfigureLootEntryItemIcon(entryRect);
            entryItem.SetSiblingIndex(1);
        }

        ConfigureLootSlotDropChance(slot);
    }

    private const float LootIconPadding = 4f;

    private static void ConfigureLootEntryItemIcon(RectTransform entryRect)
    {
        if (!entryRect)
            return;

        float iconBoxSize = LootIconSize - LootIconPadding * 2f;
        entryRect.anchorMin = new Vector2(0.5f, 0f);
        entryRect.anchorMax = new Vector2(0.5f, 0f);
        entryRect.pivot = new Vector2(0.5f, 0.5f);
        entryRect.anchoredPosition = new Vector2(0f, LootIconSize * 0.5f);
        entryRect.sizeDelta = new Vector2(iconBoxSize, iconBoxSize);

        if (entryRect.TryGetComponent(out Image iconImage))
        {
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = true;
        }
    }

    private static void ConfigureLootIconBackgroundFill(RectTransform slot)
    {
        slot.TryGetComponent(out Image rootImage);
        Color backgroundColor = rootImage != null
            ? rootImage.color
            : DefaultLootBackgroundColor;

        Transform fill = slot.Find("IconBackgroundFill");
        if (fill == null)
        {
            var fillGo = new GameObject("IconBackgroundFill", typeof(RectTransform), typeof(Image));
            fill = fillGo.transform;
            fill.SetParent(slot, false);
            fillGo.GetComponent<Image>().raycastTarget = false;
        }

        if (fill is RectTransform fillRect)
        {
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 0f);
            fillRect.pivot = new Vector2(0.5f, 0f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(0f, LootIconSize);
        }

        if (fill.TryGetComponent(out Image fillImage))
        {
            fillImage.color = backgroundColor;
            fillImage.raycastTarget = false;
        }

        if (rootImage != null)
            rootImage.enabled = false;

        fill.SetSiblingIndex(0);
    }

    private static readonly Color DefaultLootBackgroundColor = new Color(0.9547169f, 0.8938162f, 0.8268209f, 1f);

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
            nameText = transform.Find("RightArea/NameText")?.GetComponent<TMP_Text>()
                       ?? transform.Find("NameText")?.GetComponent<TMP_Text>();
        if (!combatProfileLabel)
            combatProfileLabel = transform.Find("CombatProfileLabel")?.GetComponent<TMP_Text>();
        if (!enemyIcon)
            enemyIcon = transform.Find("IconBorder/EnemyIcon")?.GetComponent<Image>()
                        ?? transform.Find("EnemyIcon")?.GetComponent<Image>();
        if (!lootRow)
            lootRow = transform.Find("RightArea/LootRow") ?? transform.Find("LootRow");
        if (!locationsRow)
            locationsRow = transform.Find("RightArea/LocationsRow") ?? transform.Find("LocationsRow");
        if (!locationsText)
            locationsText = transform.Find("RightArea/LocationsRow/LocationsText")?.GetComponent<TMP_Text>()
                            ?? transform.Find("LocationsRow/LocationsText")?.GetComponent<TMP_Text>();
        if (!lootEntryBackgroundTemplate && lootRow != null)
            lootEntryBackgroundTemplate = lootRow.Find("LootTableBackground")?.gameObject;
        if (!abilityRow)
            abilityRow = transform.Find("RightArea/AbilityRow") ?? transform.Find("AbilityRow");
        if (!abilityEntryBackgroundTemplate && abilityRow != null)
            abilityEntryBackgroundTemplate = abilityRow.Find("AbilityBackground")?.gameObject;
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

    private void SpawnAbilityEntries(
        List<EnemyAbilityDefinition> abilities,
        SharedTooltipUI tooltip,
        bool showAbilityRow)
    {
        if (!showAbilityRow || abilityRow == null || abilityEntryBackgroundTemplate == null || tooltip == null)
            return;

        for (int i = 0; i < abilities.Count; i++)
        {
            EnemyAbilityDefinition ability = abilities[i];
            if (ability == null)
                continue;

            string abilityKey = string.IsNullOrWhiteSpace(ability.abilityId)
                ? ability.name
                : ability.abilityId.Trim();

            if (!TrySpawnAbilityEntryBackground($"AbilityBackground_{abilityKey}_{i}", out GameObject backgroundGo))
                continue;

            if (!backgroundGo.TryGetComponent(out DatabaseEnemyAbilityEntryUI entryUi))
                entryUi = backgroundGo.AddComponent<DatabaseEnemyAbilityEntryUI>();

            entryUi.Bind(ability, tooltip);
        }

        abilityEntryBackgroundTemplate.transform.SetAsLastSibling();
    }

    private bool TrySpawnAbilityEntryBackground(string backgroundName, out GameObject backgroundGo)
    {
        backgroundGo = null;
        if (!abilityRow || !abilityEntryBackgroundTemplate)
            return false;

        backgroundGo = Instantiate(abilityEntryBackgroundTemplate, abilityRow);
        backgroundGo.SetActive(true);
        backgroundGo.name = backgroundName;
        return true;
    }

    private void ClearSpawnedAbilityEntries()
    {
        if (!abilityRow || !abilityEntryBackgroundTemplate)
            return;

        Transform label = abilityRow.Find("LootTableLabel");
        Transform template = abilityEntryBackgroundTemplate.transform;

        for (int i = abilityRow.childCount - 1; i >= 0; i--)
        {
            Transform child = abilityRow.GetChild(i);
            if (child == null || child == label || child == template)
                continue;

            Destroy(child.gameObject);
        }

        abilityEntryBackgroundTemplate.SetActive(false);
    }

    private static List<EnemyAbilityDefinition> CollectAbilityDefinitions(EnemyDefinition enemy)
    {
        var abilities = new List<EnemyAbilityDefinition>();
        if (enemy?.abilities == null)
            return abilities;

        for (int i = 0; i < enemy.abilities.Count; i++)
        {
            EnemyAbilityDefinition ability = enemy.abilities[i];
            if (ability == null)
                continue;

            abilities.Add(ability);
        }

        return abilities;
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
