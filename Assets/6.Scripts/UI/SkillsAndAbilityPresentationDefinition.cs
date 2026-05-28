using UnityEngine;

/// <summary>
/// Centralized display copy for skills / abilities / passives (tooltips, lists, headers).
/// Gameplay ScriptableObjects keep numeric rules; this asset holds wording and icons.
/// League tooltip layout (skills ability list): flavor shortDescription → blue scaling → Effects →
/// Active Enhancement (green) → Required weapon (bottom). Numeric scaling/effects stay in
/// <see cref="AbilityTooltipDamagePreview"/> keyed by <see cref="AbilityDefinition.abilityId"/>.
/// </summary>
[CreateAssetMenu(
    fileName = "Presentation_",
    menuName = "Desktop Idle Game/Skills/Skills And Ability Presentation")]
public sealed class SkillsAndAbilityPresentationDefinition : ScriptableObject
{
    [Tooltip("Optional editor-only note (which ability/skill row this mirrors).")]
    [SerializeField] private string notes;

    [Header("Names")]
    [Tooltip("When set, overrides AbilityDefinition.displayName / SkillDefinition.displayName (etc.) in UI.")]
    [SerializeField] private string displayNameOverride;

    [Header("Icon (abilities)")]
    [Tooltip("Ability icon shown in lists, action bar, buff HUD, and skill tree.")]
    [SerializeField] private Sprite icon;

    [Header("Descriptions")]
    [Tooltip("Flavor one-liner only (no % damage, range, or effect bullets). Shown at top of ability tooltips before blue scaling and Effects.")]
    [TextArea(2, 6)]
    [SerializeField] private string shortDescription;

    [Tooltip("Extra prose for skill tree / unlock rows when you want more than the short line (abilities: optional; skills/unlocks: common).")]
    [TextArea(3, 12)]
    [SerializeField] private string primaryDescriptionOverride;

    [Tooltip("Optional extra paragraph appended after the primary description.")]
    [TextArea(2, 8)]
    [SerializeField] private string flavourText;

    [Header("Tooltip chrome")]
    [Tooltip("When set, replaces the tag line normally derived from AbilityDefinition.tag (e.g. Buff / Active). Plain text — tooltip builder applies color markup.")]
    [SerializeField] private string tooltipCategoryTagOverride;

    public string Notes => notes;
    public string DisplayNameOverride => displayNameOverride;
    public Sprite Icon => icon;
    public string ShortDescription => shortDescription;
    public string PrimaryDescriptionOverride => primaryDescriptionOverride;
    public string FlavourText => flavourText;
    public string TooltipCategoryTagOverride => tooltipCategoryTagOverride;
}

/// <summary>
/// Resolves presentation strings from optional <see cref="SkillsAndAbilityPresentationDefinition"/> references.
/// Does not touch combat, saves, or item definitions.
/// </summary>
public static class SkillsAbilityPresentationResolver
{
    public static Sprite ResolveAbilityIcon(AbilityDefinition def)
    {
        if (def?.presentation != null && def.presentation.Icon != null)
            return def.presentation.Icon;

        return null;
    }

    public static Sprite ResolveChoiceIcon(SkillChoiceDefinition choice)
    {
        if (choice == null)
            return null;

        if (choice.icon != null)
            return choice.icon;

        if (choice.presentation != null && choice.presentation.Icon != null)
            return choice.presentation.Icon;

        if (choice.ability != null)
            return ResolveAbilityIcon(choice.ability);

        return null;
    }

    public static string ResolveAbilityDisplayName(AbilityDefinition def)
    {
        if (def == null)
            return string.Empty;

        if (def.presentation != null && !string.IsNullOrWhiteSpace(def.presentation.DisplayNameOverride))
            return def.presentation.DisplayNameOverride.Trim();

        return string.IsNullOrWhiteSpace(def.displayName) ? string.Empty : def.displayName.Trim();
    }

    public static string ResolveAbilityPrimaryDescription(AbilityDefinition def)
    {
        if (def == null)
            return "No description.";

        string primary = null;
        if (def.presentation != null && !string.IsNullOrWhiteSpace(def.presentation.PrimaryDescriptionOverride))
            primary = def.presentation.PrimaryDescriptionOverride.Trim();
        else if (def.presentation != null && !string.IsNullOrWhiteSpace(def.presentation.ShortDescription))
            primary = def.presentation.ShortDescription.Trim();

        if (string.IsNullOrEmpty(primary))
            return "No description.";

        if (def.presentation != null && !string.IsNullOrWhiteSpace(def.presentation.FlavourText))
            return $"{primary}\n\n{def.presentation.FlavourText.Trim()}";

        return primary;
    }

    /// <summary>
    /// League-style ability tooltip: prefers <see cref="SkillsAndAbilityPresentationDefinition.ShortDescription"/> when set
    /// (compact intro), otherwise <see cref="ResolveAbilityPrimaryDescription"/>.
    /// </summary>
    public static string ResolveAbilityLeagueIntroParagraph(AbilityDefinition def)
    {
        if (def?.presentation != null && !string.IsNullOrWhiteSpace(def.presentation.ShortDescription))
            return def.presentation.ShortDescription.Trim();

        return ResolveAbilityPrimaryDescription(def);
    }

    /// <summary>Skill tree row body for an ability-backed unlock: short + optional primary override, else unlock text (handled by caller).</summary>
    public static string ResolveAbilitySkillTreeBodyFromPresentation(AbilityDefinition ability)
    {
        if (ability?.presentation == null)
            return string.Empty;

        string intro = ResolveAbilityLeagueIntroParagraph(ability);
        string extra = ability.presentation != null && !string.IsNullOrWhiteSpace(ability.presentation.PrimaryDescriptionOverride)
            ? ability.presentation.PrimaryDescriptionOverride.Trim()
            : string.Empty;

        if (!string.IsNullOrWhiteSpace(intro) && intro != "No description." && !string.IsNullOrWhiteSpace(extra))
            return $"{intro}\n\n{extra}";
        if (!string.IsNullOrWhiteSpace(extra))
            return extra;
        if (!string.IsNullOrWhiteSpace(intro) && intro != "No description.")
            return intro;

        return string.Empty;
    }

    /// <summary>Skill tree row body when an unlock references an ability with optional presentation.</summary>
    public static string ResolveSkillTreeAbilityUnlockDescription(SkillUnlockDefinition unlock)
    {
        if (unlock?.ability != null)
        {
            string fromAbility = ResolveAbilitySkillTreeBodyFromPresentation(unlock.ability);

            if (!string.IsNullOrWhiteSpace(fromAbility))
                return fromAbility;
        }

        if (unlock != null && !string.IsNullOrWhiteSpace(unlock.description))
            return unlock.description.Trim();

        return "No description yet.";
    }

    /// <summary>Skill tree row title: ability-backed unlocks prefer the ability display name.</summary>
    public static string ResolveTreeUnlockTitle(SkillUnlockDefinition unlock)
    {
        if (unlock == null)
            return "Untitled";

        if (unlock.unlockType == SkillUnlockType.Ability && unlock.ability != null)
            return ResolveAbilityDisplayName(unlock.ability);

        string t = ResolveUnlockTitle(unlock);
        return string.IsNullOrWhiteSpace(t) ? "Untitled" : t;
    }

    public static string ResolveAbilityShortDescription(AbilityDefinition def)
    {
        if (def?.presentation != null && !string.IsNullOrWhiteSpace(def.presentation.ShortDescription))
            return def.presentation.ShortDescription.Trim();

        return string.Empty;
    }

    /// <summary>Plain category label before tooltip color wrapping; null = use <see cref="AbilityDefinition.tag"/> / legacy rules.</summary>
    public static string ResolveAbilityTooltipCategoryLabelOrNull(AbilityDefinition def)
    {
        if (def?.presentation == null)
            return null;

        string o = def.presentation.TooltipCategoryTagOverride;
        return string.IsNullOrWhiteSpace(o) ? null : o.Trim();
    }

    public static string ResolveSkillDisplayName(SkillDefinition def)
    {
        if (def == null)
            return string.Empty;

        if (def.presentation != null && !string.IsNullOrWhiteSpace(def.presentation.DisplayNameOverride))
            return def.presentation.DisplayNameOverride.Trim();

        return string.IsNullOrWhiteSpace(def.displayName) ? string.Empty : def.displayName.Trim();
    }

    public static string ResolveSkillDescription(SkillDefinition def)
    {
        if (def == null)
            return string.Empty;

        string primary = null;
        if (def.presentation != null && !string.IsNullOrWhiteSpace(def.presentation.PrimaryDescriptionOverride))
            primary = def.presentation.PrimaryDescriptionOverride.Trim();
        else if (!string.IsNullOrWhiteSpace(def.description))
            primary = def.description.Trim();

        if (string.IsNullOrEmpty(primary))
            return string.Empty;

        if (def.presentation != null && !string.IsNullOrWhiteSpace(def.presentation.FlavourText))
            return $"{primary}\n\n{def.presentation.FlavourText.Trim()}";

        return primary;
    }

    public static string ResolveUnlockTitle(SkillUnlockDefinition unlock)
    {
        if (unlock == null)
            return string.Empty;

        if (unlock.presentation != null && !string.IsNullOrWhiteSpace(unlock.presentation.DisplayNameOverride))
            return unlock.presentation.DisplayNameOverride.Trim();

        return string.IsNullOrWhiteSpace(unlock.title) ? string.Empty : unlock.title.Trim();
    }

    public static string ResolveUnlockDescription(SkillUnlockDefinition unlock)
    {
        if (unlock == null)
            return string.Empty;

        string primary = null;
        if (unlock.presentation != null && !string.IsNullOrWhiteSpace(unlock.presentation.PrimaryDescriptionOverride))
            primary = unlock.presentation.PrimaryDescriptionOverride.Trim();
        else if (unlock.ability != null && unlock.ability.presentation != null)
        {
            string abilityText = ResolveAbilityPrimaryDescription(unlock.ability);
            if (!string.IsNullOrWhiteSpace(abilityText) && abilityText != "No description.")
                primary = abilityText;
        }

        if (primary == null && !string.IsNullOrWhiteSpace(unlock.description))
            primary = unlock.description.Trim();

        if (string.IsNullOrEmpty(primary))
            return string.Empty;

        if (unlock.presentation != null && !string.IsNullOrWhiteSpace(unlock.presentation.FlavourText))
            return $"{primary}\n\n{unlock.presentation.FlavourText.Trim()}";

        return primary;
    }

    public static string ResolveChoiceTitle(SkillChoiceDefinition choice)
    {
        if (choice == null)
            return string.Empty;

        if (choice.presentation != null && !string.IsNullOrWhiteSpace(choice.presentation.DisplayNameOverride))
            return choice.presentation.DisplayNameOverride.Trim();

        return string.IsNullOrWhiteSpace(choice.title) ? string.Empty : choice.title.Trim();
    }

    public static string ResolveChoiceDescription(SkillChoiceDefinition choice)
    {
        if (choice == null)
            return string.Empty;

        string primary = null;
        if (choice.presentation != null && !string.IsNullOrWhiteSpace(choice.presentation.PrimaryDescriptionOverride))
            primary = choice.presentation.PrimaryDescriptionOverride.Trim();
        else if (!string.IsNullOrWhiteSpace(choice.description))
            primary = choice.description.Trim();

        if (string.IsNullOrEmpty(primary))
            return string.Empty;

        if (choice.presentation != null && !string.IsNullOrWhiteSpace(choice.presentation.FlavourText))
            return $"{primary}\n\n{choice.presentation.FlavourText.Trim()}";

        return primary;
    }
}
