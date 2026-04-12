using System;
using UnityEngine;

/// <summary>
/// First summon runtime: spectral main-hand weapon. Home anchor + idle wobble, launch/return, one hit per trip.
/// Combat: <see cref="MinionRuntimeStatsCalculator"/> only — inherited owner <see cref="SplitDamage"/> snapshot (average min/max)
/// plus owner minion bonuses; local base APS/crit; fixed minion crit multiplier. Does not use player APS/crit/procs/ailments/LS.
/// </summary>
[DisallowMultipleComponent]
public class SpectralWeaponMinion : MonoBehaviour
{
    public enum MotionState
    {
        Idle,
        Attacking,
        Returning
    }

    [Header("Optional")]
    [Tooltip("Defaults to SpriteRenderer on this object. Assigned at runtime by Initialize.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [SerializeField] private bool debugLogs;

    [SerializeField] private bool debugDrawGizmo;

    private CharacterStats _ownerStats;
    private MinionDefinition _def;
    private Transform _homeAnchor;
    private Transform _attackerTransform;
    private Action<SpectralWeaponMinion> _onDespawned;

    private float _expireTime;
    private MotionState _state = MotionState.Idle;
    private EnemyBaseController _strikeTarget;
    private float _nextStrikeReadyTime;
    private MinionRuntimeCombatStats _runtimeStats;
    private float _baseRotationZ;
    private Vector3 _lastMoveDir = Vector3.right;
    private Vector3 _attackArcStart;
    private float _attackArcT;
    private bool _initialized;
    private bool _warnedOwnerNull;
    private bool _warnedDefinitionNull;

    /// <summary>Combat snapshot: average of owner min/max split (no player crit roll in the snapshot).</summary>
    public static SplitDamage GetAverageOwnerHitSplit(CharacterStats stats)
    {
        if (!stats) return SplitDamage.Zero;
        SplitDamage a = stats.MinSplitDamage;
        SplitDamage b = stats.MaxSplitDamage;
        return new SplitDamage(
            (a.physical + b.physical) * 0.5f,
            (a.magic + b.magic) * 0.5f,
            (a.corruptionDamage + b.corruptionDamage) * 0.5f
        );
    }

    /// <summary>
    /// Entry point for future ability spawn code. Pass weapon sprite from caller (e.g. main-hand HeldSprite); if null, uses definition placeholder.
    /// </summary>
    /// <param name="attackerTransform">Source for enemy aggro / damage attribution; defaults to ownerStats.transform.</param>
    /// <param name="onDespawned">Optional callback when destroyed (duration or Cancel).</param>
    public bool Initialize(
        CharacterStats ownerStats,
        MinionDefinition definition,
        Transform homeAnchor,
        Sprite weaponSprite = null,
        Transform attackerTransform = null,
        Action<SpectralWeaponMinion> onDespawned = null)
    {
        if (!ownerStats)
        {
            if (!_warnedOwnerNull)
            {
                Debug.LogWarning("[SpectralWeaponMinion] Initialize failed: owner CharacterStats is null.", this);
                _warnedOwnerNull = true;
            }

            return false;
        }

        if (!definition)
        {
            if (!_warnedDefinitionNull)
            {
                Debug.LogWarning("[SpectralWeaponMinion] Initialize failed: MinionDefinition is null.", this);
                _warnedDefinitionNull = true;
            }

            return false;
        }

        _ownerStats = ownerStats;
        _def = definition;
        _attackerTransform = attackerTransform ? attackerTransform : ownerStats.transform;
        if (!homeAnchor)
            homeAnchor = ownerStats.transform;
        _homeAnchor = homeAnchor;
        _onDespawned = onDespawned;

        if (!spriteRenderer)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        _expireTime = Time.time + Mathf.Max(0.1f, _def.summonDuration);
        ApplyWeaponVisual(weaponSprite);
        RefreshCombatStats();
        _nextStrikeReadyTime = Time.time + 0.15f;
        _baseRotationZ = transform.eulerAngles.z;
        transform.localScale = Vector3.one * Mathf.Max(0.05f, _def.visualWorldScale);
        _initialized = true;
        return true;
    }

    /// <summary>Clears despawn callback then destroys (e.g. replacing another summon).</summary>
    public void CancelAndDestroy()
    {
        _onDespawned = null;
        Destroy(gameObject);
    }

    public void ExpireImmediately() => Destroy(gameObject);

    private void OnDestroy()
    {
        _onDespawned?.Invoke(this);
    }

    private void ApplyWeaponVisual(Sprite weaponSprite)
    {
        if (!spriteRenderer) return;

        Sprite s = weaponSprite ? weaponSprite : _def.placeholderWeaponSprite;
        if (s)
            spriteRenderer.sprite = s;

        spriteRenderer.color = _def.spectralTint;
        spriteRenderer.sortingOrder = _def.spriteSortingOrder;
    }

    private void RefreshCombatStats()
    {
        SplitDamage inherited = _def.combatConfig.damageSourceMode == MinionDamageSourceMode.InheritOwnerHitSplit
            ? GetAverageOwnerHitSplit(_ownerStats)
            : SplitDamage.Zero;

        _runtimeStats = MinionRuntimeStatsCalculator.Compute(_ownerStats, _def.combatConfig, inherited);
    }

    private Vector3 GetHomeWorldPosition()
    {
        if (_homeAnchor && _homeAnchor != _attackerTransform)
            return _homeAnchor.position;

        if (_attackerTransform)
            return _attackerTransform.position + new Vector3(-0.55f, 0.38f, 0f);

        return transform.position;
    }

    private void Update()
    {
        if (!_initialized || !_def || !_ownerStats)
            return;

        if (Time.time >= _expireTime)
        {
            ExpireImmediately();
            return;
        }

        RefreshCombatStats();

        switch (_state)
        {
            case MotionState.Idle:
                TickIdle();
                break;
            case MotionState.Attacking:
                TickAttacking();
                break;
            case MotionState.Returning:
                TickReturning();
                break;
        }

        ApplyMotionFacing();
    }

    private void TickIdle()
    {
        Vector3 home = GetHomeWorldPosition();
        if (_homeAnchor)
            _baseRotationZ = _homeAnchor.eulerAngles.z;

        float t = Time.time * Mathf.Max(0.01f, _def.wobbleFrequency);
        Vector3 bob = new Vector3(
            Mathf.Sin(t) * _def.wobbleAmplitudeX,
            Mathf.Sin(t * 1.13f + 0.7f) * _def.wobbleAmplitudeY,
            0f);
        Vector3 target = home + bob;
        transform.position = Vector3.MoveTowards(transform.position, target, _def.idleFollowSpeed * Time.deltaTime);

        float rotWobble = Mathf.Sin(t * 0.9f + 0.2f) * _def.rotationWobbleDegrees;
        Vector3 e = transform.eulerAngles;
        e.z = _baseRotationZ + rotWobble;
        transform.eulerAngles = e;

        if (Time.time < _nextStrikeReadyTime)
            return;

        EnemyBaseController enemy = FindNearestEnemy(home, _def.attackRange);
        if (!enemy)
            return;

        _strikeTarget = enemy;
        _attackArcStart = transform.position;
        _attackArcT = 0f;
        _state = MotionState.Attacking;
        if (debugLogs)
            Debug.Log($"[SpectralWeapon] Attack → {enemy.name}", this);
    }

    private void TickAttacking()
    {
        if (!_strikeTarget || _strikeTarget.IsDead)
        {
            _strikeTarget = null;
            _state = MotionState.Returning;
            return;
        }

        Vector3 goal = _strikeTarget.transform.position;
        goal.z = transform.position.z;

        Vector3 ctrl = BuildAttackArcControl(_attackArcStart, goal);
        float arcLen = ApproximateQuadraticBezierLength(_attackArcStart, ctrl, goal, 20);
        float dt = Time.deltaTime;
        _attackArcT += (_def.launchSpeed * dt) / Mathf.Max(0.12f, arcLen);
        _attackArcT = Mathf.Clamp01(_attackArcT);

        Vector3 onCurve = QuadraticBezier(_attackArcStart, ctrl, goal, _attackArcT);
        transform.position = onCurve;

        Vector3 tan = QuadraticBezierTangent(_attackArcStart, ctrl, goal, Mathf.Clamp01(_attackArcT));
        if (tan.sqrMagnitude > 1e-8f)
        {
            tan.Normalize();
            _lastMoveDir = tan;
            float tangentZ = Mathf.Atan2(tan.y, tan.x) * Mathf.Rad2Deg + _def.attackSwingRotationOffsetDegrees;
            float horizontalZ = ComputeStrikeHorizontalRotationZ(goal) + _def.attackSwingRotationOffsetDegrees +
                _def.attackStrikeHorizontalOffsetDegrees;
            float blend = Mathf.InverseLerp(_def.attackStrikeHorizontalBlendStart, 1f, _attackArcT);
            blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blend));
            float swingZ = Mathf.LerpAngle(tangentZ, horizontalZ, blend);
            Vector3 e = transform.eulerAngles;
            e.z = swingZ;
            transform.eulerAngles = e;
        }

        Vector3 delta = goal - transform.position;
        bool inRange = delta.magnitude <= _def.hitRadius;
        bool pathComplete = _attackArcT >= 0.999f;
        if (inRange || pathComplete)
        {
            ApplyHit(_strikeTarget);
            _strikeTarget = null;
            _state = MotionState.Returning;
            ScheduleNextStrike();
        }
    }

    /// <summary>World Z for a side-view blade lying flat along X, tip toward the strike direction.</summary>
    private float ComputeStrikeHorizontalRotationZ(Vector3 goal)
    {
        float dx = goal.x - _attackArcStart.x;
        if (Mathf.Abs(dx) < 0.02f)
            dx = _lastMoveDir.x;
        return dx >= 0f ? 0f : 180f;
    }

    private Vector3 BuildAttackArcControl(Vector3 start, Vector3 end)
    {
        Vector3 mid = Vector3.Lerp(start, end, Mathf.Clamp01(_def.attackArcPeakAlong));
        Vector2 flat = new Vector2(end.x - start.x, end.y - start.y);
        float flatMag = flat.magnitude;
        float heightScale = flatMag < 0.15f ? 0.35f : Mathf.Clamp01(flatMag / 3.2f);
        return mid + Vector3.up * (_def.attackArcHeight * heightScale);
    }

    private static Vector3 QuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    private static Vector3 QuadraticBezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return 2f * u * (p1 - p0) + 2f * t * (p2 - p1);
    }

    private static float ApproximateQuadraticBezierLength(Vector3 p0, Vector3 p1, Vector3 p2, int segments)
    {
        segments = Mathf.Max(2, segments);
        float len = 0f;
        Vector3 prev = p0;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 p = QuadraticBezier(p0, p1, p2, t);
            len += Vector3.Distance(prev, p);
            prev = p;
        }

        return len;
    }

    private void TickReturning()
    {
        Vector3 home = GetHomeWorldPosition();
        home.z = transform.position.z;
        Vector3 delta = home - transform.position;
        _lastMoveDir = delta.sqrMagnitude > 0.0001f ? delta.normalized : _lastMoveDir;
        transform.position = Vector3.MoveTowards(transform.position, home, _def.returnSpeed * Time.deltaTime);

        if (_homeAnchor)
        {
            Vector3 e = transform.eulerAngles;
            e.z = _homeAnchor.eulerAngles.z;
            transform.eulerAngles = e;
        }

        if (delta.magnitude < 0.08f)
        {
            _state = MotionState.Idle;
            if (debugLogs)
                Debug.Log("[SpectralWeapon] Idle", this);
        }
    }

    private void ScheduleNextStrike()
    {
        float aps = Mathf.Max(0.01f, _runtimeStats.AttacksPerSecond);
        _nextStrikeReadyTime = Time.time + 1f / aps;
    }

    /// <summary>Minion crit on phys/magic; corruption not multiplied (same as player basics).</summary>
    private void ApplyHit(EnemyBaseController enemy)
    {
        if (!enemy || enemy.IsDead || !_ownerStats) return;

        SplitDamage d = _runtimeStats.FinalDamageSplit;
        bool crit = UnityEngine.Random.value < _runtimeStats.CritChance;
        float cm = crit ? _runtimeStats.CritDamageMultiplier : 1f;

        float p = Mathf.Max(0f, d.physical * cm);
        float m = Mathf.Max(0f, d.magic * cm);
        float c = Mathf.Max(0f, d.corruptionDamage);

        int ip = Mathf.RoundToInt(p);
        int im = Mathf.RoundToInt(m);
        int ic = Mathf.RoundToInt(c);

        Transform atk = _attackerTransform ? _attackerTransform : transform;
        if (ip > 0)
            enemy.TakeDamage(ip, DamageType.Physical, crit, atk);
        if (im > 0)
            enemy.TakeDamage(im, DamageType.Magic, crit, atk);
        if (ic > 0)
            enemy.TakeDamage(ic, DamageType.Corruption, false, atk);

        if (debugLogs)
            Debug.Log($"[SpectralWeapon] Hit {enemy.name} p={ip} m={im} c={ic} crit={crit}", this);
    }

    private static EnemyBaseController FindNearestEnemy(Vector3 from, float range)
    {
        float r2 = range * range;
        var candidates = FindObjectsByType<EnemyBaseController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        EnemyBaseController best = null;
        float bestD = r2;
        for (int i = 0; i < candidates.Length; i++)
        {
            EnemyBaseController e = candidates[i];
            if (!e || e.IsDead) continue;
            float d = (e.transform.position - from).sqrMagnitude;
            if (d <= bestD)
            {
                bestD = d;
                best = e;
            }
        }

        return best;
    }

    private void ApplyMotionFacing()
    {
        if (!spriteRenderer || _state == MotionState.Attacking) return;
        if (Mathf.Abs(_lastMoveDir.x) < 0.05f) return;
        spriteRenderer.flipX = _lastMoveDir.x < 0f;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!debugDrawGizmo || !_def) return;
        Vector3 h = Application.isPlaying && _initialized ? GetHomeWorldPosition() : transform.position;
        Gizmos.color = new Color(0.3f, 0.85f, 1f, 0.35f);
        Gizmos.DrawWireSphere(h, 0.12f);
        Gizmos.DrawWireSphere(h, _def.attackRange);
    }
#endif
}
