using UnityEngine;

/// <summary>Slowly pulses floor trap marker alpha for Hunter's Swiftness traps.</summary>
[DisallowMultipleComponent]
public sealed class HunterTrapFloorPulse : MonoBehaviour
{
    [SerializeField] private SpriteRenderer fillRenderer;
    [SerializeField] private SpriteRenderer ringRenderer;
    [SerializeField, Min(0.1f)] private float pulsePeriodSeconds = 1.6f;
    [SerializeField, Range(0.05f, 1f)] private float minAlpha = 0.22f;
    [SerializeField, Range(0.05f, 1f)] private float maxAlpha = 0.82f;

    private Color _fillBaseColor;
    private Color _ringBaseColor;
    private float _phaseOffset;

    public void Configure(SpriteRenderer fill, SpriteRenderer ring, float periodSeconds = 1.6f)
    {
        fillRenderer = fill;
        ringRenderer = ring;
        pulsePeriodSeconds = Mathf.Max(0.1f, periodSeconds);
        CacheBaseColors();
        _phaseOffset = Random.Range(0f, pulsePeriodSeconds);
    }

    private void Awake()
    {
        CacheBaseColors();
        _phaseOffset = Random.Range(0f, pulsePeriodSeconds);
    }

    private void Update()
    {
        if (fillRenderer == null && ringRenderer == null)
            return;

        float t = (Time.time + _phaseOffset) / pulsePeriodSeconds;
        float wave = (Mathf.Sin(t * Mathf.PI * 2f) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, wave);

        if (fillRenderer != null)
        {
            Color c = _fillBaseColor;
            c.a = _fillBaseColor.a * alpha;
            fillRenderer.color = c;
        }

        if (ringRenderer != null)
        {
            Color c = _ringBaseColor;
            c.a = _ringBaseColor.a * alpha;
            ringRenderer.color = c;
        }
    }

    private void CacheBaseColors()
    {
        if (fillRenderer != null)
            _fillBaseColor = fillRenderer.color;
        if (ringRenderer != null)
            _ringBaseColor = ringRenderer.color;
    }
}
