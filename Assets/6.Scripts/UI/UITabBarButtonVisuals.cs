using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared selected/unselected styling for compact tab bars (SkillsAbilityPageNEW skill tabs, world map node filters, inventory filters, gear sets, stats tabs).
/// </summary>
public static class UITabBarButtonVisuals
{
    public static readonly Color SelectedImageColor = new Color(0.36078432f, 0.26666668f, 0.12941177f, 1f);
    public static readonly Color NormalImageColor = new Color(0.18431373f, 0.16078432f, 0.13725491f, 1f);
    public static readonly Color SelectedOutlineColor = new Color(0.6039216f, 0.48235294f, 0.2627451f, 0.5f);
    public static readonly Vector2 SelectedOutlineDistance = new Vector2(4f, -4f);

    public static void Apply(Button button, bool selected)
    {
        if (button == null)
            return;

        Image image = button.targetGraphic as Image;
        if (image == null)
            image = button.GetComponent<Image>();

        if (image != null)
            image.color = selected ? SelectedImageColor : NormalImageColor;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.9607843f, 0.9607843f, 0.9607843f, 1f);
        colors.pressedColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;

        Outline outline = button.GetComponent<Outline>();
        if (selected)
        {
            if (outline == null)
                outline = button.gameObject.AddComponent<Outline>();

            outline.effectColor = SelectedOutlineColor;
            outline.effectDistance = SelectedOutlineDistance;
            outline.useGraphicAlpha = true;
            outline.enabled = true;
        }
        else if (outline != null)
        {
            outline.enabled = false;
        }
    }
}
