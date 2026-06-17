using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared minion combat brain: target selection, chase, melee attacks, and inherited minion damage.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MinionUnit))]
public class MinionCombatController : MonoBehaviour
{
    public enum CombatState
    {
        Idle,
        Approaching,
        Attacking,
        Returning
    }

    [SerializeField] private MinionUnit unit;
    [SerializeField] private float meleeHitImpactDelay = 0.12f;
    [SerializeField] private float meleeRangePadding = 0.15f;
    [SerializeField] private float stopSlack = 0.04f;
    [SerializeField] private float localEngageRange = 12f;
    [SerializeField] private float homeSideOffsetX = 0.65f;
    [SerializeField] private float followStopDistance = 3f;
    [SerializeField] private float maxWanderRadiusFromPlayer = 5f;
    [SerializeField] private float idleWanderDelaySeconds = 5f;
    [SerializeField] private float wanderDistanceMin = 1f;
    [SerializeField] private float wanderDistanceMax = 3f;
    [SerializeField] private float wanderMoveSpeedMultiplier = 0.65f;
    [SerializeField] private float homeArrivalDistance = 0.12f;
    [SerializeField] private float maxLeashDistanceFromPlayer = AbilityCombatPower.SoulforgedWarriorMaxLeashDistance;
    [SerializeField] private bool debugLogs;

    private CharacterStats _ownerStats;
    private MinionDefinition _def;
    private SoulforgedWeaponMinionPresentation _presentation;
    private Transform _homeAnchor;
    private Transform _rangeOrigin;

    private CombatState _state = CombatState.Idle;
    private EnemyBaseController _strikeTarget;
    private MinionRuntimeCombatStats _runtimeStats;
    private MinionOwnerWeaponSnapshot _ownerWeaponSnapshot;
    private float _ownerMeleeRange;
    private float _ownerMeleeRangePadding;
    private float _moveSpeed;
    private float _nextStrikeReadyTime;
    private bool _initialized;

    private bool _attackHitPending;
    private float _attackHitTime;
    private EnemyBaseController _pendingHitTarget;

    private float _idleStationarySince = -1f;
    private bool _isWandering;
    private float _wanderTargetX;
    private string _outgoingDamageSourceLabel = PlayerCombatController.DefaultMinionOutgoingSourceLabel;

    private Collider2D _minionCol;
    private Collider2D _strikeTargetCol;
    private bool _attackBufferedFromRange;
    private bool _isClosingDistanceForAttack;
    private bool _recastReturnToPlayer;
    private int _recastReturnPreviousTargetId;
    public EnemyBaseController CurrentTarget => _strikeTarget;
    public bool IsInitialized => _initialized;

    /// <summary>True while approaching or attacking a living strike target (not idle/returning).</summary>
    public bool IsActivelyEngagedInCombat =>
        _initialized &&
        _strikeTarget != null &&
        !_strikeTarget.IsDead &&
        (_state == CombatState.Approaching || _state == CombatState.Attacking);

    private void Awake()
    {
        if (!unit)
            unit = GetComponent<MinionUnit>();
        _minionCol = GetComponent<Collider2D>();
        EnsureLeashDefaults();
    }

    private void OnEnable()
    {
        MinionControlService.OnStanceChanged += HandleMinionStanceChanged;
    }

    private void OnDisable()
    {
        MinionControlService.OnStanceChanged -= HandleMinionStanceChanged;
    }

    private void HandleMinionStanceChanged(MinionControlStance stance)
    {
        if (!_initialized)
            return;

        if (stance == MinionControlStance.Passive)
        {
            ReturnToPlayerSide();
            return;
        }

        TickMinionStanceRules();
    }

    private void EnsureLeashDefaults()
    {
        if (maxLeashDistanceFromPlayer <= 0f)
            maxLeashDistanceFromPlayer = AbilityCombatPower.SoulforgedWarriorMaxLeashDistance;
    }

    public bool Initialize(
        CharacterStats ownerStats,
        MinionDefinition definition,
        SoulforgedWeaponMinionPresentation presentation,
        Transform homeAnchor,
        Transform rangeOrigin = null,
        string outgoingDamageSourceLabel = null)
    {
        if (!ownerStats || !definition || !unit)
            return false;

        _ownerStats = ownerStats;
        _def = definition;
        _presentation = SoulforgedWeaponMinionPresentation.Resolve(presentation);
        _homeAnchor = homeAnchor ? homeAnchor : ownerStats.transform;
        _rangeOrigin = rangeOrigin ? rangeOrigin : ownerStats.transform;
        _outgoingDamageSourceLabel = string.IsNullOrWhiteSpace(outgoingDamageSourceLabel)
            ? PlayerCombatController.DefaultMinionOutgoingSourceLabel
            : outgoingDamageSourceLabel.Trim();

        _ownerMeleeRange = Mathf.Max(0.5f, ownerStats.Range);
        PlayerCombatController ownerCombat = ownerStats.GetComponent<PlayerCombatController>();
        if (!ownerCombat)
            ownerCombat = ownerStats.GetComponentInParent<PlayerCombatController>();
        _ownerMeleeRangePadding = ownerCombat ? ownerCombat.GetMeleeRangePadding() : meleeRangePadding;
        _moveSpeed = Mathf.Max(0.01f, ownerStats.FinalMoveSpeed * 0.85f);
        _ownerWeaponSnapshot = MinionOwnerWeaponSnapshot.From(ownerStats);
        EnsureLeashDefaults();
        RefreshCombatStats(1f);

        _state = CombatState.Idle;
        _strikeTarget = null;
        _nextStrikeReadyTime = Time.time + 0.2f;
        ResetIdleWanderState();
        _initialized = true;
        return true;
    }

    public void Shutdown()
    {
        _initialized = false;
        _attackHitPending = false;
        unit?.StopMovement();
    }

    public void RebindOwnerAnchor(CharacterStats ownerStats, Transform homeAnchor, Transform rangeOrigin = null)
    {
        if (!ownerStats)
            return;

        _ownerStats = ownerStats;
        _homeAnchor = homeAnchor ? homeAnchor : ownerStats.transform;
        _rangeOrigin = rangeOrigin ? rangeOrigin : _homeAnchor;
    }

    public void ResetStateAfterSceneLoad()
    {
        if (!_initialized || !unit)
            return;

        _strikeTarget = null;
        _state = CombatState.Idle;
        _recastReturnToPlayer = false;
        _recastReturnPreviousTargetId = 0;
        _attackHitPending = false;
        unit.StopMovement();
        ResetIdleFollowState();
        _lastAlignedFloorTopY = float.NaN;
        unit.AlignToLaneFloor();
    }

    public bool TryApplyStrikeToEnemy(EnemyBaseController enemy, float damageMultiplier)
    {
        if (!_initialized || !enemy || enemy.IsDead || !_ownerStats)
            return false;

        SplitDamage d = _runtimeStats.FinalDamageSplitRange.RollBasicAttackDamage(
            _runtimeStats.CritChance,
            _runtimeStats.CritDamageMultiplier,
            out bool crit);

        float mult = Mathf.Max(0f, damageMultiplier);
        d.physical *= mult;
        d.magic *= mult;
        d.corruptionDamage *= mult;

        int ip = Mathf.RoundToInt(Mathf.Max(0f, d.physical));
        int im = Mathf.RoundToInt(Mathf.Max(0f, d.magic));
        int ic = Mathf.RoundToInt(Mathf.Max(0f, d.corruptionDamage));

        Transform atk = transform;
        if (ip > 0)
            enemy.TakeDamage(ip, DamageType.Physical, crit, atk, null, DpsDamageBucket.Minion, outgoingDpsSourceLabel: _outgoingDamageSourceLabel);
        if (im > 0)
            enemy.TakeDamage(im, DamageType.Magic, crit, atk, null, DpsDamageBucket.Minion, outgoingDpsSourceLabel: _outgoingDamageSourceLabel);
        if (ic > 0)
            enemy.TakeDamage(ic, DamageType.Corruption, false, atk, null, DpsDamageBucket.Minion, outgoingDpsSourceLabel: _outgoingDamageSourceLabel);

        MinionHitEffects.ApplyAilmentsFromOwnerWeapon(
            enemy,
            _ownerWeaponSnapshot,
            _runtimeStats,
            ip,
            im,
            ic,
            atk,
            _outgoingDamageSourceLabel,
            attributeOutgoingToMinion: true);

        return ip + im + ic > 0;
    }

    public void CollectEnemiesInFrontArc(float range, System.Collections.Generic.List<EnemyBaseController> results)
    {
        results.Clear();
        if (!_initialized || !unit)
            return;

        float myX = transform.position.x;
        float faceSign = unit.FacingSignX;
        float r = Mathf.Max(0.1f, range);
        IReadOnlyList<EnemyBaseController> all = CombatEnemyRegistry.GetLiveEnemies();
        for (int i = 0; i < all.Count; i++)
        {
            EnemyBaseController e = all[i];
            if (!e || e.IsDead)
                continue;

            float dx = e.transform.position.x - myX;
            if (Mathf.Abs(dx) > r)
                continue;
            if (dx * faceSign < -0.05f)
                continue;

            results.Add(e);
        }
    }

    public void TryRecastRetargetOrReturn()
    {
        if (!_initialized)
            return;

        int previousTargetId = _strikeTarget ? _strikeTarget.GetInstanceID() : 0;
        EnemyBaseController previousTarget = ResolveEnemyByInstanceId(previousTargetId);
        _strikeTarget = null;
        _strikeTargetCol = null;
        _attackHitPending = false;
        _pendingHitTarget = null;
        _attackBufferedFromRange = false;
        _isClosingDistanceForAttack = false;
        ResetIdleFollowState();
        unit?.StopMovement();

        Vector3 playerPos = GetFollowAnchorPosition();
        bool alreadyNearPlayer = Vector2.Distance(transform.position, playerPos) <= followStopDistance + homeArrivalDistance;

        if (alreadyNearPlayer)
        {
            _recastReturnToPlayer = false;
            _recastReturnPreviousTargetId = 0;
            if (TryAcquireEnemyTargetNearestToPlayer(previousTarget))
            {
                if (debugLogs)
                    Debug.Log("[MinionCombat] Recast near player → retarget from player", this);
                return;
            }

            _state = CombatState.Idle;
            if (debugLogs)
                Debug.Log("[MinionCombat] Recast near player → idle (no enemy)", this);
            return;
        }

        _recastReturnPreviousTargetId = previousTargetId;
        _recastReturnToPlayer = true;
        _state = CombatState.Returning;
        if (debugLogs)
            Debug.Log("[MinionCombat] Recast → drop target, return to player", this);
    }

    public bool ForceTarget(EnemyBaseController enemy)
    {
        if (!_initialized)
            return false;

        if (!enemy || enemy.IsDead)
        {
            _strikeTarget = null;
            _state = CombatState.Returning;
            return false;
        }

        _strikeTarget = enemy;
        _strikeTargetCol = null;
        _attackBufferedFromRange = false;
        _isClosingDistanceForAttack = false;
        _state = CombatState.Approaching;
        ResetIdleFollowState();
        return true;
    }

    /// <summary>Passive stance: retaliate after an enemy hits this minion.</summary>
    public void NotifyStruckByEnemy(EnemyBaseController sourceEnemy)
    {
        if (!_initialized || MinionControlService.CurrentStance != MinionControlStance.Passive)
            return;

        if (!sourceEnemy || sourceEnemy.IsDead)
            return;

        ForceTarget(sourceEnemy);
    }

    private void Update()
    {
        if (!_initialized || !_def || !_ownerStats || !unit.IsAliveVisual)
            return;

        if (_attackHitPending && Time.time >= _attackHitTime)
        {
            _attackHitPending = false;
            ApplyHit(_pendingHitTarget);
            _pendingHitTarget = null;
        }

        TickMinionStanceRules();

        switch (_state)
        {
            case CombatState.Idle:
                TickIdle();
                break;
            case CombatState.Approaching:
                TickApproaching();
                break;
            case CombatState.Attacking:
                TickAttacking();
                break;
            case CombatState.Returning:
                TickReturning();
                break;
        }
    }

    private float _lastAlignedFloorTopY = float.NaN;

    private void LateUpdate()
    {
        if (!_initialized || !_def || !_ownerStats || !unit.IsAliveVisual)
            return;

        float floorTop = LaneGroundEffectPlacement.GetLaneFloorTopWorldY();
        if (float.IsNaN(_lastAlignedFloorTopY) || Mathf.Abs(floorTop - _lastAlignedFloorTopY) > 1e-4f)
        {
            _lastAlignedFloorTopY = floorTop;
            unit.AlignToLaneFloor();
        }

        EnforceLeashTeleport();
    }


    private void TickIdle()
    {
        Vector3 playerPos = GetFollowAnchorPosition();

        if (TryAcquireEnemyTarget())
            return;

        if (ShouldTryFollowPlayerInIdle() && TryTickFollowPlayer(playerPos))
            return;

        if (unit.IsMoving && !_isWandering)
        {
            unit.StopMovement();
            _idleStationarySince = Time.time;
        }

        if (_isWandering)
        {
            if (Mathf.Abs(transform.position.x - _wanderTargetX) <= homeArrivalDistance)
            {
                _isWandering = false;
                _idleStationarySince = Time.time;
                unit.StopMovement();
                return;
            }

            unit.SetMoveTargetX(_wanderTargetX, _moveSpeed * wanderMoveSpeedMultiplier);
            return;
        }

        if (_idleStationarySince < 0f)
            _idleStationarySince = Time.time;

        if (Time.time - _idleStationarySince >= idleWanderDelaySeconds)
        {
            if (ShouldAllowIdleWander() && TryPickWanderTarget(playerPos))
            {
                _isWandering = true;
                return;
            }

            _idleStationarySince = Time.time;
        }

        unit.StopMovement();
    }

    private void TickApproaching()
    {
        if (!_strikeTarget || _strikeTarget.IsDead)
        {
            OnStrikeTargetLost();
            return;
        }

        float enemyX = _strikeTarget.transform.position.x;
        float myX = transform.position.x;
        float myRange = GetMinionMeleeReach();

        Collider2D enemyCol = GetStrikeTargetCollider(_strikeTarget);
        float myHalf = HalfWidthX(_minionCol);
        float enemyHalf = HalfWidthX(enemyCol);
        float gap = EdgeGapX(myX, enemyX, myHalf, enemyHalf);
        float closeEnoughToSwing = myRange + stopSlack;
        bool inAttackRange = gap <= closeEnoughToSwing;
        if (inAttackRange)
            _attackBufferedFromRange = true;
        else
            _attackBufferedFromRange = false;

        float desiredCenterDist = myRange + myHalf + enemyHalf;
        float desiredX = myX < enemyX ? enemyX - desiredCenterDist : enemyX + desiredCenterDist;
        bool shouldStartClosing = gap > closeEnoughToSwing;
        bool shouldKeepClosing = _isClosingDistanceForAttack && gap > closeEnoughToSwing;
        bool shouldCloseDistance = !_attackBufferedFromRange && (shouldStartClosing || shouldKeepClosing);

        if (shouldCloseDistance)
        {
            _isClosingDistanceForAttack = true;
            unit.SetMoveTargetX(desiredX, _moveSpeed);
            return;
        }

        _isClosingDistanceForAttack = false;
        unit.StopMovement();
        unit.FaceTargetX(enemyX);
        _state = CombatState.Attacking;
    }

    private void TickAttacking()
    {
        if (!_strikeTarget || _strikeTarget.IsDead)
        {
            OnStrikeTargetLost();
            return;
        }

        float enemyX = _strikeTarget.transform.position.x;
        float myX = transform.position.x;
        float myRange = GetMinionMeleeReach();

        Collider2D enemyCol = GetStrikeTargetCollider(_strikeTarget);
        float myHalf = HalfWidthX(_minionCol);
        float enemyHalf = HalfWidthX(enemyCol);
        float gap = EdgeGapX(myX, enemyX, myHalf, enemyHalf);
        float closeEnoughToSwing = myRange + stopSlack + 0.2f;

        if (gap > closeEnoughToSwing)
        {
            _state = CombatState.Approaching;
            return;
        }

        unit.StopMovement();
        unit.FaceTargetX(enemyX);

        if (_attackHitPending || Time.time < _nextStrikeReadyTime)
            return;

        BeginMeleeAttack(_strikeTarget);
        ScheduleNextStrike();
    }

    private void TickReturning()
    {
        Vector3 playerPos = GetFollowAnchorPosition();
        float distToPlayer = Vector2.Distance(transform.position, playerPos);
        bool arrivedAtPlayer = distToPlayer <= followStopDistance + homeArrivalDistance;

        if (_recastReturnToPlayer)
        {
            if (!arrivedAtPlayer)
            {
                if (!TryTickFollowPlayer(playerPos))
                    unit.StopMovement();
                return;
            }

            _recastReturnToPlayer = false;
            if (TryAcquireEnemyTargetAtReturnDestination())
                return;

            unit.StopMovement();
            _state = CombatState.Idle;
            ResetIdleFollowState();
            ScheduleNextStrike();
            return;
        }

        if (arrivedAtPlayer)
        {
            if (TryAcquireEnemyTarget())
                return;

            unit.StopMovement();
            _state = CombatState.Idle;
            ResetIdleFollowState();
            ScheduleNextStrike();
            return;
        }

        if (TryAcquireEnemyTarget())
            return;

        if (!TryTickFollowPlayer(playerPos))
        {
            unit.StopMovement();
            _state = CombatState.Idle;
            ResetIdleFollowState();
            ScheduleNextStrike();
        }
    }

    private void BeginMeleeAttack(EnemyBaseController enemy)
    {
        if (!enemy || enemy.IsDead)
            return;

        unit.FaceTargetX(enemy.transform.position.x);
        unit.TriggerMeleeAttack();
        _pendingHitTarget = enemy;
        _attackHitTime = Time.time + Mathf.Max(0.02f, meleeHitImpactDelay);
        _attackHitPending = true;
    }

    private void ScheduleNextStrike()
    {
        float aps = Mathf.Max(0.01f, _runtimeStats.AttacksPerSecond);
        _nextStrikeReadyTime = Time.time + 1f / aps;
    }

    private void ApplyHit(EnemyBaseController enemy)
    {
        if (!enemy || enemy.IsDead || !_ownerStats)
            return;

        SplitDamage d = _runtimeStats.FinalDamageSplitRange.RollBasicAttackDamage(
            _runtimeStats.CritChance,
            _runtimeStats.CritDamageMultiplier,
            out bool crit);

        int ip = Mathf.RoundToInt(Mathf.Max(0f, d.physical));
        int im = Mathf.RoundToInt(Mathf.Max(0f, d.magic));
        int ic = Mathf.RoundToInt(Mathf.Max(0f, d.corruptionDamage));

        Transform atk = transform;
        if (ip > 0)
            enemy.TakeDamage(ip, DamageType.Physical, crit, atk, null, DpsDamageBucket.Minion, outgoingDpsSourceLabel: _outgoingDamageSourceLabel);
        if (im > 0)
            enemy.TakeDamage(im, DamageType.Magic, crit, atk, null, DpsDamageBucket.Minion, outgoingDpsSourceLabel: _outgoingDamageSourceLabel);
        if (ic > 0)
            enemy.TakeDamage(ic, DamageType.Corruption, false, atk, null, DpsDamageBucket.Minion, outgoingDpsSourceLabel: _outgoingDamageSourceLabel);

        MinionHitEffects.ApplyAilmentsFromOwnerWeapon(
            enemy,
            _ownerWeaponSnapshot,
            _runtimeStats,
            ip,
            im,
            ic,
            atk,
            _outgoingDamageSourceLabel,
            attributeOutgoingToMinion: true);
    }

    /// <summary>Called once at summon; gear and owner stat changes do not affect this instance.</summary>
    private void RefreshCombatStats(float inheritedDamageMultiplier)
    {
        SplitDamageRange inheritedRange = default;
        if (_def.combatConfig.damageSourceMode == MinionDamageSourceMode.InheritOwnerHitSplit && _ownerStats)
        {
            float mult = Mathf.Max(0f, inheritedDamageMultiplier);
            inheritedRange = new SplitDamageRange
            {
                min = _ownerStats.MinSplitDamage * mult,
                max = _ownerStats.MaxSplitDamage * mult
            };
        }

        _runtimeStats = MinionRuntimeStatsCalculator.Compute(_ownerStats, _def.combatConfig, inheritedRange);
    }

    private Vector3 GetHomeWorldPosition()
    {
        Vector3 anchor = GetFollowAnchorPosition();
        return anchor + new Vector3(homeSideOffsetX, 0f, 0f);
    }

    private Vector3 GetFollowAnchorPosition() =>
        _homeAnchor ? _homeAnchor.position : transform.position;

    private Vector3 GetRangeOrigin() =>
        _rangeOrigin ? _rangeOrigin.position : GetFollowAnchorPosition();

    private void SyncGroundToOwner()
    {
        if (!unit)
            return;

        unit.AlignToLaneFloor();
    }

    private float GetFollowStopWorldX(Vector3 playerPos)
    {
        float myX = transform.position.x;
        float dx = playerPos.x - myX;
        if (Mathf.Abs(dx) <= followStopDistance)
            return myX;

        float sign = Mathf.Sign(dx);
        if (Mathf.Approximately(sign, 0f))
            sign = 1f;

        return playerPos.x - sign * followStopDistance;
    }

    private void OnStrikeTargetLost()
    {
        _strikeTarget = null;
        _strikeTargetCol = null;
        _attackBufferedFromRange = false;
        _isClosingDistanceForAttack = false;
        unit.StopMovement();

        if (TryAcquireEnemyTarget())
            return;

        _state = CombatState.Returning;
    }

    private Vector3 GetReturnDestinationWorldPosition()
    {
        Vector3 playerPos = GetFollowAnchorPosition();
        float destX = GetFollowStopWorldX(playerPos);
        Vector3 pos = transform.position;
        return new Vector3(destX, pos.y, pos.z);
    }

    /// <summary>
    /// After a recast return completes, pick the nearest in-range enemy to the home slot (not mid-path position).
    /// Prefers a different target than the one dropped on recast when possible.
    /// </summary>
    private bool TryAcquireEnemyTargetAtReturnDestination()
    {
        MinionControlStance stance = MinionControlService.CurrentStance;
        if (stance == MinionControlStance.Passive)
            return false;

        if (stance == MinionControlStance.Assist)
        {
            EnemyBaseController ownerTarget = GetOwnerCurrentTarget();
            return ownerTarget && AssignStrikeTarget(ownerTarget);
        }

        Vector3 dest = GetReturnDestinationWorldPosition();
        EnemyBaseController previousTarget = ResolveEnemyByInstanceId(_recastReturnPreviousTargetId);
        _recastReturnPreviousTargetId = 0;

        EnemyBaseController enemy = FindNearestEnemyExcluding(dest, localEngageRange, previousTarget);
        if (!enemy)
            enemy = FindNearestEnemy(dest, localEngageRange);

        if (!enemy && !IsOwnerInCombat())
        {
            float ownerRange = GetOwnerAttackRangeSnapshot();
            Vector3 origin = GetRangeOrigin();
            enemy = FindNearestEnemyExcluding(origin, ownerRange, previousTarget);
            if (!enemy)
                enemy = FindNearestEnemy(origin, ownerRange);
        }

        return AssignStrikeTarget(enemy);
    }

    /// <summary>Recast while already beside the player: nearest enemy to the player (not the minion).</summary>
    private bool TryAcquireEnemyTargetNearestToPlayer(EnemyBaseController excludePrevious)
    {
        MinionControlStance stance = MinionControlService.CurrentStance;
        if (stance == MinionControlStance.Passive)
            return false;

        if (stance == MinionControlStance.Assist)
        {
            EnemyBaseController ownerTarget = GetOwnerCurrentTarget();
            if (!ownerTarget || ownerTarget == excludePrevious)
                return false;

            return AssignStrikeTarget(ownerTarget);
        }

        Vector3 playerPos = GetFollowAnchorPosition();
        EnemyBaseController enemy = FindNearestEnemyExcluding(playerPos, localEngageRange, excludePrevious);
        if (!enemy)
            enemy = FindNearestEnemy(playerPos, localEngageRange);

        if (!enemy)
        {
            float ownerRange = GetOwnerAttackRangeSnapshot();
            Vector3 origin = GetRangeOrigin();
            enemy = FindNearestEnemyExcluding(origin, ownerRange, excludePrevious);
            if (!enemy)
                enemy = FindNearestEnemy(origin, ownerRange);
        }

        return AssignStrikeTarget(enemy);
    }

    private bool AssignStrikeTarget(EnemyBaseController enemy)
    {
        if (!enemy)
            return false;

        CancelWander();
        _strikeTarget = enemy;
        _strikeTargetCol = null;
        _attackBufferedFromRange = false;
        _isClosingDistanceForAttack = false;
        _state = CombatState.Approaching;
        return true;
    }

    private void EnforceLeashTeleport()
    {
        if (maxLeashDistanceFromPlayer <= 0f)
            return;

        Vector3 playerPos = GetFollowAnchorPosition();
        float maxD = maxLeashDistanceFromPlayer;
        if ((transform.position - playerPos).sqrMagnitude <= maxD * maxD)
            return;

        _strikeTarget = null;
        _strikeTargetCol = null;
        _attackHitPending = false;
        _pendingHitTarget = null;
        _attackBufferedFromRange = false;
        _isClosingDistanceForAttack = false;
        _recastReturnToPlayer = false;
        _recastReturnPreviousTargetId = 0;
        _state = CombatState.Idle;
        CancelWander();

        TeleportToReturnDestination();
        Physics2D.SyncTransforms();

        _lastAlignedFloorTopY = LaneGroundEffectPlacement.GetLaneFloorTopWorldY();
        unit?.AlignToLaneFloor();
    }

    private void TeleportToReturnDestination()
    {
        Vector3 dest = GetReturnDestinationWorldPosition();
        var rb = GetComponent<Rigidbody2D>();
        transform.position = dest;
        if (rb)
        {
            rb.position = new Vector2(dest.x, dest.y);
            rb.linearVelocity = Vector2.zero;
        }

        unit?.StopMovement();
        _attackHitPending = false;
        _pendingHitTarget = null;
        _attackBufferedFromRange = false;
        _isClosingDistanceForAttack = false;
        SyncGroundToOwner();
    }

    private static EnemyBaseController ResolveEnemyByInstanceId(int instanceId)
    {
        if (instanceId == 0)
            return null;

        IReadOnlyList<EnemyBaseController> candidates = CombatEnemyRegistry.GetLiveEnemies();
        for (int i = 0; i < candidates.Count; i++)
        {
            EnemyBaseController e = candidates[i];
            if (e && e.GetInstanceID() == instanceId && !e.IsDead)
                return e;
        }

        return null;
    }

    private bool HasNearbyEnemy() =>
        FindNearestEnemy(transform.position, localEngageRange) != null;

    private bool IsOwnerInCombat()
    {
        if (!_ownerStats)
            return false;

        PlayerCombatController ownerCombat = _ownerStats.GetComponent<PlayerCombatController>();
        if (!ownerCombat)
            ownerCombat = _ownerStats.GetComponentInParent<PlayerCombatController>();
        if (!ownerCombat)
            return false;

        EnemyBaseController target = ownerCombat.CurrentTarget;
        return target != null && !target.IsDead;
    }

    private bool TryAcquireEnemyTarget()
    {
        MinionControlStance stance = MinionControlService.CurrentStance;
        if (stance == MinionControlStance.Passive)
            return false;

        if (stance == MinionControlStance.Assist)
        {
            EnemyBaseController ownerTarget = GetOwnerCurrentTarget();
            if (!ownerTarget)
                return false;

            if (_strikeTarget == ownerTarget && !ownerTarget.IsDead)
            {
                if (_state == CombatState.Idle || _state == CombatState.Returning)
                    _state = CombatState.Approaching;
                return true;
            }

            return AssignStrikeTarget(ownerTarget);
        }

        if (_strikeTarget && !_strikeTarget.IsDead)
        {
            if (_state == CombatState.Idle || _state == CombatState.Returning)
                _state = CombatState.Approaching;
            return true;
        }

        EnemyBaseController enemy = FindNearestEnemy(transform.position, localEngageRange);
        if (!enemy && !IsOwnerInCombat())
            enemy = FindNearestEnemy(GetRangeOrigin(), GetOwnerAttackRangeSnapshot());
        if (!enemy)
            return false;

        CancelWander();
        _strikeTarget = enemy;
        _strikeTargetCol = null;
        _attackBufferedFromRange = false;
        _isClosingDistanceForAttack = false;
        _state = CombatState.Approaching;
        return true;
    }

    private void TickMinionStanceRules()
    {
        MinionControlStance stance = MinionControlService.CurrentStance;
        if (stance == MinionControlStance.Aggressive)
            return;

        if (stance == MinionControlStance.Assist)
        {
            EnemyBaseController ownerTarget = GetOwnerCurrentTarget();
            if (!ownerTarget)
            {
                if (_strikeTarget != null)
                    DropStrikeTargetToIdle();
                return;
            }

            if (_strikeTarget != ownerTarget)
                AssignStrikeTarget(ownerTarget);
            return;
        }

        if (_strikeTarget != null && _strikeTarget.IsDead)
            DropStrikeTargetToIdle();
    }

    private EnemyBaseController GetOwnerCurrentTarget()
    {
        if (!_ownerStats)
            return null;

        PlayerCombatController ownerCombat = _ownerStats.GetComponent<PlayerCombatController>();
        if (!ownerCombat)
            ownerCombat = _ownerStats.GetComponentInParent<PlayerCombatController>();
        if (!ownerCombat)
            return null;

        EnemyBaseController target = ownerCombat.CurrentTarget;
        return target != null && !target.IsDead ? target : null;
    }

    private void DropStrikeTargetToIdle()
    {
        _strikeTarget = null;
        _strikeTargetCol = null;
        _attackHitPending = false;
        _pendingHitTarget = null;
        _attackBufferedFromRange = false;
        _isClosingDistanceForAttack = false;
        _recastReturnToPlayer = false;
        _recastReturnPreviousTargetId = 0;
        _state = CombatState.Idle;
        unit?.StopMovement();
        ResetIdleFollowState();
    }

    /// <summary>Disengage and walk back toward the owner's side (used when switching to Passive mid-fight).</summary>
    private void ReturnToPlayerSide()
    {
        _strikeTarget = null;
        _strikeTargetCol = null;
        _attackHitPending = false;
        _pendingHitTarget = null;
        _attackBufferedFromRange = false;
        _isClosingDistanceForAttack = false;
        _recastReturnToPlayer = false;
        _recastReturnPreviousTargetId = 0;
        CancelWander();

        Vector3 playerPos = GetFollowAnchorPosition();
        bool alreadyNearPlayer =
            Vector2.Distance(transform.position, playerPos) <= followStopDistance + homeArrivalDistance;

        if (alreadyNearPlayer)
        {
            _state = CombatState.Idle;
            unit?.StopMovement();
            ResetIdleFollowState();
            return;
        }

        _state = CombatState.Returning;
    }

    private bool ShouldTryFollowPlayerInIdle()
    {
        MinionControlStance stance = MinionControlService.CurrentStance;
        if (stance == MinionControlStance.Passive || stance == MinionControlStance.Assist)
        {
            if (IsOwnerInCombat())
                return true;

            return !HasNearbyEnemy();
        }

        return !HasNearbyEnemy();
    }

    private bool ShouldAllowIdleWander()
    {
        MinionControlStance stance = MinionControlService.CurrentStance;
        if (stance == MinionControlStance.Assist)
            return !IsOwnerInCombat();

        return stance == MinionControlStance.Aggressive || stance == MinionControlStance.Passive;
    }

    /// <summary>
    /// Owner weapon range snapshotted at summon (matches <see cref="CharacterStats.Range"/> at spawn).
    /// Same formula as <see cref="PlayerCombatController.GetEffectiveMeleeReach"/> without live stat refresh.
    /// </summary>
    private float GetOwnerAttackRangeSnapshot() => Mathf.Max(0f, _ownerMeleeRange);

    private float GetMinionMeleeReach()
    {
        float weaponRange = GetOwnerAttackRangeSnapshot();
        if (_ownerStats != null)
        {
            PlayerAbilityController abilities = _ownerStats.GetComponent<PlayerAbilityController>();
            if (abilities == null)
                abilities = _ownerStats.GetComponentInParent<PlayerAbilityController>();

            if (abilities != null && abilities.IsCleavingStrikesActive)
                weaponRange = Mathf.Max(weaponRange, AbilityCombatPower.CleavingStrikesMinMeleeReach);
        }

        return weaponRange + _ownerMeleeRangePadding;
    }

    private Collider2D GetStrikeTargetCollider(EnemyBaseController enemy)
    {
        if (!enemy)
            return null;

        if (_strikeTargetCol && _strikeTargetCol.gameObject == enemy.gameObject)
            return _strikeTargetCol;

        _strikeTargetCol = enemy.GetComponent<Collider2D>();
        if (!_strikeTargetCol)
            _strikeTargetCol = enemy.GetComponentInChildren<Collider2D>();
        return _strikeTargetCol;
    }

    private static float HalfWidthX(Collider2D col) => col ? col.bounds.extents.x : 0f;

    private static float EdgeGapX(float ax, float bx, float aHalf, float bHalf) =>
        Mathf.Abs(bx - ax) - (aHalf + bHalf);

    private bool TryTickFollowPlayer(Vector3 playerPos)
    {
        float distX = Mathf.Abs(playerPos.x - transform.position.x);
        if (distX <= followStopDistance + homeArrivalDistance)
            return false;

        CancelWander();
        unit.SetMoveTargetX(GetFollowStopWorldX(playerPos), _moveSpeed);
        return true;
    }

    private void CancelWander()
    {
        _idleStationarySince = -1f;
        _isWandering = false;
    }

    private void ResetIdleFollowState() => CancelWander();

    private void ResetIdleWanderState()
    {
        ResetIdleFollowState();
    }

    private bool TryPickWanderTarget(Vector3 playerPos)
    {
        float myX = transform.position.x;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float sign = Random.value < 0.5f ? -1f : 1f;
            float dist = Random.Range(wanderDistanceMin, wanderDistanceMax);
            float candidateX = myX + sign * dist;

            if (Mathf.Abs(candidateX - playerPos.x) > maxWanderRadiusFromPlayer)
                continue;

            _wanderTargetX = candidateX;
            return true;
        }

        return false;
    }

    private static EnemyBaseController FindNearestEnemy(Vector3 from, float range) =>
        FindNearestEnemyExcluding(from, range, null);

    private static EnemyBaseController FindNearestEnemyExcluding(Vector3 from, float range, EnemyBaseController exclude)
    {
        float r2 = range * range;
        IReadOnlyList<EnemyBaseController> candidates = CombatEnemyRegistry.GetLiveEnemies();
        EnemyBaseController best = null;
        float bestD = float.MaxValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            EnemyBaseController e = candidates[i];
            if (!e || e.IsDead || e == exclude)
                continue;
            float d = (e.transform.position - from).sqrMagnitude;
            if (d > r2 || d >= bestD)
                continue;
            bestD = d;
            best = e;
        }

        return best;
    }
}
