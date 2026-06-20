using UnityEngine;

/// <summary>
/// Pushes pond water shader settings into the sprite renderer via MaterialPropertyBlock.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteWaterSurface : MonoBehaviour
{
    [SerializeField] private float rippleSpeed = 0.95f;
    [SerializeField] private float rippleStrength = 0.015f;
    [SerializeField] private float rippleScale = 11f;
    [SerializeField] private float shimmerStrength = 0.14f;
    [SerializeField] private float phaseOffset;
    [SerializeField] private Vector2 waterRectMin = new Vector2(0.34f, 0.36f);
    [SerializeField] private Vector2 waterRectMax = new Vector2(0.66f, 0.51f);
    [SerializeField] private float waterEdgeSoftness = 0.025f;
    [SerializeField] private float colorMaskStrength = 1f;
    [SerializeField] private bool randomizePhaseOnAwake = true;

    private static readonly int RippleSpeedId = Shader.PropertyToID("_RippleSpeed");
    private static readonly int RippleStrengthId = Shader.PropertyToID("_RippleStrength");
    private static readonly int RippleScaleId = Shader.PropertyToID("_RippleScale");
    private static readonly int ShimmerStrengthId = Shader.PropertyToID("_ShimmerStrength");
    private static readonly int PhaseOffsetId = Shader.PropertyToID("_PhaseOffset");
    private static readonly int WaterRectMinId = Shader.PropertyToID("_WaterRectMin");
    private static readonly int WaterRectMaxId = Shader.PropertyToID("_WaterRectMax");
    private static readonly int WaterEdgeSoftnessId = Shader.PropertyToID("_WaterEdgeSoftness");
    private static readonly int ColorMaskStrengthId = Shader.PropertyToID("_ColorMaskStrength");

    private SpriteRenderer _spriteRenderer;
    private MaterialPropertyBlock _propertyBlock;

    private void Awake()
    {
        if (randomizePhaseOnAwake && Application.isPlaying)
            phaseOffset = Random.Range(0f, 1f);
    }

    private void OnEnable()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        Apply();
    }

    private void OnValidate()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        Apply();
    }

    private void Apply()
    {
        if (_spriteRenderer == null)
            return;

        _propertyBlock ??= new MaterialPropertyBlock();
        _spriteRenderer.GetPropertyBlock(_propertyBlock);

        _propertyBlock.SetFloat(RippleSpeedId, rippleSpeed);
        _propertyBlock.SetFloat(RippleStrengthId, rippleStrength);
        _propertyBlock.SetFloat(RippleScaleId, rippleScale);
        _propertyBlock.SetFloat(ShimmerStrengthId, shimmerStrength);
        _propertyBlock.SetFloat(PhaseOffsetId, phaseOffset);
        _propertyBlock.SetVector(WaterRectMinId, waterRectMin);
        _propertyBlock.SetVector(WaterRectMaxId, waterRectMax);
        _propertyBlock.SetFloat(WaterEdgeSoftnessId, waterEdgeSoftness);
        _propertyBlock.SetFloat(ColorMaskStrengthId, colorMaskStrength);

        _spriteRenderer.SetPropertyBlock(_propertyBlock);
    }
}
