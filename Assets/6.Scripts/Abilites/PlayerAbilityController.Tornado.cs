using System.Collections.Generic;
using UnityEngine;

public partial class PlayerAbilityController
{
    private bool _tornadoActive;
    private float _tornadoEndsAt;
    private float _tornadoDuration;
    private float _lastSyncedTornadoHudEnd = float.NaN;
    private AbilityDefinition _tornadoCooldownAbilityDef;
    private TornadoCastGroup _activeTornadoGroup;

    private static bool IsTornadoAbilityId(string abilityId) =>
        string.Equals(abilityId, AbilityCombatPower.TornadoAbilityId, System.StringComparison.OrdinalIgnoreCase);

    public bool IsTornadoActive => _tornadoActive && Time.time < _tornadoEndsAt;

    public void ApplyTornadoSplitDamage(EnemyBaseController enemy, SplitDamage hit, bool applyGlobalPhysical)
    {
        if (enemy == null || enemy.IsDead || stats == null)
            return;

        SplitDamage scaled = hit;
        if (applyGlobalPhysical && scaled.physical > 0f)
        {
            float mult = 1f + stats.GlobalPhysicalDamageBonusPercentPoints / 100f;
            scaled = new SplitDamage(scaled.physical * mult, scaled.magic, scaled.corruptionDamage);
        }

        ApplySplitDamageToEnemy(
            enemy,
            scaled,
            wasCrit: false,
            outgoingDamageSourceLabel: AbilityCombatPower.TornadoOutgoingDamageSourceLabel);
    }

    private void BeginTornadoCast(AbilityDefinition def)
    {
        if (IsTornadoActive)
            ForceEndTornadoEarly(applyCooldown: false, awardDeferredCooldown: false);

        _tornadoCooldownAbilityDef = def;
        _activeTornadoGroup = new TornadoCastGroup();

        int choice = GetTornadoSelectedChoice();
        bool lightningTornado = choice == AbilityCombatPower.TornadoEnh1LightningTornadoChoiceIndex;
        bool bowInfused = choice == AbilityCombatPower.TornadoEnh2BowInfusedChoiceIndex;

        float scale = bowInfused
            ? AbilityCombatPower.TornadoEnh2Scale
            : AbilityCombatPower.TornadoBaseScale;

        float bowMinBonus = 0f;
        float bowMaxBonus = 0f;
        if (bowInfused && stats != null)
        {
            bowMinBonus = stats.MinSplitDamage.physical * AbilityCombatPower.TornadoEnh2BowDamageFraction;
            bowMaxBonus = stats.MaxSplitDamage.physical * AbilityCombatPower.TornadoEnh2BowDamageFraction;
        }

        Vector3 spawnPoint = GetTornadoSpawnPoint();
        EnemyBaseController target = FindTornadoInitialTarget(spawnPoint, null);

        if (abilityVfx == null)
            abilityVfx = GetComponent<PlayerAbilityVfxController>();

        abilityVfx?.SpawnTornadoInstance(
            this,
            _activeTornadoGroup,
            target,
            AbilityCombatPower.TornadoBaseDurationSeconds,
            scale,
            canAbsorbLightning: lightningTornado,
            useBowPhysicalBonus: bowInfused,
            bowPhysicalMinBonus: bowMinBonus,
            bowPhysicalMaxBonus: bowMaxBonus,
            spawnPoint);

        _tornadoActive = true;
        _tornadoDuration = AbilityCombatPower.TornadoBaseDurationSeconds;
        _tornadoEndsAt = Time.time + _tornadoDuration;
        _lastSyncedTornadoHudEnd = float.NaN;

        stats?.ApplyTornadoCombatModifiers(AbilityCombatPower.TornadoActiveGlobalPhysicalDamageBonus);
        SyncTornadoHudBuff();
    }

    private Vector3 GetTornadoSpawnPoint()
    {
        float anchorX = transform.position.x;
        EnemyBaseController attackTarget = combat != null ? combat.CurrentTarget : null;
        if (attackTarget != null && !attackTarget.IsDead && attackTarget.gameObject.activeInHierarchy)
            anchorX = attackTarget.transform.position.x;

        return LaneGroundEffectPlacement.SnapWorldPointToLaneFloor(new Vector3(anchorX, 0f, 0f), 0.05f);
    }

    private EnemyBaseController FindTornadoInitialTarget(
        Vector3 origin,
        System.Collections.Generic.HashSet<EnemyBaseController> exclude)
    {
        IReadOnlyList<EnemyBaseController> enemies = CombatEnemyRegistry.GetLiveEnemies();
        EnemyBaseController best = null;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyBaseController enemy = enemies[i];
            if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
                continue;
            if (exclude != null && exclude.Contains(enemy))
                continue;

            float distSq = (enemy.transform.position - origin).sqrMagnitude;
            if (distSq >= bestDistSq)
                continue;

            bestDistSq = distSq;
            best = enemy;
        }

        return best;
    }

    private void ForceEndTornadoEarly(bool applyCooldown, bool awardDeferredCooldown = true)
    {
        if (!_tornadoActive && (_activeTornadoGroup == null || _activeTornadoGroup.Instances.Count == 0))
            return;

        AbilityDefinition defForCooldown = _tornadoCooldownAbilityDef;
        _tornadoCooldownAbilityDef = null;

        _tornadoActive = false;
        _tornadoEndsAt = 0f;
        _tornadoDuration = 0f;
        _lastSyncedTornadoHudEnd = float.NaN;

        DestroyActiveTornadoInstances();
        _activeTornadoGroup = null;

        stats?.ClearTornadoCombatModifiers();
        SyncTornadoHudBuff();

        if (!awardDeferredCooldown)
            return;

        AbilityDefinition cooldownDef = applyCooldown
            ? defForCooldown ?? GetAbilityDefinition(AbilityCombatPower.TornadoAbilityId)
            : defForCooldown;

        if (cooldownDef != null && cooldownDef.cooldown > 0f)
            StartCooldown(cooldownDef);
    }

    private void DestroyActiveTornadoInstances()
    {
        if (_activeTornadoGroup == null)
            return;

        for (int i = _activeTornadoGroup.Instances.Count - 1; i >= 0; i--)
        {
            TornadoInstance instance = _activeTornadoGroup.Instances[i];
            if (instance != null)
                Destroy(instance.gameObject);
        }

        _activeTornadoGroup.Instances.Clear();
    }

    private void CleanupTornadoIfExpired()
    {
        if (!_tornadoActive)
            return;

        if ((player != null && player.IsDead) || (stats != null && stats.IsDead))
        {
            ForceEndTornadoEarly(applyCooldown: false, awardDeferredCooldown: false);
            return;
        }

        if (Time.time < _tornadoEndsAt)
            return;

        ForceEndTornadoEarly(applyCooldown: false, awardDeferredCooldown: true);
    }

    private void SyncTornadoHudBuff()
    {
        if (!buffController)
            return;

        string hudId = AbilityCombatPower.TornadoAbilityId;
        if (!IsTornadoActive)
        {
            if (buffController.IsHudAbilityBuffActive(hudId))
                buffController.ClearHudAbilityBuff(hudId);
            _lastSyncedTornadoHudEnd = float.NaN;
            return;
        }

        if (Mathf.Approximately(_lastSyncedTornadoHudEnd, _tornadoEndsAt)
            && buffController.IsHudAbilityBuffActive(hudId))
            return;

        _lastSyncedTornadoHudEnd = _tornadoEndsAt;
        buffController.SetHudAbilityBuff(hudId, 1, _tornadoEndsAt, _tornadoDuration);
    }

    private int GetTornadoSelectedChoice()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Ranged,
            AbilityCombatPower.TornadoEnhancementParentSpineNodeId,
            -1);
    }
}
