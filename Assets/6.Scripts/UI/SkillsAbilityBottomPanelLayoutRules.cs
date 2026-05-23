/// <summary>
/// Rules for when the NEW skills page bottom bar shows all four columns vs. details-only.
/// </summary>
public static class SkillsAbilityBottomPanelLayoutRules
{
    public static bool ShouldExpandBottomBar(SkillTimelineNodeBinding binding)
    {
        if (binding?.Unlock == null)
            return false;

        switch (binding.Unlock.unlockType)
        {
            case SkillUnlockType.Ability:
            case SkillUnlockType.MajorPassive:
            case SkillUnlockType.CapstonePassive:
                return true;
            default:
                return false;
        }
    }
}
