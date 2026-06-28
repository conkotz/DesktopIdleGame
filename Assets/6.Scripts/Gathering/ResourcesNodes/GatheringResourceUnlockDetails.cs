using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Resolves gathering skill-tree resource unlock rows (trees, deposits, ponds) to <see cref="NodeDefinition"/>
/// and builds extra details-panel copy.
/// </summary>
public static class GatheringResourceUnlockDetails
{
    private const string StatsColorHex = "#7A847C";

    private static readonly (SkillType skill, string unlockTitle, string nodeAssetName)[] UnlockNodeMappings =
    {
        (SkillType.Woodcutting, "Splitwood Trees", "splitwood_tree_node"),
        (SkillType.Woodcutting, "Hardwood Trees", "hardwood_tree_node"),
        (SkillType.Woodcutting, "Wildwood Trees", "wildwood_tree_node"),
        (SkillType.Woodcutting, "Ember Oak Trees", "ember_oak_tree_node"),
        (SkillType.Woodcutting, "Spiritwood Trees", "spiritwood_tree_node"),
        (SkillType.Mining, "Stone", "stone_deposit_node"),
        (SkillType.Mining, "Iron", "iron_deposit_node"),
        (SkillType.Mining, "Runestone", "runestone_deposit_node"),
        (SkillType.Mining, "Mythril", "mythril_deposit_node"),
        (SkillType.Mining, "Runite", "runite_deposit_node"),
        (SkillType.Mining, "Gemstone", "gemstone_deposit_node"),
        (SkillType.Mining, "Celestium", "celestium_deposit_node"),
        (SkillType.Fishing, "Pond Fishing", "small_pond_node"),
    };

    public static bool IsGatheringResourceUnlock(SkillDefinition skill, SkillUnlockDefinition unlock)
    {
        if (skill == null || unlock == null || unlock.unlockType != SkillUnlockType.Unlock)
            return false;

        if (skill.skillType != SkillType.Woodcutting
            && skill.skillType != SkillType.Mining
            && skill.skillType != SkillType.Fishing)
        {
            return false;
        }

        if (IsTierToolUnlock(unlock))
            return false;

        return TryGetMappedNodeAssetName(skill.skillType, unlock.title, out _);
    }

    public static bool TryResolveNode(
        SkillDefinition skill,
        SkillUnlockDefinition unlock,
        out NodeDefinition node)
    {
        node = null;
        if (!IsGatheringResourceUnlock(skill, unlock))
            return false;

        if (!TryGetMappedNodeAssetName(skill.skillType, unlock.title, out string nodeAssetName))
            return false;

        if (!TryGetNodeByAssetName(nodeAssetName, out node))
        {
            Debug.LogWarning(
                $"[GatheringResourceUnlockDetails] Missing node '{nodeAssetName}' for unlock '{unlock.title}'. "
                + "Ensure Assets/Resources/Databases/ResourceNodeCatalog.asset lists all resource nodes.");
            return false;
        }

        return true;
    }

    private static bool TryGetNodeByAssetName(string nodeAssetName, out NodeDefinition node)
    {
        node = null;
        if (string.IsNullOrWhiteSpace(nodeAssetName))
            return false;

        ResourceNodeCatalog catalog = ResourceNodeCatalog.Instance;
        if (catalog != null && catalog.TryGetByAssetName(nodeAssetName, out node) && node != null)
            return true;

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets($"{nodeAssetName} t:NodeDefinition");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<NodeDefinition>(path) is NodeDefinition editorNode
                && string.Equals(editorNode.name, nodeAssetName, StringComparison.OrdinalIgnoreCase))
            {
                node = editorNode;
                return true;
            }
        }
#endif

        return false;
    }

    public static string AppendResourceStatsRichText(
        string baseDescription,
        NodeDefinition node,
        SkillType skillType,
        CharacterStats stats,
        int unlockLevel)
    {
        if (node == null)
            return baseDescription ?? string.Empty;

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(baseDescription))
            sb.Append(baseDescription.Trim());

        string mainYield = BuildMainYieldLabel(node, unlockLevel);
        string gatherTime = FormatGatherInterval(node);
        string staminaCost = FormatEffectiveStaminaCost(node, skillType, stats);
        string resourceSupply = FormatResourceSupply(node);

        AppendStatLine(sb, "Main yield", mainYield);
        AppendStatLine(sb, "Base gather time", gatherTime);
        AppendStatLine(sb, "Stamina cost", staminaCost);
        AppendStatLine(sb, "Resource supply", resourceSupply);

        return sb.ToString();
    }

    private static bool IsTierToolUnlock(SkillUnlockDefinition unlock)
    {
        string title = unlock.title ?? string.Empty;
        string description = unlock.description ?? string.Empty;

        if (description.IndexOf("equip tier", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (title.StartsWith("T", StringComparison.OrdinalIgnoreCase)
            && (title.IndexOf("Axes", StringComparison.OrdinalIgnoreCase) >= 0
                || title.IndexOf("Pickaxe", StringComparison.OrdinalIgnoreCase) >= 0
                || title.IndexOf("Rod", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            return true;
        }

        return false;
    }

    private static bool TryGetMappedNodeAssetName(SkillType skillType, string unlockTitle, out string nodeAssetName)
    {
        nodeAssetName = null;
        if (string.IsNullOrWhiteSpace(unlockTitle))
            return false;

        string trimmedTitle = unlockTitle.Trim();
        for (int i = 0; i < UnlockNodeMappings.Length; i++)
        {
            (SkillType skill, string title, string assetName) = UnlockNodeMappings[i];
            if (skill != skillType)
                continue;
            if (!string.Equals(title, trimmedTitle, StringComparison.Ordinal))
                continue;

            nodeAssetName = assetName;
            return true;
        }

        return false;
    }

    private static string BuildMainYieldLabel(NodeDefinition node, int unlockLevel)
    {
        if (node.mainYieldEntries == null || node.mainYieldEntries.Length == 0)
            return "—";

        int levelGate = Mathf.Max(1, unlockLevel);
        if (node.actionType == NodeAction.Fishing)
        {
            var names = new List<string>();
            for (int i = 0; i < node.mainYieldEntries.Length; i++)
            {
                NodeDefinition.MainYieldEntry entry = node.mainYieldEntries[i];
                if (entry.item == null || string.IsNullOrWhiteSpace(entry.item.displayName))
                    continue;
                if (levelGate < Mathf.Max(1, entry.itemRequiredLevel))
                    continue;

                string name = entry.item.displayName.Trim();
                if (!names.Contains(name))
                    names.Add(name);
            }

            return names.Count > 0 ? string.Join(", ", names) : "—";
        }

        ItemDefinition primary = node.GetPrimaryMainYieldItem();
        if (primary != null && !string.IsNullOrWhiteSpace(primary.displayName))
            return primary.displayName.Trim();

        return "—";
    }

    private static string FormatGatherInterval(NodeDefinition node)
    {
        float min = Mathf.Max(0.01f, node.minInterval);
        float max = Mathf.Max(min, node.maxInterval);
        return $"{FormatIntervalValue(min)}-{FormatIntervalValue(max)}";
    }

    private static string FormatIntervalValue(float seconds)
    {
        if (Mathf.Approximately(seconds, Mathf.Round(seconds)))
            return Mathf.RoundToInt(seconds).ToString();
        return seconds.ToString("0.#");
    }

    private static string FormatEffectiveStaminaCost(NodeDefinition node, SkillType skillType, CharacterStats stats)
    {
        float staminaEfficiency = GetStaminaEfficiency(skillType, stats);
        float effectivePercent = GetEffectiveStaminaPercentOfMax(node, stats, staminaEfficiency);
        return $"{Mathf.RoundToInt(effectivePercent)}%";
    }

    private static float GetStaminaEfficiency(SkillType skillType, CharacterStats stats)
    {
        if (stats == null)
            return 0f;

        return skillType switch
        {
            SkillType.Woodcutting => stats.AxeStaminaEfficiency,
            SkillType.Mining => stats.PickaxeStaminaEfficiency,
            SkillType.Fishing => stats.RodStaminaEfficiency,
            _ => 0f
        };
    }

    private static float GetEffectiveStaminaPercentOfMax(
        NodeDefinition node,
        CharacterStats stats,
        float staminaEfficiency)
    {
        staminaEfficiency = Mathf.Clamp01(staminaEfficiency);
        float multiplier = 1f - staminaEfficiency;

        if (node.energyCostFlatPerSwing > 0f)
        {
            float maxEnergy = stats != null ? Mathf.Max(1f, stats.MaxEnergy) : 100f;
            float basePercent = node.energyCostFlatPerSwing / maxEnergy * 100f;
            return Mathf.Max(0f, basePercent * multiplier);
        }

        return Mathf.Max(0f, node.energyCostPercentOfMaxPerSwing * multiplier);
    }

    private static string FormatResourceSupply(NodeDefinition node)
    {
        if (node.depletionGatherCount <= 0)
            return "Infinite";

        return node.depletionGatherCount.ToString();
    }

    private static void AppendStatLine(StringBuilder sb, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (sb.Length > 0)
            sb.Append('\n');

        sb.Append("<color=").Append(StatsColorHex).Append('>');
        sb.Append(label).Append(": ").Append(value);
        sb.Append("</color>");
    }
}
