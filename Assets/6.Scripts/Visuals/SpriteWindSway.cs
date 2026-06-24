using UnityEngine;

/// <summary>
/// Pushes wind sway bounds and per-instance timing into the sprite wind shader via MaterialPropertyBlock.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteWindSway : MonoBehaviour
{
    [SerializeField] private float windStrength = 0.15f;
    [SerializeField] private float windSpeed = 0.32f;
    [SerializeField, Range(0f, 1f)] private float baseAnchorHeight = 0.15f;
    [SerializeField] private float phaseOffset;

    private static readonly int WindStrengthId = Shader.PropertyToID("_WindStrength");
    private static readonly int WindSpeedId = Shader.PropertyToID("_WindSpeed");
    private static readonly int BaseAnchorHeightId = Shader.PropertyToID("_BaseAnchorHeight");
    private static readonly int PhaseOffsetId = Shader.PropertyToID("_PhaseOffset");
    private static readonly int SwayWaveId = Shader.PropertyToID("_SwayWave");
    private static readonly int SpriteBottomYId = Shader.PropertyToID("_SpriteBottomY");
    private static readonly int SpriteHeightId = Shader.PropertyToID("_SpriteHeight");

    private SpriteRenderer _spriteRenderer;
    private MaterialPropertyBlock _propertyBlock;
    private bool _swayEnabled = true;
    private Sprite _lastAppliedSprite;

    public float WindStrength
    {
        get => windStrength;
        set => windStrength = value;
    }

    public float WindSpeed
    {
        get => windSpeed;
        set => windSpeed = value;
    }

    public float BaseAnchorHeight
    {
        get => baseAnchorHeight;
        set => baseAnchorHeight = value;
    }

    public float PhaseOffset
    {
        get => phaseOffset;
        set => phaseOffset = value;
    }

    public bool SwayEnabled => _swayEnabled;

    public void SetSwayEnabled(bool enabled)
    {
        _swayEnabled = enabled;
        Apply();
    }

    private void OnEnable()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _lastAppliedSprite = null;
        Apply();
    }

    private void OnValidate()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        _lastAppliedSprite = null;
        Apply();
    }

    private void LateUpdate()
    {
        if (_spriteRenderer == null)
            return;

        if (!_swayEnabled)
        {
            ApplyStatic(0f);
            return;
        }

        if (_spriteRenderer.sprite != _lastAppliedSprite)
            _lastAppliedSprite = null;

        float wave = Mathf.Sin((Application.isPlaying ? Time.time : Time.realtimeSinceStartup) * windSpeed + phaseOffset);
        ApplyStatic(wave);
    }

    private void Apply()
    {
        if (!_swayEnabled)
        {
            ApplyStatic(0f);
            return;
        }

        float wave = Mathf.Sin((Application.isPlaying ? Time.time : Time.realtimeSinceStartup) * windSpeed + phaseOffset);
        ApplyStatic(wave);
    }

    private void ApplyStatic(float swayWave)
    {
        if (_spriteRenderer == null)
            return;

        Sprite sprite = _spriteRenderer.sprite;
        if (!sprite)
        {
            _spriteRenderer.SetPropertyBlock(null);
            return;
        }

        _propertyBlock ??= new MaterialPropertyBlock();
        _propertyBlock.Clear();

        Bounds bounds = sprite.bounds;
        float spriteHeight = Mathf.Max(bounds.size.y, 0.001f);

        _propertyBlock.SetFloat(WindStrengthId, _swayEnabled ? windStrength : 0f);
        _propertyBlock.SetFloat(WindSpeedId, windSpeed);
        _propertyBlock.SetFloat(BaseAnchorHeightId, baseAnchorHeight);
        _propertyBlock.SetFloat(PhaseOffsetId, phaseOffset);
        _propertyBlock.SetFloat(SwayWaveId, swayWave);
        _propertyBlock.SetFloat(SpriteBottomYId, bounds.min.y);
        _propertyBlock.SetFloat(SpriteHeightId, spriteHeight);

        _spriteRenderer.SetPropertyBlock(_propertyBlock);
        _lastAppliedSprite = sprite;
    }
}
