using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lingering ground fire from <see cref="PlayerAbilityController"/> Flame Charge.
/// Ticks fire damage and burn on enemies inside the radius until duration expires.
/// </summary>
public class FlameChargeTrailSegment : MonoBehaviour
{
    private static readonly List<FlameChargeTrailSegment> ActiveSegments = new();

    private PlayerAbilityController _owner;
    private AbilityDefinition _abilityDef;
    private int _castId;
    private float _endsAt;
    private float _radius;
    private float _tickInterval;
    private float _flatFirePerTick;
    private float _tickAccum;
    private GameObject _visualRoot;

    /// <summary>
    /// Double Ignition: a second dash cannot drop fire on the same ground as an earlier dash.
    /// Segments from the same dash may overlap along the path.
    /// </summary>
    public static bool WouldOverlapOtherDash(Vector3 worldPosition, float radius, int castId)
    {
        float minSep = Mathf.Max(0.1f, radius * 1.05f);
        for (int i = 0; i < ActiveSegments.Count; i++)
        {
            FlameChargeTrailSegment seg = ActiveSegments[i];
            if (!seg || seg._castId == castId)
                continue;
            if (Vector2.Distance(seg.transform.position, worldPosition) < minSep)
                return true;
        }

        return false;
    }

    public static FlameChargeTrailSegment Spawn(
        PlayerAbilityController owner,
        AbilityDefinition abilityDef,
        int castId,
        Vector3 worldPosition,
        float durationSeconds,
        float radius,
        float tickIntervalSeconds,
        float flatFirePerTick,
        GameObject visualRoot)
    {
        if (!owner || !abilityDef || durationSeconds <= 0f)
            return null;

        var go = new GameObject("FlameChargeTrail");
        LaneGroundEffectPlacement.PlaceOnLaneFloor(go.transform, worldPosition, 0.1f);
        var seg = go.AddComponent<FlameChargeTrailSegment>();
        seg._owner = owner;
        seg._abilityDef = abilityDef;
        seg._castId = castId;
        seg._endsAt = Time.time + durationSeconds;
        seg._radius = Mathf.Max(0.15f, radius);
        seg._tickInterval = Mathf.Max(0.1f, tickIntervalSeconds);
        seg._flatFirePerTick = Mathf.Max(0f, flatFirePerTick);
        seg._visualRoot = visualRoot;
        if (visualRoot != null)
            visualRoot.transform.SetParent(go.transform, false);

        ActiveSegments.Add(seg);
        return seg;
    }

    private void OnDestroy()
    {
        ActiveSegments.Remove(this);
        if (_visualRoot != null)
            Destroy(_visualRoot);
    }

    private void Update()
    {
        if (_owner == null || _abilityDef == null || Time.time >= _endsAt)
        {
            Destroy(gameObject);
            return;
        }

        _tickAccum += Time.deltaTime;
        if (_tickAccum < _tickInterval)
            return;

        _tickAccum = 0f;
        _owner.TickFlameChargeTrailSegmentDamage(_abilityDef, transform.position, _radius, _flatFirePerTick);
    }
}
