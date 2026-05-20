#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Creates / updates <c>Assets/2.Prefabs/UI/SettingsDropdownRow.prefab</c>.</summary>
public static class SettingsDropdownRowPrefabCreator
{
    private const string PrefabPath = "Assets/2.Prefabs/UI/SettingsDropdownRow.prefab";
    private const string ToggleRowPath = "Assets/2.Prefabs/UI/SettingsToggleRow.prefab";

    [InitializeOnLoadMethod]
    private static void ScheduleAutoCreateIfMissing()
    {
        EditorApplication.delayCall += EnsurePrefabExists;
    }

    private static void EnsurePrefabExists()
    {
        if (Application.isPlaying)
            return;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            return;

        CreateOrUpdate();
    }

    [MenuItem("Desktop Idle/UI/Create Settings Dropdown Row Prefab")]
    public static void CreateOrUpdateFromMenu() => CreateOrUpdate();

    public static void CreateOrUpdate()
    {
        GameObject toggleRowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ToggleRowPath);
        if (!toggleRowPrefab)
        {
            Debug.LogError($"[SettingsDropdownRowPrefabCreator] Missing template prefab at '{ToggleRowPath}'.");
            return;
        }

        GameObject row = Object.Instantiate(toggleRowPrefab);
        row.name = "SettingsDropdownRow";

        Toggle oldToggle = row.GetComponentInChildren<Toggle>(true);
        TMP_Text labelTemplate = row.transform.Find("SettingName")?.GetComponent<TMP_Text>();
        TMP_FontAsset font = labelTemplate != null ? labelTemplate.font : null;
        float fontSize = labelTemplate != null ? labelTemplate.fontSize : 18f;
        Color labelColor = labelTemplate != null ? labelTemplate.color : Color.white;

        if (oldToggle)
            Object.DestroyImmediate(oldToggle.gameObject);

        Object.DestroyImmediate(row.GetComponent<ToggleSettingsRowUI>());

        GameObject dropdownRoot = TMP_DefaultControls.CreateDropdown(GetTmpResources());
        dropdownRoot.name = "Dropdown";
        dropdownRoot.transform.SetParent(row.transform, false);

        RectTransform dropdownRt = dropdownRoot.GetComponent<RectTransform>();
        dropdownRt.anchorMin = new Vector2(1f, 0.5f);
        dropdownRt.anchorMax = new Vector2(1f, 0.5f);
        dropdownRt.pivot = new Vector2(1f, 0.5f);
        dropdownRt.anchoredPosition = Vector2.zero;
        dropdownRt.sizeDelta = new Vector2(220f, 36f);

        LayoutElement dropdownLe = dropdownRoot.GetComponent<LayoutElement>() ?? dropdownRoot.AddComponent<LayoutElement>();
        dropdownLe.minWidth = 180f;
        dropdownLe.preferredWidth = 220f;
        dropdownLe.flexibleWidth = 0f;
        dropdownLe.minHeight = 36f;
        dropdownLe.preferredHeight = 36f;
        dropdownLe.flexibleHeight = 0f;
        dropdownLe.layoutPriority = 2;

        TMP_Dropdown dropdown = dropdownRoot.GetComponent<TMP_Dropdown>();
        ApplyDropdownFont(dropdown, font, fontSize, labelColor);
        StyleDropdown(dropdown);

        DropdownSettingsRowUI rowUi = row.AddComponent<DropdownSettingsRowUI>();
        SerializedObject so = new SerializedObject(rowUi);
        so.FindProperty("settingId").enumValueIndex = (int)DropdownSettingId.CapFramerate;
        so.FindProperty("settingNameText").objectReferenceValue = labelTemplate;
        so.FindProperty("dropdown").objectReferenceValue = dropdown;
        so.FindProperty("optionLabelsOverride").arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();

        EnsureRowLayout(row, labelTemplate);

        if (labelTemplate)
        {
            labelTemplate.text = DropdownSettingsStore.GetDisplayName(DropdownSettingId.CapFramerate);
            labelTemplate.color = labelColor;
        }

        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefabAsset)
            PrefabUtility.SaveAsPrefabAsset(row, PrefabPath);
        else
            PrefabUtility.SaveAsPrefabAsset(row, PrefabPath);

        Object.DestroyImmediate(row);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SettingsDropdownRowPrefabCreator] Saved '{PrefabPath}'.");
    }

    private static void EnsureRowLayout(GameObject row, TMP_Text labelTemplate)
    {
        HorizontalLayoutGroup hlg = row.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
        {
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.padding = new RectOffset(8, 8, 0, 0);
        }

        if (labelTemplate)
        {
            GameObject labelGo = labelTemplate.gameObject;
            LayoutElement le = labelGo.GetComponent<LayoutElement>() ?? labelGo.AddComponent<LayoutElement>();
            le.minWidth = 0f;
            le.preferredWidth = -1f;
            le.flexibleWidth = 1f;
            le.minHeight = 40f;
            le.preferredHeight = 40f;
            le.flexibleHeight = 0f;
            le.layoutPriority = 1;

            ContentSizeFitter fitter = labelGo.GetComponent<ContentSizeFitter>() ?? labelGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform rt = labelTemplate.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 0.5f);

            labelTemplate.textWrappingMode = TextWrappingModes.Normal;
            labelTemplate.overflowMode = TextOverflowModes.Overflow;
        }

        LayoutElement rowLe = row.GetComponent<LayoutElement>();
        if (rowLe != null)
        {
            rowLe.preferredHeight = 40f;
            rowLe.minHeight = 40f;
        }

        RectTransform rowRt = row.GetComponent<RectTransform>();
        if (rowRt != null)
            rowRt.sizeDelta = new Vector2(552f, 40f);
    }

    private static void ApplyDropdownFont(TMP_Dropdown dropdown, TMP_FontAsset font, float fontSize, Color color)
    {
        if (!dropdown)
            return;

        if (dropdown.captionText)
        {
            if (font)
                dropdown.captionText.font = font;
            dropdown.captionText.fontSize = fontSize;
            dropdown.captionText.color = color;
        }

        if (dropdown.itemText)
        {
            if (font)
                dropdown.itemText.font = font;
            dropdown.itemText.fontSize = fontSize;
            dropdown.itemText.color = color;
        }
    }

    private static void StyleDropdown(TMP_Dropdown dropdown)
    {
        if (!dropdown)
            return;

        Color bg = new Color(0.35f, 0.32f, 0.32f, 0.85f);
        Color itemBg = new Color(0.42f, 0.39f, 0.39f, 0.98f);
        Color highlight = new Color(0.52f, 0.48f, 0.48f, 1f);

        if (dropdown.targetGraphic is Image target)
            target.color = bg;

        Image arrow = dropdown.transform.Find("Arrow")?.GetComponent<Image>();
        if (arrow)
            arrow.color = new Color(0.85f, 0.85f, 0.85f, 1f);

        if (!dropdown.template)
            return;

        Image templateBg = dropdown.template.GetComponent<Image>();
        if (templateBg)
            templateBg.color = itemBg;

        Toggle[] toggles = dropdown.template.GetComponentsInChildren<Toggle>(true);
        for (int i = 0; i < toggles.Length; i++)
        {
            Toggle t = toggles[i];
            if (!t)
                continue;

            ColorBlock cb = t.colors;
            cb.normalColor = itemBg;
            cb.highlightedColor = highlight;
            cb.selectedColor = highlight;
            cb.pressedColor = highlight;
            t.colors = cb;
        }
    }

    private static TMP_DefaultControls.Resources GetTmpResources()
    {
        return new TMP_DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
        };
    }
}
#endif
