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
    [Tooltip(
        "Woodcutting / Mining: XP granted per successful main-yield tick (after bonuses). " +
        "Fishing: each main-yield entry has its own XP; if an entry's XP is 0, this value is used as fallback for that entry.")]
    [Min(0)] public int xpPerTick = 1;

    [Serializable]
    public class MainYieldEntry
    {
        public ItemDefinition item;
        [Min(1)]
        [Tooltip("Minimum skill level for this node's action type before this row can roll (node base requiredLevel still applies first).")]
        public int itemRequiredLevel = 1;
        [Min(0f)]
        [Tooltip(
            "Woodcutting / Mining: each eligible row rolls once; value is chance 0–100 (100 = always try). " +
            "Fishing: relative weight among all level-eligible rows in one pick per tick (e.g. 70 vs 30 ⇒ 70% vs 30%); only one main fish per tick before multipliers.")]
        public float chancePercent = 100f;
        [Min(0)]
        [Tooltip("Fishing only: XP for this item when it is the result of a successful roll. 0 = use the node's XP Per Tick as fallback. Ignored for woodcutting / mining.")]
        public int xpPerTick = 0;
        [Min(1)]
        [Tooltip("Woodcutting / Mining: pieces granted when this row succeeds (random between min and max, inclusive).")]
        public int amountMin = 1;
        [Min(1)]
        [Tooltip("Woodcutting / Mining: max pieces per successful roll for this row.")]
        public int amountMax = 1;
    }

    [Header("Main Yield (rolled each gather tick)")]
    [Tooltip(
        "Woodcutting / Mining: each eligible entry rolls its chance independently (can yield multiple types). " +
        "Fishing: exactly one main catch per tick, chosen at random weighted by each eligible row's Chance %.")]
    public MainYieldEntry[] mainYieldEntries;

    [Header("Gather Interval")]
    [Tooltip("Random seconds between gather ticks (always used).")]
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

    [Serializable]
    public class HiddenDrop
    {
        public ItemDefinition item;
        [Range(0f, 1f)] public float chance = 0f;
        [Min(1)] public int amountMin = 1;
        [Min(1)] public int amountMax = 1;
    }

    [Header("Hidden Drops (0% Base by Default)")]
    [Tooltip("Separate from bonus drops. Not scaled by Bonus Resource Find Chance. Hidden rolls run only after at least one bonus drop succeeds on the same gather tick; then each entry uses base chance plus ctx.hiddenChanceFlatBonus.")]
    public HiddenDrop[] hiddenDrops;

    [Header("Tool Requirement (Optional)")]
    public bool requiresTool = false;

    [Tooltip("Which tool must exist in toolbelt to gather this node (visual key match).")]
    public ToolKey requiredTool = ToolKey.None;

    [Tooltip("Message shown if tool is missing.")]
    public string missingToolMessage = "No tool available in toolbelt.";

    [Header("Energy Cost")]
    [Tooltip("When > 0, spend this flat energy per gather swing instead of % of max.")]
    [Min(0f)] public float energyCostFlatPerSwing = 0f;
    [Tooltip("Percent of maximum energy spent per gather swing (e.g. 10 = 10%). Ignored when Energy Cost Flat Per Swing is > 0.")]
    [Range(0f, 100f)] public float energyCostPercentOfMaxPerSwing = 10f;

    [Header("Depletion (Optional)")]
    [Tooltip("Successful main gather ticks (counted once per tick, before yield bonuses) before this node becomes depleted. 0 = infinite / no depletion.")]
    [Min(0)] public int depletionGatherCount = 0;
    [Tooltip("Seconds after depletion until the node is gatherable again. 0 = stays depleted until the scene reloads.")]
    [Min(0f)] public float depletionRegenSeconds = 60f;
    [Tooltip("Per-item chance / weight while depleted (stochastic rolls). 0.3 ≈ 70% fewer resources on average.")]
    [Range(0f, 1f)] public float depletedYieldMultiplier = 0.3f;

    public bool UsesDepletion => depletionGatherCount > 0;

    /// <summary>First main-yield item the player meets <see cref="MainYieldEntry.itemRequiredLevel"/> for (list order).</summary>
    public string GetPrimaryYieldItemIdForSkillLevel(int gatherSkillLevel)
    {
        if (mainYieldEntries == null)
            return string.Empty;

        int gate = gatherSkillLevel;
        for (int i = 0; i < mainYieldEntries.Length; i++)
        {
            MainYieldEntry e = mainYieldEntries[i];
            if (e.item == null || string.IsNullOrWhiteSpace(e.item.itemId))
                continue;
            if (gate < Mathf.Max(1, e.itemRequiredLevel))
                continue;
            return e.item.itemId;
        }

        return string.Empty;
    }

    /// <summary>First configured main-yield item id (ignores per-entry level; use <see cref="GetPrimaryYieldItemIdForSkillLevel"/> when skill matters).</summary>
    public string PrimaryYieldItemId
    {
        get
        {
            if (mainYieldEntries == null)
                return string.Empty;
            for (int i = 0; i < mainYieldEntries.Length; i++)
            {
                MainYieldEntry e = mainYieldEntries[i];
                if (e.item != null && !string.IsNullOrWhiteSpace(e.item.itemId))
                    return e.item.itemId;
            }

            return string.Empty;
        }
    }

    /// <summary>Legacy name for <see cref="PrimaryYieldItemId"/> (single canonical id for woodcutting-style secondaries).</summary>
    public string YieldItemId => PrimaryYieldItemId;

    public ItemDefinition GetPrimaryMainYieldItem()
    {
        if (mainYieldEntries == null)
            return null;
        for (int i = 0; i < mainYieldEntries.Length; i++)
        {
            if (mainYieldEntries[i].item != null && !string.IsNullOrWhiteSpace(mainYieldEntries[i].item.itemId))
                return mainYieldEntries[i].item;
        }

        return null;
    }

    public bool IsMainYieldPoolItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || mainYieldEntries == null)
            return false;
        for (int i = 0; i < mainYieldEntries.Length; i++)
        {
            ItemDefinition it = mainYieldEntries[i].item;
            if (it != null && string.Equals(it.itemId, itemId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public float GetNextInterval()
    {
        float lo = Mathf.Max(0.01f, minInterval);
        float hi = Mathf.Max(lo, maxInterval);
        return UnityEngine.Random.Range(lo, hi);
    }

    private static int RollMainYieldAmount(MainYieldEntry entry)
    {
        if (entry == null)
            return 1;

        int min = Mathf.Max(1, entry.amountMin);
        int max = Mathf.Max(min, entry.amountMax);
        return UnityEngine.Random.Range(min, max + 1);
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
    /// Woodcutting / Mining: rolls each eligible main-yield entry independently (chance 0–100). Each success adds 1.
    /// Fishing: picks exactly one eligible row using <see cref="MainYieldEntry.chancePercent"/> as relative weights, adds 1 of that item,
    /// and appends that row's XP to <paramref name="fishingXpPerSuccessOut"/> when non-null.
    /// </summary>
    public void RollMainYieldCounts(Dictionary<string, int> counts, int gatherSkillLevel, List<int> fishingXpPerSuccessOut)
    {
        if (counts == null)
            return;
        counts.Clear();
        fishingXpPerSuccessOut?.Clear();
        if (mainYieldEntries == null)
            return;

        if (actionType == NodeAction.Fishing)
        {
            if (TryPickWeightedFishingMainYield(gatherSkillLevel, out MainYieldEntry picked))
            {
                counts[picked.item.itemId] = RollMainYieldAmount(picked);
                if (fishingXpPerSuccessOut != null)
                {
                    int x = picked.xpPerTick > 0 ? picked.xpPerTick : xpPerTick;
                    fishingXpPerSuccessOut.Add(Mathf.Max(0, x));
                }
            }

            return;
        }

        for (int i = 0; i < mainYieldEntries.Length; i++)
        {
            MainYieldEntry e = mainYieldEntries[i];
            if (e.item == null || string.IsNullOrWhiteSpace(e.item.itemId))
                continue;

            if (gatherSkillLevel < Mathf.Max(1, e.itemRequiredLevel))
                continue;

            float p = Mathf.Clamp01(e.chancePercent / 100f);
            if (p <= 0f)
                continue;
            if (UnityEngine.Random.value >= p)
                continue;

            string id = e.item.itemId;
            counts.TryGetValue(id, out int c);
            counts[id] = c + RollMainYieldAmount(e);
        }
    }

    /// <summary>
    /// One weighted random choice among fishing rows the player meets <see cref="MainYieldEntry.itemRequiredLevel"/> for.
    /// <see cref="MainYieldEntry.chancePercent"/> is a relative weight (70 vs 30 ⇒ 70% / 30%). Rows with weight ≤ 0 are skipped.
    /// </summary>
    private bool TryPickWeightedFishingMainYield(int gatherSkillLevel, out MainYieldEntry picked)
    {
        picked = null;
        float total = 0f;
        for (int i = 0; i < mainYieldEntries.Length; i++)
        {
            MainYieldEntry e = mainYieldEntries[i];
            if (e.item == null || string.IsNullOrWhiteSpace(e.item.itemId))
                continue;
            if (gatherSkillLevel < Mathf.Max(1, e.itemRequiredLevel))
                continue;
            float w = Mathf.Max(0f, e.chancePercent);
            if (w <= 0f)
                continue;
            total += w;
        }

        if (total <= 0f)
            return false;

        float roll = UnityEngine.Random.Range(0f, total);
        float acc = 0f;
        for (int i = 0; i < mainYieldEntries.Length; i++)
        {
            MainYieldEntry e = mainYieldEntries[i];
            if (e.item == null || string.IsNullOrWhiteSpace(e.item.itemId))
                continue;
            if (gatherSkillLevel < Mathf.Max(1, e.itemRequiredLevel))
                continue;
            float w = Mathf.Max(0f, e.chancePercent);
            if (w <= 0f)
                continue;
            acc += w;
            if (roll < acc)
            {
                picked = e;
                return true;
            }
        }

        // Fallback for float edge cases (roll ~= total).
        for (int j = mainYieldEntries.Length - 1; j >= 0; j--)
        {
            MainYieldEntry e = mainYieldEntries[j];
            if (e.item == null || string.IsNullOrWhiteSpace(e.item.itemId))
                continue;
            if (gatherSkillLevel < Mathf.Max(1, e.itemRequiredLevel))
                continue;
            if (Mathf.Max(0f, e.chancePercent) <= 0f)
                continue;
            picked = e;
            return true;
        }

        return false;
    }

    /// <summary>Highest main-yield XP the player can currently earn from this node (for HUD hint). Fishing uses per-row XP; wood/mining use <see cref="xpPerTick"/>.</summary>
    public int GetBestMainYieldXpHint(int gatherSkillLevel)
    {
        if (actionType != NodeAction.Fishing)
            return Mathf.Max(0, xpPerTick);

        int best = 0;
        if (mainYieldEntries == null)
            return Mathf.Max(0, xpPerTick);

        for (int i = 0; i < mainYieldEntries.Length; i++)
        {
            MainYieldEntry e = mainYieldEntries[i];
            if (e.item == null || string.IsNullOrWhiteSpace(e.item.itemId))
                continue;
            if (gatherSkillLevel < Mathf.Max(1, e.itemRequiredLevel))
                continue;
            int x = e.xpPerTick > 0 ? e.xpPerTick : xpPerTick;
            best = Mathf.Max(best, Mathf.Max(0, x));
        }

        return Mathf.Max(best, Mathf.Max(0, xpPerTick));
    }

    /// <summary>Sum of all values in <paramref name="counts"/> (total main pieces this tick).</summary>
    public static int SumMainYieldCounts(Dictionary<string, int> counts)
    {
        if (counts == null || counts.Count == 0)
            return 0;
        int s = 0;
        foreach (var kv in counts)
            s += kv.Value;
        return s;
    }

    /// <summary>
    /// NEW: Roll drops into a list (no inventory mutation).
    /// PlayerController decides add vs drop-on-ground.
    /// </summary>
    public void PreviewDrops(List<Drop> outDrops)
    {
        PreviewDrops(outDrops, 0f, default, int.MaxValue);
    }

    /// <summary>
    /// Rolls drops with a multiplier applied to bonus drop chances only (not main yield).
    /// Formula: effectiveChance = baseChance * (1 + bonusFindChanceMultiplier).
    /// Hidden drops use default passive context and only roll after at least one bonus drop succeeds on this gather tick.
    /// </summary>
    public void PreviewDrops(List<Drop> outDrops, float bonusFindChanceMultiplier)
    {
        PreviewDrops(outDrops, bonusFindChanceMultiplier, default, int.MaxValue);
    }

    /// <summary>
    /// Same as <see cref="PreviewDrops(List{Drop}, float)"/> with extra woodcutting major-passive hooks:
    /// Forest's Favor adds a chance to grant +1 to each bonus drop, and Ancient Lumbercraft adds a flat
    /// chance bonus to hidden drops plus a chance to double their amount. Bonus find chance never affects hidden drops.
    /// Hidden drops are rolled only if at least one bonus drop succeeded on this gather tick.
    /// </summary>
    /// <param name="gatherSkillLevelForMainYield">Skill level for this node's action; main rows below their <see cref="MainYieldEntry.itemRequiredLevel"/> are skipped. Use <see cref="int.MaxValue"/> to preview all rows.</param>
    public void PreviewDrops(List<Drop> outDrops, float bonusFindChanceMultiplier, GatherDropContext ctx, int gatherSkillLevelForMainYield = int.MaxValue)
    {
        if (outDrops == null) return;

        AppendMainYieldPreviewDrops(outDrops, gatherSkillLevelForMainYield);

        // Bonus drops (independent chance)
        bool anyBonusDropProc = false;
        if (bonusDrops != null)
        {
            foreach (var b in bonusDrops)
            {
                if (b == null || b.item == null) continue;
                if (string.IsNullOrWhiteSpace(b.item.itemId)) continue;

                float effectiveChance = Mathf.Clamp01(b.chance * (1f + Mathf.Max(0f, bonusFindChanceMultiplier)));
                // Strict zero-floor: never roll if chance is non-positive (avoids the Random.value == 0 corner case
                // that would otherwise let a 0% drop fire on rare ties).
                if (effectiveChance <= 0f) continue;
                if (UnityEngine.Random.value < effectiveChance)
                {
                    anyBonusDropProc = true;
                    int amt = UnityEngine.Random.Range(b.amountMin, b.amountMax + 1);
                    if (ctx.bonusDropExtraOneChance > 0f && UnityEngine.Random.value < ctx.bonusDropExtraOneChance)
                        amt += 1;
                    outDrops.Add(new Drop(b.item.itemId, amt));
                }
            }
        }

        if (hiddenDrops != null && anyBonusDropProc)
        {
            foreach (var h in hiddenDrops)
            {
                if (h == null || h.item == null) continue;
                if (string.IsNullOrWhiteSpace(h.item.itemId)) continue;

                float effectiveHiddenChance = Mathf.Clamp01(h.chance + Mathf.Max(0f, ctx.hiddenChanceFlatBonus));
                // Strict zero-floor: never roll if chance is non-positive. Hidden drops should require an
                // explicit non-zero base or a hidden-specific bonus (e.g. Ancient Lumbercraft).
                if (effectiveHiddenChance <= 0f) continue;
                if (UnityEngine.Random.value < effectiveHiddenChance)
                {
                    int amt = UnityEngine.Random.Range(h.amountMin, h.amountMax + 1);
                    if (ctx.hiddenDoubleAmountChance > 0f && UnityEngine.Random.value < ctx.hiddenDoubleAmountChance)
                        amt *= 2;
                    outDrops.Add(new Drop(h.item.itemId, amt));
                }
            }
        }
    }

    private void AppendMainYieldPreviewDrops(List<Drop> outDrops, int gatherSkillLevelForMainYield)
    {
        if (mainYieldEntries == null)
            return;

        if (actionType == NodeAction.Fishing)
        {
            if (TryPickWeightedFishingMainYield(gatherSkillLevelForMainYield, out MainYieldEntry picked))
                outDrops.Add(new Drop(picked.item.itemId, RollMainYieldAmount(picked)));
            return;
        }

        for (int i = 0; i < mainYieldEntries.Length; i++)
        {
            MainYieldEntry e = mainYieldEntries[i];
            if (e.item == null || string.IsNullOrWhiteSpace(e.item.itemId))
                continue;
            if (gatherSkillLevelForMainYield < Mathf.Max(1, e.itemRequiredLevel))
                continue;
            float p = Mathf.Clamp01(e.chancePercent / 100f);
            if (p <= 0f)
                continue;
            if (UnityEngine.Random.value >= p)
                continue;
            outDrops.Add(new Drop(e.item.itemId, RollMainYieldAmount(e)));
        }
    }

    /// <summary>
    /// Optional per-roll modifiers for bonus and hidden drops (e.g. woodcutting Lv35 major passives).
    /// All fields default to 0 so non-woodcutting callers stay unaffected.
    /// </summary>
    public struct GatherDropContext
    {
        /// <summary>Flat chance added to each hidden drop's base chance (e.g. Ancient Lumbercraft), rolled only after a bonus drop succeeds on the same gather tick. Not affected by Bonus Resource Find Chance.</summary>
        public float hiddenChanceFlatBonus;

        /// <summary>Chance to double each successful hidden drop's amount (Treasure Hunter, 10%).</summary>
        public float hiddenDoubleAmountChance;

        /// <summary>Chance to add +1 to each successful bonus drop's amount (Forest's Favor / Hidden Riches).</summary>
        public float bonusDropExtraOneChance;
    }

    /// <summary>
    /// Old behaviour (still available if you want it anywhere else).
    /// </summary>
    public void RollDrops(Inventory inventory)
    {
        RollDrops(inventory, 0f, int.MaxValue);
    }

    public void RollDrops(Inventory inventory, float bonusFindChanceMultiplier, int gatherSkillLevel = int.MaxValue)
    {
        if (!inventory) return;

        if (mainYieldEntries != null)
        {
            if (actionType == NodeAction.Fishing)
            {
                if (TryPickWeightedFishingMainYield(gatherSkillLevel, out MainYieldEntry fp))
                    inventory.Add(fp.item.itemId, RollMainYieldAmount(fp));
            }
            else
            {
                for (int i = 0; i < mainYieldEntries.Length; i++)
                {
                    MainYieldEntry e = mainYieldEntries[i];
                    if (e.item == null || string.IsNullOrWhiteSpace(e.item.itemId))
                        continue;
                    if (gatherSkillLevel < Mathf.Max(1, e.itemRequiredLevel))
                        continue;
                    float p = Mathf.Clamp01(e.chancePercent / 100f);
                    if (p <= 0f || UnityEngine.Random.value >= p)
                        continue;
                    inventory.Add(e.item.itemId, RollMainYieldAmount(e));
                }
            }
        }

        bool anyBonusDropProc = false;
        if (bonusDrops != null)
        {
            foreach (var b in bonusDrops)
            {
                if (b == null || b.item == null) continue;
                if (string.IsNullOrWhiteSpace(b.item.itemId)) continue;

                float effectiveChance = Mathf.Clamp01(b.chance * (1f + Mathf.Max(0f, bonusFindChanceMultiplier)));
                if (effectiveChance <= 0f) continue;
                if (UnityEngine.Random.value < effectiveChance)
                {
                    anyBonusDropProc = true;
                    int amt = UnityEngine.Random.Range(b.amountMin, b.amountMax + 1);
                    inventory.Add(b.item.itemId, amt);
                }
            }
        }

        if (hiddenDrops != null && anyBonusDropProc)
        {
            foreach (var h in hiddenDrops)
            {
                if (h == null || h.item == null) continue;
                if (string.IsNullOrWhiteSpace(h.item.itemId)) continue;

                float effectiveHiddenChance = Mathf.Clamp01(h.chance);
                if (effectiveHiddenChance <= 0f) continue;
                if (UnityEngine.Random.value < effectiveHiddenChance)
                {
                    int amt = UnityEngine.Random.Range(h.amountMin, h.amountMax + 1);
                    inventory.Add(h.item.itemId, amt);
                }
            }
        }
    }

    public bool HasMainYield
    {
        get
        {
            if (mainYieldEntries == null || mainYieldEntries.Length == 0)
                return false;
            for (int i = 0; i < mainYieldEntries.Length; i++)
            {
                if (mainYieldEntries[i].item != null && !string.IsNullOrWhiteSpace(mainYieldEntries[i].item.itemId))
                    return true;
            }

            return false;
        }
    }
}
