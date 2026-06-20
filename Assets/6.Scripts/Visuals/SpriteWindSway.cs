using UnityEngine;

/// <summary>
/// Pushes wind sway bounds and per-instance timing into the sprite wind shader via MaterialPropertyBlock.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteWindSway : MonoBehaviour
{
    [SerializeField] private float windStrength = 0.1f;
    [SerializeField] private float windSpeed = 1.2f;
    [SerializeField] private float baseAnchorHeight = 0.2f;
    [SerializeField] private float phaseOffset;
    [SerializeField] private float windVariation = 0.4f;
    [SerializeField] private bool randomizeOnAwake = true;

    private static readonly int WindStrengthId = Shader.PropertyToID("_WindStrength");
    private static readonly int WindSpeedId = Shader.PropertyToID("_WindSpeed");
    private static readonly int BaseAnchorHeightId = Shader.PropertyToID("_BaseAnchorHeight");
    private static readonly int PhaseOffsetId = Shader.PropertyToID("_PhaseOffset");
    private static readonly int WindVariationId = Shader.PropertyToID("_WindVariation");
    private static readonly int SpriteBottomYId = Shader.PropertyToID("_SpriteBottomY");
    private static readonly int SpriteHeightId = Shader.PropertyToID("_SpriteHeight");
    private static readonly int SpriteCenterXId = Shader.PropertyToID("_SpriteCenterX");

    private SpriteRenderer _spriteRenderer;
    private MaterialPropertyBlock _propertyBlock;
    private float _baseWindSpeed;

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

    private void Awake()
    {
        _baseWindSpeed = windSpeed;

        if (!randomizeOnAwake || !Application.isPlaying)
            return;

        phaseOffset += Random.Range(0f, Mathf.PI * 2f);
        windVariation *= Random.Range(0.92f, 1.08f);
        windSpeed = _baseWindSpeed * Random.Range(0.94f, 1.06f);
    }

    private void OnEnable()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_baseWindSpeed <= 0f)
            _baseWindSpeed = windSpeed;

        Apply();
    }

    private void OnValidate()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_baseWindSpeed <= 0f)
            _baseWindSpeed = windSpeed;

        Apply();
    }

    private void Apply()
    {
        if (_spriteRenderer == null)
            return;

        _propertyBlock ??= new MaterialPropertyBlock();
        _spriteRenderer.GetPropertyBlock(_propertyBlock);

        Bounds localBounds = _spriteRenderer.localBounds;
        _propertyBlock.SetFloat(WindStrengthId, windStrength);
        _propertyBlock.SetFloat(WindSpeedId, windSpeed);
        _propertyBlock.SetFloat(BaseAnchorHeightId, baseAnchorHeight);
        _propertyBlock.SetFloat(PhaseOffsetId, phaseOffset);
        _propertyBlock.SetFloat(WindVariationId, windVariation);
        _propertyBlock.SetFloat(SpriteBottomYId, localBounds.min.y);
        _propertyBlock.SetFloat(SpriteHeightId, localBounds.size.y);
        _propertyBlock.SetFloat(SpriteCenterXId, localBounds.center.x);

        _spriteRenderer.SetPropertyBlock(_propertyBlock);
    }
}
