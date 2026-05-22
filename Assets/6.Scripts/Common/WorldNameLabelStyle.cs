using System;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Shared styling for world-space <c>NameLabel</c> TextMeshPro plates (outline + sorting).
/// </summary>
public static class WorldNameLabelStyle
{
    public const float DefaultOutlineWidth = 0.22f;

    private static readonly Color32 DefaultOutlineColor = new(0, 0, 0, 255);
    private const int SortingOrderOffset = 3;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StyleNameLabelsInLoadedScene()
    {
        TMP_Text[] labels = UnityEngine.Object.FindObjectsByType<TMP_Text>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            if (!label || label.name != "NameLabel" || IsScreenSpaceUI(label))
                continue;

            Apply(label);
        }
    }

    public static void Apply(TMP_Text label, SpriteRenderer sortReference = null)
    {
        if (!label)
            return;

        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.alignment = TextAlignmentOptions.Center;
        label.richText = true;

        ApplySorting(label, sortReference ?? FindSortReference(label.transform));

        if (!CanMutateTextMaterial(label))
            return;

        try
        {
            label.outlineWidth = DefaultOutlineWidth;
            label.outlineColor = DefaultOutlineColor;
            label.UpdateMeshPadding();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WorldNameLabelStyle] Skipped outline on '{label.name}': {ex.Message}", label);
        }
    }

    /// <summary>
    /// Outline / padding create a material instance via <c>renderer.material</c>.
    /// Only safe on scene instances during play — never prefab assets.
    /// </summary>
    private static bool CanMutateTextMaterial(TMP_Text label)
    {
        if (!Application.isPlaying || !label)
            return false;

        if (!label.gameObject.scene.IsValid())
            return false;

#if UNITY_EDITOR
        if (PrefabUtility.IsPartOfPrefabAsset(label))
            return false;
#endif

        return true;
    }

    private static void ApplySorting(TMP_Text label, SpriteRenderer host)
    {
        if (!host)
            return;

        Renderer textRenderer = label.GetComponent<Renderer>();
        if (!textRenderer)
            return;

        textRenderer.sortingLayerID = host.sortingLayerID;
        textRenderer.sortingOrder = host.sortingOrder + SortingOrderOffset;
    }

    private static SpriteRenderer FindSortReference(Transform labelTransform)
    {
        if (!labelTransform)
            return null;

        for (Transform t = labelTransform.parent; t != null; t = t.parent)
        {
            SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
            if (sr)
                return sr;
        }

        return null;
    }

    private static bool IsScreenSpaceUI(TMP_Text label)
    {
        Canvas canvas = label.GetComponentInParent<Canvas>();
        return canvas && canvas.renderMode != RenderMode.WorldSpace;
    }
}
