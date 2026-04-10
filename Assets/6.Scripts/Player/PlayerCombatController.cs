using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[DisallowMultipleComponent]
public class PlayerCombatController : MonoBehaviour, ISaveable
{
    private struct DamageSample
    {
        public float time;
        public float amount;
    }
    [Header("Refs")]
    [SerializeField] private PlayerController player;
    [SerializeField] private CharacterStats stats;

    [Header("Optional Colliders (for edge-to-edge range)")]
    [SerializeField] private Collider2D playerCol;
    private Collider2D _targetColCached;

    [Header("Targeting")]
    [SerializeField] private bool clearTargetIfDead = true;

    [Header("Attack")]
    [Tooltip("Extra padding so range doesn't feel pixel-perfect.")]
    [SerializeField] private float rangePadding = 0.05f;

    [Tooltip("Stops micro-corrections at range edge (prevents tiny walk jitter).")]
    [SerializeField] private float stopSlack = 0.04f;

    [Tooltip("If false, player will not attack while gathering.")]
    [SerializeField] private bool allowAttackingWhileGathering = false;

    [Tooltip("If true, we keep moving to stay exactly at range edge. If false, we just move into range and stop.")]
    [SerializeField] private bool kiteAtRangeEdge = false;

    [Tooltip("If true, face the current target when in range (before swinging).")]
    [SerializeField] private bool faceTargetWhenAttacking = true;

    [Header("Ranged Projectile Visuals")]
    [SerializeField] private ProjectileVisual rangedProjectilePrefab;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField, Min(0.01f)] private float rangedProjectileSpeed = 12f;
    [SerializeField] private float rangedProjectileRotationOffset = 0f;
    [Tooltip("Delay from attack start to projectile release (animation sync).")]
    [SerializeField, Min(0f)] private float rangedProjectileFireDelay = 0f;
    [SerializeField, Min(0f)] private float rangedDamageDelayOffset = 0f;

    [Header("Magic Projectile Visuals")]
    [SerializeField] private MonoBehaviour magicProjectilePrefab;
    [SerializeField] private MonoBehaviour magicLightningProjectilePrefab;
    [SerializeField] private MonoBehaviour magicFireProjectilePrefab;
    [SerializeField] private MonoBehaviour magicIceProjectilePrefab;
    [SerializeField] private Transform magicProjectileSpawnPoint;
    [SerializeField, Min(0.01f)] private float magicProjectileSpeed = 14f;
    [SerializeField] private float magicProjectileRotationOffset = 0f;
    [Tooltip("Delay from attack start to magic bolt release (animation sync).")]
    [SerializeField, Min(0f)] private float magicProjectileFireDelay = 0f;

    [Header("Idle Combat (Auto Target)")]
    [SerializeField] private bool idleCombatEnabled = false;

    [Tooltip("How often to rescan for closest enemy (seconds).")]
    [SerializeField] private float idleRescanInterval = 0.25f;

    [Tooltip("While idle combat is on, every N seconds all dropped items on the scene are picked up (no walking).")]
    [SerializeField, Min(0.5f)] private float idleAutoPickupIntervalSeconds = 5f;
    private float _nextIdleAutoPickupTime;
    [Header("Retaliation")]
    [SerializeField] private bool retaliationEnabled = false;

    [Header("Auto Consumables")]
    [SerializeField] private ActionBarUI actionBar;
    [SerializeField] private PlayerConsumableController consumableController;
    [SerializeField] private PlayerAbilityController abilityController;
    [SerializeField] private Inventory inventory;

    [SerializeField] private float autoConsumeInterval = 0.2f;
    [SerializeField] private bool autoUseFood = true;
    [SerializeField] private bool autoUsePotions = true;
    [SerializeField] private float autoAbilityInterval = 0.1f;
    [SerializeField] private bool autoUseAbilities = true;

    private float _nextAutoConsumeTime;
    private float _nextAutoAbilityTime;

    [Header("Combat XP")]
    [SerializeField, Range(0f, 5f)]
    private float xpPerDamage = 0.1f;

    [SerializeField]
    private string combatXpSource = "Combat";

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    [Header("DPS Tracking")]
    [SerializeField, Min(0.1f)] private float dpsResetOutOfCombatSeconds = 5f;

    public event System.Action<bool> OnIdleCombatChanged;
    public event System.Action<bool> OnRetaliationChanged;
    public event System.Action OnTargetChanged;

    public EnemyBaseController CurrentTarget => _target;
    public EnemyBaseController Target => _target;
    public bool IdleCombatEnabled => idleCombatEnabled;
    public bool RetaliationEnabled => retaliationEnabled;
    public float NextAttackTime => _nextAttackTime;

    private EnemyBaseController _target;
    private float _nextAttackTime;
    private float _nextIdleScanTime;
    private float _nextLowManaPopupTime;
    private float _combatSessionStartTime = -1f;
    private float _combatSessionDamageSum;
    private float _lastDamageTime = -999f;
    private float _lastCombatActivityTime = -999f;

    public float GetAttackCooldownSeconds()
    {
        if (stats == null) return 0f;
        return 1f / Mathf.Max(0.01f, stats.AttacksPerSecond);
    }

    public float GetAttackCycleNormalized()
    {
        float cooldown = GetAttackCooldownSeconds();
        if (cooldown <= 0f)
            return 0f;

        float remaining = Mathf.Max(0f, _nextAttackTime - Time.time);
        float readyProgress = 1f - (remaining / cooldown);
        return Mathf.Clamp01(readyProgress);
    }

    /// <summary>Whether an ability may consume the current attack cycle right now.</summary>
    public bool CanConsumeAttackCycleNow()
    {
        return Time.time >= _nextAttackTime;
    }

    /// <summary>
    /// Reserves the next attack cycle for an ability-cast attack timing gate.
    /// Returns false if the normal attack timer is still cooling down.
    /// </summary>
    public bool TryConsumeAttackCycleForAbilityCast()
    {
        if (!CanConsumeAttackCycleNow())
            return false;
        if (stats == null)
            return false;

        float cooldown = 1f / Mathf.Max(0.01f, stats.AttacksPerSecond);
        _nextAttackTime = Time.time + cooldown;
        if (player != null)
            player.SetActionOverride(PlayerController.PlayerAction.Fighting);
        return true;
    }

    public float GetCurrentDps()
    {
        float now = Time.time;
        if (!IsCombatEngaged() && now - _lastCombatActivityTime >= Mathf.Max(0.1f, dpsResetOutOfCombatSeconds))
            return 0f;

        if (_combatSessionStartTime < 0f || _combatSessionDamageSum <= 0f)
            return 0f;

        float duration = Mathf.Max(0.001f, now - _combatSessionStartTime);
        if (duration <= 0f)
            return 0f;

        return _combatSessionDamageSum / duration;
    }

    private void Awake()
    {
        if (!playerCol) playerCol = GetComponent<Collider2D>();
        TryResolveAutoConsumeRefs();
        if (idleCombatEnabled)
            _nextIdleAutoPickupTime = Time.time + Mathf.Max(0.5f, idleAutoPickupIntervalSeconds);
    }

    private void Update()
    {
        if (!player || !stats) return;

        if (IsCombatEngaged())
        {
            _lastCombatActivityTime = Time.time;
            if (_combatSessionStartTime < 0f)
                _combatSessionStartTime = Time.time;
        }
        else if (Time.time - _lastCombatActivityTime >= Mathf.Max(0.1f, dpsResetOutOfCombatSeconds))
        {
            ResetDpsSession();
        }

        if (idleCombatEnabled)
        {
            TickAutoConsumables();
            TickAutoAbilities();
            TickIdleCombatTargeting();
            TickIdleAutoPickup();
        }

        if (_target == null) return;

        if (clearTargetIfDead && _target.IsDead)
        {
            ClearTargetInternal();

            if (player != null)
                player.ForceIdleAction();

            return;
        }

        if (!allowAttackingWhileGathering)
        {
            var a = player.CurrentAction;
            bool isGathering =
                (a == PlayerController.PlayerAction.Mining ||
                 a == PlayerController.PlayerAction.Woodcutting ||
                 a == PlayerController.PlayerAction.Fishing);

            if (isGathering)
                return;
        }

        if (stats.AttacksPerSecond <= 0f || stats.MaxDamage <= 0)
        {
            var mainDef = player != null && stats != null
                ? GetMainWeaponDefForPopup()
                : null;

            if (_target != null && mainDef != null && mainDef.RequiresOffhandSupport)
            {
                player.SendMessage(
                    "ShowPopup",
                    $"Requires {mainDef.RequiredSupportType} in offhand.",
                    SendMessageOptions.DontRequireReceiver
                );
            }

            return;
        }

        float myRange = Mathf.Max(0f, stats.Range) + rangePadding;

        float enemyX = _target.transform.position.x;
        float myX = transform.position.x;

        if (_targetColCached == null || _targetColCached.gameObject != _target.gameObject)
            _targetColCached = _target.GetComponent<Collider2D>();

        float myHalf = HalfWidthX(playerCol);
        float enemyHalf = HalfWidthX(_targetColCached);

        float gap = EdgeGapX(myX, enemyX, myHalf, enemyHalf);

        if (gap > myRange + stopSlack)
        {
            float desiredCenterDist = myRange + myHalf + enemyHalf;
            float desiredX = (myX < enemyX) ? (enemyX - desiredCenterDist) : (enemyX + desiredCenterDist);

            player.ClearActionOverride();
            player.MoveToPointX_Combat(desiredX);
            return;
        }

        player.StopMoveOnly();

        if (kiteAtRangeEdge)
        {
            float desiredCenterDist = myRange + myHalf + enemyHalf;
            float desiredX = (myX < enemyX) ? (enemyX - desiredCenterDist) : (enemyX + desiredCenterDist);
            player.MoveToPointX_Combat(desiredX);
        }

        if (faceTargetWhenAttacking)
            player.FaceTargetX(enemyX);

        float cooldown = 1f / Mathf.Max(0.01f, stats.AttacksPerSecond);

        // Prioritize queued Crescent Slash over normal auto attack cadence.
        if (abilityController != null && abilityController.TryAutoReleaseQueuedCrescentSlashFromCadence())
            return;

        if (Time.time < _nextAttackTime)
        {
            player.ClearActionOverride();
            return;
        }

        if (IsMagicAttack() && !TrySpendManaForMagicAttack())
        {
            player.ClearActionOverride();
            if (Time.time >= _nextLowManaPopupTime)
            {
                player.ShowPopup("Not enough mana.");
                _nextLowManaPopupTime = Time.time + 0.4f;
            }
            return;
        }

        _nextAttackTime = Time.time + cooldown;

        player.SetActionOverride(PlayerController.PlayerAction.Fighting);

        SplitDamage rolled = stats.RollSplitAttackDamage(out bool wasCrit);

        if (abilityController != null)
            abilityController.TryConsumeQueuedAttackModifier(ref rolled);

        if (rolled.IsEmpty)
        {
            player.ClearActionOverride();
            return;
        }

        player.TriggerAttackAnim();

        if (IsRangedAttack())
        {
            HandleRangedAttack(_target, rolled, wasCrit);
        }
        else if (IsMagicAttack())
        {
            HandleMagicAttack(_target, rolled, wasCrit);
        }
        else
        {
            ResolveAttackHitNow(_target, rolled, wasCrit);
        }
    }

    private bool IsCombatEngaged()
    {
        bool hasLiveTarget = _target != null && !_target.IsDead && _target.gameObject.activeInHierarchy;
        bool playerInCombat = player != null && player.InCombat;
        return hasLiveTarget || playerInCombat;
    }

    private void TickAutoConsumables()
    {
        TryResolveAutoConsumeRefs();

        if (Time.time < _nextAutoConsumeTime)
            return;

        _nextAutoConsumeTime = Time.time + Mathf.Max(0.05f, autoConsumeInterval);

        if (!idleCombatEnabled)
            return;

        if (!IsPlayerAlive())
            return;

        if (actionBar == null || consumableController == null || inventory == null)
            return;

        if (autoUseFood && TryAutoUseFood())
            return;

        if (autoUsePotions)
            TryAutoUsePotion();
    }

    private bool TryAutoUseFood()
    {
        float currentHp = GetCurrentHP();
        float maxHp = GetMaxHP();

        if (maxHp <= 0f || currentHp <= 0f)
            return false;

        float missingHp = maxHp - currentHp;

        foreach (var slot in actionBar.GetSlots())
        {
            if (slot == null)
                continue;

            var action = slot.AssignedAction;
            if (action == null || !action.IsAssigned || !action.IsItem)
                continue;

            if (slot.SlotType != ActionBarSlotType.Food &&
                slot.SlotType != ActionBarSlotType.Any)
                continue;

            var def = inventory.GetItemDef(action.id);
            if (!def || !def.IsConsumable || !def.IsFood)
                continue;

            if (def.HealAmount <= 0)
                continue;

            int count = consumableController.CountItem(action.id);
            if (count <= 0)
                continue;

            if (missingHp < (float)def.HealAmount)
                continue;

            return consumableController.TryUseItem(action.id);
        }

        return false;
    }

    private bool TryAutoUsePotion()
    {
        foreach (var slot in actionBar.GetSlots())
        {
            if (slot == null)
                continue;

            var action = slot.AssignedAction;
            if (action == null || !action.IsAssigned || !action.IsItem)
                continue;

            if (slot.SlotType != ActionBarSlotType.Potion &&
                slot.SlotType != ActionBarSlotType.Any)
                continue;

            var def = inventory.GetItemDef(action.id);
            if (!def || !def.IsConsumable || !def.IsPotion)
                continue;

            int count = consumableController.CountItem(action.id);
            if (count <= 0)
                continue;

            if (consumableController.IsOnCooldown(action.id, out _))
                continue;

            bool used = consumableController.TryUseItem(action.id);
            return used;
        }

        return false;
    }

    private bool IsPlayerAlive()
    {

        return player != null && !player.IsDead;
    }

    private void TickAutoAbilities()
    {
        TryResolveAutoConsumeRefs();

        if (Time.time < _nextAutoAbilityTime)
            return;

        _nextAutoAbilityTime = Time.time + Mathf.Max(0.05f, autoAbilityInterval);

        if (!idleCombatEnabled || !autoUseAbilities)
            return;

        if (!IsPlayerAlive())
            return;

        if (actionBar == null || abilityController == null)
            return;

        if (_target == null || _target.IsDead)
            return;

        var orderedSlots = actionBar
            .GetSlots()
            .Where(slot => slot != null)
            .OrderBy(slot => slot.SlotIndex);

        foreach (var slot in orderedSlots)
        {
            var action = slot.AssignedAction;
            if (action == null || !action.IsAssigned || !action.IsAbility)
                continue;

            if (!slot.CanAccept(action))
                continue;

            if (abilityController.TryUseAbility(action.id, showLockedFeedback: false))
                break;
        }
    }

    private float GetCurrentHP()
    {
        return player != null ? player.HP : 0f;
    }

    private float GetMaxHP()
    {
        return player != null ? player.MaxHP : 0f;
    }


    private void TryConsumeOffHandSupportAmmo()
    {
        var equipment = GetComponent<EquipmentManager>();
        if (!equipment) return;

        var offDef = equipment.GetOffHandDef();
        if (!offDef || !offDef.IsCombatSupport) return;
        if (!offDef.SupportConsumableOnAttack) return;

        int consume = Mathf.Max(1, offDef.SupportConsumeAmountPerAttack);
        equipment.ConsumeOffHandSupport(consume);
    }

    private ItemDefinition GetMainWeaponDefForPopup()
    {
        if (stats == null) return null;

        var equipment = GetComponent<EquipmentManager>();
        var inventory = GetComponent<Inventory>();

        if (equipment == null || inventory == null) return null;
        if (string.IsNullOrWhiteSpace(equipment.MainHandItemId)) return null;

        return inventory.GetItemDef(equipment.MainHandItemId);
    }

    private bool IsRangedAttack()
    {
        return stats != null && stats.CurrentAttackSkill == AttackSkill.Ranged;
    }

    private bool IsMagicAttack()
    {
        return stats != null && stats.CurrentAttackSkill == AttackSkill.Magic;
    }

    private bool TrySpendManaForMagicAttack()
    {
        if (player == null)
            return false;

        float manaCost = 0f;
        var weapon = GetMainWeaponDefForPopup();
        if (weapon != null)
            manaCost = weapon.ManaCostPerAttack;

        if (manaCost <= 0f)
            return true;

        return player.SpendMana(manaCost);
    }

    private void HandleRangedAttack(EnemyBaseController targetAtFireTime, SplitDamage rolled, bool wasCrit)
    {
        float fireDelay = Mathf.Max(0f, rangedProjectileFireDelay);
        if (fireDelay <= 0f)
        {
            ResolveRangedAttackAtRelease(targetAtFireTime, rolled, wasCrit);
            return;
        }

        StartCoroutine(ResolveRangedAttackAfterFireDelay(targetAtFireTime, rolled, wasCrit, fireDelay));
    }

    private System.Collections.IEnumerator ResolveRangedAttackAfterFireDelay(EnemyBaseController targetAtFireTime, SplitDamage rolled, bool wasCrit, float fireDelay)
    {
        yield return new WaitForSeconds(fireDelay);
        ResolveRangedAttackAtRelease(targetAtFireTime, rolled, wasCrit);
    }

    private void ResolveRangedAttackAtRelease(EnemyBaseController targetAtFireTime, SplitDamage rolled, bool wasCrit)
    {
        if (targetAtFireTime == null || targetAtFireTime.IsDead)
            return;

        float delay = Mathf.Max(0f, rangedDamageDelayOffset);
        bool spawnedProjectile = TrySpawnRangedProjectile(targetAtFireTime, out float travelTime);
        if (spawnedProjectile)
            delay += travelTime;

        if (delay <= 0f)
        {
            ResolveAttackHitNow(targetAtFireTime, rolled, wasCrit);
            return;
        }

        StartCoroutine(ResolveAttackHitAfterDelay(targetAtFireTime, rolled, wasCrit, delay));
    }

    private void HandleMagicAttack(EnemyBaseController targetAtFireTime, SplitDamage rolled, bool wasCrit)
    {
        float fireDelay = Mathf.Max(0f, magicProjectileFireDelay);
        if (fireDelay <= 0f)
        {
            ResolveMagicAttackAtRelease(targetAtFireTime, rolled, wasCrit);
            return;
        }

        StartCoroutine(ResolveMagicAttackAfterFireDelay(targetAtFireTime, rolled, wasCrit, fireDelay));
    }

    private System.Collections.IEnumerator ResolveMagicAttackAfterFireDelay(EnemyBaseController targetAtFireTime, SplitDamage rolled, bool wasCrit, float fireDelay)
    {
        yield return new WaitForSeconds(fireDelay);
        ResolveMagicAttackAtRelease(targetAtFireTime, rolled, wasCrit);
    }

    private void ResolveMagicAttackAtRelease(EnemyBaseController targetAtFireTime, SplitDamage rolled, bool wasCrit)
    {
        if (targetAtFireTime == null || targetAtFireTime.IsDead)
            return;

        if (!TrySpawnMagicProjectile(targetAtFireTime, out IMagicProjectileVisual bolt))
        {
            ResolveAttackHitNow(targetAtFireTime, rolled, wasCrit);
            return;
        }

        bolt.OnImpact += () => ResolveAttackHitNow(targetAtFireTime, rolled, wasCrit);
    }

    private bool TrySpawnRangedProjectile(EnemyBaseController targetAtFireTime, out float travelTime)
    {
        travelTime = 0f;

        if (rangedProjectilePrefab == null || targetAtFireTime == null)
            return false;

        Transform spawn = projectileSpawnPoint != null ? projectileSpawnPoint : transform;
        Vector3 start = spawn.position;
        Vector3 targetCenter = GetTargetCenterMass(targetAtFireTime);

        ProjectileVisual proj = Instantiate(rangedProjectilePrefab, start, Quaternion.identity);
        proj.Launch(
            start,
            targetAtFireTime.transform,
            targetCenter,
            rangedProjectileSpeed,
            rangedProjectileRotationOffset
        );

        travelTime = Mathf.Max(0f, proj.EstimatedTravelTime);
        return true;
    }

    private static Vector3 GetTargetCenterMass(EnemyBaseController target)
    {
        if (target == null)
            return Vector3.zero;

        if (target.TryGetComponent<Collider2D>(out var col) && col != null)
            return col.bounds.center;

        var childCol = target.GetComponentInChildren<Collider2D>();
        if (childCol != null)
            return childCol.bounds.center;

        var sr = target.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
            return sr.bounds.center;

        return target.transform.position;
    }

    private bool TrySpawnMagicProjectile(EnemyBaseController targetAtFireTime, out IMagicProjectileVisual bolt)
    {
        bolt = null;

        MonoBehaviour prefab = ResolveMagicProjectilePrefab();
        if (prefab == null || targetAtFireTime == null)
            return false;

        if (!prefab.TryGetComponent<IMagicProjectileVisual>(out _))
            return false;

        Transform spawn = magicProjectileSpawnPoint != null
            ? magicProjectileSpawnPoint
            : (projectileSpawnPoint != null ? projectileSpawnPoint : transform);

        Vector3 start = spawn.position;
        Vector3 targetCenter = GetTargetCenterMass(targetAtFireTime);

        MonoBehaviour instance = Instantiate(prefab, start, Quaternion.identity);
        if (!instance.TryGetComponent<IMagicProjectileVisual>(out bolt))
        {
            Destroy(instance.gameObject);
            return false;
        }

        bolt.Launch(
            start,
            targetAtFireTime.transform,
            targetCenter,
            magicProjectileSpeed,
            magicProjectileRotationOffset
        );

        return true;
    }

    private MonoBehaviour ResolveMagicProjectilePrefab()
    {
        MagicAttackType type = stats != null ? stats.CurrentMagicAttackType : MagicAttackType.Lightning;
        return type switch
        {
            MagicAttackType.Fire => magicFireProjectilePrefab != null ? magicFireProjectilePrefab : magicProjectilePrefab,
            MagicAttackType.Ice => magicIceProjectilePrefab != null ? magicIceProjectilePrefab : magicProjectilePrefab,
            _ => magicLightningProjectilePrefab != null ? magicLightningProjectilePrefab : magicProjectilePrefab
        };
    }


    private System.Collections.IEnumerator ResolveAttackHitAfterDelay(EnemyBaseController targetAtFireTime, SplitDamage rolled, bool wasCrit, float delay)
    {
        yield return new WaitForSeconds(delay);
        ResolveAttackHitNow(targetAtFireTime, rolled, wasCrit);
    }

    private void ResolveAttackHitNow(EnemyBaseController targetToHit, SplitDamage rolled, bool wasCrit)
    {
        DamageResult dealt = ApplySplitDamageToTarget(targetToHit, rolled, wasCrit);
        float totalDealt = dealt.Total;
        bool primaryHitSucceeded = totalDealt > 0f;
        var alreadyHit = new HashSet<EnemyBaseController>();
        if (targetToHit != null && primaryHitSucceeded)
            alreadyHit.Add(targetToHit);

        if (totalDealt > 0f)
            player.ApplyLifeSteal(totalDealt);

        if (totalDealt > 0f)
            TryConsumeOffHandSupportAmmo();

        bool suppressBleed = false;
        bool suppressPoison = false;
        bool triggerCrescentSlash = false;
        bool crescentAppliesElemental = false;
        bool crescentPenetrating = false;
        if (abilityController != null)
        {
            var queued = abilityController.ConsumeQueuedHitEffects(targetToHit, dealt.physical, dealt.corruptionDamage);
            suppressBleed = queued.suppressDefaultBleed;
            suppressPoison = queued.suppressDefaultPoison;
            triggerCrescentSlash = queued.triggerCrescentSlash;
            crescentAppliesElemental = queued.crescentAppliesElemental;
            crescentPenetrating = queued.crescentPenetrating;
        }

        if (!suppressBleed)
            TryApplyBleed(targetToHit, dealt);
        if (!suppressPoison)
            TryApplyPoison(targetToHit, dealt);
        TryApplyElementalMagicAilment(targetToHit, dealt);
        TryApplyMeleeShock(targetToHit, dealt);

        if (primaryHitSucceeded && abilityController != null &&
            abilityController.TryConsumeCleavingExtraTargetsOnSuccessfulHit(out int cleaveExtraTargets) &&
            cleaveExtraTargets > 0)
        {
            ApplyCleaveSecondaryHits(targetToHit, cleaveExtraTargets, alreadyHit);
        }

        if (primaryHitSucceeded && triggerCrescentSlash)
            ApplyCrescentSlashSecondaryHits(targetToHit, crescentPenetrating, crescentAppliesElemental, alreadyHit);
    }

    private void ApplyCleaveSecondaryHits(EnemyBaseController primaryTarget, int extraTargets, HashSet<EnemyBaseController> alreadyHit)
    {
        if (extraTargets <= 0 || stats == null)
            return;

        // Cleave uses at least 2f search range; longer-range weapons keep their full range.
        float range = Mathf.Max(2f, stats.Range);
        Vector3 origin = transform.position;
        var candidates = FindObjectsByType<EnemyBaseController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var nearest = new List<(EnemyBaseController enemy, float sqr)>(candidates.Length);
        float r2 = range * range;

        for (int i = 0; i < candidates.Length; i++)
        {
            EnemyBaseController e = candidates[i];
            if (!IsValidSecondaryTarget(e, alreadyHit))
                continue;
            float sqr = (e.transform.position - origin).sqrMagnitude;
            if (sqr > r2)
                continue;
            nearest.Add((e, sqr));
        }

        nearest.Sort((a, b) => a.sqr.CompareTo(b.sqr));
        int count = Mathf.Min(extraTargets, nearest.Count);
        float critMult = Mathf.Max(1f, stats.CritMultiplier);
        for (int i = 0; i < count; i++)
        {
            EnemyBaseController e = nearest[i].enemy;

            // Roll each target independently (min/max + crit).
            SplitDamage secondaryBase = stats.RollSplitAttackDamage(out bool baseWasCrit);
            if (baseWasCrit && critMult > 1f)
            {
                secondaryBase.physical /= critMult;
                secondaryBase.magical /= critMult;
                // corruption damage is not crit-scaled on cleave base rolls.
            }

            SplitDamage secondaryHit = abilityController != null
                ? abilityController.BuildCleavingSecondarySplit(secondaryBase)
                : secondaryBase;
            if (secondaryHit.IsEmpty)
                continue;

            bool secondaryCrit = false;
            if (UnityEngine.Random.value <= Mathf.Clamp01(stats.CritChance))
            {
                secondaryCrit = true;
                secondaryHit.physical *= critMult;
                secondaryHit.magical *= critMult;
            }

            ApplySecondaryHitPipeline(e, secondaryHit, secondaryCrit, forceElementalAilment: false);
            alreadyHit.Add(e);
        }
    }

    private void ApplyCrescentSlashSecondaryHits(EnemyBaseController primaryTarget, bool penetrating, bool applyElemental, HashSet<EnemyBaseController> alreadyHit)
    {
        if (stats == null)
            return;

        float reach = Mathf.Max(0.1f, stats.Range + 6f);
        float forward = player != null ? Mathf.Sign(player.transform.localScale.x >= 0f ? 1f : -1f) : 1f;
        Vector3 origin = transform.position;
        var candidates = FindObjectsByType<EnemyBaseController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var forwardHits = new List<(EnemyBaseController enemy, float dist)>(candidates.Length);

        for (int i = 0; i < candidates.Length; i++)
        {
            EnemyBaseController e = candidates[i];
            if (!IsValidSecondaryTarget(e, alreadyHit))
                continue;

            Vector3 to = e.transform.position - origin;
            float forwardDist = to.x * forward;
            if (forwardDist <= 0f || forwardDist > reach)
                continue;

            float laneWidth = Mathf.Max(0.6f, reach * 0.35f);
            if (Mathf.Abs(to.y) > laneWidth)
                continue;

            forwardHits.Add((e, forwardDist));
        }

        forwardHits.Sort((a, b) => a.dist.CompareTo(b.dist));
        // Base Crescent Slash is "up to 3 enemies" including the primary hit, so apply to up to 2 additional targets here.
        int cap = penetrating ? forwardHits.Count : Mathf.Min(2, forwardHits.Count);
        for (int i = 0; i < cap; i++)
        {
            EnemyBaseController e = forwardHits[i].enemy;
            // Roll each AoE target independently (damage range + crit).
            SplitDamage secondaryHit = stats.RollSplitAttackDamage(out bool secondaryWasCrit);
            ApplySecondaryHitPipeline(e, secondaryHit, secondaryWasCrit, forceElementalAilment: applyElemental);
            alreadyHit.Add(e);
        }
    }

    private bool IsValidSecondaryTarget(EnemyBaseController enemy, HashSet<EnemyBaseController> alreadyHit)
    {
        if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
            return false;
        if (alreadyHit != null && alreadyHit.Contains(enemy))
            return false;
        return true;
    }

    private void ApplySecondaryHitPipeline(EnemyBaseController target, SplitDamage rolled, bool wasCrit, bool forceElementalAilment)
    {
        DamageResult dealt = ApplySplitDamageToTarget(target, rolled, wasCrit);
        if (dealt.Total <= 0f)
            return;

        player.ApplyLifeSteal(dealt.Total);
        TryApplyBleed(target, dealt);
        TryApplyPoison(target, dealt);
        TryApplyElementalMagicAilment(target, dealt, forceElementalAilment);
        TryApplyMeleeShock(target, dealt);
    }

    public void ToggleIdleCombat()
    {
        SetIdleCombatEnabled(!idleCombatEnabled);
    }

    public void ToggleRetaliation()
    {
        SetRetaliationEnabled(!retaliationEnabled);
    }

    public void SetRetaliationEnabled(bool enabled)
    {
        retaliationEnabled = enabled;
        OnRetaliationChanged?.Invoke(retaliationEnabled);
    }

    public void SaveInto(SaveData data)
    {
        if (data == null) return;
        data.retaliationEnabled = retaliationEnabled;
    }

    public void LoadFrom(SaveData data)
    {
        if (data == null) return;
        SetRetaliationEnabled(data.retaliationEnabled);
    }

    public void SetIdleCombatEnabled(bool enabled)
    {
        idleCombatEnabled = enabled;
        OnIdleCombatChanged?.Invoke(idleCombatEnabled);

        if (enabled)
        {
            player?.SetMovementLocked(true);
            _nextIdleScanTime = 0f;
            _nextAutoConsumeTime = 0f;
            _nextAutoAbilityTime = 0f;
            _nextIdleAutoPickupTime = Time.time + Mathf.Max(0.5f, idleAutoPickupIntervalSeconds);
            TickAutoConsumables();
            TickAutoAbilities();
            TickIdleCombatTargeting();
        }
        else
        {
            player?.SetMovementLocked(false);
            ClearTarget();
        }
    }

    private void TickIdleAutoPickup()
    {
        if (!inventory) return;
        if (Time.time < _nextIdleAutoPickupTime) return;
        _nextIdleAutoPickupTime = Time.time + Mathf.Max(0.5f, idleAutoPickupIntervalSeconds);

        var drops = FindObjectsByType<ItemDrop>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < drops.Length; i++)
        {
            var d = drops[i];
            if (d != null)
                d.TryPickup(inventory);
        }
    }

    private void TickIdleCombatTargeting()
    {
        if (Time.time < _nextIdleScanTime) return;
        _nextIdleScanTime = Time.time + Mathf.Max(0.05f, idleRescanInterval);

        if (_target != null && !_target.IsDead && _target.gameObject.activeInHierarchy)
            return;

        var closest = FindClosestLivingEnemy();
        if (closest != null)
        {
            SetTargetInternal(closest);

            if (debugLogs)
                Debug.Log($"[Combat] Idle picked target: {closest.name}", this);
        }
        else
        {
            ClearTargetInternal();
        }
    }

    private EnemyBaseController FindClosestLivingEnemy()
    {
        var enemies = FindObjectsByType<EnemyBaseController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (enemies == null || enemies.Length == 0) return null;

        float bestDist = float.MaxValue;
        EnemyBaseController best = null;

        float myX = transform.position.x;

        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            if (!e) continue;
            if (e.IsDead) continue;
            if (!e.gameObject.activeInHierarchy) continue;

            float d = Mathf.Abs(e.transform.position.x - myX);
            if (d < bestDist)
            {
                bestDist = d;
                best = e;
            }
        }

        return best;
    }

    public bool TryRetaliateFromAttacker(Transform attackerTransform)
    {
        if (!retaliationEnabled)
            return false;
        if (attackerTransform == null)
            return false;

        // Keep the current engaged target. Never override it.
        if (_target != null && !_target.IsDead && _target.gameObject.activeInHierarchy)
            return false;

        EnemyBaseController attacker = attackerTransform.GetComponentInParent<EnemyBaseController>();
        if (attacker == null || attacker.IsDead || !attacker.gameObject.activeInHierarchy)
            return false;

        SetTargetInternal(attacker);
        return true;
    }

    public void SetTarget(EnemyBaseController enemy)
    {
        if (!enemy || enemy.IsDead)
        {
            ClearTarget();
            return;
        }

        if (_target == enemy)
            return;

        SetTargetInternal(enemy);
    }

    private void SetTargetInternal(EnemyBaseController enemy)
    {
        _target = enemy;
        _targetColCached = null;
        OnTargetChanged?.Invoke();
    }

    public void ClearTarget()
    {
        ClearTargetInternal();
    }

    private void ClearTargetInternal()
    {
        _target = null;
        _targetColCached = null;

        if (player)
        {
            player.ClearActionOverride();
            player.StopMoveOnly();
        }

        OnTargetChanged?.Invoke();
    }

    private static float HalfWidthX(Collider2D c) => c ? c.bounds.extents.x : 0f;

    private static float EdgeGapX(float ax, float bx, float aHalf, float bHalf)
    {
        return Mathf.Abs(bx - ax) - (aHalf + bHalf);
    }

    private struct DamageResult
    {
        public float physical;
        public float magical;
        public float corruptionDamage;

        public float Total => physical + magical + corruptionDamage;
    }

    private DamageResult ApplySplitDamageToTarget(EnemyBaseController target, SplitDamage rolled, bool wasCrit)
    {
        DamageResult result = default;
        if (target == null || target.IsDead) return result;
        float conditionalDamageMult = GetConditionalMeleeDamageMultiplier(target);

        if (rolled.physical > 0f)
        {
            int dealt = target.TakeDamage(
                Mathf.RoundToInt(rolled.physical * conditionalDamageMult),
                DamageType.Physical,
                wasCrit,
                player.transform
            );

            result.physical = Mathf.Max(0f, dealt);
        }

        if (rolled.magical > 0f)
        {
            int dealt = target.TakeDamage(
                Mathf.RoundToInt(rolled.magical * conditionalDamageMult),
                DamageType.Magical,
                wasCrit,
                player.transform
            );

            result.magical = Mathf.Max(0f, dealt);
        }

        if (rolled.corruptionDamage > 0f)
        {
            float potency = Mathf.Max(0f, rolled.corruptionDamage * conditionalDamageMult);
            int dealt = target.TakeDamage(
                Mathf.RoundToInt(potency),
                DamageType.Corruption,
                wasCrit,
                player.transform
            );

            result.corruptionDamage = Mathf.Max(0f, dealt);
        }

        return result;
    }

    private float GetConditionalMeleeDamageMultiplier(EnemyBaseController target)
    {
        if (stats == null || target == null)
            return 1f;

        float bonus = 0f;
        AilmentController ailments = target.GetComponent<AilmentController>();
        if (ailments != null)
        {
            if (ailments.HasBleed) bonus += stats.MeleeDamageVsBleeding;
            if (ailments.HasPoison) bonus += stats.MeleeDamageVsPoisoned;
            if (ailments.HasShock) bonus += stats.MeleeDamageVsShocked;
        }

        CharacterStats targetStats = target.GetComponent<CharacterStats>();
        if (targetStats != null && targetStats.MaxHP > 0f)
        {
            float hp01 = targetStats.HP / Mathf.Max(1f, targetStats.MaxHP);
            if (hp01 <= stats.MeleeLowHpThreshold01)
                bonus += stats.MeleeDamageVsLowHp;
        }

        return 1f + Mathf.Max(0f, bonus);
    }

    private void TryApplyBleed(EnemyBaseController target, DamageResult dealt)
    {
        if (target == null) return;
        if (dealt.physical <= 0f) return;
        if (stats.BleedChance <= 0f) return;

        if (Random.value > stats.BleedChance)
            return;

        float duration = Mathf.Max(1f, stats.BleedDuration);
        int ticks = Mathf.Max(1, Mathf.RoundToInt(duration));
        float baseDuration = Mathf.Max(1f, stats.BleedBaseDuration);

        float bleedTickDamage = dealt.physical * (1f + stats.BleedMultiplier) / baseDuration;
        if (bleedTickDamage <= 0f) return;

        float totalBleedDamage = bleedTickDamage * ticks;

        var payload = new BleedPayload(
            totalBleedDamage,
            duration,
            ticks,
            transform
        );

        var ailments = target.GetComponent<AilmentController>();
        if (ailments != null)
            ailments.ApplyBleedFromHit(payload);
    }

    private void TryApplyPoison(EnemyBaseController target, DamageResult dealt)
    {
        if (target == null) return;

        float poisonSourceDamage = dealt.corruptionDamage;

        if (poisonSourceDamage <= 0f) return;
        if (stats.PoisonChance <= 0f || stats.PoisonMultiplier < 0f) return;

        if (Random.value > stats.PoisonChance)
            return;

        float totalPoisonDamage = poisonSourceDamage * (1f + stats.PoisonMultiplier);
        if (totalPoisonDamage <= 0f) return;

        float duration = Mathf.Max(0.1f, stats.PoisonDuration);
        int ticks = Mathf.Max(1, Mathf.RoundToInt(duration));
        int maxStacks = Mathf.Max(1, stats.PoisonMaxStacks);

        var payload = new PoisonPayload(
            totalPoisonDamage,
            duration,
            ticks,
            maxStacks,
            transform
        );

        var ailments = target.GetComponent<AilmentController>();
        if (ailments != null)
            ailments.ApplyPoisonFromHit(payload);
    }

    private void TryApplyElementalMagicAilment(EnemyBaseController target, DamageResult dealt, bool forceApply = false)
    {
        if (target == null) return;
        if (stats == null) return;
        var ailments = target.GetComponent<AilmentController>();
        if (ailments == null) return;

        if (stats.CurrentAttackAppliesAsFireForBurn && dealt.Total > 0f)
        {
            ailments.TryApplyBurnFromFireHit(
                dealt.Total,
                stats.BurnApplyChance,
                stats.BurnExplosionMultiplier,
                transform);
            return;
        }

        // Non-fire elemental ailments use normal apply chance per hit.
        if (!forceApply && dealt.magical <= 0f) return;
        if (!forceApply && stats.CurrentAttackSkill != AttackSkill.Magic) return;

        float chance = stats.MagicAilmentApplyChance;
        if (!forceApply)
        {
            if (chance <= 0f) return;
            if (Random.value > chance) return;
        }

        switch (stats.CurrentMagicAttackType)
        {
            case MagicAttackType.Ice:
                ailments.ApplyChillFromHit(new ChillPayload(
                    duration: stats.ChillDuration,
                    maxStacks: stats.ChillMaxStacks,
                    slowPerStack: stats.ChillSlowPerStack,
                    source: transform
                ));
                break;

            case MagicAttackType.Lightning:
            default:
                ailments.ApplyShockFromHit(new ShockPayload(
                    duration: stats.ShockDuration,
                    damageTakenMultiplier: stats.ShockDamageTakenMultiplier,
                    source: transform
                ));
                break;
        }
    }

    private void TryApplyMeleeShock(EnemyBaseController target, DamageResult dealt)
    {
        if (target == null || stats == null)
            return;
        if (dealt.Total <= 0f)
            return;

        float chance = stats.MeleeShockChance;
        if (chance <= 0f || Random.value > chance)
            return;

        AilmentController ailments = target.GetComponent<AilmentController>();
        if (ailments == null)
            return;

        ailments.ApplyShockFromHit(new ShockPayload(
            duration: stats.ShockDuration,
            damageTakenMultiplier: stats.ShockDamageTakenMultiplier,
            source: transform
        ));
    }

    public void AwardCombatXp(float damageDealt)
    {
        if (damageDealt <= 0f)
            return;

        RecordDamageForDps(damageDealt);

        if (xpPerDamage <= 0f)
            return;

        var sm = SkillsManager.Instance;
        if (sm == null)
            return;

        SkillType skill = sm.GetCombatSkillFromCurrentWeapon(player, stats);
        sm.AddXpFloat(skill, damageDealt * xpPerDamage, combatXpSource);
    }

    private void RecordDamageForDps(float damageAmount)
    {
        if (damageAmount <= 0f)
            return;

        float now = Time.time;
        _lastDamageTime = now;
        if (_combatSessionStartTime < 0f)
            _combatSessionStartTime = now;
        _combatSessionDamageSum += damageAmount;
    }

    private void ResetDpsSession()
    {
        _combatSessionStartTime = -1f;
        _combatSessionDamageSum = 0f;
        _lastDamageTime = -999f;
    }

    private void TryResolveAutoConsumeRefs()
    {
        if (!player) player = GetComponent<PlayerController>();
        if (!stats) stats = GetComponent<CharacterStats>();
        if (!inventory) inventory = GetComponent<Inventory>();
        if (!consumableController) consumableController = GetComponent<PlayerConsumableController>();
        if (!abilityController) abilityController = GetComponent<PlayerAbilityController>();

        if (!actionBar)
            actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
    }
}