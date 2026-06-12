using UnityEngine;
using UnityEngine.UI;

/// <summary>Creates the Show dev panel settings row at runtime if the scene does not already include it.</summary>
public static class ShowDevPanelSettingsInstaller
{
    private static readonly Color DevPanelRowStripColor = new Color(0.82f, 0.18f, 0.18f, 0.92f);

    private static bool _installed;

    public static void ApplyRowStyle(ToggleSettingsRowUI row) => ApplyShowDevPanelRowStyle(row);

    public static void EnsureSettingsRowExists()
    {
        ToggleSettingsRowUI[] rows = Object.FindObjectsByType<ToggleSettingsRowUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] != null && rows[i].SettingIdValue == ToggleSettingId.ShowDevPanel)
            {
                ApplyShowDevPanelRowStyle(rows[i]);
                _installed = true;
                return;
            }
        }

        if (_installed)
            return;

        ToggleSettingsRowUI template = null;
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] != null && rows[i].SettingIdValue == ToggleSettingId.ShowFps)
            {
                template = rows[i];
                break;
            }
        }

        if (!template)
            return;

        Transform parent = template.transform.parent;
        if (!parent)
            return;

        GameObject clone = Object.Instantiate(template.gameObject, parent);
        clone.name = "SettingsToggleRowDevPanel";
        clone.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);

        ToggleSettingsRowUI row = clone.GetComponent<ToggleSettingsRowUI>();
        if (row)
        {
            row.ConfigureSetting(ToggleSettingId.ShowDevPanel);
            ApplyShowDevPanelRowStyle(row);
        }

        _installed = true;
    }

    private static void ApplyShowDevPanelRowStyle(ToggleSettingsRowUI row)
    {
        if (!row)
            return;

        Image rowBackground = row.GetComponent<Image>();
        if (!rowBackground)
            rowBackground = row.gameObject.AddComponent<Image>();

        rowBackground.color = DevPanelRowStripColor;
        rowBackground.raycastTarget = true;
    }
}
