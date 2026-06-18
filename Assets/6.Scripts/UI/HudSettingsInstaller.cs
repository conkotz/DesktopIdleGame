using UnityEngine;

/// <summary>Creates HUD-related settings rows at runtime when the scene does not already include them.</summary>
public static class HudSettingsInstaller
{
    private static bool _installed;

    public static void EnsureSettingsRowsExist()
    {
        if (_installed)
            return;

        ToggleSettingsRowUI minimiseHudRow = FindRow(ToggleSettingId.MinimiseHud);
        if (!minimiseHudRow)
            return;

        EnsureRowAfter(minimiseHudRow, ToggleSettingId.HidePlayerOverheadBars);
        ToggleSettingsRowUI hideRow = FindRow(ToggleSettingId.HidePlayerOverheadBars);
        if (hideRow)
            EnsureRowAfter(hideRow, ToggleSettingId.DimHudWhenOverlapped);
        else
            EnsureRowAfter(minimiseHudRow, ToggleSettingId.DimHudWhenOverlapped);

        _installed = true;
    }

    private static ToggleSettingsRowUI FindRow(ToggleSettingId id)
    {
        ToggleSettingsRowUI[] rows = Object.FindObjectsByType<ToggleSettingsRowUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] != null && rows[i].SettingIdValue == id)
                return rows[i];
        }

        return null;
    }

    private static void EnsureRowAfter(ToggleSettingsRowUI anchor, ToggleSettingId id)
    {
        if (FindRow(id) != null)
            return;

        Transform parent = anchor.transform.parent;
        if (!parent)
            return;

        GameObject clone = Object.Instantiate(anchor.gameObject, parent);
        clone.name = $"SettingsToggleRow{id}";
        clone.transform.SetSiblingIndex(anchor.transform.GetSiblingIndex() + 1);

        ToggleSettingsRowUI row = clone.GetComponent<ToggleSettingsRowUI>();
        if (row)
            row.ConfigureSetting(id);
    }
}
