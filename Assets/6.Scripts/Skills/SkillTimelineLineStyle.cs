using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared thickness and color for horizontal timeline spine + connector lines.</summary>
public static class SkillTimelineLineStyle
{
    /// <summary>Spine track, milestone stems, and choice-group branch/drops.</summary>
    public const float LineThickness = 4f;

    /// <summary>Gold progress overlay on the spine — slightly thicker than <see cref="LineThickness"/>.</summary>
    public const float ProgressThickness = 5f;

    /// <summary>Really dark brown — all dark timeline lines use this.</summary>
    public static readonly Color LineColor = new(0.1f, 0.075f, 0.055f, 1f);

    private static Sprite _lineSprite;

    public static Sprite LineSprite
    {
        get
        {
            if (_lineSprite != null)
                return _lineSprite;

            _lineSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                100f);
            return _lineSprite;
        }
    }

    public static void Apply(Image image)
    {
        if (image == null)
            return;

        image.sprite = LineSprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = LineColor;
        image.raycastTarget = false;
        image.maskable = true;
        image.enabled = true;
    }

    public static void ApplyHorizontalBar(RectTransform rt, float centerX, float centerY, float width)
    {
        if (rt == null)
            return;

        float thickness = LineThickness;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(centerX, centerY);
        rt.sizeDelta = new Vector2(Mathf.Max(thickness, width), thickness);
        Apply(rt.GetComponent<Image>());
    }

    public static void ApplyVerticalBar(RectTransform rt, float centerX, float topY, float height)
    {
        if (rt == null)
            return;

        float thickness = LineThickness;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(centerX, topY);
        rt.sizeDelta = new Vector2(thickness, Mathf.Max(thickness, height));
        Apply(rt.GetComponent<Image>());
    }

    public static void ApplySegment(RectTransform rt, Vector2 lineStart, Vector2 lineEnd, float thickness)
    {
        if (rt == null)
            return;

        Vector2 seg = lineEnd - lineStart;
        float segLen = seg.magnitude;
        if (segLen <= 0.001f)
            return;

        thickness = Mathf.Max(LineThickness, thickness);
        bool mostlyVertical = Mathf.Abs(seg.x) < Mathf.Abs(seg.y) * 0.5f;
        bool mostlyHorizontal = Mathf.Abs(seg.y) < Mathf.Abs(seg.x) * 0.5f;

        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.localScale = Vector3.one;

        if (mostlyVertical)
        {
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = lineStart;
            rt.localRotation = Quaternion.identity;
            rt.sizeDelta = new Vector2(thickness, segLen);
        }
        else if (mostlyHorizontal)
        {
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(lineStart.x, lineStart.y);
            rt.localRotation = Quaternion.identity;
            rt.sizeDelta = new Vector2(segLen, thickness);
        }
        else
        {
            Vector2 pos = lineStart + seg * 0.5f;
            float angle = Mathf.Atan2(seg.y, seg.x) * Mathf.Rad2Deg;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
            rt.sizeDelta = new Vector2(segLen, thickness);
        }

        Apply(rt.GetComponent<Image>());
    }
}
