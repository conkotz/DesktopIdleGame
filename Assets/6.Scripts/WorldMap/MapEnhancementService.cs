using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class MapEnhancementService
{
    public const int SlotCount = 3;

    public static bool IsRolledMapEnhancement(string itemId) =>
        MapEnhancementRegistry.IsRuntimeItem(itemId);

    public static bool IsMapEnhancementTemplate(ItemDefinition def) =>
        def != null && def.IsMapEnhancement && !MapEnhancementRegistry.IsRuntimeItem(def.itemId);

    public static string CreateRolledDropItemId(ItemDefinition template, MapNodeDefinition map, ItemDatabase itemDb)
    {
        if (template == null || map == null || itemDb == null || !template.IsMapEnhancement)
            return template != null ? template.itemId : null;

        string runtimeId = $"{template.itemId.Trim()}{MapEnhancementRegistry.RuntimeSeparator}{Guid.NewGuid():N}";
        var instance = new MapEnhancementInstanceData
        {
            itemId = runtimeId,
            baseItemId = template.itemId.Trim(),
            sourceMapNodeId = map.nodeId?.Trim() ?? string.Empty,
            tier = (int)template.MapEnhancementTier,
            displayName = BuildDisplayName(map),
            mods = RollMods(template, map)
        };

        ItemDefinition runtime = UnityEngine.Object.Instantiate(template);
        runtime.name = runtimeId;
        runtime.itemId = runtimeId;
        runtime.displayName = instance.displayName;
        runtime.description = BuildDescription(instance);
        runtime.maxStack = 1;

        MapEnhancementRegistry.RegisterRuntimeItem(runtime, instance);
        return runtimeId;
    }

    public static string ResolveLootDropItemId(string itemId, MapNodeDefinition map, ItemDatabase itemDb)
    {
        if (string.IsNullOrWhiteSpace(itemId) || itemDb == null || map == null)
            return itemId;

        if (MapEnhancementRegistry.IsRuntimeItem(itemId))
            return itemId;

        ItemDefinition def = itemDb.Get(itemId);
        if (!IsMapEnhancementTemplate(def))
            return itemId;

        return CreateRolledDropItemId(def, map, itemDb);
    }

    public static bool CanEquipOnNode(string itemId, string nodeId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || string.IsNullOrWhiteSpace(nodeId))
            return false;

        if (!MapEnhancementRegistry.TryGetInstance(itemId, out MapEnhancementInstanceData data))
            return false;

        return string.Equals(data.sourceMapNodeId?.Trim(), nodeId.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryGetSourceMapNodeId(string itemId, out string nodeId)
    {
        nodeId = null;
        if (!MapEnhancementRegistry.TryGetInstance(itemId, out MapEnhancementInstanceData data))
            return false;

        if (string.IsNullOrWhiteSpace(data.sourceMapNodeId))
            return false;

        nodeId = data.sourceMapNodeId.Trim();
        return true;
    }

    public static bool TryEquipFromInventorySlotAuto(Inventory inventory, int slotIndex)
    {
        if (inventory == null)
            return false;

        Inventory.Slot slot = inventory.GetSlot(slotIndex);
        if (slot.IsEmpty || !TryGetSourceMapNodeId(slot.itemId, out string nodeId))
            return false;

        return TryEquipFromInventorySlot(inventory, slotIndex, nodeId);
    }

    public static bool TryEquipFromInventorySlot(Inventory inventory, int slotIndex, string nodeId)
    {
        if (inventory == null || string.IsNullOrWhiteSpace(nodeId))
            return false;

        Inventory.Slot slot = inventory.GetSlot(slotIndex);
        if (slot.IsEmpty || slot.amount <= 0)
            return false;

        if (!CanEquipOnNode(slot.itemId, nodeId))
            return false;

        WorldMapProgressManager progress = WorldMapProgressManager.Instance ??
            UnityEngine.Object.FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        if (progress == null)
            return false;

        if (!progress.TryEquipMapEnhancement(nodeId, slot.itemId, out _))
            return false;

        if (inventory.RemoveAmountAtSlot(slotIndex, 1) != 1)
        {
            progress.TryRemoveMapEnhancement(nodeId, progress.FindMapEnhancementSlot(nodeId, slot.itemId), out _);
            return false;
        }

        progress.NotifyProgressChangedAndSave();
        MapCombatScalingPopupUI.RefreshEnhancementReloadWarningIfOpen(nodeId);
        InventoryGridUI.RefreshAllGrids();
        return true;
    }

    public static bool TryRemoveToInventory(string nodeId, int slotIndex, Inventory inventory)
    {
        if (inventory == null || string.IsNullOrWhiteSpace(nodeId))
            return false;

        WorldMapProgressManager progress = WorldMapProgressManager.Instance ??
            UnityEngine.Object.FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        if (progress == null || !progress.TryRemoveMapEnhancement(nodeId, slotIndex, out string itemId))
            return false;

        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        if (inventory.Add(itemId, 1, notifyItemGainPopup: false))
        {
            progress.NotifyProgressChangedAndSave();
            MapCombatScalingPopupUI.RefreshEnhancementReloadWarningIfOpen(nodeId);
            InventoryGridUI.RefreshAllGrids();
            return true;
        }

        PlayerStorage storage = UnityEngine.Object.FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
        if (storage != null && storage.TryDepositAmountFromExternal(itemId, 1) == 1)
        {
            GameLog.Add(
                "Inventory was full — moved map enhancement to storage.",
                GameLog.CannotMessageColor);
            progress.NotifyProgressChangedAndSave();
            MapCombatScalingPopupUI.RefreshEnhancementReloadWarningIfOpen(nodeId);
            InventoryGridUI.RefreshAllGrids();
            return true;
        }

        PendingLootRecoveryStore.Enqueue(itemId, 1);
        GameLog.Add(
            "Inventory and storage are full — held the unequipped map enhancement until you free space.",
            GameLog.CannotMessageColor);
        progress.NotifyProgressChangedAndSave();
        MapCombatScalingPopupUI.RefreshEnhancementReloadWarningIfOpen(nodeId);
        InventoryGridUI.RefreshAllGrids();
        SaveManager.Instance?.NotifyInventoryChangedDebounced();
        return true;
    }

    /// <summary>Only extra enemy spawns are baked into the initial level spawn pass.</summary>
    public static bool ModTypeRequiresMapReentryToApply(MapEnhancementModType modType) =>
        modType == MapEnhancementModType.ExtraEnemySpawns;

    public static MapEnhancementAggregate BuildAggregate(string nodeId)
    {
        var aggregate = new MapEnhancementAggregate();
        if (string.IsNullOrWhiteSpace(nodeId))
            return aggregate;

        WorldMapProgressManager progress = WorldMapProgressManager.Instance ??
            UnityEngine.Object.FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        if (progress == null)
            return aggregate;

        IReadOnlyList<string> slots = progress.GetMapEnhancementSlots(nodeId);
        if (slots == null)
            return aggregate;

        for (int i = 0; i < slots.Count; i++)
        {
            string itemId = slots[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (!MapEnhancementRegistry.TryGetInstance(itemId, out MapEnhancementInstanceData data) || data.mods == null)
                continue;

            for (int m = 0; m < data.mods.Count; m++)
                ApplyModToAggregate(aggregate, data.mods[m]);
        }

        return aggregate;
    }

    public static MapEnhancementAggregate BuildAggregate(MapNodeDefinition node) =>
        node != null ? BuildAggregate(node.nodeId) : new MapEnhancementAggregate();

    public static string BuildDescription(MapEnhancementInstanceData data)
    {
        if (data == null)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("Permanent map enhancement.");
        sb.AppendLine("Equip on the map where it dropped.");
        sb.AppendLine();

        if (data.mods == null || data.mods.Count == 0)
        {
            sb.AppendLine("No modifiers rolled.");
            return sb.ToString().TrimEnd();
        }

        for (int i = 0; i < data.mods.Count; i++)
            sb.AppendLine("• " + FormatModLine(data.mods[i]));

        return sb.ToString().TrimEnd();
    }

    public static string FormatModLine(MapEnhancementMod mod) =>
        FormatModLine(mod, advancedView: false, rollConfigs: null);

    public static string FormatModLine(
        MapEnhancementMod mod,
        bool advancedView,
        MapEnhancementModRollConfig[] rollConfigs)
    {
        if (advancedView && rollConfigs != null && TryGetRollConfig(rollConfigs, mod.modType, out MapEnhancementModRollConfig cfg))
            return FormatModRangeLine(mod, cfg);

        switch (mod.modType)
        {
            case MapEnhancementModType.RespawnTimeReduction:
                return FormatRespawnTimeLine(mod.value);
            case MapEnhancementModType.ExtraEnemySpawns:
                return FormatExtraSpawnsLine(Mathf.RoundToInt(mod.value), mod.extraSpawnEnemyId);
            case MapEnhancementModType.LootBonus:
                return FormatLootBonusLine(Mathf.RoundToInt(mod.value * 100f));
            case MapEnhancementModType.EnemyDamageReduction:
                return FormatEnemyDamageLine(Mathf.RoundToInt(mod.value * 100f));
            case MapEnhancementModType.GoldBonus:
                return FormatEnemyGoldLine(Mathf.RoundToInt(mod.value * 100f));
            case MapEnhancementModType.EliteSpawnChanceBonus:
                return FormatEliteSpawnChanceLine(Mathf.RoundToInt(mod.value * 100f));
            case MapEnhancementModType.EliteSpawnDouble:
                return FormatEliteDoubleSpawnLine(Mathf.RoundToInt(mod.value * 100f));
            case MapEnhancementModType.EliteHealthReduction:
                return FormatEliteHealthLine(Mathf.RoundToInt(mod.value * 100f));
            default:
                return "Unknown modifier";
        }
    }

    public static string BuildSlotTooltipBody(string itemId, bool advancedView = false)
    {
        if (!MapEnhancementRegistry.TryGetInstance(itemId, out MapEnhancementInstanceData data)
            || data.mods == null
            || data.mods.Count == 0)
        {
            return string.Empty;
        }

        ItemDefinition template = ResolveTemplateForInstance(data);
        MapEnhancementModRollConfig[] rollConfigs = template != null
            ? template.GetMapEnhancementModRollConfigs()
            : MapEnhancementRollDefaults.CreateDefaultRollConfigs();

        var sb = new StringBuilder();
        for (int i = 0; i < data.mods.Count; i++)
        {
            if (i > 0)
                sb.AppendLine();

            sb.Append(FormatModLine(data.mods[i], advancedView, rollConfigs));
        }

        return sb.ToString();
    }

    /// <summary>Rolled map enhancement effect lines for inventory tooltips (rolled values, or roll ranges when Alt is held).</summary>
    public static string BuildInventoryEffectText(string itemId, bool advancedView)
    {
        if (!IsRolledMapEnhancement(itemId))
            return string.Empty;

        return BuildSlotTooltipBody(itemId, advancedView);
    }

    /// <summary>Possible modifier ranges for an unrolled map enhancement template (Alt-held).</summary>
    public static string BuildTemplateRollRangesText(ItemDefinition template)
    {
        if (template == null || !template.IsMapEnhancement)
            return string.Empty;

        MapEnhancementModRollConfig[] configs = template.GetMapEnhancementModRollConfigs();
        if (configs == null || configs.Length == 0)
            return string.Empty;

        var sb = new StringBuilder();
        for (int i = 0; i < configs.Length; i++)
        {
            MapEnhancementModRollConfig cfg = configs[i];
            if (cfg.rollWeight <= 0f)
                continue;

            if (sb.Length > 0)
                sb.Append('\n');

            sb.Append(FormatPossibleModRangeLine(cfg));
        }

        return sb.ToString();
    }

    private static ItemDefinition ResolveTemplateForInstance(MapEnhancementInstanceData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.baseItemId))
            return null;

        Inventory inventory = UnityEngine.Object.FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        ItemDatabase db = inventory ? inventory.GetItemDatabase() : Resources.Load<ItemDatabase>("Databases/ItemDatabase");
        return db != null ? db.Get(data.baseItemId) : null;
    }

    private static bool TryGetRollConfig(
        MapEnhancementModRollConfig[] configs,
        MapEnhancementModType modType,
        out MapEnhancementModRollConfig config)
    {
        config = default;
        if (configs == null)
            return false;

        for (int i = 0; i < configs.Length; i++)
        {
            if (configs[i].modType != modType)
                continue;

            config = configs[i];
            return true;
        }

        return false;
    }

    private static string FormatModRangeLine(MapEnhancementMod mod, MapEnhancementModRollConfig cfg)
    {
        switch (mod.modType)
        {
            case MapEnhancementModType.RespawnTimeReduction:
                return FormatRespawnTimeRangeLine(cfg.minValue, cfg.maxValue);
            case MapEnhancementModType.ExtraEnemySpawns:
                return FormatExtraSpawnsRangeLine(cfg.minValue, cfg.maxValue, mod.extraSpawnEnemyId);
            case MapEnhancementModType.LootBonus:
                return FormatLootBonusRangeLine(cfg.minValue, cfg.maxValue);
            case MapEnhancementModType.EnemyDamageReduction:
                return FormatEnemyDamageRangeLine(cfg.minValue, cfg.maxValue);
            case MapEnhancementModType.GoldBonus:
                return FormatEnemyGoldRangeLine(cfg.minValue, cfg.maxValue);
            case MapEnhancementModType.EliteSpawnChanceBonus:
                return FormatEliteSpawnChanceRangeLine(cfg.minValue, cfg.maxValue);
            case MapEnhancementModType.EliteSpawnDouble:
                return FormatEliteDoubleSpawnRangeLine(cfg.minValue, cfg.maxValue);
            case MapEnhancementModType.EliteHealthReduction:
                return FormatEliteHealthRangeLine(cfg.minValue, cfg.maxValue);
            default:
                return "Unknown modifier";
        }
    }

    private static string FormatPossibleModRangeLine(MapEnhancementModRollConfig cfg)
    {
        switch (cfg.modType)
        {
            case MapEnhancementModType.RespawnTimeReduction:
                return FormatRespawnTimeRangeLine(cfg.minValue, cfg.maxValue);
            case MapEnhancementModType.ExtraEnemySpawns:
                return FormatExtraSpawnsRangeLine(cfg.minValue, cfg.maxValue, enemyId: null);
            case MapEnhancementModType.LootBonus:
                return FormatLootBonusRangeLine(cfg.minValue, cfg.maxValue);
            case MapEnhancementModType.EnemyDamageReduction:
                return FormatEnemyDamageRangeLine(cfg.minValue, cfg.maxValue);
            case MapEnhancementModType.GoldBonus:
                return FormatEnemyGoldRangeLine(cfg.minValue, cfg.maxValue);
            case MapEnhancementModType.EliteSpawnChanceBonus:
                return FormatEliteSpawnChanceRangeLine(cfg.minValue, cfg.maxValue);
            case MapEnhancementModType.EliteSpawnDouble:
                return FormatEliteDoubleSpawnRangeLine(cfg.minValue, cfg.maxValue);
            case MapEnhancementModType.EliteHealthReduction:
                return FormatEliteHealthRangeLine(cfg.minValue, cfg.maxValue);
            default:
                return "Unknown modifier";
        }
    }

    private static string FormatRespawnTimeLine(float seconds) =>
        $"Enemy Respawn Time: -{seconds:0.#}s";

    private static string FormatRespawnTimeRangeLine(float minSeconds, float maxSeconds) =>
        $"Enemy Respawn Time: -{minSeconds:0.#}-{maxSeconds:0.#}s";

    private static string FormatExtraSpawnsLine(int count, string enemyId) =>
        $"Extra Spawns: +{count} {FormatEnemyName(enemyId)}";

    private static string FormatExtraSpawnsRangeLine(float minValue, float maxValue, string enemyId)
    {
        string count = FormatIntRange(minValue, maxValue);
        string enemy = string.IsNullOrWhiteSpace(enemyId) ? "enemies" : FormatEnemyName(enemyId);
        return $"Extra Spawns: +{count} {enemy}";
    }

    private static string FormatLootBonusLine(int percent) =>
        $"Enemy Loot Chance: +{percent}% (Non elite)";

    private static string FormatLootBonusRangeLine(float minFraction, float maxFraction)
    {
        int min = Mathf.RoundToInt(minFraction * 100f);
        int max = Mathf.RoundToInt(maxFraction * 100f);
        string percent = min == max ? $"+{min}%" : $"+{min}-{max}%";
        return $"Enemy Loot Chance: {percent} (Non elite)";
    }

    private static string FormatEnemyDamageLine(int percent) =>
        $"Enemy Damage: -{percent}%";

    private static string FormatEnemyDamageRangeLine(float minFraction, float maxFraction) =>
        $"Enemy Damage: -{FormatPercentRange(minFraction, maxFraction)}";

    private static string FormatEnemyGoldLine(int percent) =>
        $"Enemy Gold: +{percent}%";

    private static string FormatEnemyGoldRangeLine(float minFraction, float maxFraction) =>
        $"Enemy Gold: +{FormatPercentRange(minFraction, maxFraction)}";

    private static string FormatEliteSpawnChanceLine(int percent) =>
        $"Elite Spawn Chance: +{percent}%";

    private static string FormatEliteSpawnChanceRangeLine(float minFraction, float maxFraction) =>
        $"Elite Spawn Chance: +{FormatPercentRange(minFraction, maxFraction)}";

    private static string FormatEliteDoubleSpawnLine(int percent) =>
        $"Elite Double Spawn: {percent}%";

    private static string FormatEliteDoubleSpawnRangeLine(float minFraction, float maxFraction) =>
        $"Elite Double Spawn: {FormatPercentRange(minFraction, maxFraction)}";

    private static string FormatEliteHealthLine(int percent) =>
        $"Elite Health: -{percent}%";

    private static string FormatEliteHealthRangeLine(float minFraction, float maxFraction) =>
        $"Elite Health: -{FormatPercentRange(minFraction, maxFraction)}";

    /// <summary>Base elite chance multiplied by (1 + relative bonus). 10% base + 10% relative bonus = 11%.</summary>
    public static float GetEffectiveEliteSpawnChance(float baseChance, MapEnhancementAggregate aggregate)
    {
        float chance = Mathf.Max(0f, baseChance);
        if (aggregate == null || aggregate.eliteSpawnChanceBonusFraction <= 0.001f)
            return Mathf.Clamp01(chance);

        return Mathf.Clamp01(chance * (1f + aggregate.eliteSpawnChanceBonusFraction));
    }

    private static string FormatIntRange(float minValue, float maxValue)
    {
        int min = Mathf.RoundToInt(minValue);
        int max = Mathf.RoundToInt(maxValue);
        return min == max ? min.ToString() : $"{min}-{max}";
    }

    private static string FormatPercentRange(float minFraction, float maxFraction) =>
        $"{Mathf.RoundToInt(minFraction * 100f)}-{Mathf.RoundToInt(maxFraction * 100f)}%";

    public static string BuildAggregateEffectsText(string nodeId)
    {
        return BuildAggregateEffectsText(BuildAggregate(nodeId));
    }

    public static string BuildAggregateEffectsText(MapEnhancementAggregate aggregate)
    {
        if (aggregate == null || !aggregate.HasAnyEffect)
            return "None";

        var lines = new List<string>();

        if (aggregate.respawnTimeReductionSeconds > 0.001f)
            lines.Add(FormatRespawnTimeLine(aggregate.respawnTimeReductionSeconds));

        foreach (KeyValuePair<string, int> pair in aggregate.extraSpawnsByEnemyId)
        {
            if (pair.Value <= 0)
                continue;

            lines.Add(FormatExtraSpawnsLine(pair.Value, pair.Key));
        }

        if (aggregate.lootBonusFraction > 0.001f)
            lines.Add(FormatLootBonusLine(Mathf.RoundToInt(aggregate.lootBonusFraction * 100f)));

        if (aggregate.enemyDamageReductionFraction > 0.001f)
            lines.Add(FormatEnemyDamageLine(Mathf.RoundToInt(aggregate.enemyDamageReductionFraction * 100f)));

        if (aggregate.goldBonusFraction > 0.001f)
            lines.Add(FormatEnemyGoldLine(Mathf.RoundToInt(aggregate.goldBonusFraction * 100f)));

        if (aggregate.eliteSpawnChanceBonusFraction > 0.001f)
            lines.Add(FormatEliteSpawnChanceLine(Mathf.RoundToInt(aggregate.eliteSpawnChanceBonusFraction * 100f)));

        if (aggregate.eliteDoubleSpawnChance > 0.001f)
            lines.Add(FormatEliteDoubleSpawnLine(Mathf.RoundToInt(aggregate.eliteDoubleSpawnChance * 100f)));

        if (aggregate.eliteHealthReductionFraction > 0.001f)
            lines.Add(FormatEliteHealthLine(Mathf.RoundToInt(aggregate.eliteHealthReductionFraction * 100f)));

        return lines.Count == 0 ? "None" : string.Join("\n", lines);
    }

    private static string BuildDisplayName(MapNodeDefinition map)
    {
        string label = !string.IsNullOrWhiteSpace(map.displayName) ? map.displayName.Trim() : map.nodeId;
        return $"{label} Map Enhancement";
    }

    private static List<MapEnhancementMod> RollMods(ItemDefinition template, MapNodeDefinition map)
    {
        if (template == null)
            return new List<MapEnhancementMod>();

        int count = Mathf.Clamp(template.MapEnhancementModCount, 1, 2);
        MapEnhancementModRollConfig[] configs = template.GetMapEnhancementModRollConfigs();
        var pickedTypes = new HashSet<MapEnhancementModType>();
        var mods = new List<MapEnhancementMod>(count);

        for (int i = 0; i < count; i++)
        {
            if (!TryPickWeightedModConfig(configs, pickedTypes, out MapEnhancementModRollConfig config))
                break;

            pickedTypes.Add(config.modType);
            mods.Add(RollModFromConfig(config, map));
        }

        return mods;
    }

    private static bool TryPickWeightedModConfig(
        MapEnhancementModRollConfig[] configs,
        HashSet<MapEnhancementModType> excludeTypes,
        out MapEnhancementModRollConfig picked)
    {
        picked = default;
        if (configs == null || configs.Length == 0)
            return false;

        float totalWeight = 0f;
        for (int i = 0; i < configs.Length; i++)
        {
            MapEnhancementModRollConfig config = configs[i];
            if (!config.IsEnabled || (excludeTypes != null && excludeTypes.Contains(config.modType)))
                continue;

            totalWeight += config.rollWeight;
        }

        if (totalWeight <= 0.001f)
            return false;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < configs.Length; i++)
        {
            MapEnhancementModRollConfig config = configs[i];
            if (!config.IsEnabled || (excludeTypes != null && excludeTypes.Contains(config.modType)))
                continue;

            cumulative += config.rollWeight;
            if (roll > cumulative)
                continue;

            picked = config;
            return true;
        }

        return false;
    }

    private static MapEnhancementMod RollModFromConfig(MapEnhancementModRollConfig config, MapNodeDefinition map)
    {
        float min = Mathf.Min(config.minValue, config.maxValue);
        float max = Mathf.Max(config.minValue, config.maxValue);

        switch (config.modType)
        {
            case MapEnhancementModType.RespawnTimeReduction:
                return new MapEnhancementMod
                {
                    modType = config.modType,
                    value = UnityEngine.Random.Range(min, max)
                };
            case MapEnhancementModType.ExtraEnemySpawns:
                return new MapEnhancementMod
                {
                    modType = config.modType,
                    value = UnityEngine.Random.Range(Mathf.RoundToInt(min), Mathf.RoundToInt(max) + 1),
                    extraSpawnEnemyId = PickRandomEnemyIdFromMap(map)
                };
            case MapEnhancementModType.LootBonus:
            case MapEnhancementModType.EnemyDamageReduction:
            case MapEnhancementModType.GoldBonus:
            case MapEnhancementModType.EliteSpawnChanceBonus:
            case MapEnhancementModType.EliteSpawnDouble:
            case MapEnhancementModType.EliteHealthReduction:
                return new MapEnhancementMod
                {
                    modType = config.modType,
                    value = UnityEngine.Random.Range(min, max)
                };
            default:
                return new MapEnhancementMod { modType = config.modType, value = 0f };
        }
    }

    public static string PickRandomEnemyIdFromMap(MapNodeDefinition map)
    {
        if (map?.spawnGroupPlans == null)
            return string.Empty;

        var enemyIds = new List<string>();
        for (int p = 0; p < map.spawnGroupPlans.Count; p++)
        {
            LevelSpawnGroupPlan plan = map.spawnGroupPlans[p];
            if (plan?.spawns == null)
                continue;

            for (int s = 0; s < plan.spawns.Count; s++)
            {
                SpawnPrefabCount row = plan.spawns[s];
                if (row?.enemyDefinition == null || string.IsNullOrWhiteSpace(row.enemyDefinition.enemyId))
                    continue;

                enemyIds.Add(row.enemyDefinition.enemyId.Trim());
            }
        }

        if (enemyIds.Count == 0)
            return string.Empty;

        return enemyIds[UnityEngine.Random.Range(0, enemyIds.Count)];
    }

    private static void ApplyModToAggregate(MapEnhancementAggregate aggregate, MapEnhancementMod mod)
    {
        if (aggregate == null)
            return;

        switch (mod.modType)
        {
            case MapEnhancementModType.RespawnTimeReduction:
                aggregate.respawnTimeReductionSeconds += Mathf.Max(0f, mod.value);
                break;
            case MapEnhancementModType.ExtraEnemySpawns:
            {
                string enemyId = mod.extraSpawnEnemyId?.Trim();
                if (string.IsNullOrEmpty(enemyId))
                    break;

                int add = Mathf.Max(0, Mathf.RoundToInt(mod.value));
                if (add <= 0)
                    break;

                aggregate.extraSpawnsByEnemyId.TryGetValue(enemyId, out int current);
                aggregate.extraSpawnsByEnemyId[enemyId] = current + add;
                break;
            }
            case MapEnhancementModType.LootBonus:
                aggregate.lootBonusFraction += Mathf.Max(0f, mod.value);
                break;
            case MapEnhancementModType.EnemyDamageReduction:
                aggregate.enemyDamageReductionFraction += Mathf.Max(0f, mod.value);
                break;
            case MapEnhancementModType.GoldBonus:
                aggregate.goldBonusFraction += Mathf.Max(0f, mod.value);
                break;
            case MapEnhancementModType.EliteSpawnChanceBonus:
                aggregate.eliteSpawnChanceBonusFraction += Mathf.Max(0f, mod.value);
                break;
            case MapEnhancementModType.EliteSpawnDouble:
                aggregate.eliteDoubleSpawnChance += Mathf.Max(0f, mod.value);
                break;
            case MapEnhancementModType.EliteHealthReduction:
                aggregate.eliteHealthReductionFraction += Mathf.Max(0f, mod.value);
                break;
        }
    }

    private static string FormatEnemyName(string enemyId)
    {
        if (string.IsNullOrWhiteSpace(enemyId))
            return "enemies";

        return enemyId.Replace('_', ' ');
    }
}
