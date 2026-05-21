using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inspector wiring for a right-panel section: root, header, and content area.
/// </summary>
[System.Serializable]
public class RightPanelSectionRefs
{
    [Tooltip("Section root (e.g. CapstoneSection).")]
    public GameObject section;

    [Tooltip("Header row (title label, optional divider).")]
    public GameObject header;

    [Tooltip("Content area (e.g. CapstoneContent — row list parent).")]
    public Transform content;
}

/// <summary>
/// Major passives: empty-state text object + separate list content for spawned rows.
/// </summary>
[System.Serializable]
public class RightPanelMajorPassivesSectionRefs
{
    public GameObject section;
    public GameObject header;

    [Tooltip("Shown until the player commits their first major passive (e.g. MajorPassivesText).")]
    public GameObject emptyText;

    [Tooltip("Row list parent (e.g. MajorPassivesContent). Shown after the first major passive is unlocked.")]
    public Transform listContent;
}

/// <summary>
/// Text-only section (minor / additional unlocks).
/// </summary>
[System.Serializable]
public class RightPanelTextSectionRefs
{
    public GameObject section;
    public GameObject header;
    public TMP_Text content;
}

/// <summary>
/// Abilities section: summary line + draggable ability rows (separate from other sections).
/// </summary>
[System.Serializable]
public class RightPanelAbilitiesSectionRefs : RightPanelSectionRefs
{
    [Tooltip("Summary line (e.g. AbilitiesUnlockedText — unlocked/selected counts).")]
    public TMP_Text summaryText;

    public void ClearAbilityRows()
    {
        if (!content)
            return;

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            if (child && child.GetComponent<AbilityEntryUI>())
                UnityEngine.Object.Destroy(child.gameObject);
        }
    }
}

/// <summary>
/// Helpers for major passive / capstone row lists.
/// </summary>
public static class RightPanelMajorPassiveListUtil
{
    public static void ClearRows(Transform listContent)
    {
        if (!listContent)
            return;

        for (int i = listContent.childCount - 1; i >= 0; i--)
        {
            Transform child = listContent.GetChild(i);
            if (child && child.GetComponent<MajorPassiveListEntryUI>())
                UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    public static void EnsureListSpacing(Transform listContent, float rowSpacing)
    {
        if (listContent is not RectTransform rt)
            return;

        VerticalLayoutGroup v = rt.GetComponent<VerticalLayoutGroup>();
        if (v == null)
            v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = rowSpacing;
    }
}
