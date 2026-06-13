using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Soulforged Warrior lifecycle: vitals, spectral Soldier rig, shared <see cref="MinionCombatController"/> brain.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterStats))]
[RequireComponent(typeof(MinionCombatTarget))]
[RequireComponent(typeof(MinionUnit))]
[RequireComponent(typeof(MinionCombatController))]
public class SoulforgedWarriorMinion : MonoBehaviour
{
    [SerializeField] private CharacterStats minionStats;
    [SerializeField] private MinionCombatTarget combatTarget;
    [SerializeField] private MinionUnit unit;
    [SerializeField] private MinionCombatController combat;
    [SerializeField] private Transform overheadAnchor;
    [SerializeField] private bool debugLogs;

    [Header("Soulforged Warrior Position Adjustment")]
    [SerializeField] private SoulforgedWarriorPositionAdjustment positionAdjustment = SoulforgedWarriorPositionAdjustment.Default;
    [SerializeField] private float spawnDistanceFromPlayer = 3f;
    [Tooltip("Fallback spawn side when owner facing cannot be resolved (+1 = right, -1 = left).")]
    [SerializeField] private float spawnSideSign = 1f;

    [Header("Warcry VFX")]
    [SerializeField] private float warcryRingMaxRadius = 8f;
    [SerializeField] private float warcryRingDuration = 0.55f;

    [Header("Furious Slam VFX")]
    [SerializeField] private float furiousSlamShockwaveDuration = 0.35f;

    private CharacterStats _ownerStats;
    private SkillsManager _skillsManager;
    private PlayerAbilityVfxController _ownerAbilityVfx;
    private Vector3 _baseOverheadAnchorLocal;
    private MinionDefinition _def;
    private Transform _homeAnchor;
    private Action<SoulforgedWarriorMinion> _onDespawned;
    private float _expireTime;
    private bool _initialized;
    private Coroutine _deathRoutine;
    private Coroutine _warcryRoutine;
    private Coroutine _furiousSlamRoutine;

    private readonly List<EnemyBaseController> _aoeScratch = new();

    public EnemyBaseController CurrentTarget => combat ? combat.CurrentTarget : null;
    public bool IsOperational => _initialized && combatTarget && combatTarget.IsAlive;

    private void Awake()
    {
        ResolveComponents();
        CacheBaseOverheadAnchorLocal();
    }

    public bool Initialize(
        CharacterStats ownerStats,
        MinionDefinition definition,
        SoulforgedWeaponMinionPresentation presentationFromController,
        Transform homeAnchor,
        Transform attackerTransform = null,
        Action<SoulforgedWarriorMinion> onDespawned = null,
        float durationOverrideSeconds = -1f,
        SkillsManager skillsManager = null)
    {
        if (!ownerStats || !definition || !definition.runtimePrefab)
            return false;

        ResolveComponents();

        _ownerStats = ownerStats;
        _skillsManager = skillsManager ? skillsManager : SkillsManager.Instance;
        _ownerAbilityVfx = ResolveOwnerAbilityVfx(ownerStats);
        _def = definition;
        _homeAnchor = homeAnchor ? homeAnchor : ownerStats.transform;
        _onDespawned = onDespawned;

        Transform rangeOrigin = attackerTransform ? attackerTransform : ownerStats.transform;
        SoulforgedWeaponMinionPresentation presentation =
            SoulforgedWeaponMinionPresentation.Resolve(presentationFromController);

        float duration = durationOverrideSeconds > 0f ? durationOverrideSeconds : _def.summonDuration;
        _expireTime = Time.time + Mathf.Max(0.1f, duration);

        int maxHp = ComputeMaxHealth(ownerStats, definition);
        minionStats.ApplySummonVitals(maxHp, "Warrior");
        minionStats.ApplySummonDefensesFromOwner(ownerStats, definition.defensiveInheritance);
        minionStats.OnDied += HandleMinionDied;

        float visualScale = presentation.visualWorldScale *
                            Mathf.Max(0.05f, positionAdjustment.visualScaleMultiplier);
        if (!unit.SetupFromOwner(ownerStats.transform, Color.white, visualScale))
        {
            if (debugLogs)
                Debug.LogWarning("[SoulforgedWarrior] Soldier visual setup failed.", this);
        }

        ApplyPositionAdjustment();

        if (!combat.Initialize(
                ownerStats,
                definition,
                presentation,
                _homeAnchor,
                rangeOrigin,
                AbilityCombatPower.SoulforgedWarriorOutgoingSourceLabel))
            return false;

        PlayerCombatController ownerCombat = ownerStats.GetComponent<PlayerCombatController>();
        if (!ownerCombat)
            ownerCombat = ownerStats.GetComponentInParent<PlayerCombatController>();
        combatTarget.BindOwnerCombat(ownerCombat);

        SnapToSpawnPosition();
        unit?.AlignFloorToOwnerSoldier(ownerStats.transform);
        _initialized = true;
        RestartWarcryRoutine();
        RestartFuriousSlamRoutine();
        return true;
    }

    public void BindReleasedCallback(Action<SoulforgedWarriorMinion> onDespawned) => _onDespawned = onDespawned;

    public void TryRecastRetargetOrReturn() => combat?.TryRecastRetargetOrReturn();

    public bool ForceTarget(EnemyBaseController enemy) =>
        combat && combat.ForceTarget(enemy);

    public void CancelAndDestroy()
    {
        _onDespawned = null;
        Destroy(gameObject);
    }

    public void ExpireImmediately() => Destroy(gameObject);

    public void PersistAcrossSceneLoads()
    {
        transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
    }

    public void RefreshAfterSceneLoad(
        CharacterStats ownerStats,
        Transform ownerAnchor,
        Transform rangeOrigin = null)
    {
        if (!ownerStats || !ownerAnchor)
            return;

        gameObject.SetActive(true);
        _ownerStats = ownerStats;
        _homeAnchor = ownerAnchor;
        _ownerAbilityVfx = ResolveOwnerAbilityVfx(ownerStats);

        PlayerCombatController ownerCombat = ownerStats.GetComponent<PlayerCombatController>();
        if (!ownerCombat)
            ownerCombat = ownerStats.GetComponentInParent<PlayerCombatController>();
        combatTarget?.BindOwnerCombat(ownerCombat);

        if (combat)
        {
            combat.RebindOwnerAnchor(ownerStats, ownerAnchor, rangeOrigin ? rangeOrigin : ownerAnchor);
            combat.ResetStateAfterSceneLoad();
        }

        SnapToSpawnPosition();
        unit?.AlignFloorToOwnerSoldier(_homeAnchor);
    }

    private void Update()
    {
        if (!_initialized || !combatTarget.IsAlive)
            return;

        if (Time.time >= _expireTime)
        {
            ExpireImmediately();
            return;
        }
    }

    private void OnDestroy()
    {
        StopWarcryRoutine();
        StopFuriousSlamRoutine();
        if (minionStats)
            minionStats.OnDied -= HandleMinionDied;
        combat?.Shutdown();
        _onDespawned?.Invoke(this);
    }

    private void HandleMinionDied()
    {
        if (_deathRoutine != null)
            return;

        StopWarcryRoutine();
        StopFuriousSlamRoutine();
        combat?.Shutdown();
        unit?.PlayDie();
        _deathRoutine = StartCoroutine(CoDespawnAfterDeath());
    }

    private IEnumerator CoDespawnAfterDeath()
    {
        float wait = unit ? unit.GetDieClipLength() : 0.8f;
        yield return new WaitForSeconds(Mathf.Max(0.1f, wait));
        ExpireImmediately();
    }

    private void RestartWarcryRoutine()
    {
        StopWarcryRoutine();
        if (isActiveAndEnabled)
            _warcryRoutine = StartCoroutine(CoWarcryLoop());
    }

    private void StopWarcryRoutine()
    {
        if (_warcryRoutine == null)
            return;

        StopCoroutine(_warcryRoutine);
        _warcryRoutine = null;
    }

    private IEnumerator CoWarcryLoop()
    {
        yield return new WaitForSeconds(AbilityCombatPower.SoulforgedWarriorWarcryFirstDelaySeconds);

        while (IsOperational)
        {
            PerformWarcry();
            yield return new WaitForSeconds(AbilityCombatPower.SoulforgedWarriorWarcryIntervalSeconds);
        }
    }

    private void PerformWarcry()
    {
        if (!IsOperational)
            return;

        Vector3 center = transform.position;
        SoulforgedWarriorMinionVfx.PlayWarcryRing(
            this,
            center,
            warcryRingMaxRadius > 0f ? warcryRingMaxRadius : AbilityCombatPower.SoulforgedWarriorWarcryAllyRange,
            warcryRingDuration);

        ApplyWarcryAllyBuffs();
        if (HasTauntingShoutEnhancement())
            TauntNearbyEnemies(AbilityCombatPower.SoulforgedWarriorTauntRange);
    }

    private void ApplyWarcryAllyBuffs()
    {
        float range = AbilityCombatPower.SoulforgedWarriorWarcryAllyRange;
        float rangeSq = range * range;
        Vector3 myPos = transform.position;

        if (_ownerStats)
        {
            float distSq = (_ownerStats.transform.position - myPos).sqrMagnitude;
            if (distSq <= rangeSq)
                ApplyPhysicalBuffToCharacter(_ownerStats);
        }

        IReadOnlyList<MinionCombatTarget> allies = MinionCombatTarget.ActiveTargets;
        for (int i = 0; i < allies.Count; i++)
        {
            MinionCombatTarget ally = allies[i];
            if (!ally || ally == combatTarget || !ally.IsAlive)
                continue;

            if ((ally.transform.position - myPos).sqrMagnitude > rangeSq)
                continue;

            ApplyPhysicalBuffToCharacter(ally.Stats);
        }
    }

    private static void ApplyPhysicalBuffToCharacter(CharacterStats targetStats)
    {
        if (!targetStats)
            return;

        PlayerBuffController buffs = targetStats.GetComponent<PlayerBuffController>();
        if (!buffs)
            buffs = targetStats.GetComponentInParent<PlayerBuffController>();
        if (!buffs)
            return;

        buffs.ApplyBuff(new ConsumableGrantedEffect
        {
            effectType = ConsumableEffectType.PhysicalDamageBoost,
            magnitude = AbilityCombatPower.SoulforgedWarriorWarcryPhysicalDamageBonus,
            duration = AbilityCombatPower.SoulforgedWarriorWarcryBuffDurationSeconds
        });
    }

    private void TauntNearbyEnemies(float range)
    {
        float rangeSq = range * range;
        Vector3 myPos = transform.position;
        EnemyBaseController[] enemies = FindObjectsByType<EnemyBaseController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyBaseController enemy = enemies[i];
            if (!enemy || enemy.IsDead)
                continue;

            if ((enemy.transform.position - myPos).sqrMagnitude > rangeSq)
                continue;

            enemy.ForceTauntToMinion(transform, HasTauntingShoutEnhancement());
        }
    }

    private void RestartFuriousSlamRoutine()
    {
        StopFuriousSlamRoutine();
        if (isActiveAndEnabled && HasFuriousSlamEnhancement())
            _furiousSlamRoutine = StartCoroutine(CoFuriousSlamLoop());
    }

    private void StopFuriousSlamRoutine()
    {
        if (_furiousSlamRoutine == null)
            return;

        StopCoroutine(_furiousSlamRoutine);
        _furiousSlamRoutine = null;
    }

    private IEnumerator CoFuriousSlamLoop()
    {
        yield return new WaitForSeconds(AbilityCombatPower.SoulforgedWarriorFuriousSlamCooldownSeconds);

        while (IsOperational)
        {
            PerformFuriousSlam();
            yield return new WaitForSeconds(AbilityCombatPower.SoulforgedWarriorFuriousSlamCooldownSeconds);
        }
    }

    private void PerformFuriousSlam()
    {
        if (!IsOperational || !combat || !HasFuriousSlamEnhancement())
            return;

        if (!combat.IsActivelyEngagedInCombat)
            return;

        EnemyBaseController currentTarget = combat.CurrentTarget;
        if (currentTarget && !currentTarget.IsDead)
            unit?.FaceTargetX(currentTarget.transform.position.x);

        float slamReach = AbilityCombatPower.SoulforgedWarriorFuriousSlamRange;
        combat.CollectEnemiesInFrontArc(slamReach, _aoeScratch);
        if (_aoeScratch.Count <= 0)
            return;

        float faceSign = unit ? unit.FacingSignX : 1f;
        Color slamColor = SoulforgedWarriorMinionVfx.FuriousSlamShockwaveColor;
        if (_ownerAbilityVfx)
            _ownerAbilityVfx.SpawnGuardiansHammerShockwaveAt(transform.position, slamReach, faceSign, slamColor);
        else
            SoulforgedWarriorMinionVfx.PlayFuriousSlamShockwave(
                this,
                transform.position,
                faceSign,
                slamReach,
                furiousSlamShockwaveDuration);

        for (int i = 0; i < _aoeScratch.Count; i++)
        {
            combat.TryApplyStrikeToEnemy(
                _aoeScratch[i],
                AbilityCombatPower.SoulforgedWarriorFuriousSlamDamageMultiplier);
        }
    }

    private static PlayerAbilityVfxController ResolveOwnerAbilityVfx(CharacterStats ownerStats)
    {
        if (!ownerStats)
            return null;

        PlayerAbilityController abilities = ownerStats.GetComponent<PlayerAbilityController>();
        if (!abilities)
            abilities = ownerStats.GetComponentInParent<PlayerAbilityController>();
        return abilities ? abilities.GetComponent<PlayerAbilityVfxController>() : null;
    }

    private int GetEnhancementChoice()
    {
        if (!_skillsManager)
            _skillsManager = SkillsManager.Instance;
        if (!_skillsManager)
            return -1;

        return _skillsManager.GetSkillChoiceSelection(
            SkillType.Melee,
            AbilityCombatPower.SoulforgedWarriorEnhancementParentSpineNodeId,
            -1);
    }

    private bool HasFuriousSlamEnhancement() =>
        GetEnhancementChoice() == AbilityCombatPower.SoulforgedWarriorFuriousSlamChoiceIndex;

    private bool HasTauntingShoutEnhancement() =>
        GetEnhancementChoice() == AbilityCombatPower.SoulforgedWarriorTauntingShoutChoiceIndex;

    private void ResolveComponents()
    {
        if (!minionStats)
            minionStats = GetComponent<CharacterStats>();
        if (!combatTarget)
            combatTarget = GetComponent<MinionCombatTarget>();
        if (!unit)
            unit = GetComponent<MinionUnit>();
        if (!combat)
            combat = GetComponent<MinionCombatController>();
    }

    private void CacheBaseOverheadAnchorLocal()
    {
        if (!overheadAnchor)
        {
            Transform found = transform.Find("OverheadAnchor");
            overheadAnchor = found ? found : transform;
        }

        _baseOverheadAnchorLocal = overheadAnchor.localPosition;
    }

    private void ApplyPositionAdjustment()
    {
        unit?.ApplyExtraVisualLocalOffset(positionAdjustment.soldierVisualLocalOffset);
        BindOverheadToFlipRoot();
    }

    private void BindOverheadToFlipRoot()
    {
        if (!overheadAnchor || !unit || !unit.TryGetVisualFlipRoot(out Transform flipRoot))
            return;

        if (overheadAnchor.parent != flipRoot)
        {
            Vector3 worldPos = overheadAnchor.position;
            overheadAnchor.SetParent(flipRoot, true);
            Vector3 localOnFlip = flipRoot.InverseTransformPoint(worldPos);
            localOnFlip.x = positionAdjustment.overheadAnchorLocalOffset.x;
            localOnFlip.y += positionAdjustment.overheadAnchorLocalOffset.y;
            localOnFlip.z += positionAdjustment.overheadAnchorLocalOffset.z;
            overheadAnchor.localPosition = localOnFlip;
            return;
        }

        Vector3 lp = overheadAnchor.localPosition;
        lp.x = positionAdjustment.overheadAnchorLocalOffset.x;
        overheadAnchor.localPosition = lp;
    }

    private void SnapToSpawnPosition()
    {
        Vector3 spawnPos = GetSpawnWorldPosition();
        var rb = GetComponent<Rigidbody2D>();
        if (rb)
            rb.position = new Vector2(spawnPos.x, spawnPos.y);
        else
            transform.position = spawnPos;
    }

    private Vector3 GetSpawnWorldPosition()
    {
        if (!_homeAnchor)
            return transform.position;

        float forwardSign = ResolveOwnerForwardSign();
        return _homeAnchor.position + new Vector3(spawnDistanceFromPlayer * forwardSign, 0f, 0f);
    }

    private float ResolveOwnerForwardSign()
    {
        if (_ownerStats)
        {
            PlayerController playerController = _ownerStats.GetComponent<PlayerController>();
            if (!playerController)
                playerController = _ownerStats.GetComponentInParent<PlayerController>();
            if (playerController && !Mathf.Approximately(playerController.FacingDirectionX, 0f))
                return Mathf.Sign(playerController.FacingDirectionX);
        }

        return Mathf.Approximately(spawnSideSign, 0f) ? 1f : Mathf.Sign(spawnSideSign);
    }

    private static int ComputeMaxHealth(CharacterStats ownerStats, MinionDefinition definition)
    {
        if (!ownerStats || !definition)
            return 1;

        float lifeMult = 1f + ownerStats.GetEffectiveMinionMaxLifePercent(definition.combatConfig.damageSourceMode);
        float baseHp = definition.ownerMaxHealthFraction > 0f
            ? ownerStats.MaxHP * definition.ownerMaxHealthFraction
            : definition.baseMaxHealth;

        return Mathf.Max(1, Mathf.RoundToInt(baseHp * lifeMult));
    }
}

