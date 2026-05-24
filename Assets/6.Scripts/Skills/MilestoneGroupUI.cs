/// <summary>
/// Marker component for the single milestone choice-group prefab (<c>MilestoneGroupUI.prefab</c>).
/// All behaviour is inherited from <see cref="SkillChoiceGroupUI"/>.
/// </summary>
/// <remarks>
/// Do not use <c>SkillChoiceGroupUI.prefab</c> — it is legacy. Per-type vertical offsets (ability / major / capstone)
/// are configured on <see cref="HorizontalSkillTreeScaffoldUI"/> → Choice Group Layout, not on this prefab.
/// </remarks>
public sealed class MilestoneGroupUI : SkillChoiceGroupUI
{
}
