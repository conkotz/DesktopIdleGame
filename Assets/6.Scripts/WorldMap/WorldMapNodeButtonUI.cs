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
    [SerializeField] private GameObject selectedHighlight;
    [Header("Theme — Node Type Colors")]
    [SerializeField] private Color townColor = new Color32(196, 171, 128, 255);          // beige
    [SerializeField] private Color combatColor = new Color32(154, 82, 82, 255);          // lighter red
    [SerializeField] private Color gatheringColor = new Color32(161, 190, 168, 255);     // light/soft green
    [SerializeField] private Color enduranceTrialColor = new Color32(130, 88, 60, 255);  // reddy-brown
    [SerializeField] private Color specialColor = new Color32(177, 143, 63, 255);         // gold-ish
    [SerializeField] private Color dungeonColor = new Color32(88, 106, 122, 255);
    [SerializeField] private Color bossColor = new Color32(118, 60, 90, 255);
    [SerializeField] private Color fallbackColor = new Color32(74, 81, 95, 255);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color selectedTint = new Color(1f, 1f, 1f, 0.35f);          // fallback when Selected image has no Image component

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

        Color baseCol = ResolveNodeTypeColor(node);
        if (selected)
            baseCol = Blend(baseCol, ResolveSelectedTint());

        Color hoverCol = Lift(baseCol, 0.10f);
        Color pressedCol = Lift(baseCol, 0.18f);

        ColorBlock cb = button.colors;
        cb.normalColor = baseCol;
        cb.highlightedColor = hoverCol;
        cb.selectedColor = hoverCol;
        cb.pressedColor = pressedCol;
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        button.colors = cb;

        if (button.targetGraphic)
            button.targetGraphic.color = baseCol;

        if (nameText) nameText.color = textColor;
        if (typeText) typeText.color = textColor;
        if (stateText) stateText.color = textColor;
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

    private Color ResolveSelectedTint()
    {
        if (selectedHighlight)
        {
            Image img = selectedHighlight.GetComponent<Image>();
            if (img != null)
                return img.color;
        }

        return selectedTint;
    }
}
