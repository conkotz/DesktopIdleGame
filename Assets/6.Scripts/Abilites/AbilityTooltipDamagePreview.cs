using UnityEngine;

/// <summary>
/// Expected damage snippets for ability tooltips (average weapon split × multipliers, AP × mult).
/// Matches instant-cast / Whirling-style scaling; Power Slash tooltip uses the same preview for readability.
/// </summary>
public static class AbilityTooltipDamagePreview
{
    public static CharacterStats FindLocalPlayerStats()
    {
        var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null)
        {
            var s = player.GetComponent<CharacterStats>();
            if (s != null)
                return s;
        }

        return Object.FindFirstObjectByType<CharacterStats>(FindObjectsInactive.Include);
    }

    public static string FormatPhysSuffix(CharacterStats stats, float physicalMultiplier)
    {
        if (stats == null)
            return "";

        float avgPhys = (Mathf.Max(0f, stats.MinSplitDamage.physical) + Mathf.Max(0f, stats.MaxSplitDamage.physical)) * 0.5f;
        int n = Mathf.RoundToInt(avgPhys * Mathf.Max(0f, physicalMultiplier));
        if (n <= 0)
            return "";

        return $" ({n} phys)";
    }

    /// <summary>AP contribution is shown as phys — matches runtime (added to physical on most abilities).</summary>
    public static string FormatAbilityPowerSuffix(CharacterStats stats, float abilityPowerMultiplier)
    {
        if (stats == null)
            return "";

        int n = Mathf.RoundToInt(Mathf.Max(0f, stats.AbilityPower) * Mathf.Max(0f, abilityPowerMultiplier));
        if (n <= 0)
            return "";

        return $" ({n} phys)";
    }

    /// <summary>
    /// TMP rich-text line for ability tooltips. Empty when <see cref="AbilityWeaponRequirement.Any"/>.
    /// Red when equipped weapon does not match; neutral or orange (action bar) when it does.
    /// </summary>
    public static string BuildWeaponRequirementRichLine(AbilityDefinition def, CharacterStats stats, bool orangeWhenOk = false)
    {
        if (def == null || def.requiredWeaponType == AbilityWeaponRequirement.Any)
            return "";

        string label = def.requiredWeaponType switch
        {
            AbilityWeaponRequirement.Melee => "Melee",
            AbilityWeaponRequirement.Ranged => "Ranged",
            AbilityWeaponRequirement.Magic => "Magic",
            _ => "Any"
        };

        string line = $"Required: {label} weapon";
        bool ok = stats == null || stats.IsAbilityUsableWithEquippedWeapon(def);
        if (!ok)
            return $"<color=#FF5C5C>{line}</color>";

        if (orangeWhenOk)
            return $"<color=#FFB347>{line}</color>";

        return line;
    }
}
