using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Finds embedded world-space <c>NameLabel</c> children on a prefab root (signposts, trees, caves, etc.)
/// and prepares styling plus <see cref="WorldNameLabelScreenClamp"/>.
/// Merchants use the reusable <c>NameLabel</c> prefab instead and are skipped here.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldNameLabelResolver : MonoBehaviour
{
    [SerializeField] private TMP_Text[] nameLabels;
    [SerializeField] private bool searchInactiveChildren = true;
    [SerializeField] private SpriteRenderer sortingReference;

    public IReadOnlyList<TMP_Text> NameLabels => nameLabels;

    private void Awake()
    {
        if (GetComponent<UnitOverheadUI>() != null || GetComponent<SkillTimelineNodeUI>() != null)
        {
            Destroy(this);
            return;
        }

        ResolveNameLabels();
    }

#if UNITY_EDITOR
    private static WorldNameLabelResolver s_validating;

    private void Reset()
    {
        sortingReference = GetComponent<SpriteRenderer>();
        ResolveNameLabels();
    }

    private void OnValidate()
    {
        if (Application.isPlaying || s_validating == this)
            return;

        s_validating = this;
        try
        {
            ResolveNameLabels();
        }
        finally
        {
            if (s_validating == this)
                s_validating = null;
        }
    }
#endif

    /// <summary>Collects labels and applies shared world nameplate setup.</summary>
    public void ResolveNameLabels()
    {
        nameLabels = CollectWorldNameLabels(transform, searchInactiveChildren);

#if UNITY_EDITOR
        if (!Application.isPlaying)
            return;
#endif

        SpriteRenderer sortRef = sortingReference != null
            ? sortingReference
            : GetComponent<SpriteRenderer>();

        for (int i = 0; i < nameLabels.Length; i++)
        {
            TMP_Text label = nameLabels[i];
            WorldNameLabelStyle.PrepareWorldSpaceNameLabel(label, sortRef);
            if (label && label.TryGetComponent(out WorldNameLabelScreenClamp clamp))
                clamp.RefreshClamp();
        }
    }

    public static TMP_Text[] CollectWorldNameLabels(Transform root, bool includeInactive = true)
    {
        var found = new List<TMP_Text>(4);
        CollectWorldNameLabels(root, includeInactive, found);
        return found.ToArray();
    }

    public static void CollectWorldNameLabels(Transform root, bool includeInactive, List<TMP_Text> results)
    {
        if (!root || results == null)
            return;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (!child)
                continue;

            if (!includeInactive && !child.gameObject.activeInHierarchy)
                continue;

            if (child.TryGetComponent(out TMP_Text label) && WorldNameLabelStyle.IsEligibleWorldNameLabel(label))
                results.Add(label);

            CollectWorldNameLabels(child, includeInactive, results);
        }
    }

    public static bool HasResolvableWorldNameLabels(Transform root, bool includeInactive = true)
    {
        if (!root)
            return false;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (!child)
                continue;

            if (!includeInactive && !child.gameObject.activeInHierarchy)
                continue;

            if (child.TryGetComponent(out TMP_Text label) && WorldNameLabelStyle.IsEligibleWorldNameLabel(label))
                return true;

            if (HasResolvableWorldNameLabels(child, includeInactive))
                return true;
        }

        return false;
    }
}
