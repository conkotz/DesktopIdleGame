using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Move-pivot mode dropdown for toggling individual window ghosts on/off.</summary>
[DisallowMultipleComponent]
public sealed class PivotWindowVisibilityMenu : MonoBehaviour
{
    private const float RowHeight = 32f;
    private const float PanelMinWidth = 280f;
    private const float PanelPadding = 8f;
    private const float GapBelowButton = 4f;

    private RectTransform _panelRoot;
    private RectTransform _rowsRoot;
    private Button _toggleButton;
    private readonly List<Toggle> _rowToggles = new(12);
    private readonly Dictionary<string, Toggle> _toggleByKey = new(StringComparer.Ordinal);
    private Action<string, bool> _onVisibilityChanged;
    private bool _suppressCallbacks;

    public static PivotWindowVisibilityMenu AttachToButton(
        Button toggleButton,
        Action<string, bool> onVisibilityChanged)
    {
        if (!toggleButton)
            return null;

        PivotWindowVisibilityMenu menu = toggleButton.GetComponent<PivotWindowVisibilityMenu>();
        if (!menu)
            menu = toggleButton.gameObject.AddComponent<PivotWindowVisibilityMenu>();

        menu._toggleButton = toggleButton;
        menu._onVisibilityChanged = onVisibilityChanged;
        menu.EnsureUi();
        return menu;
    }

    public void SetToggleButton(Button button)
    {
        _toggleButton = button;
        if (_panelRoot)
            RepositionPanel();
    }

    public void RebuildRows(IReadOnlyList<UIWindowLayoutBinding> bindings, IReadOnlyDictionary<string, bool> visibilityByKey)
    {
        EnsureUi();
        if (_rowsRoot == null)
            return;

        _suppressCallbacks = true;
        ClearRows();

        int count = bindings != null ? bindings.Count : 0;
        for (int i = 0; i < count; i++)
        {
            UIWindowLayoutBinding binding = bindings[i];
            if (binding == null || string.IsNullOrWhiteSpace(binding.MemoryKey))
                continue;

            bool visible = visibilityByKey == null
                           || !visibilityByKey.TryGetValue(binding.MemoryKey, out bool stored)
                           || stored;

            CreateRow(binding.DisplayLabel, binding.MemoryKey, visible);
        }

        float panelHeight = Mathf.Max(RowHeight + PanelPadding * 2f, count * RowHeight + PanelPadding * 2f);
        if (_panelRoot)
        {
            float panelWidth = Mathf.Max(PanelMinWidth, GetButtonWidth());
            _panelRoot.sizeDelta = new Vector2(panelWidth, panelHeight);
            RepositionPanel();
        }

        _suppressCallbacks = false;
        SetExpanded(false);
    }

    public void SetExpanded(bool expanded)
    {
        if (!_panelRoot)
            return;

        _panelRoot.gameObject.SetActive(expanded);
        if (expanded)
            RepositionPanel();
    }

    public void ToggleExpanded()
    {
        if (_panelRoot == null)
            return;

        SetExpanded(!_panelRoot.gameObject.activeSelf);
    }

    public void SetRowVisible(string memoryKey, bool visible)
    {
        if (string.IsNullOrWhiteSpace(memoryKey))
            return;

        if (_toggleByKey.TryGetValue(memoryKey, out Toggle toggle) && toggle)
            toggle.isOn = visible;
    }

    private void EnsureUi()
    {
        if (_panelRoot != null)
            return;

        if (!_toggleButton)
            return;

        _panelRoot = CreateUiObject("DropdownPanel", _toggleButton.transform, typeof(RectTransform), typeof(Image))
            .GetComponent<RectTransform>();
        Image panelImage = _panelRoot.GetComponent<Image>();
        panelImage.color = new Color(0.14f, 0.13f, 0.11f, 0.96f);
        panelImage.raycastTarget = true;

        _panelRoot.pivot = new Vector2(0.5f, 1f);
        _panelRoot.sizeDelta = new Vector2(PanelMinWidth, 240f);
        RepositionPanel();

        _rowsRoot = CreateUiObject("Rows", _panelRoot, typeof(RectTransform), typeof(VerticalLayoutGroup))
            .GetComponent<RectTransform>();
        StretchFull(_rowsRoot);
        VerticalLayoutGroup layout = _rowsRoot.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset((int)PanelPadding, (int)PanelPadding, (int)PanelPadding, (int)PanelPadding);
        layout.spacing = 4f;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;

        _panelRoot.gameObject.SetActive(false);
    }

    private void RepositionPanel()
    {
        if (!_panelRoot || !_toggleButton)
            return;

        RectTransform buttonRect = _toggleButton.transform as RectTransform;
        if (!buttonRect)
            return;

        _panelRoot.anchorMin = new Vector2(0.5f, 0f);
        _panelRoot.anchorMax = new Vector2(0.5f, 0f);
        _panelRoot.pivot = new Vector2(0.5f, 1f);
        _panelRoot.anchoredPosition = new Vector2(0f, -GapBelowButton);
        _panelRoot.sizeDelta = new Vector2(Mathf.Max(PanelMinWidth, GetButtonWidth()), _panelRoot.sizeDelta.y);
        _panelRoot.SetAsLastSibling();
    }

    private float GetButtonWidth()
    {
        if (!_toggleButton)
            return PanelMinWidth;

        RectTransform buttonRect = _toggleButton.transform as RectTransform;
        return buttonRect ? buttonRect.rect.width : PanelMinWidth;
    }

    private void ClearRows()
    {
        _rowToggles.Clear();
        _toggleByKey.Clear();

        if (!_rowsRoot)
            return;

        for (int i = _rowsRoot.childCount - 1; i >= 0; i--)
            Destroy(_rowsRoot.GetChild(i).gameObject);
    }

    private void CreateRow(string label, string memoryKey, bool visible)
    {
        GameObject rowGo = CreateUiObject($"Row_{memoryKey}", _rowsRoot, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowGo.GetComponent<LayoutElement>().preferredHeight = RowHeight;

        HorizontalLayoutGroup rowLayout = rowGo.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 8f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandHeight = true;

        TMP_Text labelText = CreateLabel(rowGo.transform, label);
        labelText.fontSize = 15f;

        Toggle toggle = CreateToggle(rowGo.transform, visible);
        toggle.onValueChanged.AddListener(isOn => HandleToggleChanged(memoryKey, isOn));
        _rowToggles.Add(toggle);
        _toggleByKey[memoryKey] = toggle;
    }

    private void HandleToggleChanged(string memoryKey, bool visible)
    {
        if (_suppressCallbacks)
            return;

        _onVisibilityChanged?.Invoke(memoryKey, visible);
    }

    private static Toggle CreateToggle(Transform parent, bool isOn)
    {
        GameObject toggleGo = CreateUiObject("Toggle", parent, typeof(RectTransform), typeof(Toggle), typeof(Image), typeof(LayoutElement));
        LayoutElement layoutElement = toggleGo.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = 24f;
        layoutElement.preferredHeight = 24f;
        layoutElement.flexibleWidth = 0f;
        Image bg = toggleGo.GetComponent<Image>();
        bg.color = new Color(0.22f, 0.2f, 0.16f, 1f);

        Toggle toggle = toggleGo.GetComponent<Toggle>();

        GameObject checkGo = CreateUiObject("Checkmark", toggleGo.transform, typeof(RectTransform), typeof(Image));
        StretchFull(checkGo.GetComponent<RectTransform>());
        Image checkImage = checkGo.GetComponent<Image>();
        checkImage.color = new Color(0.35f, 0.75f, 0.45f, 1f);

        toggle.targetGraphic = bg;
        toggle.graphic = checkImage;
        toggle.isOn = isOn;
        return toggle;
    }

    private static TMP_Text CreateLabel(Transform parent, string text)
    {
        GameObject labelGo = CreateUiObject("Label", parent, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        labelGo.GetComponent<LayoutElement>().flexibleWidth = 1f;
        TMP_Text tmp = labelGo.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 16f;
        tmp.color = new Color(0.95f, 0.9f, 0.82f, 1f);
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static GameObject CreateUiObject(string name, Transform parent, params Type[] components)
    {
        GameObject go = new GameObject(name, components);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        if (go.TryGetComponent(out RectTransform rt))
            rt.localScale = Vector3.one;
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        if (!rt)
            return;

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
