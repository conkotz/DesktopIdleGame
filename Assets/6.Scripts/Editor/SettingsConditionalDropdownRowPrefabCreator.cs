#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Creates / updates <c>Assets/2.Prefabs/UI/SettingsConditionalDropdownrow.prefab</c>.</summary>
public static class SettingsConditionalDropdownRowPrefabCreator
{
    private const string PrefabPath = "Assets/2.Prefabs/UI/SettingsConditionalDropdownrow.prefab";
    private const string ToggleRowPath = "Assets/2.Prefabs/UI/SettingsToggleRow.prefab";
    private const float RowWidth = 552f;
    private const float HalfRowHeight = 40f;
    private const float FullRowHeight = 80f;

    [MenuItem("Desktop Idle/UI/Create Settings Conditional Dropdown Row Prefab")]
    public static void CreateOrUpdateFromMenu() => CreateOrUpdate();

    public static void CreateOrUpdate()
    {
        GameObject toggleRowTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(ToggleRowPath);
        if (!toggleRowTemplate)
        {
            Debug.LogError($"[SettingsConditionalDropdownRowPrefabCreator] Missing template at '{ToggleRowPath}'.");
            return;
        }

        GameObject root = new GameObject("SettingsConditionalDropdownrow", typeof(RectTransform));
        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0f, 1f);
        rootRt.anchorMax = new Vector2(0f, 1f);
        rootRt.pivot = new Vector2(0f, 1f);
        rootRt.sizeDelta = new Vector2(RowWidth, FullRowHeight);

        Image rowBg = root.AddComponent<Image>();
        rowBg.color = new Color(0.4792453f, 0.43312925f, 0.43312925f, 0.13725491f);
        rowBg.raycastTarget = true;

        LayoutElement rootLe = root.AddComponent<LayoutElement>();
        rootLe.minHeight = FullRowHeight;
        rootLe.preferredHeight = FullRowHeight;
        rootLe.flexibleWidth = 1f;

        VerticalLayoutGroup rootVlg = root.AddComponent<VerticalLayoutGroup>();
        rootVlg.padding = new RectOffset(0, 0, 0, 0);
        rootVlg.spacing = 0f;
        rootVlg.childAlignment = TextAnchor.UpperLeft;
        rootVlg.childControlWidth = true;
        rootVlg.childControlHeight = true;
        rootVlg.childForceExpandWidth = true;
        rootVlg.childForceExpandHeight = false;

        GameObject toggleRow = BuildToggleRow(toggleRowTemplate);
        toggleRow.transform.SetParent(root.transform, false);

        GameObject dropdownsRow = BuildDropdownsRow(toggleRow);
        dropdownsRow.transform.SetParent(root.transform, false);

        ConditionalAutoBattleSettingsRowUI rowUi = root.AddComponent<ConditionalAutoBattleSettingsRowUI>();
        SerializedObject so = new SerializedObject(rowUi);
        so.FindProperty("enableLabelText").objectReferenceValue =
            toggleRow.transform.Find("ActionLabelText")?.GetComponent<TMP_Text>();
        so.FindProperty("enableToggle").objectReferenceValue =
            toggleRow.transform.Find("Toggle")?.GetComponent<Toggle>();
        so.FindProperty("set1LabelText").objectReferenceValue =
            dropdownsRow.transform.Find("Set1Column/ColumnLabel")?.GetComponent<TMP_Text>();
        so.FindProperty("set1Dropdown").objectReferenceValue =
            dropdownsRow.transform.Find("Set1Column/Dropdown")?.GetComponent<TMP_Dropdown>();
        so.FindProperty("set2LabelText").objectReferenceValue =
            dropdownsRow.transform.Find("Set2Column/ColumnLabel")?.GetComponent<TMP_Text>();
        so.FindProperty("set2Dropdown").objectReferenceValue =
            dropdownsRow.transform.Find("Set2Column/Dropdown")?.GetComponent<TMP_Dropdown>();
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefabAsset)
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        else
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SettingsConditionalDropdownRowPrefabCreator] Saved '{PrefabPath}'.");
    }

    private static GameObject BuildToggleRow(GameObject toggleRowTemplate)
    {
        GameObject row = Object.Instantiate(toggleRowTemplate);
        row.name = "ToggleRow";

        Object.DestroyImmediate(row.GetComponent<ToggleSettingsRowUI>());

        RectTransform rowRt = row.GetComponent<RectTransform>();
        if (rowRt)
            rowRt.sizeDelta = new Vector2(RowWidth, HalfRowHeight);

        LayoutElement rowLe = row.GetComponent<LayoutElement>();
        if (rowLe)
        {
            rowLe.preferredHeight = HalfRowHeight;
            rowLe.minHeight = HalfRowHeight;
        }

        TMP_Text label = row.transform.Find("ActionLabelText")?.GetComponent<TMP_Text>();
        if (label)
            label.text = ConditionalAutoBattleSettingsStore.DisplayName;

        Toggle toggle = row.transform.Find("Toggle")?.GetComponent<Toggle>();
        if (toggle)
            toggle.SetIsOnWithoutNotify(false);

        return row;
    }

    private static GameObject BuildDropdownsRow(GameObject toggleRow)
    {
        TMP_Text labelTemplate = toggleRow.transform.Find("ActionLabelText")?.GetComponent<TMP_Text>();
        TMP_FontAsset font = labelTemplate != null ? labelTemplate.font : null;
        float fontSize = labelTemplate != null ? labelTemplate.fontSize : 16f;
        Color labelColor = labelTemplate != null ? labelTemplate.color : Color.white;

        GameObject row = new GameObject("DropdownsRow", typeof(RectTransform));
        RectTransform rowRt = row.GetComponent<RectTransform>();
        rowRt.sizeDelta = new Vector2(RowWidth, HalfRowHeight);

        LayoutElement rowLe = row.AddComponent<LayoutElement>();
        rowLe.minHeight = HalfRowHeight;
        rowLe.preferredHeight = HalfRowHeight;
        rowLe.flexibleWidth = 1f;

        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(8, 8, 0, 0);
        hlg.spacing = 8f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = false;

        BuildDropdownColumn(row.transform, "Set1Column", "Set 1", font, fontSize, labelColor);
        BuildDropdownColumn(row.transform, "Set2Column", "Set 2", font, fontSize, labelColor);

        return row;
    }

    private static void BuildDropdownColumn(
        Transform parent,
        string columnName,
        string labelText,
        TMP_FontAsset font,
        float fontSize,
        Color labelColor)
    {
        GameObject column = new GameObject(columnName, typeof(RectTransform));
        column.transform.SetParent(parent, false);

        LayoutElement columnLe = column.AddComponent<LayoutElement>();
        columnLe.flexibleWidth = 1f;
        columnLe.minHeight = HalfRowHeight;
        columnLe.preferredHeight = HalfRowHeight;

        HorizontalLayoutGroup hlg = column.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        GameObject labelGo = new GameObject("ColumnLabel", typeof(RectTransform));
        labelGo.transform.SetParent(column.transform, false);
        TMP_Text label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = labelText;
        if (font)
            label.font = font;
        label.fontSize = fontSize;
        label.color = labelColor;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;

        LayoutElement labelLe = labelGo.AddComponent<LayoutElement>();
        labelLe.minWidth = 52f;
        labelLe.preferredWidth = 52f;
        labelLe.flexibleWidth = 0f;

        GameObject dropdownRoot = TMP_DefaultControls.CreateDropdown(GetTmpResources());
        dropdownRoot.name = "Dropdown";
        dropdownRoot.transform.SetParent(column.transform, false);

        LayoutElement dropdownLe = dropdownRoot.GetComponent<LayoutElement>() ?? dropdownRoot.AddComponent<LayoutElement>();
        dropdownLe.minWidth = 120f;
        dropdownLe.preferredWidth = -1f;
        dropdownLe.flexibleWidth = 1f;
        dropdownLe.minHeight = 36f;
        dropdownLe.preferredHeight = 36f;

        TMP_Dropdown dropdown = dropdownRoot.GetComponent<TMP_Dropdown>();
        ApplyDropdownFont(dropdown, font, fontSize, labelColor);
        StyleDropdown(dropdown);
        TmpDropdownListLayoutUtility.ApplyTallListItems(dropdown);
        dropdown.interactable = false;
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
            dropdown.itemText.fontSize = Mathf.Min(fontSize, 14f);
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
