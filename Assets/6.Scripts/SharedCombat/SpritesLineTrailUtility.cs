using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Shared TrailRenderer setup for 2D sprite-style colored lines (matches ability VFX trails).
/// </summary>
public static class SpritesLineTrailUtility
{
    private static Material s_BaseLineMaterial;

    public static Material GetBaseLineMaterial()
    {
        if (s_BaseLineMaterial != null)
            return s_BaseLineMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            return null;

        s_BaseLineMaterial = new Material(shader);
        s_BaseLineMaterial.mainTexture = Texture2D.whiteTexture;
        SetMaterialColor(s_BaseLineMaterial, Color.white);
        return s_BaseLineMaterial;
    }

    public static Material CreateColoredTrailMaterial(Color color)
    {
        Material source = GetBaseLineMaterial();
        if (source == null)
            return null;

        Material instance = new Material(source);
        SetMaterialColor(instance, color);
        return instance;
    }

    public static void ConfigureTrail(
        TrailRenderer trail,
        Color color,
        float width,
        float time,
        int sortingLayerId = 0,
        int sortingOrder = 40)
    {
        if (trail == null)
            return;

        color.a = Mathf.Clamp01(color.a);

        Material coloredMaterial = CreateColoredTrailMaterial(color);
        if (coloredMaterial != null)
            trail.material = coloredMaterial;
        else if (GetBaseLineMaterial() != null)
            trail.sharedMaterial = GetBaseLineMaterial();

        trail.time = Mathf.Max(0.05f, time);
        trail.minVertexDistance = 0.015f;
        trail.widthMultiplier = Mathf.Max(0.01f, width);
        trail.numCornerVertices = 4;
        trail.numCapVertices = 2;
        trail.alignment = LineAlignment.TransformZ;
        trail.textureMode = LineTextureMode.Stretch;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.emitting = true;

        if (sortingLayerId != 0)
            trail.sortingLayerID = sortingLayerId;

        trail.sortingOrder = sortingOrder;

        ApplyVertexColors(trail, color);
        trail.Clear();
    }

    public static void ApplyVertexColors(TrailRenderer trail, Color color)
    {
        if (trail == null)
            return;

        color.a = Mathf.Clamp01(color.a);
        trail.startColor = color;
        trail.endColor = color;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, 1f)
            },
            new[]
            {
                new GradientAlphaKey(color.a, 0f),
                new GradientAlphaKey(color.a, 1f)
            });
        trail.colorGradient = gradient;
    }

    public static void ConfigureTrailMaterialColor(Material material, Color color)
    {
        SetMaterialColor(material, color);
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        material.color = color;
    }
}
