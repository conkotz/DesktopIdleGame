using System.Collections;
using UnityEngine;

/// <summary>
/// World-space trail that follows a projectile while it flies, then lingers after the projectile is destroyed.
/// On linger, shortens the trail over time so the oldest vertices (player side) disappear first.
/// </summary>
[DisallowMultipleComponent]
public class SnipeLingeringTrailFollower : MonoBehaviour
{
    public struct TrailSettings
    {
        public float LingerSeconds;
        public float Width;
        public float StartAlpha;
        public Color Color;

        public static TrailSettings Default => new TrailSettings
        {
            LingerSeconds = 1.5f,
            Width = 0.18f,
            StartAlpha = 0.95f,
            Color = Color.white
        };
    }

    private Transform _followTarget;
    private TrailRenderer _trail;
    private TrailSettings _settings;
    private bool _detached;
    private Coroutine _dissipateRoutine;

    public static SnipeLingeringTrailFollower Create(Transform followTarget, TrailSettings? settings = null)
    {
        if (followTarget == null)
            return null;

        TrailSettings resolved = settings ?? TrailSettings.Default;
        var trailGo = new GameObject("SnipeArrowTrail");
        trailGo.transform.position = followTarget.position;

        var follower = trailGo.AddComponent<SnipeLingeringTrailFollower>();
        follower._followTarget = followTarget;
        follower._settings = resolved;
        follower._trail = follower.ConfigureTrail(trailGo, followTarget);

        var lifetime = followTarget.gameObject.AddComponent<SnipeProjectileTrailLifetime>();
        lifetime.follower = follower;
        return follower;
    }

    private void LateUpdate()
    {
        if (_detached || _followTarget == null)
            return;

        transform.position = _followTarget.position;
    }

    public void DetachAndLinger()
    {
        if (_detached)
            return;

        _detached = true;
        _followTarget = null;

        if (_trail != null)
        {
            _trail.emitting = false;
            _dissipateRoutine = StartCoroutine(CoDissipateTrailFromPlayerSide());
            return;
        }

        Destroy(gameObject, _settings.LingerSeconds + 0.1f);
    }

    private IEnumerator CoDissipateTrailFromPlayerSide()
    {
        float duration = Mathf.Max(0.1f, _settings.LingerSeconds);
        float startAlpha = Mathf.Clamp01(_settings.StartAlpha);
        float startTrailTime = Mathf.Max(_trail.time, duration);
        _trail.time = startTrailTime;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Shrinking trail.time removes the oldest vertices first (player side).
            _trail.time = Mathf.Lerp(startTrailTime, 0f, t);

            float alpha = Mathf.Lerp(startAlpha, 0f, t);
            ApplyTrailAlpha(alpha);

            yield return null;
        }

        Destroy(gameObject);
    }

    private void ApplyTrailAlpha(float alpha)
    {
        if (_trail == null)
            return;

        Color c = _settings.Color;
        c.a *= Mathf.Clamp01(alpha);
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(c, 0f),
                new GradientColorKey(c, 1f)
            },
            new[]
            {
                new GradientAlphaKey(c.a, 0f),
                new GradientAlphaKey(c.a, 1f)
            });
        _trail.colorGradient = gradient;
    }

    private TrailRenderer ConfigureTrail(GameObject owner, Transform followTarget)
    {
        TrailRenderer trail = owner.AddComponent<TrailRenderer>();
        trail.time = Mathf.Max(0.1f, _settings.LingerSeconds);
        trail.minVertexDistance = 0.015f;
        trail.widthMultiplier = Mathf.Max(0.01f, _settings.Width);
        trail.numCornerVertices = 4;
        trail.numCapVertices = 2;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.emitting = true;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
            trail.material = new Material(shader);

        trail.widthCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0.15f));

        ApplyTrailAlpha(_settings.StartAlpha);

        SpriteRenderer projectileSprite = followTarget.GetComponentInChildren<SpriteRenderer>();
        if (projectileSprite != null)
        {
            trail.sortingLayerID = projectileSprite.sortingLayerID;
            trail.sortingOrder = projectileSprite.sortingOrder - 1;
        }
        else
        {
            trail.sortingOrder = 40;
        }

        trail.Clear();
        return trail;
    }
}

[DisallowMultipleComponent]
public class SnipeProjectileTrailLifetime : MonoBehaviour
{
    public SnipeLingeringTrailFollower follower;

    private void OnDestroy()
    {
        if (follower != null)
            follower.DetachAndLinger();
    }
}
