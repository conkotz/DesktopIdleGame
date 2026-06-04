using UnityEngine;

/// <summary>Resolves capstone HUD / UI icons from skill definitions when not assigned in the inspector.</summary>
public static class CapstonePresentationIcons
{
    private static Sprite _wayOfTheBerserker;
    private static Sprite _holySealHudIcon;
    private static Sprite _wayOfTheBladeDancer;

    public static Sprite ResolveWayOfTheBerserkerIcon()
    {
        if (_wayOfTheBerserker != null)
            return _wayOfTheBerserker;

        SkillDefinition melee = SkillDatabase.LoadDefault()?.Get(SkillType.Melee);
        if (melee?.unlocks == null)
            return null;

        for (int i = 0; i < melee.unlocks.Count; i++)
        {
            SkillUnlockDefinition unlock = melee.unlocks[i];
            if (unlock == null || unlock.unlockType != SkillUnlockType.CapstonePassive)
                continue;

            if (unlock.choices == null
                || unlock.choices.Count <= AbilityCombatPower.MeleeCapstoneWayOfTheBerserkerChoiceIndex)
            {
                return null;
            }

            SkillChoiceDefinition choice =
                unlock.choices[AbilityCombatPower.MeleeCapstoneWayOfTheBerserkerChoiceIndex];
            _wayOfTheBerserker = choice?.icon;
            return _wayOfTheBerserker;
        }

        return null;
    }

    public static Sprite ResolveHolySealHudIcon()
    {
        if (_holySealHudIcon != null)
            return _holySealHudIcon;

        SkillDefinition melee = SkillDatabase.LoadDefault()?.Get(SkillType.Melee);
        if (melee?.unlocks == null)
            return null;

        for (int i = 0; i < melee.unlocks.Count; i++)
        {
            SkillUnlockDefinition unlock = melee.unlocks[i];
            if (unlock == null || unlock.unlockType != SkillUnlockType.CapstonePassive)
                continue;

            if (unlock.choices == null
                || unlock.choices.Count <= AbilityCombatPower.MeleeCapstoneWayOfTheCrusaderChoiceIndex)
            {
                return null;
            }

            SkillChoiceDefinition choice =
                unlock.choices[AbilityCombatPower.MeleeCapstoneWayOfTheCrusaderChoiceIndex];
            _holySealHudIcon = choice?.presentation != null ? choice.presentation.Icon : null;
            return _holySealHudIcon;
        }

        return null;
    }

    public static Sprite ResolveWayOfTheBladeDancerIcon()
    {
        if (_wayOfTheBladeDancer != null)
            return _wayOfTheBladeDancer;

        SkillDefinition melee = SkillDatabase.LoadDefault()?.Get(SkillType.Melee);
        if (melee?.unlocks == null)
            return null;

        for (int i = 0; i < melee.unlocks.Count; i++)
        {
            SkillUnlockDefinition unlock = melee.unlocks[i];
            if (unlock == null || unlock.unlockType != SkillUnlockType.CapstonePassive)
                continue;

            if (unlock.choices == null
                || unlock.choices.Count <= AbilityCombatPower.MeleeCapstoneWayOfTheBladeDancerChoiceIndex)
            {
                return null;
            }

            SkillChoiceDefinition choice =
                unlock.choices[AbilityCombatPower.MeleeCapstoneWayOfTheBladeDancerChoiceIndex];
            _wayOfTheBladeDancer = choice?.icon;
            return _wayOfTheBladeDancer;
        }

        return null;
    }
}
