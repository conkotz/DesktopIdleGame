using UnityEngine;

/// <summary>
/// Destroys UI hierarchy children without leaving the Inspector bound to destroyed TMP / RectTransforms
/// (avoids TMP_BaseEditorPanel.OnSceneGUI MissingReferenceException spam in the Editor).
/// </summary>
public static class UiDestroyUtility
{
    public static void DestroyChildren(Transform parent)
    {
        if (!parent)
            return;

        ClearEditorSelectionIfTargetingChildOf(parent);

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child)
                Object.Destroy(child.gameObject);
        }
    }

    private static void ClearEditorSelectionIfTargetingChildOf(Transform parent)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            return;

        GameObject active = UnityEditor.Selection.activeGameObject;
        if (!active || active == parent.gameObject)
            return;

        if (!active.transform.IsChildOf(parent))
            return;

        UnityEditor.Selection.activeGameObject = parent.gameObject;
#endif
    }
}
