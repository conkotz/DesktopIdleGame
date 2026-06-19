using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reusable equipped-arrow overlays (glow tint, etc.) for buffed ranged shots.
/// </summary>
[DisallowMultipleComponent]
public class EquippedArrowVisualEffects : MonoBehaviour
{
    public enum EffectId
    {
        LightningOutline = 0,
    }

    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField, Min(1f)] private float outlineScale = 1.1f;
    [SerializeField] private Color lightningOutlineColor = new Color(1f, 0.92f, 0.35f, 0.5f);

    private readonly HashSet<EffectId> _activeEffects = new();
    private Color _defaultTargetColor = Color.white;
    private bool _defaultTargetColorCaptured;
    private Transform _glowRoot;
    private SpriteRenderer _glowRenderer;
    private static Material s_AdditiveSpriteMaterial;

    private void Awake()
    {
        if (!targetRenderer)
            targetRenderer = GetComponent<SpriteRenderer>();

        CaptureDefaultTargetColor();
    }

    private void LateUpdate()
    {
        SyncGlowSprite();
    }

    public void SetEffectActive(EffectId effectId, bool active)
    {
        if (active)
            _activeEffects.Add(effectId);
        else
            _activeEffects.Remove(effectId);

        RefreshVisual();
    }

    public void ClearAllEffects()
    {
        _activeEffects.Clear();
        RefreshVisual();
    }

    public void ApplySettings(Color outlineColor, float scale, float offsetUnused = 0.025f)
    {
        lightningOutlineColor = outlineColor;
        outlineScale = Mathf.Max(1f, scale);
        RefreshVisual();
    }

    private void CaptureDefaultTargetColor()
    {
        if (!targetRenderer || _defaultTargetColorCaptured)
            return;

        _defaultTargetColor = targetRenderer.color;
        _defaultTargetColorCaptured = true;
    }

    private void RefreshVisual()
    {
        CaptureDefaultTargetColor();

        bool glowActive = _activeEffects.Contains(EffectId.LightningOutline);
        if (!glowActive)
        {
            if (_glowRoot != null)
                _glowRoot.gameObject.SetActive(false);

            if (targetRenderer)
                targetRenderer.color = _defaultTargetColor;

            return;
        }

        EnsureGlowRenderer();
        _glowRoot.gameObject.SetActive(true);
        _glowRenderer.color = lightningOutlineColor;

        if (targetRenderer)
        {
            Color tint = Color.Lerp(_defaultTargetColor, lightningOutlineColor, lightningOutlineColor.a);
            targetRenderer.color = tint;
        }

        SyncGlowSprite(force: true);
    }

    private void EnsureGlowRenderer()
    {
        if (_glowRoot != null)
            return;

        if (!targetRenderer)
            targetRenderer = GetComponent<SpriteRenderer>();
        if (!targetRenderer)
            return;

        var go = new GameObject("ArrowGlow");
        go.transform.SetParent(targetRenderer.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * outlineScale;
        _glowRoot = go.transform;

        _glowRenderer = go.AddComponent<SpriteRenderer>();
        _glowRenderer.drawMode = targetRenderer.drawMode;
        _glowRenderer.maskInteraction = targetRenderer.maskInteraction;
        Material additive = GetAdditiveSpriteMaterial();
        if (additive != null)
            _glowRenderer.sharedMaterial = additive;

        SyncGlowSprite(force: true);
    }

    private void SyncGlowSprite(bool force = false)
    {
        if (_glowRenderer == null || !targetRenderer)
            return;
        if (!_glowRoot.gameObject.activeSelf && !force)
            return;

        _glowRenderer.sprite = targetRenderer.sprite;
        _glowRenderer.flipX = targetRenderer.flipX;
        _glowRenderer.flipY = targetRenderer.flipY;
        _glowRenderer.sortingLayerID = targetRenderer.sortingLayerID;
        _glowRenderer.sortingOrder = targetRenderer.sortingOrder - 1;
        _glowRoot.localScale = Vector3.one * outlineScale;
    }

    private static Material GetAdditiveSpriteMaterial()
    {
        if (s_AdditiveSpriteMaterial != null)
            return s_AdditiveSpriteMaterial;

        Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null)
            shader = Shader.Find("Particles/Additive");
        if (shader == null)
            return null;

        s_AdditiveSpriteMaterial = new Material(shader);
        return s_AdditiveSpriteMaterial;
    }
}
