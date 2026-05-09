using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Desktop Idle Game/Resource Node Definition", fileName = "NewNode")]
public class NodeDefinition : ScriptableObject
{
    [Header("UI")]
    public string displayName = "Resource";

    [Header("Action")]
    public NodeAction actionType = NodeAction.Woodcutting;

    [Header("Animation")]
    public PlayerController.PlayerAction gatherPlayerAction = PlayerController.PlayerAction.Woodcutting;

    [Header("Requirements")]
    public bool useLevelRequirement = false;
    [Min(1)] public int requiredLevel = 1;

    [Header("Experience")]
[Tooltip("XP granted per successful gather tick (based on MAIN yield only; bonus drops do not grant XP).")]
[Min(0)] public int xpPerTick = 1;

    [Header("Main Yield (Guaranteed)")]
    public ItemDefinition yieldItem;
    [Min(1)] public int yieldAmountMin = 1;
    [Min(1)] public int yieldAmountMax = 1;

    [Tooltip("Items gained per second (if not using random interval).")]
    public float ratePerSecond = 1f;

    [Header("Optional: Random Gather Interval (Overrides ratePerSecond if enabled)")]
    public bool useRandomInterval = false;
    public float minInterval = 5f;
    public float maxInterval = 10f;

    [Serializable]
    public class BonusDrop
    {
        public ItemDefinition item;
        [Range(0f, 1f)] public float chance = 0.01f;
        [Min(1)] public int amountMin = 1;
        [Min(1)] public int amountMax = 1;
    }

    [Header("Bonus Drops (Independent Rolls)")]
    public BonusDrop[] bonusDrops;

    [Header("Tool Requirement (Optional)")]
    public bool requiresTool = false;

    [Tooltip("Which tool must exist in toolbelt to gather this node (visual key match).")]
    public ToolKey requiredTool = ToolKey.None;

    [Tooltip("Message shown if tool is missing.")]
    public string missingToolMessage = "No tool available in toolbelt.";

    [Header("Energy Cost")]
    [Tooltip("Percent of maximum energy spent per gather swing (e.g. 10 = 10%). Cost scales with max energy so a bigger pool does not give more swings per full bar; raise stamina efficiency to reduce cost.")]
    [Range(0f, 100f)] public float energyCostPercentOfMaxPerSwing = 10f;

    [Header("Depletion (Optional)")]
    [Tooltip("Successful main gather ticks (counted once per tick, before yield bonuses) before this node becomes depleted. 0 = infinite / no depletion.")]
    [Min(0)] public int depletionGatherCount = 0;
    [Tooltip("Seconds after depletion until the node is gatherable again. 0 = stays depleted until the scene reloads.")]
    [Min(0f)] public float depletionRegenSeconds = 60f;
    [Tooltip("Per-item chance / weight while depleted (stochastic rolls). 0.3 ≈ 70% fewer resources on average.")]
    [Range(0f, 1f)] public float depletedYieldMultiplier = 0.3f;

    public string YieldItemId => yieldItem ? yieldItem.itemId : string.Empty;
    public bool UsesDepletion => depletionGatherCount > 0;

    public float GetNextInterval()
    {
        if (!useRandomInterval)
        {
            if (ratePerSecond <= 0f) return float.MaxValue;
            return 1f / ratePerSecond;
        }
        return UnityEngine.Random.Range(minInterval, maxInterval);
    }

    public string GetActionText()
    {
        return actionType switch
        {
            NodeAction.Mining => "Mining...",
            NodeAction.Woodcutting => "Chopping...",
            NodeAction.Fishing => "Fishing...",
            _ => string.Empty
        };
    }

    /// <summary>
    /// NEW: Roll drops into a list (no inventory mutation).
    /// PlayerController decides add vs drop-on-ground.
    /// </summary>
    public void PreviewDrops(List<Drop> outDrops)
    {
        PreviewDrops(outDrops, 0f);
    }

    /// <summary>
    /// Rolls drops with a multiplier applied ONLY to bonus drop chances.
    /// Formula: effectiveChance = baseChance * (1 + bonusFindChanceMultiplier)
    /// </summary>
    public void PreviewDrops(List<Drop> outDrops, float bonusFindChanceMultiplier)
    {
        if (outDrops == null) return;

        // Main yield (guaranteed)
        if (yieldItem != null && !string.IsNullOrWhiteSpace(yieldItem.itemId))
        {
            int amt = UnityEngine.Random.Range(yieldAmountMin, yieldAmountMax + 1);
            outDrops.Add(new Drop(yieldItem.itemId, amt));
        }

        // Bonus drops (independent chance)
        if (bonusDrops == null) return;

        foreach (var b in bonusDrops)
        {
            if (b == null || b.item == null) continue;
            if (string.IsNullOrWhiteSpace(b.item.itemId)) continue;

            float effectiveChance = Mathf.Clamp01(b.chance * (1f + Mathf.Max(0f, bonusFindChanceMultiplier)));
            if (UnityEngine.Random.value <= effectiveChance)
            {
                int amt = UnityEngine.Random.Range(b.amountMin, b.amountMax + 1);
                outDrops.Add(new Drop(b.item.itemId, amt));
            }
        }
    }

    /// <summary>
    /// Old behaviour (still available if you want it anywhere else).
    /// </summary>
    public void RollDrops(Inventory inventory)
    {
        RollDrops(inventory, 0f);
    }

    public void RollDrops(Inventory inventory, float bonusFindChanceMultiplier)
    {
        if (!inventory) return;

        if (yieldItem != null && !string.IsNullOrWhiteSpace(yieldItem.itemId))
        {
            int amt = UnityEngine.Random.Range(yieldAmountMin, yieldAmountMax + 1);
            inventory.Add(yieldItem.itemId, amt);
        }

        if (bonusDrops == null) return;

        foreach (var b in bonusDrops)
        {
            if (b == null || b.item == null) continue;
            if (string.IsNullOrWhiteSpace(b.item.itemId)) continue;

            float effectiveChance = Mathf.Clamp01(b.chance * (1f + Mathf.Max(0f, bonusFindChanceMultiplier)));
            if (UnityEngine.Random.value <= effectiveChance)
            {
                int amt = UnityEngine.Random.Range(b.amountMin, b.amountMax + 1);
                inventory.Add(b.item.itemId, amt);
            }
        }
    }

    public bool HasMainYield => yieldItem != null && !string.IsNullOrWhiteSpace(yieldItem.itemId);
    public int RollMainYieldAmount()
    {
        return UnityEngine.Random.Range(yieldAmountMin, yieldAmountMax + 1);
    }
}