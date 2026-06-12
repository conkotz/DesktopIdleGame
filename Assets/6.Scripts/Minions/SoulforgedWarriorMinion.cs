using System;
using System.Collections;
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

    private CharacterStats _ownerStats;
    private Vector3 _baseOverheadAnchorLocal;
    private MinionDefinition _def;
    private Transform _homeAnchor;
    private Action<SoulforgedWarriorMinion> _onDespawned;
    private float _expireTime;
    private bool _initialized;
    private Coroutine _deathRoutine;

    public EnemyBaseController CurrentTarget => combat ? combat.CurrentTarget : null;

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
        float durationOverrideSeconds = -1f)
    {
        if (!ownerStats || !definition || !definition.runtimePrefab)
            return false;

        ResolveComponents();

        _ownerStats = ownerStats;
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
        return true;
    }

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

    public void RebindOwnerAfterSceneLoad(
        CharacterStats ownerStats,
        Transform ownerAnchor,
        Transform rangeOrigin = null)
    {
        if (!ownerStats || !ownerAnchor)
            return;

        _ownerStats = ownerStats;
        _homeAnchor = ownerAnchor;

        PlayerCombatController ownerCombat = ownerStats.GetComponent<PlayerCombatController>();
        if (!ownerCombat)
            ownerCombat = ownerStats.GetComponentInParent<PlayerCombatController>();
        combatTarget?.BindOwnerCombat(ownerCombat);

        combat?.RebindOwnerAnchor(
            ownerStats,
            ownerAnchor,
            rangeOrigin ? rangeOrigin : ownerAnchor);
    }

    public void SnapToOwnerAfterSceneLoad()
    {
        if (!_initialized || !_homeAnchor)
            return;

        SnapToSpawnPosition();
        unit?.AlignFloorToOwnerSoldier(_homeAnchor);
        combat?.SnapBesideOwnerAfterSceneLoad();
    }

    private void Update()
    {
        if (!_initialized || !combatTarget.IsAlive)
            return;

        if (Time.time >= _expireTime)
            ExpireImmediately();
    }

    private void OnDestroy()
    {
        if (minionStats)
            minionStats.OnDied -= HandleMinionDied;
        combat?.Shutdown();
        _onDespawned?.Invoke(this);
    }

    private void HandleMinionDied()
    {
        if (_deathRoutine != null)
            return;

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
