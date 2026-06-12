using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Creates the Move pivots settings row at runtime if the scene does not already include it.</summary>
public static class MovePivotsSettingsInstaller
{
    private static bool _installed;

    public static void EnsureSettingsRowExists()
    {
        if (_installed)
            return;

        ToggleSettingsRowUI[] rows = Object.FindObjectsByType<ToggleSettingsRowUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] != null && rows[i].SettingIdValue == ToggleSettingId.MoveWindowPivots)
            {
                _installed = true;
                return;
            }
        }

        ToggleSettingsRowUI template = null;
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] != null && rows[i].SettingIdValue == ToggleSettingId.ShowWindowResizeHandles)
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
        clone.name = "SettingsToggleRowMovePivots";
        clone.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);

        ToggleSettingsRowUI row = clone.GetComponent<ToggleSettingsRowUI>();
        if (row)
            row.ConfigureSetting(ToggleSettingId.MoveWindowPivots);

        _installed = true;
    }
}
