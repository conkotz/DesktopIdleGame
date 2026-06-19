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
        /// <summary>When false, the trail is destroyed as soon as the projectile is removed (no hang-time fade).</summary>
        public bool LingerOnDetach;

        public static TrailSettings Default => new TrailSettings
        {
            LingerSeconds = 1.5f,
            Width = 0.18f,
            StartAlpha = 0.95f,
            Color = Color.white,
            LingerOnDetach = true
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

        var trailGo = new GameObject("ProjectileTrail");
        trailGo.transform.SetParent(followTarget, false);
        trailGo.transform.localPosition = Vector3.zero;

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

        if (!_settings.LingerOnDetach)
        {
            Destroy(gameObject);
            return;
        }

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

            _trail.time = Mathf.Lerp(startTrailTime, 0f, t);

            float alpha = Mathf.Lerp(startAlpha, 0f, t);
            ApplyTrailColor(alpha);

            yield return null;
        }

        Destroy(gameObject);
    }

    private void ApplyTrailColor(float alpha)
    {
        if (_trail == null)
            return;

        Color c = _settings.Color;
        c.a = _settings.Color.a * Mathf.Clamp01(alpha);

        SpritesLineTrailUtility.ApplyVertexColors(_trail, c);

        Material mat = _trail.material;
        if (mat != null)
            SpritesLineTrailUtility.ConfigureTrailMaterialColor(mat, c);
    }

    private TrailRenderer ConfigureTrail(GameObject owner, Transform followTarget)
    {
        TrailRenderer trail = owner.AddComponent<TrailRenderer>();

        Color c = _settings.Color;
        c.a = _settings.Color.a * Mathf.Clamp01(_settings.StartAlpha);

        SpriteRenderer projectileSprite = followTarget.GetComponentInChildren<SpriteRenderer>();
        int layerId = projectileSprite != null ? projectileSprite.sortingLayerID : 0;
        int order = projectileSprite != null ? projectileSprite.sortingOrder - 1 : 40;

        SpritesLineTrailUtility.ConfigureTrail(
            trail,
            c,
            _settings.Width,
            _settings.LingerSeconds,
            layerId,
            order);

        trail.widthCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0.15f));

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
