using System.Collections.Generic;

/// <summary>
/// Persists "skill row / tree unlock glow" requests when <see cref="SkillsAbilitiesPageUI"/> is still on an
/// inactive menu tab (so it never subscribed to <see cref="SkillsManager.OnLevelUp"/>). <see cref="SkillsManager"/>
/// appends here on every level-up; the skills page merges on <see cref="SkillsAbilitiesPageUI"/> rebuild.
/// </summary>
public static class SkillsAbilitiesColdStartLevelUpGlow
{
    private static readonly HashSet<SkillType> s_pendingEntryGlow = new();
    private static readonly Dictionary<SkillType, HashSet<int>> s_pendingTreeGlowLevels = new();

    /// <summary>Called from <see cref="SkillsManager"/> before <c>OnLevelUp</c> listeners (page may be inactive).</summary>
    public static void Append(SkillType type, int newLevel)
    {
        s_pendingEntryGlow.Add(type);
        if (!s_pendingTreeGlowLevels.TryGetValue(type, out HashSet<int> levels))
        {
            levels = new HashSet<int>();
            s_pendingTreeGlowLevels[type] = levels;
        }

        levels.Add(newLevel);
    }

    /// <summary>
    /// Merges cold-start data into the active UI pending sets and clears the cold-start buffer.
    /// Call when building the skill list (first open or tab revisit).
    /// </summary>
    public static void MergeInto(HashSet<SkillType> pendingEntryGlow, Dictionary<SkillType, HashSet<int>> pendingTreeGlowLevels)
    {
        if (pendingEntryGlow == null || pendingTreeGlowLevels == null)
            return;

        foreach (SkillType t in s_pendingEntryGlow)
            pendingEntryGlow.Add(t);
        s_pendingEntryGlow.Clear();

        foreach (KeyValuePair<SkillType, HashSet<int>> kv in s_pendingTreeGlowLevels)
        {
            if (!pendingTreeGlowLevels.TryGetValue(kv.Key, out HashSet<int> dest))
            {
                dest = new HashSet<int>();
                pendingTreeGlowLevels[kv.Key] = dest;
            }

            if (kv.Value != null)
            {
                foreach (int lvl in kv.Value)
                    dest.Add(lvl);
            }
        }

        s_pendingTreeGlowLevels.Clear();
    }

    /// <summary>
    /// Removes cold-start data for <paramref name="type"/> because <see cref="SkillsAbilitiesPageUI"/> is
    /// subscribed and will track this level-up in its own pending sets (avoids duplicate re-glow on tab revisit).
    /// </summary>
    public static void ConsumeBecauseLiveUiHandled(SkillType type)
    {
        s_pendingEntryGlow.Remove(type);
        s_pendingTreeGlowLevels.Remove(type);
    }
}
