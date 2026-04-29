using UnityEngine;

[DisallowMultipleComponent]
public class SimpleHoverHighlight2D : MonoBehaviour
{
    [Header("Behaviour toggles")]
    [SerializeField] private bool enableScale = true;
    [SerializeField] private bool enableDarken = true;

    [Header("Scale")]
    [SerializeField] private float hoverScaleMul = 1.06f;
    [SerializeField] private float targetedScaleMul = 1.08f;
    [SerializeField] private float scaleLerpSpeed = 14f;

    [Header("Darken Amount")]
    [Tooltip("1 = no change. 0.9 = slightly darker.")]
    [Range(0.5f, 1f)]
    [SerializeField] private float hoverDarkenMul = 0.92f;

    [Range(0.5f, 1f)]
    [SerializeField] private float targetedDarkenMul = 0.85f;

    [SerializeField] private float colorLerpSpeed = 14f;

    [Header("Optional Pulse")]
    [SerializeField] private bool pulseWhenTargeted = true;
    [SerializeField] private float pulseSpeed = 5f;
    [SerializeField] private float pulseAmount = 0.015f;

    private Vector3 _baseScale;

    private SpriteRenderer[] _renderers;
    private Color[] _baseColors;

    private bool _hovered;
    private bool _targeted;

    private void Awake()
    {
        _baseScale = transform.localScale;

        // Grab ALL sprite renderers under this object (Body, Head, arms, legs, etc)
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);

        _baseColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            _baseColors[i] = _renderers[i].color;
    }

    private void OnDisable()
    {
        transform.localScale = _baseScale;

        if (_renderers != null)
        {
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i]) _renderers[i].color = _baseColors[i];
        }

        _hovered = false;
        _targeted = false;
    }

    public void SetHovered(bool hovered) => _hovered = hovered;
    public void SetTargeted(bool targeted) => _targeted = targeted;

    private void Update()
    {
        float mul = 1f;
        float darkenMul = 1f;

        if (_targeted)
        {
            mul = targetedScaleMul;
            darkenMul = targetedDarkenMul;
        }
        else if (_hovered)
        {
            mul = hoverScaleMul;
            darkenMul = hoverDarkenMul;
        }

        if (NPCDialogueBoxUI.ActiveDialogueIsDescendantOf(transform))
        {
            mul = 1f;
            darkenMul = 1f;
        }

        float pulse = 0f;
        if (NPCDialogueBoxUI.ActiveDialogueIsDescendantOf(transform))
            pulse = 0f;
        else if (_targeted && pulseWhenTargeted)
            pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;

        Vector3 targetScale = _baseScale * (mul + pulse);

        // Scale the whole merchant (root)
        if (enableScale)
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleLerpSpeed);
        else
            transform.localScale = Vector3.Lerp(transform.localScale, _baseScale, Time.deltaTime * scaleLerpSpeed);

        // Darken ALL sprite parts
        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            if (!r) continue;

            Color baseCol = _baseColors[i];

            Color targetCol = enableDarken
                ? new Color(baseCol.r * darkenMul, baseCol.g * darkenMul, baseCol.b * darkenMul, baseCol.a)
                : baseCol;

            r.color = Color.Lerp(r.color, targetCol, Time.deltaTime * colorLerpSpeed);
        }
    }
}