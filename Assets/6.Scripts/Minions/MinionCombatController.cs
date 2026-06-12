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
    public EnemyBaseController CurrentTarget => _strikeTarget;
    public bool IsInitialized => _initialized;

    private void Awake()
    {
        if (!unit)
            unit = GetComponent<MinionUnit>();
        _minionCol = GetComponent<Collider2D>();
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
        _moveSpeed = Mathf.Max(0.01f, ownerStats.FinalMoveSpeed * 0.85f);
        _ownerWeaponSnapshot = MinionOwnerWeaponSnapshot.From(ownerStats);
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
        _attackHitPending = false;
        unit.StopMovement();
        ResetIdleFollowState();
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
        EnemyBaseController[] all = FindObjectsByType<EnemyBaseController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
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

        _strikeTarget = null;
        _attackBufferedFromRange = false;
        _isClosingDistanceForAttack = false;
        _recastReturnToPlayer = true;
        _state = CombatState.Returning;
        ResetIdleFollowState();
        if (debugLogs)
            Debug.Log("[MinionCombat] Recast → return to player", this);
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

        SyncGroundToOwner();

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


    private void TickIdle()
    {
        Vector3 playerPos = GetFollowAnchorPosition();

        if (TryAcquireEnemyTarget())
            return;

        if (!HasNearbyEnemy() && TryTickFollowPlayer(playerPos))
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
            if (TryPickWanderTarget(playerPos))
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
            if (TryAcquireEnemyTarget())
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
        if (!_homeAnchor || !unit)
            return;

        unit.AlignFloorToOwnerSoldier(_homeAnchor);
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
        if (_strikeTarget && !_strikeTarget.IsDead)
        {
            if (_state == CombatState.Idle || _state == CombatState.Returning)
                _state = CombatState.Approaching;
            return true;
        }

        EnemyBaseController enemy = FindNearestEnemy(transform.position, localEngageRange);
        if (!enemy && !IsOwnerInCombat())
            enemy = FindNearestEnemy(GetRangeOrigin(), _presentation.attackRange);
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

    private float GetMinionMeleeReach() =>
        Mathf.Max(AbilityCombatPower.CleavingStrikesMinMeleeReach, 1.5f) + meleeRangePadding;

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
        if (distX <= followStopDistance)
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

    private static EnemyBaseController FindNearestEnemy(Vector3 from, float range)
    {
        float r2 = range * range;
        var candidates = FindObjectsByType<EnemyBaseController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        EnemyBaseController best = null;
        float bestD = float.MaxValue;
        for (int i = 0; i < candidates.Length; i++)
        {
            EnemyBaseController e = candidates[i];
            if (!e || e.IsDead)
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
