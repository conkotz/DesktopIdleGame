using System;
using UnityEngine;

/// <summary>
/// Spectral main-hand weapon: root transform follows the handle-bottom world pivot; child SlashPivot rotates for slashes.
/// Idle at spawn anchor, approach without rotating root, attach beside target and face them, slash around pivot with damage each strike.
/// Combat: <see cref="MinionRuntimeStatsCalculator"/> — inherited owner <see cref="SplitDamage"/> snapshot plus minion bonuses.
/// </summary>
[DisallowMultipleComponent]
public class SpectralWeaponMinion : MonoBehaviour
{
    public enum MotionState
    {
        Idle,
        Approaching,
        Attached,
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
    private MinionOwnerWeaponSnapshot _ownerWeaponSnapshot;
    private float _baseRotationZ;
    private Vector3 _lastMoveDir = Vector3.right;
    private float _slashUnwrappedTime;
    private bool _initialized;
    private bool _warnedOwnerNull;
    private bool _warnedDefinitionNull;

    private EnemyBaseController _cachedBoundsEnemy;
    private Collider2D[] _cachedEnemyColliders;
    private SpriteRenderer[] _cachedEnemySpriteRenderers;

    /// <summary>World X offset from enemy root, captured once per attach so mirror flips on the enemy do not re-bias the flank.</summary>
    private float _attachFrozenDeltaXFromEnemyRoot;

    /// <summary>World Y = stable root Y + this (set once at attach). Avoids following animated sprite/collider bounds each frame.</summary>
    private float _attachHoverHeightAboveStableRoot;

    private bool _attachFrozenHorizontalValid;

    private bool _returnFlipLocked;
    private bool _returnFlipX;
    private bool _returnFlipY;
    private float _returnLockedEulerZ;

    /// <summary>Child: local Z rotation only = slash swing; pivot is parent (handle bottom).</summary>
    private Transform _swingPivot;

    /// <summary>Child of swing pivot: positions sprite so its texture pivot sits correctly relative to the handle.</summary>
    private Transform _spriteMount;

    /// <summary>Average of owner min/max split (utility; summoned minions use a full min–max snapshot at spawn).</summary>
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
    /// Entry point for ability spawn. Pass weapon sprite from caller (e.g. main-hand HeldSprite); if null, uses definition placeholder.
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
        EnsureHandlePivotHierarchy();
        _ownerWeaponSnapshot = MinionOwnerWeaponSnapshot.From(ownerStats);
        RefreshCombatStats();
        _nextStrikeReadyTime = Time.time + 0.15f;
        _baseRotationZ = transform.eulerAngles.z;
        ApplyVisualScaleUniform();
        ApplyFacingFromPlayerVisuals();
        _initialized = true;
        return true;
    }

    /// <summary>
    /// Ability pressed again while summon is alive: prefer a different in-range enemy (recast retarget), else same/nearest, or return home if none.
    /// </summary>
    public void TryRecastRetargetOrReturn()
    {
        if (!_initialized || !_def || !_ownerStats)
            return;

        Vector3 origin = GetPlayerRangeOrigin();
        float range = _def.attackRange;
        EnemyBaseController enemy = FindNearestEnemyExcluding(origin, range, _strikeTarget);
        if (!enemy)
            enemy = FindNearestEnemy(origin, range);
        if (enemy)
        {
            _strikeTarget = enemy;
            _attachFrozenHorizontalValid = false;
            _returnFlipLocked = false;
            _state = MotionState.Approaching;
            if (debugLogs)
                Debug.Log($"[SpectralWeapon] Recast → approach {enemy.name}", this);
        }
        else
        {
            _strikeTarget = null;
            _attachFrozenHorizontalValid = false;
            _returnFlipLocked = false;
            _state = MotionState.Returning;
            if (debugLogs)
                Debug.Log("[SpectralWeapon] Recast → return (no enemy in range)", this);
        }
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

    /// <summary>
    /// Root = handle bottom in world. SlashPivot child gets swing rotation; SpriteMount offsets art from pivot per definition.
    /// </summary>
    private void EnsureHandlePivotHierarchy()
    {
        if (_swingPivot)
            return;

        var swingGo = new GameObject("SlashPivot");
        swingGo.transform.SetParent(transform, false);
        swingGo.transform.localPosition = Vector3.zero;
        swingGo.transform.localRotation = Quaternion.identity;
        swingGo.transform.localScale = Vector3.one;
        _swingPivot = swingGo.transform;

        var mountGo = new GameObject("SpriteMount");
        mountGo.transform.SetParent(_swingPivot, false);
        mountGo.transform.localPosition = _def.handlePivotToSpritePivotLocal;
        mountGo.transform.localRotation = Quaternion.identity;
        mountGo.transform.localScale = Vector3.one;
        _spriteMount = mountGo.transform;

        if (!spriteRenderer)
            return;

        if (spriteRenderer.transform == transform)
        {
            SpriteRenderer oldSr = spriteRenderer;
            spriteRenderer = mountGo.AddComponent<SpriteRenderer>();
            CopySpriteRenderer(oldSr, spriteRenderer);
            Destroy(oldSr);
        }
        else
        {
            spriteRenderer.transform.SetParent(_spriteMount, false);
            spriteRenderer.transform.localPosition = Vector3.zero;
            spriteRenderer.transform.localRotation = Quaternion.identity;
            spriteRenderer.transform.localScale = Vector3.one;
        }
    }

    private static void CopySpriteRenderer(SpriteRenderer src, SpriteRenderer dst)
    {
        if (!src || !dst) return;
        dst.sprite = src.sprite;
        dst.color = src.color;
        dst.flipX = src.flipX;
        dst.flipY = src.flipY;
        dst.drawMode = src.drawMode;
        dst.size = src.size;
        dst.tileMode = src.tileMode;
        dst.adaptiveModeThreshold = src.adaptiveModeThreshold;
        dst.maskInteraction = src.maskInteraction;
        dst.spriteSortPoint = src.spriteSortPoint;
        dst.sortingLayerID = src.sortingLayerID;
        dst.sortingOrder = src.sortingOrder;
        dst.sortingLayerName = src.sortingLayerName;
        if (src.sharedMaterial)
            dst.sharedMaterial = src.sharedMaterial;
    }

    private void ResetSwingPivotLocalRotation()
    {
        if (!_swingPivot) return;
        _swingPivot.localRotation = Quaternion.identity;
    }

    /// <summary>Call once at summon; stats stay fixed for this instance (gear changes do not apply).</summary>
    private void RefreshCombatStats()
    {
        SplitDamageRange inheritedRange = default;
        if (_def.combatConfig.damageSourceMode == MinionDamageSourceMode.InheritOwnerHitSplit && _ownerStats)
        {
            inheritedRange = new SplitDamageRange
            {
                min = _ownerStats.MinSplitDamage,
                max = _ownerStats.MaxSplitDamage
            };
        }

        _runtimeStats = MinionRuntimeStatsCalculator.Compute(_ownerStats, _def.combatConfig, inheritedRange);
    }

    private Vector3 GetHomeWorldPosition()
    {
        if (_homeAnchor && _homeAnchor != _attackerTransform)
            return _homeAnchor.position;

        if (_attackerTransform)
            return _attackerTransform.position + new Vector3(-0.55f, 0.38f, 0f);

        return transform.position;
    }

    private Vector3 GetAttachWorldPosition(EnemyBaseController enemy)
    {
        if (!enemy)
            return transform.position;

        Vector3 p = enemy.transform.position;
        float topY = GetEnemyVisualTopY(enemy);
        p.y = topY + Mathf.Max(0f, _def.attachHeightAboveEnemy);
        p.z = transform.position.z;

        // Always use +world X flank (same side as when the owner is right of the enemy). Bias-toward-player
        // mirrored the sprite/read for left-flank attaches and the chop faced away from the target.
        if (Mathf.Abs(_def.attachHorizontalOffsetTowardPlayer) > 1e-4f)
            p.x += Mathf.Abs(_def.attachHorizontalOffsetTowardPlayer);

        return p;
    }

    /// <summary>
    /// Highest Y of enemy body (colliders first, then sprites). Uses colliders before sprites so overhead UI sprites do not raise the hover point.
    /// </summary>
    private float GetEnemyVisualTopY(EnemyBaseController enemy)
    {
        if (!enemy)
            return transform.position.y;

        if (enemy != _cachedBoundsEnemy || _cachedEnemyColliders == null || _cachedEnemySpriteRenderers == null)
        {
            _cachedBoundsEnemy = enemy;
            _cachedEnemyColliders = enemy.GetComponentsInChildren<Collider2D>(true);
            _cachedEnemySpriteRenderers = enemy.GetComponentsInChildren<SpriteRenderer>(true);
        }

        float topY = enemy.transform.position.y;
        bool found = false;

        // Prefer the largest AABB (main body). Small equipment / weapon colliders can win max.y after a flip and yank the hover point.
        Collider2D primaryBody = null;
        float primaryArea = -1f;
        for (int i = 0; i < _cachedEnemyColliders.Length; i++)
        {
            Collider2D c = _cachedEnemyColliders[i];
            if (!c || !c.enabled || !c.gameObject.activeInHierarchy)
                continue;
            Vector2 s = c.bounds.size;
            float area = Mathf.Abs(s.x * s.y);
            if (area > primaryArea)
            {
                primaryArea = area;
                primaryBody = c;
            }
        }

        if (primaryBody)
        {
            topY = primaryBody.bounds.max.y;
            found = true;
        }

        if (!found)
        {
            for (int i = 0; i < _cachedEnemySpriteRenderers.Length; i++)
            {
                SpriteRenderer sr = _cachedEnemySpriteRenderers[i];
                if (!sr || !sr.enabled || !sr.gameObject.activeInHierarchy)
                    continue;
                float y = sr.bounds.max.y;
                if (!found || y > topY)
                {
                    topY = y;
                    found = true;
                }
            }
        }

        return topY;
    }

    /// <summary>
    /// Physics body / root position (not animated sprite bounds). Matches how the player home anchor stays stable while visuals animate.
    /// </summary>
    private static Vector2 GetEnemyStableWorldAnchor(EnemyBaseController enemy)
    {
        if (!enemy)
            return Vector2.zero;
        var rb = enemy.GetComponent<Rigidbody2D>();
        if (rb)
            return rb.position;
        return enemy.transform.position;
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

        switch (_state)
        {
            case MotionState.Idle:
                TickIdle();
                break;
            case MotionState.Approaching:
                TickApproaching();
                break;
            case MotionState.Attached:
                TickAttached();
                break;
            case MotionState.Returning:
                TickReturning();
                break;
        }

        ApplyMotionFacing();
    }

    /// <summary>Hover at home: 80% of player move speed (when owner stats exist), else definition fallback.</summary>
    private float GetIdleFollowSpeed()
    {
        if (_ownerStats)
            return Mathf.Max(0.01f, _ownerStats.FinalMoveSpeed * 0.8f);
        return Mathf.Max(0.01f, _def.idleFollowSpeed);
    }

    /// <summary>Bob + drift + rotation wobble at idle home (Idle only — Returning uses locked rotation + definition return speed).</summary>
    private void ApplyIdleStyleFloatMotion()
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
        transform.position = Vector3.MoveTowards(transform.position, target, GetIdleFollowSpeed() * Time.deltaTime);

        float rotWobble = Mathf.Sin(t * 0.9f + 0.2f) * _def.rotationWobbleDegrees;
        Vector3 e = transform.eulerAngles;
        e.z = _baseRotationZ + rotWobble;
        transform.eulerAngles = e;
    }

    /// <summary>Return flight: same bob target as idle but moves at <see cref="MinionDefinition.returnSpeed"/>; world Z rotation stays locked until idle.</summary>
    private void ApplyReturningFloatMotion()
    {
        Vector3 home = GetHomeWorldPosition();
        float t = Time.time * Mathf.Max(0.01f, _def.wobbleFrequency);
        Vector3 bob = new Vector3(
            Mathf.Sin(t) * _def.wobbleAmplitudeX,
            Mathf.Sin(t * 1.13f + 0.7f) * _def.wobbleAmplitudeY,
            0f);
        Vector3 target = home + bob;
        float rs = Mathf.Max(0.01f, _def.returnSpeed);
        transform.position = Vector3.MoveTowards(transform.position, target, rs * Time.deltaTime);

        Vector3 e = transform.eulerAngles;
        e.z = _returnLockedEulerZ;
        transform.eulerAngles = e;
    }

    private void TickIdle()
    {
        ApplyVisualScaleUniform();
        ApplyIdleStyleFloatMotion();
        ResetSwingPivotLocalRotation();

        if (Time.time < _nextStrikeReadyTime)
            return;

        Vector3 rangeOrigin = GetPlayerRangeOrigin();
        EnemyBaseController enemy = FindNearestEnemy(rangeOrigin, _def.attackRange);
        if (!enemy)
            return;

        _strikeTarget = enemy;
        _attachFrozenHorizontalValid = false;
        _returnFlipLocked = false;
        _state = MotionState.Approaching;
        if (debugLogs)
            Debug.Log($"[SpectralWeapon] Approach → {enemy.name}", this);
    }

    private void TickApproaching()
    {
        if (!_strikeTarget || _strikeTarget.IsDead)
        {
            _strikeTarget = null;
            _attachFrozenHorizontalValid = false;
            _state = MotionState.Returning;
            return;
        }

        Vector3 attach = GetAttachWorldPosition(_strikeTarget);
        float dt = Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, attach, _def.launchSpeed * dt);

        Vector3 toAttach = attach - transform.position;
        if (toAttach.sqrMagnitude > 1e-8f)
        {
            Vector2 toTarget = new Vector2(toAttach.x, toAttach.y).normalized;
            // Flight uses a different base than Attached (+180 chop). Same +180 here plus flipY fought the art (blade read up).
            float z = Vector2.SignedAngle(Vector2.up, toTarget) + _def.attachedFacingExtraDegrees +
                      _def.flightApproachFacingExtraDegrees;
            Vector3 e = transform.eulerAngles;
            e.z = z;
            transform.eulerAngles = e;
        }

        ResetSwingPivotLocalRotation();

        _lastMoveDir = toAttach.sqrMagnitude > 1e-6f ? toAttach.normalized : _lastMoveDir;

        if (Vector3.Distance(transform.position, attach) <= _def.attachArrivalDistance)
        {
            Vector2 stable = GetEnemyStableWorldAnchor(_strikeTarget);
            float topY = GetEnemyVisualTopY(_strikeTarget);
            _attachHoverHeightAboveStableRoot =
                topY + Mathf.Max(0f, _def.attachHeightAboveEnemy) - stable.y;
            _attachFrozenDeltaXFromEnemyRoot = attach.x - stable.x;
            _attachFrozenHorizontalValid = true;
            _state = MotionState.Attached;
            if (debugLogs)
                Debug.Log($"[SpectralWeapon] Attached → {_strikeTarget.name}", this);
        }
    }

    private void TickAttached()
    {
        if (!_strikeTarget || _strikeTarget.IsDead)
        {
            _strikeTarget = null;
            _attachFrozenHorizontalValid = false;
            _state = MotionState.Returning;
            return;
        }

        Vector2 stable = GetEnemyStableWorldAnchor(_strikeTarget);
        if (!_attachFrozenHorizontalValid)
        {
            float topY = GetEnemyVisualTopY(_strikeTarget);
            _attachHoverHeightAboveStableRoot =
                topY + Mathf.Max(0f, _def.attachHeightAboveEnemy) - stable.y;
            _attachFrozenDeltaXFromEnemyRoot = transform.position.x - stable.x;
            _attachFrozenHorizontalValid = true;
        }

        float anchorY = stable.y + _attachHoverHeightAboveStableRoot;
        Vector3 anchorBase = new Vector3(stable.x + _attachFrozenDeltaXFromEnemyRoot, anchorY, transform.position.z);
        float wobbleT = Time.time * Mathf.Max(0.01f, _def.wobbleFrequency);
        Vector3 bob = new Vector3(
            Mathf.Sin(wobbleT) * _def.wobbleAmplitudeX,
            Mathf.Sin(wobbleT * 1.13f + 0.7f) * _def.wobbleAmplitudeY,
            0f);
        Vector3 targetPos = anchorBase + bob;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, GetIdleFollowSpeed() * Time.deltaTime);

        Vector2 toEnemy = new Vector2(
            stable.x - transform.position.x,
            stable.y - transform.position.y);
        if (toEnemy.sqrMagnitude < 1e-8f)
            toEnemy = Vector2.right;
        toEnemy.Normalize();

        // Match legacy BladeForwardRotationZ(..., spriteFlipY: true) from when player faced away from target (perfect reference).
        float baseZ = Vector2.SignedAngle(Vector2.up, toEnemy) + 180f + _def.attachedFacingExtraDegrees;
        float rotWobble = Mathf.Sin(wobbleT * 0.9f + 0.2f) * _def.rotationWobbleDegrees;

        float dt = Time.deltaTime;
        float aps = Mathf.Max(0.01f, _runtimeStats.AttacksPerSecond);
        float strike = Mathf.Max(0.04f, _def.attachedSlashStrikeSeconds);
        float recover = Mathf.Max(0.06f, _def.attachedSlashReturnMinSeconds);
        float animLen = strike + recover;
        // Hits every1/APS; strike/recover stay fast from definition. Extra time = hold upright between chops.
        float period = Mathf.Max(1f / aps, animLen + 1e-4f);

        float prevU = _slashUnwrappedTime;
        _slashUnwrappedTime += dt;
        if (_strikeTarget && !_strikeTarget.IsDead)
        {
            float hitT = Mathf.Floor(prevU / period) * period + strike;
            if (hitT <= prevU)
                hitT += period;
            while (hitT <= _slashUnwrappedTime + 1e-6f)
            {
                ApplyHit(_strikeTarget);
                hitT += period;
            }
        }

        float tInPeriod = Mathf.Repeat(_slashUnwrappedTime, period);
        float slash;
        if (tInPeriod < strike)
        {
            float u = strike > 1e-6f ? tInPeriod / strike : 1f;
            u = 1f - Mathf.Pow(1f - Mathf.Clamp01(u), 2.5f);
            slash = u * _def.attachedSlashMaxRotationDegrees;
        }
        else if (tInPeriod < animLen)
        {
            float u = recover > 1e-6f ? (tInPeriod - strike) / recover : 1f;
            u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u));
            slash = (1f - u) * _def.attachedSlashMaxRotationDegrees;
        }
        else
            slash = 0f;

        Vector3 euler = transform.eulerAngles;
        euler.z = baseZ + rotWobble;
        transform.eulerAngles = euler;

        if (_swingPivot)
        {
            Vector3 swingE = _swingPivot.localEulerAngles;
            swingE.z = slash;
            _swingPivot.localEulerAngles = swingE;
        }

        _lastMoveDir = new Vector3(toEnemy.x, toEnemy.y, 0f);
    }

    private void TickReturning()
    {
        if (!_returnFlipLocked)
        {
            if (spriteRenderer)
            {
                _returnFlipX = spriteRenderer.flipX;
                _returnFlipY = spriteRenderer.flipY;
            }

            _returnLockedEulerZ = transform.eulerAngles.z;
            _returnFlipLocked = true;
        }

        ApplyVisualScaleUniform();
        ApplyReturningFloatMotion();
        ResetSwingPivotLocalRotation();

        Vector3 home = GetHomeWorldPosition();
        home.z = transform.position.z;
        Vector3 delta = home - transform.position;
        _lastMoveDir = delta.sqrMagnitude > 0.0001f ? delta.normalized : _lastMoveDir;

        float arrive = 0.08f + Mathf.Max(_def.wobbleAmplitudeX, _def.wobbleAmplitudeY);
        if (delta.magnitude < arrive)
        {
            _slashUnwrappedTime = 0f;
            _returnFlipLocked = false;
            _state = MotionState.Idle;
            ScheduleNextStrike();
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

        SplitDamage d = _runtimeStats.FinalDamageSplitRange.RollBasicAttackDamage(
            _runtimeStats.CritChance,
            _runtimeStats.CritDamageMultiplier,
            out bool crit);

        float p = Mathf.Max(0f, d.physical);
        float m = Mathf.Max(0f, d.magic);
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

        MinionHitEffects.ApplyAilmentsFromOwnerWeapon(
            enemy,
            _ownerWeaponSnapshot,
            _runtimeStats,
            ip,
            im,
            ic,
            atk);

        if (debugLogs)
            Debug.Log($"[SpectralWeapon] Hit {enemy.name} p={ip} m={im} c={ic} crit={crit}", this);
    }

    private static EnemyBaseController FindNearestEnemy(Vector3 from, float range)
    {
        return FindNearestEnemyExcluding(from, range, null);
    }

    /// <summary>Nearest living enemy within range; skips <paramref name="exclude"/> when non-null (used so recast can swap off the current target).</summary>
    private static EnemyBaseController FindNearestEnemyExcluding(Vector3 from, float range, EnemyBaseController exclude)
    {
        float r2 = range * range;
        var candidates = FindObjectsByType<EnemyBaseController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        EnemyBaseController best = null;
        float bestD = float.MaxValue;
        for (int i = 0; i < candidates.Length; i++)
        {
            EnemyBaseController e = candidates[i];
            if (!e || e.IsDead || e == exclude) continue;
            float d = (e.transform.position - from).sqrMagnitude;
            if (d > r2 || d >= bestD) continue;
            bestD = d;
            best = e;
        }

        return best;
    }

    private void ApplyMotionFacing()
    {
        if (!spriteRenderer)
            return;

        ApplyVisualScaleUniform();

        if (_state == MotionState.Attached && _strikeTarget)
        {
            spriteRenderer.flipX = false;
            // flipY true mirrored the held-weapon sprite and read as upside-down (handle up); false matches upright chop reference.
            spriteRenderer.flipY = false;
            return;
        }

        if (_state == MotionState.Approaching && _strikeTarget)
        {
            if (_def.flightApproachFlipXFromPlayer)
                ApplyFacingFromPlayerVisuals();
            else if (_def.flightApproachFlipXManual)
                spriteRenderer.flipX = _def.flightApproachFlipX;
            else
            {
                float px = GetPlayerRangeOrigin().x;
                float ex = _strikeTarget.transform.position.x;
                spriteRenderer.flipX = px > ex;
            }

            spriteRenderer.flipY = _def.flightApproachFlipY;
            return;
        }

        if (_state == MotionState.Returning && _returnFlipLocked)
        {
            spriteRenderer.flipX = _returnFlipX;
            spriteRenderer.flipY = _returnFlipY;
            return;
        }

        spriteRenderer.flipY = false;
        ApplyFacingFromPlayerVisuals();
    }

    /// <summary>Match player rig mirror (SpectralWeaponSpawnPoint under flipped Visuals), not idle wobble direction.</summary>
    private void ApplyFacingFromPlayerVisuals()
    {
        if (!spriteRenderer)
            return;
        if (_homeAnchor && Mathf.Abs(_homeAnchor.lossyScale.x) > 1e-4f)
            spriteRenderer.flipX = _homeAnchor.lossyScale.x < 0f;
        else if (Mathf.Abs(_lastMoveDir.x) < 0.05f)
            return;
        else
            spriteRenderer.flipX = _lastMoveDir.x < 0f;
    }

    private Vector3 GetPlayerRangeOrigin()
    {
        if (_attackerTransform)
            return _attackerTransform.position;
        return _ownerStats ? _ownerStats.transform.position : transform.position;
    }

    private float UniformVisualScale => Mathf.Max(0.05f, _def.visualWorldScale);

    private void ApplyVisualScaleUniform()
    {
        float u = UniformVisualScale;
        transform.localScale = new Vector3(u, u, u);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!debugDrawGizmo || !_def) return;
        Vector3 h = Application.isPlaying && _initialized ? GetHomeWorldPosition() : transform.position;
        Gizmos.color = new Color(0.3f, 0.85f, 1f, 0.35f);
        Gizmos.DrawWireSphere(h, 0.12f);
        Vector3 ro = Application.isPlaying && _initialized ? GetPlayerRangeOrigin() : transform.position;
        Gizmos.color = new Color(0.5f, 0.95f, 1f, 0.45f);
        Gizmos.DrawWireSphere(ro, _def.attackRange);
    }
#endif
}
