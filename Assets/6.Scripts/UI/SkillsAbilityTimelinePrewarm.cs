using System.Collections;
using UnityEngine;

/// <summary>
/// Builds every skill timeline once during gameplay load so tab switches can restore cached trees instantly.
/// Must run while the skills page hierarchy is active (see MainMenuWindowUI.CoPrewarmHeavyPages).
/// </summary>
public static class SkillsAbilityTimelinePrewarm
{
    public static bool IsComplete => HorizontalSkillTreeScaffoldUI.AreAllSkillTimelinesPrewarmed;

    public static IEnumerator CoPrewarmForActivePage(SkillsAbilityPageNewUI page)
    {
        if (IsComplete)
            yield break;

        if (page == null || !page.gameObject.activeInHierarchy)
            yield break;

        HorizontalSkillTreeScaffoldUI.ResetSkillTimelinePrewarmState();
        page.StopDeferredUiRefreshCoroutines();

        HorizontalSkillTreeScaffoldUI timeline =
            page.GetComponentInChildren<HorizontalSkillTreeScaffoldUI>(true);
        SkillDatabase database = page.SkillDatabase;
        if (timeline == null || database == null || database.Skills == null || database.Skills.Count == 0)
        {
            HorizontalSkillTreeScaffoldUI.MarkAllSkillTimelinesPrewarmed();
            yield break;
        }

        SkillDefinition restoreSkill = page.SelectedSkill;

        for (int i = 0; i < database.Skills.Count; i++)
        {
            SkillDefinition skill = database.Skills[i];
            if (skill == null)
                continue;

            timeline.SetSelectedSkill(skill);
            timeline.Build(skill);
            timeline.FlushPendingTimelineLayout();
            yield return null;
        }

        if (restoreSkill != null)
        {
            timeline.SetSelectedSkill(restoreSkill);
            timeline.Build(restoreSkill);
            timeline.FlushPendingTimelineLayout();
        }

        HorizontalSkillTreeScaffoldUI.MarkAllSkillTimelinesPrewarmed();
    }

    /// <summary>Fallback when menu prewarm did not run (e.g. missing MainMenuWindowUI).</summary>
    public static IEnumerator CoPrewarmAllSkillTimelines()
    {
        if (IsComplete)
            yield break;

        SkillsAbilityPageNewUI page =
            Object.FindFirstObjectByType<SkillsAbilityPageNewUI>(FindObjectsInactive.Include);
        if (page == null)
        {
            HorizontalSkillTreeScaffoldUI.MarkAllSkillTimelinesPrewarmed();
            yield break;
        }

        bool createdActivationScope = false;
        if (!page.gameObject.activeInHierarchy)
        {
            page.gameObject.SetActive(true);
            createdActivationScope = true;
            yield return null;
        }

        yield return CoPrewarmForActivePage(page);

        if (createdActivationScope)
            page.gameObject.SetActive(false);
    }
}
