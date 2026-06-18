using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gathers <see cref="HotkeySettingsRowUI"/> rows for the hotkeys settings area. Drag section roots (e.g.
/// <c>CategoryAbilities</c>, <c>CategoryConsumables</c>) into <see cref="sectionRoots"/> so you do not have to
/// assign each row manually. Optional <see cref="explicitRows"/> overrides discovery.
/// </summary>
public class HotkeySettingsPanelUI : MonoBehaviour
{
    [Header("Row discovery")]
    [Tooltip("Drag one or more section parents here (e.g. CategoryAbilities, CategoryConsumables). All HotkeySettingsRowUI under them are collected in hierarchy order.")]
    [SerializeField] private RectTransform[] sectionRoots;

    [Tooltip("If set (size > 0), only these rows are used — section roots are ignored.")]
    [SerializeField] private HotkeySettingsRowUI[] explicitRows;

    [Tooltip("When true, rows under inactive section objects are still found.")]
    [SerializeField] private bool includeInactive = true;

    private HotkeySettingsRowUI[] _cachedRows;

    private void Awake()
    {
        EnsureMovementSectionHeaderDimming();
        EnsureEnterAreaHotkeyRow();
        EnsureStopMovementCombatHotkeyRow();
        RebuildRowCache();
    }

    private void OnEnable()
    {
        RebuildRowCache();
        RefreshAll();
        GlobalUserSettings.RestoredDefaults += OnGlobalRestoredDefaults;
    }

    private void OnDisable()
    {
        GlobalUserSettings.RestoredDefaults -= OnGlobalRestoredDefaults;
    }

    private void OnGlobalRestoredDefaults() => RefreshAll();

    /// <summary>Call if you add/remove row objects at runtime.</summary>
    public void RebuildRowCache()
    {
        _cachedRows = ResolveRows();
    }

    private HotkeySettingsRowUI[] ResolveRows()
    {
        if (explicitRows != null && explicitRows.Length > 0)
        {
            var list = new List<HotkeySettingsRowUI>();
            for (int i = 0; i < explicitRows.Length; i++)
            {
                if (explicitRows[i] != null)
                    list.Add(explicitRows[i]);
            }

            return list.ToArray();
        }

        if (sectionRoots != null && sectionRoots.Length > 0)
        {
            var list = new List<HotkeySettingsRowUI>();
            for (int s = 0; s < sectionRoots.Length; s++)
            {
                RectTransform root = sectionRoots[s];
                if (root == null)
                    continue;

                HotkeySettingsRowUI[] found = root.GetComponentsInChildren<HotkeySettingsRowUI>(includeInactive);
                for (int i = 0; i < found.Length; i++)
                {
                    if (found[i] != null)
                        list.Add(found[i]);
                }
            }

            return list.ToArray();
        }

        return GetComponentsInChildren<HotkeySettingsRowUI>(includeInactive);
    }

    public void RefreshAll()
    {
        if (_cachedRows == null || _cachedRows.Length == 0)
            RebuildRowCache();

        if (_cachedRows == null)
            return;

        for (int i = 0; i < _cachedRows.Length; i++)
        {
            if (_cachedRows[i] != null)
                _cachedRows[i].RefreshDisplay();
        }

        HotkeySettingsMovementSectionUI[] sections =
            GetComponentsInChildren<HotkeySettingsMovementSectionUI>(includeInactive);
        for (int i = 0; i < sections.Length; i++)
        {
            if (sections[i] != null)
                sections[i].RefreshHeader();
        }
    }

    private void EnsureMovementSectionHeaderDimming()
    {
        if (GetComponentInChildren<HotkeySettingsMovementSectionUI>(includeInactive) != null)
            return;

        Transform[] all = GetComponentsInChildren<Transform>(includeInactive);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || !string.Equals(t.name, "CategoryMovement", StringComparison.Ordinal))
                continue;

            t.gameObject.AddComponent<HotkeySettingsMovementSectionUI>();
            return;
        }
    }

    /// <summary>Resolved rows after last <see cref="RebuildRowCache"/>.</summary>
    public HotkeySettingsRowUI[] CachedRows => _cachedRows;

    private void EnsureEnterAreaHotkeyRow()
    {
        HotkeySettingsRowUI[] existing = GetComponentsInChildren<HotkeySettingsRowUI>(includeInactive);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null && existing[i].BindId == HotkeyBindId.EnterArea)
                return;
        }

        HotkeySettingsRowUI template = null;
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null && existing[i].BindId == HotkeyBindId.Interact)
            {
                template = existing[i];
                break;
            }
        }

        if (template == null)
            return;

        GameObject clone = Instantiate(template.gameObject, template.transform.parent);
        clone.name = "HotkeyRowEnterArea";
        HotkeySettingsRowUI row = clone.GetComponent<HotkeySettingsRowUI>();
        if (row != null)
            row.SetBindId(HotkeyBindId.EnterArea);
        clone.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);
    }

    private void EnsureStopMovementCombatHotkeyRow()
    {
        HotkeySettingsRowUI[] existing = GetComponentsInChildren<HotkeySettingsRowUI>(includeInactive);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null && existing[i].BindId == HotkeyBindId.StopMovementCombat)
                return;
        }

        HotkeySettingsRowUI template = null;
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null && existing[i].BindId == HotkeyBindId.EnterArea)
            {
                template = existing[i];
                break;
            }
        }

        if (template == null)
        {
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null && existing[i].BindId == HotkeyBindId.Interact)
                {
                    template = existing[i];
                    break;
                }
            }
        }

        if (template == null)
            return;

        GameObject clone = Instantiate(template.gameObject, template.transform.parent);
        clone.name = "HotkeyRowStopMovementCombat";
        HotkeySettingsRowUI row = clone.GetComponent<HotkeySettingsRowUI>();
        if (row != null)
            row.SetBindId(HotkeyBindId.StopMovementCombat);
        clone.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);
    }
}
