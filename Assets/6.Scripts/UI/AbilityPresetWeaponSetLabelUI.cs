using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Updates PresetSet1Text / PresetSet2Text values on the character page (headers stay in scene).</summary>
[DisallowMultipleComponent]
public sealed class AbilityPresetWeaponSetLabelUI : MonoBehaviour
{
    private const string SetOneTextName = "PresetSet1Text";
    private const string SetTwoTextName = "PresetSet2Text";
    private const string UnassignedLabel = "None Assigned";

    private TMP_Text _setOneTmp;
    private TMP_Text _setTwoTmp;
    private Text _setOneLegacy;
    private Text _setTwoLegacy;
    private SkillsManager _skillsManager;
    private float _nextRebindTime;

    public static void RefreshAll()
    {
        AbilityPresetWeaponSetLabelUI ui =
            FindFirstObjectByType<AbilityPresetWeaponSetLabelUI>(FindObjectsInactive.Include);
        if (ui != null)
        {
            ui.RefreshLabels();
            return;
        }

        ApplyAllLabels(FindSkillsManager());
    }

    private void OnEnable()
    {
        BindRefs();
        RefreshLabels();
        Subscribe();
    }

    private void OnDisable() => Unsubscribe();

    private void Update()
    {
        if (Time.unscaledTime < _nextRebindTime)
            return;

        if (HasBothLabels())
            return;

        _nextRebindTime = Time.unscaledTime + 0.5f;
        BindRefs();
        if (HasAnyLabel())
            RefreshLabels();
    }

    private void Subscribe()
    {
        if (_skillsManager != null)
            _skillsManager.OnAbilityPresetWeaponSetLinksChanged += RefreshLabels;
    }

    private void Unsubscribe()
    {
        if (_skillsManager != null)
            _skillsManager.OnAbilityPresetWeaponSetLinksChanged -= RefreshLabels;
    }

    private void BindRefs()
    {
        if (_skillsManager == null)
            _skillsManager = FindSkillsManager();

        if (_setOneTmp == null && _setOneLegacy == null)
            TryFindLabel(SetOneTextName, out _setOneTmp, out _setOneLegacy);
        if (_setTwoTmp == null && _setTwoLegacy == null)
            TryFindLabel(SetTwoTextName, out _setTwoTmp, out _setTwoLegacy);
    }

    private void RefreshLabels() => ApplyAllLabels(_skillsManager ?? FindSkillsManager());

    private static void ApplyAllLabels(SkillsManager skillsManager)
    {
        if (skillsManager == null)
            return;

        ApplyLabel(FindLabel(SetOneTextName), skillsManager.GetWeaponSetPresetDisplayLabel(0));
        ApplyLabel(FindLabel(SetTwoTextName), skillsManager.GetWeaponSetPresetDisplayLabel(1));
    }

    private static void ApplyLabel((TMP_Text tmp, Text legacy) label, string presetName)
    {
        string value = string.IsNullOrWhiteSpace(presetName) || presetName == "None Assigned"
            ? UnassignedLabel
            : presetName.Trim();

        if (label.tmp != null)
            label.tmp.text = value;
        if (label.legacy != null)
            label.legacy.text = value;
    }

    private bool HasBothLabels() =>
        (_setOneTmp != null || _setOneLegacy != null)
        && (_setTwoTmp != null || _setTwoLegacy != null);

    private bool HasAnyLabel() =>
        _setOneTmp != null || _setOneLegacy != null || _setTwoTmp != null || _setTwoLegacy != null;

    private static SkillsManager FindSkillsManager() =>
        SkillsManager.Instance ?? FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);

    private static (TMP_Text tmp, Text legacy) FindLabel(string objectName)
    {
        TryFindLabel(objectName, out TMP_Text tmp, out Text legacy);
        return (tmp, legacy);
    }

    private static void TryFindLabel(string objectName, out TMP_Text tmp, out Text legacy)
    {
        tmp = null;
        legacy = null;
        if (string.IsNullOrWhiteSpace(objectName))
            return;

        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject go = allObjects[i];
            if (go == null || !string.Equals(go.name, objectName, System.StringComparison.Ordinal))
                continue;

            tmp = go.GetComponent<TMP_Text>() ?? go.GetComponentInChildren<TMP_Text>(true);
            legacy = go.GetComponent<Text>() ?? go.GetComponentInChildren<Text>(true);
            if (tmp != null || legacy != null)
                return;
        }
    }
}
