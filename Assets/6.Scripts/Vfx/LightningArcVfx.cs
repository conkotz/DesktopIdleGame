using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reusable lightning arc VFX using stretched <see cref="Sprite"/> segments between world points.
/// Pick a different sprite variant for each chain segment (3 art variants rotate per segment).
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
[AddComponentMenu("Desktop Idle Game/VFX/Lightning Arc")]
public sealed class LightningArcVfx : MonoBehaviour
{
    private const int StaticPreviewSeed = 42;

    [Header("Sprite")]
    [Tooltip("Used when Arc Sprite Variants is empty.")]
    [SerializeField] private Sprite defaultArcSprite;
    [SerializeField] private Sprite[] arcSpriteVariants;

    [Header("Layout")]
    [SerializeField, Min(0.1f)] private float thicknessScale = 1f;
    [SerializeField, Range(0f, 0.35f)] private float lengthVariance = 0.08f;
    [SerializeField, Range(0f, 0.35f)] private float thicknessVariance = 0.12f;
    [SerializeField] private bool randomFlipY = true;

    [Header("Color")]
    [SerializeField] private Color tint = Color.white;

    [Header("Timing (runtime play only)")]
    [SerializeField, Min(0f)] private float flickerDuration;
    [SerializeField, Min(1)] private int flickerFrames = 1;
    [SerializeField, Min(0f)] private float holdDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.2f;
    [SerializeField] private bool destroyWhenFinished = true;

    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = "Player";
    [SerializeField] private int sortingOrder = 42;

    [Header("Preview")]
    [Tooltip("Always draw a still arc in the Scene/prefab view so you can see the sprite without Play Mode.")]
    [SerializeField] private bool showStaticPreviewInEditor = true;
    [Tooltip("Optional Play Mode test. Leave off if you only want the static prefab preview.")]
    [SerializeField] private bool autoPlayPreviewOnStart;
    [SerializeField] private bool useTransformAsPreviewStart = true;
    [SerializeField] private Vector3 previewWorldStart = new Vector3(-2f, 0f, 0f);
    [SerializeField] private Vector3 previewWorldEnd = new Vector3(2f, 0f, 0f);
    [SerializeField] private Vector3 previewEndOffset = new Vector3(4f, 0f, 0f);

    private readonly List<SpriteRenderer> _segmentRenderers = new();
    private Coroutine _playRoutine;
    private bool _playedExternally;

    public bool DestroyWhenFinished
    {
        get => destroyWhenFinished;
        set => destroyWhenFinished = value;
    }

    /// <summary>Spawns (or creates) an arc, plays it, and optionally auto-destroys when finished.</summary>
    public static LightningArcVfx Spawn(
        LightningArcVfx prefab,
        Vector3 worldStart,
        Vector3 worldEnd,
        Transform parent = null,
        Transform sortingReference = null,
        int? randomSeed = null)
    {
        LightningArcVfx instance;
        if (prefab != null)
        {
            instance = Instantiate(prefab, worldStart, Quaternion.identity, parent);
        }
        else
        {
            var go = new GameObject("LightningArc");
            if (parent != null)
                go.transform.SetParent(parent, worldPositionStays: true);
            go.transform.position = worldStart;
            instance = go.AddComponent<LightningArcVfx>();
        }

        instance.autoPlayPreviewOnStart = false;
        instance.showStaticPreviewInEditor = false;
        instance.destroyWhenFinished = true;
        instance.PlayBetween(worldStart, worldEnd, sortingReference, randomSeed);
        return instance;
    }

    /// <summary>Single arc between two world positions.</summary>
    public void PlayBetween(
        Vector3 worldStart,
        Vector3 worldEnd,
        Transform sortingReference = null,
        int? randomSeed = null)
    {
        PlayChain(new[] { worldStart, worldEnd }, sortingReference, randomSeed);
    }

    /// <summary>Linked chain of sprite arcs through ordered waypoints (A→B→C…).</summary>
    public void PlayChain(
        IReadOnlyList<Vector3> waypoints,
        Transform sortingReference = null,
        int? randomSeed = null)
    {
        if (waypoints == null || waypoints.Count < 2)
            return;

        if (!HasAnySprite())
        {
            Debug.LogWarning("[LightningArcVfx] No arc sprite assigned.", this);
            return;
        }

        _playedExternally = true;
        EnsureSegmentCount(waypoints.Count - 1);
        ApplySorting(sortingReference);

        if (!Application.isPlaying)
        {
            LayoutChain(waypoints, randomSeed ?? StaticPreviewSeed, 1f, deterministic: true);
            return;
        }

        if (_playRoutine != null)
            StopCoroutine(_playRoutine);

        _playRoutine = StartCoroutine(CoPlayChain(waypoints, randomSeed));
    }

    public void StopAndHide()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        SetSegmentAlpha(0f);
    }

    [ContextMenu("Refresh Static Preview")]
    private void RefreshStaticPreviewMenu()
    {
        RefreshStaticPreview();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        RefreshStaticPreview();
    }

    private void Start()
    {
        if (!Application.isPlaying || !autoPlayPreviewOnStart || _playedExternally)
            return;

        PlayBetween(ResolvePreviewStart(), ResolvePreviewEnd());
    }

    private void OnDisable()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }
    }

    private void RefreshStaticPreview()
    {
        if (!showStaticPreviewInEditor || Application.isPlaying)
            return;

        if (!HasAnySprite())
        {
            HideAllSegments();
            return;
        }

        CacheExistingSegmentRenderers();
        EnsureSegmentCount(1);
        ApplySorting(null);

        Vector3 start = ResolvePreviewStart();
        Vector3 end = ResolvePreviewEnd();
        LayoutChain(new[] { start, end }, StaticPreviewSeed, 1f, deterministic: true);
    }

    private void HideAllSegments()
    {
        CacheExistingSegmentRenderers();
        for (int i = 0; i < _segmentRenderers.Count; i++)
        {
            if (_segmentRenderers[i] != null)
                _segmentRenderers[i].gameObject.SetActive(false);
        }
    }

    private void CacheExistingSegmentRenderers()
    {
        _segmentRenderers.Clear();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer != null)
                _segmentRenderers.Add(renderer);
        }
    }

    private Vector3 ResolvePreviewStart() =>
        useTransformAsPreviewStart ? transform.position : previewWorldStart;

    private Vector3 ResolvePreviewEnd() =>
        useTransformAsPreviewStart ? transform.position + previewEndOffset : previewWorldEnd;

    private IEnumerator CoPlayChain(IReadOnlyList<Vector3> waypoints, int? randomSeed)
    {
        int baseSeed = randomSeed ?? UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        LayoutChain(waypoints, baseSeed, 1f, deterministic: false);

        float flickerTime = Mathf.Max(0f, flickerDuration);
        int frames = Mathf.Max(1, flickerFrames);
        if (flickerTime > 0.001f && frames > 1)
        {
            float frameDuration = flickerTime / frames;
            for (int frame = 1; frame < frames; frame++)
            {
                LayoutChain(waypoints, baseSeed + frame * 7919, 1f, deterministic: false);
                yield return new WaitForSeconds(frameDuration);
            }
        }

        if (holdDuration > 0f)
            yield return new WaitForSeconds(holdDuration);

        float fade = Mathf.Max(0.01f, fadeDuration);
        float elapsed = 0f;
        while (elapsed < fade)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / fade);
            SetSegmentAlpha(alpha);
            yield return null;
        }

        SetSegmentAlpha(0f);
        _playRoutine = null;

        if (destroyWhenFinished)
            Destroy(gameObject);
    }

    private void LayoutChain(IReadOnlyList<Vector3> waypoints, int seed, float alpha, bool deterministic)
    {
        var rng = new System.Random(seed);
        int segmentCount = waypoints.Count - 1;
        EnsureSegmentCount(segmentCount);

        for (int i = 0; i < segmentCount; i++)
        {
            Sprite sprite = PickSpriteForSegment(i, seed, rng, deterministic);
            LayoutSegment(_segmentRenderers[i], sprite, waypoints[i], waypoints[i + 1], alpha, rng, deterministic);
        }
    }

    private void LayoutSegment(
        SpriteRenderer renderer,
        Sprite sprite,
        Vector3 start,
        Vector3 end,
        float alpha,
        System.Random rng,
        bool deterministic)
    {
        if (renderer == null)
            return;

        if (sprite == null)
        {
            renderer.enabled = false;
            return;
        }

        Vector3 delta = end - start;
        float distance = delta.magnitude;
        if (distance < 0.001f)
        {
            renderer.enabled = false;
            return;
        }

        renderer.enabled = true;
        renderer.sprite = sprite;

        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        Transform segmentTransform = renderer.transform;
        Vector3 anchor = ResolveSegmentAnchor(sprite, start, end);
        segmentTransform.position = new Vector3(anchor.x, anchor.y, 0f);
        segmentTransform.rotation = Quaternion.Euler(0f, 0f, angle);

        float spriteWidth = Mathf.Max(0.001f, sprite.bounds.size.x);
        float lengthScale = deterministic ? 1f : 1f + (NextSigned(rng) * lengthVariance);
        float thickness = deterministic
            ? thicknessScale
            : thicknessScale * (1f + (NextSigned(rng) * thicknessVariance));
        renderer.flipY = !deterministic && randomFlipY && rng.Next(0, 2) == 0;

        segmentTransform.localScale = new Vector3(
            (distance / spriteWidth) * lengthScale,
            Mathf.Abs(thickness),
            1f);

        Color color = tint;
        color.a *= Mathf.Clamp01(alpha);
        renderer.color = color;
    }

    private void EnsureSegmentCount(int count)
    {
        while (_segmentRenderers.Count < count)
        {
            int index = _segmentRenderers.Count;
            var segmentGo = new GameObject($"Segment{index}");
            segmentGo.transform.SetParent(transform, false);

            SpriteRenderer renderer = segmentGo.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;
            _segmentRenderers.Add(renderer);
        }

        for (int i = 0; i < _segmentRenderers.Count; i++)
            _segmentRenderers[i].gameObject.SetActive(i < count);
    }

    private void SetSegmentAlpha(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        Color color = tint;
        color.a *= alpha;

        for (int i = 0; i < _segmentRenderers.Count; i++)
        {
            SpriteRenderer renderer = _segmentRenderers[i];
            if (renderer == null || !renderer.gameObject.activeSelf)
                continue;

            renderer.color = color;
        }
    }

    private void ApplySorting(Transform sortingReference)
    {
        SpriteRenderer reference = sortingReference != null
            ? sortingReference.GetComponentInParent<SpriteRenderer>()
            : null;

        int layerId = reference != null
            ? reference.sortingLayerID
            : SortingLayer.NameToID(sortingLayerName);
        int order = reference != null
            ? reference.sortingOrder + sortingOrder
            : sortingOrder;

        for (int i = 0; i < _segmentRenderers.Count; i++)
        {
            SpriteRenderer renderer = _segmentRenderers[i];
            if (renderer == null)
                continue;

            renderer.sortingLayerID = layerId;
            renderer.sortingOrder = order;
        }
    }

    private static Vector3 ResolveSegmentAnchor(Sprite sprite, Vector3 start, Vector3 end)
    {
        if (sprite == null)
            return start;

        float pivotX = sprite.pivot.x / Mathf.Max(1f, sprite.rect.width);
        return Vector3.Lerp(start, end, Mathf.Clamp01(pivotX));
    }

    private bool HasAnySprite() => GetValidVariants().Length > 0 || defaultArcSprite != null;

    private Sprite GetPreviewSprite()
    {
        Sprite[] variants = GetValidVariants();
        return variants.Length > 0 ? variants[0] : defaultArcSprite;
    }

    private Sprite[] GetValidVariants()
    {
        if (arcSpriteVariants == null || arcSpriteVariants.Length == 0)
        {
            return defaultArcSprite != null
                ? new[] { defaultArcSprite }
                : Array.Empty<Sprite>();
        }

        int count = 0;
        for (int i = 0; i < arcSpriteVariants.Length; i++)
        {
            if (arcSpriteVariants[i] != null)
                count++;
        }

        if (count == 0)
        {
            return defaultArcSprite != null
                ? new[] { defaultArcSprite }
                : Array.Empty<Sprite>();
        }

        var filtered = new Sprite[count];
        int write = 0;
        for (int i = 0; i < arcSpriteVariants.Length; i++)
        {
            if (arcSpriteVariants[i] == null)
                continue;

            filtered[write++] = arcSpriteVariants[i];
        }

        return filtered;
    }

    private Sprite PickSpriteForSegment(int segmentIndex, int seed, System.Random rng, bool deterministic)
    {
        Sprite[] variants = GetValidVariants();
        if (variants.Length == 0)
            return defaultArcSprite;
        if (variants.Length == 1)
            return variants[0];

        if (deterministic)
            return variants[segmentIndex % variants.Length];

        int startIndex = rng.Next(0, variants.Length);
        return variants[(startIndex + segmentIndex) % variants.Length];
    }

    private static float NextSigned(System.Random rng) =>
        (float)(rng.NextDouble() * 2.0 - 1.0);

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 start = ResolvePreviewStart();
        Vector3 end = ResolvePreviewEnd();
        Gizmos.color = new Color(0.4f, 0.85f, 1f, 0.85f);
        Gizmos.DrawWireSphere(start, 0.12f);
        Gizmos.DrawWireSphere(end, 0.12f);
        Gizmos.DrawLine(start, end);
    }
#endif
}
