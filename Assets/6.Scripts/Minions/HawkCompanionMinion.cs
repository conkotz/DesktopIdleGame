using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hawk Companion: circles above the owner, wanders horizontally, then dives to strike and returns to flight height.
/// Pure minion damage — not inherited from the owner's weapon.
/// </summary>
[DisallowMultipleComponent]
public class HawkCompanionMinion : MonoBehaviour
{
    public enum MotionState
    {
        Flying,
        Diving,
        SwoopRecovering,
        Returning
    }

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;

    private CharacterStats _ownerStats;
    private SkillsManager _skillsManager;
    private MinionDefinition _def;
    private HawkCompanionMinionPresentation _presentation;
    private Transform _homeAnchor;
    private Transform _attackerTransform;
    private Action<HawkCompanionMinion> _onDespawned;

    private float _expireTime;
    private MotionState _state = MotionState.Flying;
    private EnemyBaseController _strikeTarget;
    private MinionRuntimeCombatStats _runtimeStats;
    private bool _lightningInfused;
    private bool _initialized;

    private float _nextStrikeReadyTime;
    private float _nextEnemyScanAt;
    private bool _isPatrolling;
    private float _patrolTargetX;
    private float _patrolDirection = 1f;
    private float _returnFlightY;
    private bool _flightFacingLeft;
    private Vector3 _swoopCarryDirection = Vector3.up;
    private float _swoopRecoverDistanceRemaining;
    private Vector3 _returnGlideTarget;
    private float _nextHoverPauseAt;
    private float _hoverPauseUntil;

    private const float EnemyScanIntervalSeconds = 0.1f;
    private const float HoverPauseIntervalMinSeconds = 5f;
    private const float HoverPauseIntervalMaxSeconds = 10f;
    private const float HoverPauseDurationMinSeconds = 2f;
    private const float HoverPauseDurationMaxSeconds = 3f;

    public EnemyBaseController CurrentTarget => _strikeTarget;

    private void OnEnable()
    {
        WorldFloorFollowerRegistry.Register(transform, WorldFloorFollowerRegistry.Category.Actor);
        MinionControlService.OnStanceChanged += HandleMinionStanceChanged;
    }

    private void OnDisable()
    {
        MinionControlService.OnStanceChanged -= HandleMinionStanceChanged;
        WorldFloorFollowerRegistry.Unregister(transform);
    }

    public bool Initialize(
        CharacterStats ownerStats,
        MinionDefinition definition,
        HawkCompanionMinionPresentation presentationFromController,
        Transform homeAnchor,
        Transform attackerTransform = null,
        Action<HawkCompanionMinion> onDespawned = null,
        SkillsManager skillsManager = null,
        int enhancementChoice = -1,
        float durationOverrideSeconds = -1f)
    {
        if (!ownerStats || !definition)
            return false;

        _ownerStats = ownerStats;
        _skillsManager = skillsManager ? skillsManager : SkillsManager.Instance;
        _def = definition;
        _presentation = HawkCompanionMinionPresentation.Resolve(presentationFromController);
        _homeAnchor = homeAnchor ? homeAnchor : ownerStats.transform;
        _attackerTransform = attackerTransform ? attackerTransform : ownerStats.transform;
        _onDespawned = onDespawned;

        if (!spriteRenderer)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (!animator)
            animator = GetComponent<Animator>();

        int choice = enhancementChoice >= 0
            ? enhancementChoice
            : ResolveEnhancementChoice();
        _lightningInfused = choice == AbilityCombatPower.HawkCompanionLightningInfusedChoiceIndex;

        float duration = durationOverrideSeconds > 0f
            ? durationOverrideSeconds
            : definition.summonDuration;
        _expireTime = Time.time + Mathf.Max(0.1f, duration);

        RefreshCombatStats();
        ApplyVisualScale();
        if (spriteRenderer)
            spriteRenderer.sortingOrder = _presentation.spriteSortingOrder;

        SnapToFlightBand();
        BeginPatrol();
        ScheduleNextHoverPause();
        _nextStrikeReadyTime = Time.time + 0.35f;
        _initialized = true;
        return true;
    }

    public void BindReleasedCallback(Action<HawkCompanionMinion> onDespawned) => _onDespawned = onDespawned;

    public void TryRecastRetargetOrReturn()
    {
        if (!_initialized)
            return;

        if (MinionControlService.CurrentStance == MinionControlStance.Passive)
            return;

        EnemyBaseController enemy = ResolveStrikeTargetCandidate();
        if (ForceTarget(enemy))
            return;

        _strikeTarget = null;
        _state = MotionState.Flying;
        BeginPatrol();
    }

    public bool ForceTarget(EnemyBaseController enemy)
    {
        if (!_initialized || !enemy || enemy.IsDead || !CanEngageTargets())
            return false;

        BeginDive(enemy);
        return true;
    }

    public void CancelAndDestroy()
    {
        _onDespawned = null;
        Destroy(gameObject);
    }

    public void PersistAcrossSceneLoads()
    {
        transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
    }

    public void RefreshAfterSceneLoad(
        CharacterStats ownerStats,
        Transform ownerAnchor,
        Transform rangeOrigin = null,
        Action<HawkCompanionMinion> onDespawned = null)
    {
        if (!ownerStats || !ownerAnchor)
            return;

        PersistAcrossSceneLoads();
        gameObject.SetActive(true);
        _ownerStats = ownerStats;
        _homeAnchor = ownerAnchor;
        _attackerTransform = rangeOrigin ? rangeOrigin : ownerAnchor;
        _onDespawned = onDespawned;
        RefreshCombatStats();
        SnapToFlightBand();
        _state = MotionState.Flying;
        _strikeTarget = null;
        BeginPatrol();
    }

    private void Update()
    {
        if (!_initialized || !_ownerStats)
            return;

        if (Time.time >= _expireTime)
        {
            Destroy(gameObject);
            return;
        }

        switch (_state)
        {
            case MotionState.Flying:
                TickFlying();
                break;
            case MotionState.Diving:
                TickDiving();
                break;
            case MotionState.SwoopRecovering:
                TickSwoopRecovering();
                break;
            case MotionState.Returning:
                TickReturning();
                break;
        }
    }

    private void LateUpdate()
    {
        if (!_initialized)
            return;

        EnforceLeashTeleport();
    }

    private void OnDestroy()
    {
        _onDespawned?.Invoke(this);
    }

    private void HandleMinionStanceChanged(MinionControlStance stance)
    {
        if (!_initialized)
            return;

        if (stance == MinionControlStance.Passive && _state is MotionState.Diving or MotionState.SwoopRecovering)
        {
            _strikeTarget = null;
            BeginReturn();
        }
    }

    private void RefreshCombatStats()
    {
        int rangedLevel = _skillsManager != null ? _skillsManager.GetLevel(SkillType.Ranged) : 0;
        int choice = ResolveEnhancementChoice();
        _runtimeStats = HawkCompanionStatsBuilder.Compute(
            _ownerStats, _def.combatConfig, rangedLevel, choice);
    }

    private int ResolveEnhancementChoice()
    {
        if (_skillsManager == null)
            return -1;

        int selected = _skillsManager.GetSkillChoiceSelection(
            SkillType.Ranged, AbilityCombatPower.HawkCompanionEnhancementParentSpineNodeId, -1);
        if (selected < 0)
            selected = _skillsManager.GetSkillChoiceSelection(SkillType.Ranged, 5, -1);
        return selected;
    }

    private void TickFlying()
    {
        ApplyFlightBob();
        MaybeStartHoverPause();

        if (IsHoverPaused())
        {
            if (TryTickFollowOwner())
                return;
            return;
        }

        if (TryTickFollowOwner())
            return;

        if (CanEngageTargets() &&
            Time.time >= _nextStrikeReadyTime &&
            Time.time >= _nextEnemyScanAt)
        {
            _nextEnemyScanAt = Time.time + EnemyScanIntervalSeconds;
            EnemyBaseController enemy = ResolveStrikeTargetCandidate();
            if (enemy)
            {
                BeginDive(enemy);
                return;
            }
        }

        TickPatrolFlight();
    }

    private void TickPatrolFlight()
    {
        Vector3 ownerPos = GetOwnerPosition();
        float flightY = ownerPos.y + _presentation.flightHeightAbovePlayer;

        if (!_isPatrolling)
            BeginPatrol();

        Vector3 patrolPos = new Vector3(_patrolTargetX, flightY, transform.position.z);
        float speed = _presentation.horizontalFlightSpeed * Time.deltaTime;
        Vector3 before = transform.position;
        transform.position = Vector3.MoveTowards(transform.position, patrolPos, speed);

        float movedX = transform.position.x - before.x;
        if (Mathf.Abs(movedX) > 0.0005f)
            ApplyHorizontalFlightFacing(movedX < 0f);

        if (Mathf.Abs(transform.position.x - _patrolTargetX) <= _presentation.wanderArrivalDistance)
            ExtendPatrolTarget(ownerPos);

        Vector3 holdPos = new Vector3(transform.position.x, flightY, transform.position.z);
        if (Mathf.Abs(transform.position.y - flightY) > 0.01f)
            transform.position = Vector3.MoveTowards(transform.position, holdPos, speed);
    }

    private bool TryTickFollowOwner()
    {
        Vector3 ownerPos = GetOwnerPosition();
        float flightY = ownerPos.y + _presentation.flightHeightAbovePlayer;
        float distX = Mathf.Abs(ownerPos.x - transform.position.x);
        if (distX <= _presentation.followStopDistance + _presentation.wanderArrivalDistance)
            return false;

        _isPatrolling = false;
        float followX = GetFollowStopWorldX(ownerPos);
        Vector3 followPos = new Vector3(followX, flightY, transform.position.z);
        Vector3 before = transform.position;
        transform.position = Vector3.MoveTowards(
            transform.position,
            followPos,
            _presentation.horizontalFlightSpeed * Time.deltaTime);

        float movedX = transform.position.x - before.x;
        if (Mathf.Abs(movedX) > 0.0005f)
            ApplyHorizontalFlightFacing(movedX < 0f);

        return true;
    }

    private void BeginDive(EnemyBaseController enemy)
    {
        _strikeTarget = enemy;
        _isPatrolling = false;
        _state = MotionState.Diving;

        float dx = enemy.transform.position.x - transform.position.x;
        ApplyHorizontalFlightFacing(dx < 0f);

        if (spriteRenderer)
            spriteRenderer.flipY = false;
    }

    private void TickDiving()
    {
        if (!_strikeTarget || _strikeTarget.IsDead)
        {
            BeginReturn();
            return;
        }

        if (MinionControlService.CurrentStance == MinionControlStance.Assist && !IsOwnerInCombat())
        {
            BeginReturn();
            return;
        }

        if (MinionControlService.CurrentStance == MinionControlStance.Passive)
        {
            BeginReturn();
            return;
        }

        Vector3 strikePoint = GetStrikeWorldPosition(_strikeTarget);
        Vector3 before = transform.position;
        transform.position = Vector3.MoveTowards(
            transform.position,
            strikePoint,
            _presentation.diveSpeed * Time.deltaTime);

        ApplyDiveFacingRotation(strikePoint - before);

        if (spriteRenderer)
            spriteRenderer.flipY = false;

        if (Vector3.Distance(transform.position, strikePoint) <= _presentation.diveStrikeArrivalDistance)
        {
            ApplyHit(_strikeTarget);
            BeginSwoopRecover(strikePoint - before);
        }
    }

    private void TickSwoopRecovering()
    {
        Vector3 before = transform.position;
        float step = _presentation.returnSpeed * Time.deltaTime;
        transform.position += _swoopCarryDirection * step;
        _swoopRecoverDistanceRemaining -= step;

        ApplyFlightFacingFromMovement(transform.position - before);

        float pitchEase = Mathf.Clamp01(_swoopRecoverDistanceRemaining / Mathf.Max(0.1f, _presentation.swoopArcCarryDistance));
        float z = (_flightFacingLeft ? 1f : -1f) * Mathf.Lerp(8f, 42f, 1f - pitchEase);
        transform.rotation = Quaternion.Euler(0f, 0f, z);
        if (spriteRenderer)
            spriteRenderer.flipY = false;

        bool reachedFlightBand = transform.position.y >= _returnFlightY - _presentation.wanderArrivalDistance;
        if (_swoopRecoverDistanceRemaining <= 0f || reachedFlightBand)
            BeginReturnGlide();
    }

    private void TickReturning()
    {
        Vector3 before = transform.position;
        transform.position = Vector3.MoveTowards(
            transform.position,
            _returnGlideTarget,
            _presentation.returnSpeed * Time.deltaTime);

        ApplyFlightFacingFromMovement(transform.position - before);
        transform.rotation = Quaternion.identity;
        if (spriteRenderer)
            spriteRenderer.flipY = false;

        if (Vector3.Distance(transform.position, _returnGlideTarget) <= _presentation.wanderArrivalDistance + 0.05f)
        {
            _strikeTarget = null;
            _state = MotionState.Flying;
            BeginPatrol();
            ScheduleNextStrike();
        }
    }

    private void BeginSwoopRecover(Vector3 lastDiveDelta)
    {
        Vector3 ownerPos = GetOwnerPosition();
        _returnFlightY = ownerPos.y + _presentation.flightHeightAbovePlayer;

        Vector3 diveDir = lastDiveDelta.sqrMagnitude > 1e-6f ? lastDiveDelta.normalized : Vector3.down;
        float forwardX = Mathf.Abs(diveDir.x) > 0.05f ? Mathf.Sign(diveDir.x) : (_flightFacingLeft ? -1f : 1f);
        _swoopCarryDirection = new Vector3(forwardX, 1.35f, 0f).normalized;

        _swoopRecoverDistanceRemaining = Mathf.Max(0.1f, _presentation.swoopArcCarryDistance);
        _state = MotionState.SwoopRecovering;
    }

    private void BeginReturnGlide()
    {
        Vector3 ownerPos = GetOwnerPosition();
        _returnFlightY = ownerPos.y + _presentation.flightHeightAbovePlayer;
        float glideX = GetFollowStopWorldX(ownerPos);
        _returnGlideTarget = new Vector3(glideX, _returnFlightY, transform.position.z);
        _state = MotionState.Returning;
    }

    private void BeginReturn()
    {
        Vector3 ownerPos = GetOwnerPosition();
        _returnFlightY = ownerPos.y + _presentation.flightHeightAbovePlayer;
        float glideX = GetFollowStopWorldX(ownerPos);
        _returnGlideTarget = new Vector3(glideX, _returnFlightY, transform.position.z);
        _state = MotionState.Returning;
    }

    private void MaybeStartHoverPause()
    {
        if (_state != MotionState.Flying || Time.time < _nextHoverPauseAt || IsHoverPaused())
            return;

        _hoverPauseUntil = Time.time + UnityEngine.Random.Range(HoverPauseDurationMinSeconds, HoverPauseDurationMaxSeconds);
        ScheduleNextHoverPause();
        _isPatrolling = false;
    }

    private bool IsHoverPaused() => Time.time < _hoverPauseUntil;

    private void ScheduleNextHoverPause()
    {
        _nextHoverPauseAt = Time.time + UnityEngine.Random.Range(HoverPauseIntervalMinSeconds, HoverPauseIntervalMaxSeconds);
    }

    private void BeginPatrol()
    {
        _isPatrolling = true;
        ExtendPatrolTarget(GetOwnerPosition());
    }

    private void ExtendPatrolTarget(Vector3 ownerPos)
    {
        float myX = transform.position.x;
        float dist = UnityEngine.Random.Range(_presentation.wanderDistanceMin, _presentation.wanderDistanceMax);
        float candidateX = myX + _patrolDirection * dist;

        if (Mathf.Abs(candidateX - ownerPos.x) > _presentation.maxWanderRadiusFromPlayer)
        {
            _patrolDirection = -_patrolDirection;
            candidateX = myX + _patrolDirection * dist;
        }

        if (Mathf.Abs(candidateX - ownerPos.x) > _presentation.maxWanderRadiusFromPlayer)
            candidateX = ownerPos.x + _patrolDirection * _presentation.maxWanderRadiusFromPlayer * 0.5f;

        _patrolTargetX = candidateX;
    }

    private void ApplyHorizontalFlightFacing(bool facingLeft)
    {
        _flightFacingLeft = facingLeft;
        if (spriteRenderer)
            spriteRenderer.flipX = facingLeft;
    }

    private void ApplyFlightFacingFromMovement(Vector3 worldDelta)
    {
        if (Mathf.Abs(worldDelta.x) > 0.0005f)
            ApplyHorizontalFlightFacing(worldDelta.x < 0f);
    }

    private bool CanEngageTargets() =>
        MinionControlService.CurrentStance != MinionControlStance.Passive;

    private EnemyBaseController ResolveStrikeTargetCandidate()
    {
        MinionControlStance stance = MinionControlService.CurrentStance;
        if (stance == MinionControlStance.Passive)
            return null;

        if (stance == MinionControlStance.Assist)
        {
            if (!IsOwnerInCombat())
                return null;

            return GetOwnerCurrentTarget();
        }

        return FindRandomEnemyInRange(GetOwnerPosition(), _presentation.attackRange);
    }

    private void ApplyDiveFacingRotation(Vector3 toStrike)
    {
        if (toStrike.sqrMagnitude < 1e-6f)
            return;

        float pitch = Mathf.Atan2(
            Mathf.Max(0f, -toStrike.y),
            Mathf.Max(0.05f, Mathf.Abs(toStrike.x))) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(pitch, 20f, 80f);
        float z = _flightFacingLeft ? pitch : -pitch;
        transform.rotation = Quaternion.Euler(0f, 0f, z);
    }

    private bool IsOwnerInCombat()
    {
        EnemyBaseController target = GetOwnerCurrentTarget();
        return target != null;
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

    private void EnforceLeashTeleport()
    {
        float maxD = _presentation.maxLeashDistanceFromPlayer;
        if (maxD <= 0f)
            return;

        Vector3 ownerPos = GetOwnerPosition();
        if ((transform.position - ownerPos).sqrMagnitude <= maxD * maxD)
            return;

        _strikeTarget = null;
        _state = MotionState.Flying;
        transform.rotation = Quaternion.identity;
        SnapToFlightBand();
        BeginPatrol();
        if (spriteRenderer)
            spriteRenderer.flipY = false;
    }

    private float GetFollowStopWorldX(Vector3 ownerPos)
    {
        float myX = transform.position.x;
        float dx = ownerPos.x - myX;
        if (Mathf.Abs(dx) <= _presentation.followStopDistance)
            return myX;

        float sign = Mathf.Sign(dx);
        if (Mathf.Approximately(sign, 0f))
            sign = _patrolDirection;

        return ownerPos.x - sign * _presentation.followStopDistance;
    }

    private void ApplyHit(EnemyBaseController enemy)
    {
        if (!enemy || enemy.IsDead || !_ownerStats)
            return;

        SplitDamage d = _runtimeStats.FinalDamageSplitRange.RollBasicAttackDamage(
            _runtimeStats.CritChance,
            _runtimeStats.CritDamageMultiplier,
            out bool crit);

        Transform atk = _attackerTransform ? _attackerTransform : transform;
        string label = AbilityCombatPower.HawkCompanionOutgoingSourceLabel;

        if (_lightningInfused)
        {
            float total = Mathf.Max(0f, d.physical + d.magic + d.corruptionDamage);
            int lightning = Mathf.RoundToInt(total);
            if (lightning > 0)
            {
                enemy.TakeDamage(
                    lightning,
                    DamageType.Magic,
                    crit,
                    atk,
                    null,
                    DpsDamageBucket.Minion,
                    outgoingDpsSourceLabel: label);
            }

            TryApplyLightningShock(enemy, atk);
            return;
        }

        int phys = Mathf.RoundToInt(Mathf.Max(0f, d.physical));
        if (phys > 0)
        {
            enemy.TakeDamage(
                phys,
                DamageType.Physical,
                crit,
                atk,
                null,
                DpsDamageBucket.Minion,
                outgoingDpsSourceLabel: label);
        }
    }

    private void TryApplyLightningShock(EnemyBaseController enemy, Transform atk)
    {
        if (UnityEngine.Random.value > AbilityCombatPower.HawkCompanionLightningShockChance)
            return;

        AilmentController ailments = enemy.GetComponent<AilmentController>();
        if (!ailments)
            return;

        ailments.ApplyShockFromHit(new ShockPayload(
            duration: _ownerStats.ShockDuration,
            damageTakenMultiplier: _ownerStats.ShockDamageTakenMultiplier,
            source: atk));
    }

    private void ScheduleNextStrike()
    {
        float aps = Mathf.Max(0.01f, _runtimeStats.AttacksPerSecond);
        _nextStrikeReadyTime = Time.time + 1f / aps;
    }

    private void ApplyFlightBob()
    {
        float wobbleT = Time.time * Mathf.Max(0.01f, _presentation.wobbleFrequency);
        Vector3 bob = new Vector3(
            Mathf.Sin(wobbleT) * _presentation.wobbleAmplitudeX,
            Mathf.Sin(wobbleT * 1.11f + 0.5f) * _presentation.wobbleAmplitudeY,
            0f);
        transform.position += bob * Time.deltaTime;
    }

    private Vector3 GetStrikeWorldPosition(EnemyBaseController enemy)
    {
        Vector3 p = enemy.transform.position;
        p.y += _presentation.strikeHeightAboveEnemy;
        return p;
    }

    private Vector3 GetOwnerPosition() =>
        _homeAnchor ? _homeAnchor.position : (_ownerStats ? _ownerStats.transform.position : transform.position);

    private void SnapToFlightBand()
    {
        Vector3 ownerPos = GetOwnerPosition();
        _patrolDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        float offsetX = UnityEngine.Random.Range(
            _presentation.wanderDistanceMin,
            _presentation.wanderDistanceMax) * _patrolDirection;
        offsetX = Mathf.Clamp(
            offsetX,
            -_presentation.maxWanderRadiusFromPlayer,
            _presentation.maxWanderRadiusFromPlayer);
        transform.position = new Vector3(
            ownerPos.x + offsetX,
            ownerPos.y + _presentation.flightHeightAbovePlayer,
            ownerPos.z);
        ApplyHorizontalFlightFacing(_patrolDirection < 0f);
    }

    private void ApplyVisualScale()
    {
        float u = Mathf.Max(0.05f, _presentation.visualWorldScale);
        transform.localScale = new Vector3(u, u, u);
    }

    private static EnemyBaseController FindRandomEnemyInRange(Vector3 from, float range)
    {
        float r2 = range * range;
        IReadOnlyList<EnemyBaseController> candidates = CombatEnemyRegistry.GetLiveEnemies();
        EnemyBaseController pick = null;
        int count = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            EnemyBaseController e = candidates[i];
            if (!e || e.IsDead)
                continue;

            if ((e.transform.position - from).sqrMagnitude > r2)
                continue;

            count++;
            if (UnityEngine.Random.Range(0, count) == 0)
                pick = e;
        }

        return pick;
    }
}
