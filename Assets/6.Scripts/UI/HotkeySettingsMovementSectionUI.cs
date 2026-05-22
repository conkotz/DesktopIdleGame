using TMPro;
using UnityEngine;

/// <summary>
/// Optional header styling for the keyboard movement hotkeys section (e.g. CategoryMovement).
/// </summary>
[DisallowMultipleComponent]
public class HotkeySettingsMovementSectionUI : MonoBehaviour
{
    private static readonly Color ActiveHeaderColor = Color.white;

    [SerializeField] private TMP_Text sectionHeaderText;

    private void Awake()
    {
        if (sectionHeaderText != null)
            return;

        TMP_Text[] tmps = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < tmps.Length; i++)
        {
            TMP_Text tmp = tmps[i];
            if (tmp == null || tmp.GetComponentInParent<HotkeySettingsRowUI>() != null)
                continue;

            sectionHeaderText = tmp;
            break;
        }
    }

    private void OnEnable()
    {
        GlobalUserSettings.RestoredDefaults += OnGlobalRestoredDefaults;
        RefreshHeader();
    }

    private void OnDisable()
    {
        GlobalUserSettings.RestoredDefaults -= OnGlobalRestoredDefaults;
    }

    private void OnGlobalRestoredDefaults() => RefreshHeader();

    public void RefreshHeader()
    {
        if (sectionHeaderText == null)
            return;

        sectionHeaderText.color = ActiveHeaderColor;
    }
}
