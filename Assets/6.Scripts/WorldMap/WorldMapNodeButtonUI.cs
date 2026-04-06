using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorldMapNodeButtonUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text typeText;
    [SerializeField] private TMP_Text stateText;
    [Tooltip("Optional overlay when this row is the selected node. Leave empty if you only use the Button for selection.")]
    [SerializeField] private GameObject selectedHighlight;

    [Header("Theme strip")]
    [Tooltip("Top strip Image — node-type palette applies here only, not the full row.")]
    [SerializeField] private Image colourIcon;

    [Header("Row background")]
    [Tooltip("If empty, uses the Button's target graphic (usually the root row Image).")]
    [SerializeField] private Image rowBackgroundImage;
    [Tooltip("Solid fill for the row. Node-type theme colors go on Colour Icon only.")]
    [SerializeField] private Color rowBackgroundColor = new Color(0.29f, 0.32f, 0.37f, 1f);

    [Header("Theme — Node Type Colors")]
    [SerializeField] private Color townColor = new Color32(196, 171, 128, 255);
    [SerializeField] private Color combatColor = new Color32(154, 82, 82, 255);
    [SerializeField] private Color gatheringColor = new Color32(161, 190, 168, 255);
    [SerializeField] private Color enduranceTrialColor = new Color32(130, 88, 60, 255);
    [SerializeField] private Color specialColor = new Color32(177, 143, 63, 255);
    [SerializeField] private Color dungeonColor = new Color32(88, 106, 122, 255);
    [SerializeField] private Color bossColor = new Color32(118, 60, 90, 255);
    [SerializeField] private Color fallbackColor = new Color32(74, 81, 95, 255);
    [Tooltip("When this row is selected, blends toward this color on the Colour Icon only (alpha = strength). Default alpha 0 = strip stays pure node-type color; selection is via the Button only.")]
    [SerializeField] private Color selectedTint = new Color(1f, 1f, 1f, 0f);

    private MapNodeDefinition _node;
    private Action<MapNodeDefinition> _onSelected;
    public MapNodeDefinition Node => _node;

    private void Awake()
    {
        if (!button)
            button = GetComponent<Button>();

        if (button)
            button.onClick.AddListener(OnClick);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!colourIcon)
            return;
        Image row = GetRowBackgroundImage();
        if (row)
            row.color = rowBackgroundColor;
    }
#endif

    private void OnDestroy()
    {
        if (button)
            button.onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        if (_node != null)
            _onSelected?.Invoke(_node);
    }

    public void Bind(
        MapNodeDefinition node,
        string stateLabel,
        bool selected,
        Action<MapNodeDefinition> onSelected)
    {
        _node = node;
        _onSelected = onSelected;

        if (nameText)
            nameText.text = node ? node.displayName : "—";

        if (typeText)
            typeText.text = node ? node.nodeType.ToString() : "";

        if (stateText)
            stateText.text = stateLabel ?? "";

        ApplyNodeTypeTheme(node, selected);
        SetSelected(selected);
    }

    public void SetSelected(bool selected)
    {
        ApplyNodeTypeTheme(_node, selected);
        if (selectedHighlight)
            selectedHighlight.SetActive(selected);
    }

    private void ApplyNodeTypeTheme(MapNodeDefinition node, bool selected)
    {
        if (!button)
            return;

        Color themeForIcon = ResolveNodeTypeColor(node);
        if (selected && selectedTint.a > 0f)
            themeForIcon = Blend(themeForIcon, selectedTint);

        if (colourIcon)
        {
            colourIcon.color = themeForIcon;

            Image rowBg = GetRowBackgroundImage();
            if (rowBg)
                rowBg.color = rowBackgroundColor;

            button.transition = Selectable.Transition.None;
            return;
        }

        button.transition = Selectable.Transition.ColorTint;

        Color hoverColLegacy = Lift(themeForIcon, 0.10f);
        Color pressedColLegacy = Lift(themeForIcon, 0.18f);

        ColorBlock cbLegacy = button.colors;
        cbLegacy.normalColor = themeForIcon;
        cbLegacy.highlightedColor = hoverColLegacy;
        cbLegacy.selectedColor = hoverColLegacy;
        cbLegacy.pressedColor = pressedColLegacy;
        cbLegacy.colorMultiplier = 1f;
        cbLegacy.fadeDuration = 0.08f;
        button.colors = cbLegacy;

        if (button.targetGraphic)
            button.targetGraphic.color = themeForIcon;
    }

    private Color ResolveNodeTypeColor(MapNodeDefinition node)
    {
        if (!node)
            return fallbackColor;
        return GetThemeColorForNodeType(node.nodeType);
    }

    /// <summary>Same palette as row buttons — use for panels (details, etc.) via <see cref="LevelSelectPageUI"/>.</summary>
    public Color GetThemeColorForNode(MapNodeDefinition node) => ResolveNodeTypeColor(node);

    /// <summary>Theme color for a node type (matches the serialized fields above).</summary>
    public Color GetThemeColorForNodeType(MapNodeType type)
    {
        return type switch
        {
            MapNodeType.Town => townColor,
            MapNodeType.Combat => combatColor,
            MapNodeType.Gathering => gatheringColor,
            MapNodeType.EnduranceTrial => enduranceTrialColor,
            MapNodeType.Special => specialColor,
            MapNodeType.Dungeon => dungeonColor,
            MapNodeType.Boss => bossColor,
            _ => fallbackColor
        };
    }

    private static Color Lift(Color c, float amount)
    {
        amount = Mathf.Clamp01(amount);
        return new Color(
            Mathf.Clamp01(c.r + (1f - c.r) * amount),
            Mathf.Clamp01(c.g + (1f - c.g) * amount),
            Mathf.Clamp01(c.b + (1f - c.b) * amount),
            c.a);
    }

    private static Color Blend(Color baseCol, Color tint)
    {
        float t = Mathf.Clamp01(tint.a);
        return new Color(
            Mathf.Lerp(baseCol.r, tint.r, t),
            Mathf.Lerp(baseCol.g, tint.g, t),
            Mathf.Lerp(baseCol.b, tint.b, t),
            baseCol.a);
    }

    private Image GetRowBackgroundImage()
    {
        if (rowBackgroundImage)
            return rowBackgroundImage;
        if (button && button.targetGraphic is Image img)
            return img;
        return null;
    }
}
