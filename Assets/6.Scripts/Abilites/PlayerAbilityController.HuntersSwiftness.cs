using System.Collections.Generic;
using UnityEngine;

public partial class PlayerAbilityController
{
    private sealed class HuntersSwiftnessTrapState
    {
        public Vector3 Position;
        public GameObject VisualRoot;
        public float ExpiresAt;
    }

    private bool _huntersSwiftnessActive;
    private float _huntersSwiftnessEndsAt;
    private float _huntersSwiftnessDuration;
    private float _huntersSwiftnessNextTrapDropAt;
    private float _lastSyncedHuntersSwiftnessHudEnd = float.NaN;
    private AbilityDefinition _huntersSwiftnessCooldownAbilityDef;
    private readonly List<HuntersSwiftnessTrapState> _huntersSwiftnessTraps = new();
    private readonly HashSet<EnemyBaseController> _huntersSwiftnessTrapVictimsThisCast = new();

    private static bool IsHuntersSwiftnessAbilityId(string abilityId) =>
        string.Equals(abilityId, AbilityCombatPower.HuntersSwiftnessAbilityId, System.StringComparison.OrdinalIgnoreCase);

    public bool IsHuntersSwiftnessActive => _huntersSwiftnessActive && Time.time < _huntersSwiftnessEndsAt;

    public bool HasActiveHuntersSwiftnessTraps => _huntersSwiftnessTraps.Count > 0;

    private void BeginHuntersSwiftnessCast(AbilityDefinition def)
    {
        if (IsHuntersSwiftnessActive)
            ForceEndHuntersSwiftnessEarly(applyCooldown: false, clearTraps: false, awardDeferredCooldown: false);

        ActivateHuntersSwiftness(def);
    }

    private void ActivateHuntersSwiftness(AbilityDefinition def)
    {
        _huntersSwiftnessCooldownAbilityDef = def;
        _huntersSwiftnessTrapVictimsThisCast.Clear();

        _huntersSwiftnessActive = true;
        _huntersSwiftnessDuration = AbilityCombatPower.HuntersSwiftnessBaseDurationSeconds;
        _huntersSwiftnessEndsAt = Time.time + _huntersSwiftnessDuration;
        _huntersSwiftnessNextTrapDropAt = Time.time;
        _huntersSwiftnessNearbyEnemyActive = false;
        _huntersSwiftnessNextNearbyCheckAt = 0f;
        _huntersSwiftnessAppliedMoveSpeed = -1f;
        _huntersSwiftnessAppliedEvade = -1f;
        _lastSyncedHuntersSwiftnessHudEnd = float.NaN;

        RefreshHuntersSwiftnessCombatModifiers(forceNearbyCheck: true);
        SyncHuntersSwiftnessHudBuff();

        if (HasHuntersSwiftnessTrapsEnhancement())
            DropHuntersSwiftnessTrapAtPlayer();
    }

    private void ForceEndHuntersSwiftnessEarly(
        bool applyCooldown,
        bool clearTraps,
        bool awardDeferredCooldown = true)
    {
        if (!_huntersSwiftnessActive && _huntersSwiftnessTraps.Count == 0)
            return;

        AbilityDefinition defForCooldown = _huntersSwiftnessCooldownAbilityDef;
        _huntersSwiftnessCooldownAbilityDef = null;

        _huntersSwiftnessActive = false;
        _huntersSwiftnessEndsAt = 0f;
        _huntersSwiftnessDuration = 0f;
        _huntersSwiftnessNextTrapDropAt = 0f;
        _huntersSwiftnessNearbyEnemyActive = false;
        _huntersSwiftnessNextNearbyCheckAt = 0f;
        _huntersSwiftnessAppliedMoveSpeed = -1f;
        _huntersSwiftnessAppliedEvade = -1f;
        _lastSyncedHuntersSwiftnessHudEnd = float.NaN;

        stats?.ClearHuntersSwiftnessCombatModifiers();
        SyncHuntersSwiftnessHudBuff();

        if (clearTraps)
            ClearAllHuntersSwiftnessTraps();

        if (!awardDeferredCooldown)
            return;

        AbilityDefinition cooldownDef = applyCooldown
            ? defForCooldown ?? GetAbilityDefinition(AbilityCombatPower.HuntersSwiftnessAbilityId)
            : defForCooldown;

        if (cooldownDef != null && cooldownDef.cooldown > 0f)
            StartCooldown(cooldownDef);
    }

    private void CleanupHuntersSwiftnessIfExpired()
    {
        if (!_huntersSwiftnessActive)
            return;

        if ((player != null && player.IsDead) || (stats != null && stats.IsDead))
        {
            ForceEndHuntersSwiftnessEarly(applyCooldown: false, clearTraps: true, awardDeferredCooldown: false);
            return;
        }

        if (Time.time < _huntersSwiftnessEndsAt)
            return;

        ForceEndHuntersSwiftnessEarly(applyCooldown: false, clearTraps: false, awardDeferredCooldown: true);
    }

    private float _huntersSwiftnessNextNearbyCheckAt;
    private bool _huntersSwiftnessNearbyEnemyActive;
    private float _huntersSwiftnessAppliedMoveSpeed = -1f;
    private float _huntersSwiftnessAppliedEvade = -1f;
    private const float HuntersSwiftnessNearbyRecheckIntervalSeconds = 0.25f;

    private void TickHuntersSwiftness()
    {
        if (_huntersSwiftnessActive)
        {
            RefreshHuntersSwiftnessCombatModifiersIfNeeded();

            if (HasHuntersSwiftnessTrapsEnhancement() && Time.time >= _huntersSwiftnessNextTrapDropAt)
            {
                _huntersSwiftnessNextTrapDropAt = Time.time + AbilityCombatPower.HuntersSwiftnessTrapDropIntervalSeconds;
                DropHuntersSwiftnessTrapAtPlayer();
            }
        }

        TickHuntersSwiftnessTraps();
    }

    private void RefreshHuntersSwiftnessCombatModifiersIfNeeded()
    {
        if (!_huntersSwiftnessActive || stats == null)
            return;

        if (Time.time < _huntersSwiftnessNextNearbyCheckAt)
            return;

        _huntersSwiftnessNextNearbyCheckAt = Time.time + HuntersSwiftnessNearbyRecheckIntervalSeconds;
        RefreshHuntersSwiftnessCombatModifiers(forceNearbyCheck: true);
    }

    private void RefreshHuntersSwiftnessCombatModifiers(bool forceNearbyCheck = false)
    {
        if (!_huntersSwiftnessActive || stats == null)
            return;

        bool nimbleHunter = GetHuntersSwiftnessSelectedChoice()
            == AbilityCombatPower.HuntersSwiftnessEnh2NimbleHunterChoiceIndex;

        if (forceNearbyCheck)
        {
            bool nearbyEnemy = IsAnyEnemyWithinHuntersSwiftnessNearbyRange(nimbleHunter);
            _huntersSwiftnessNearbyEnemyActive = nearbyEnemy;
        }

        float moveSpeed = _huntersSwiftnessNearbyEnemyActive
            ? AbilityCombatPower.HuntersSwiftnessNearbyEnemyMoveSpeedBonus
            : AbilityCombatPower.HuntersSwiftnessBaseMoveSpeedBonus;

        if (nimbleHunter)
            moveSpeed += AbilityCombatPower.HuntersSwiftnessEnh2MoveSpeedBonus;

        float evade = AbilityCombatPower.HuntersSwiftnessBaseEvadeChance;
        if (nimbleHunter)
            evade += AbilityCombatPower.HuntersSwiftnessEnh2EvadeChanceBonus;

        if (Mathf.Approximately(moveSpeed, _huntersSwiftnessAppliedMoveSpeed)
            && Mathf.Approximately(evade, _huntersSwiftnessAppliedEvade))
            return;

        _huntersSwiftnessAppliedMoveSpeed = moveSpeed;
        _huntersSwiftnessAppliedEvade = evade;
        stats.ApplyHuntersSwiftnessCombatModifiers(moveSpeed, evade);
    }

    private bool IsAnyEnemyWithinHuntersSwiftnessNearbyRange(bool nimbleHunter)
    {
        Vector3 origin = transform.position;
        IReadOnlyList<EnemyBaseController> enemies = CombatEnemyRegistry.GetLiveEnemies();

        if (nimbleHunter)
        {
            float sideRange = AbilityCombatPower.HuntersSwiftnessNimbleHunterNearbySideRange;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyBaseController enemy = enemies[i];
                if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
                    continue;

                if (Mathf.Abs(enemy.transform.position.x - origin.x) <= sideRange)
                    return true;
            }

            return false;
        }

        float range = AbilityCombatPower.HuntersSwiftnessNearbyEnemyRange;
        float rangeSq = range * range;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyBaseController enemy = enemies[i];
            if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
                continue;

            if ((enemy.transform.position - origin).sqrMagnitude <= rangeSq)
                return true;
        }

        return false;
    }

    private void DropHuntersSwiftnessTrapAtPlayer()
    {
        Vector3 floorPoint = LaneGroundEffectPlacement.SnapWorldPointToLaneFloor(transform.position, 0.05f);
        var trap = new HuntersSwiftnessTrapState
        {
            Position = floorPoint,
            VisualRoot = abilityVfx != null ? abilityVfx.SpawnHunterTrapFloorMark(floorPoint) : null,
            ExpiresAt = Time.time + AbilityCombatPower.HuntersSwiftnessTrapLifetimeSeconds
        };
        _huntersSwiftnessTraps.Add(trap);
    }

    private void TickHuntersSwiftnessTraps()
    {
        if (_huntersSwiftnessTraps.Count == 0)
            return;

        float triggerRadius = AbilityCombatPower.HuntersSwiftnessTrapDiameterWorld * 0.5f;
        float triggerRadiusSq = triggerRadius * triggerRadius;
        IReadOnlyList<EnemyBaseController> enemies = CombatEnemyRegistry.GetLiveEnemies();

        for (int trapIndex = _huntersSwiftnessTraps.Count - 1; trapIndex >= 0; trapIndex--)
        {
            HuntersSwiftnessTrapState trap = _huntersSwiftnessTraps[trapIndex];
            if (Time.time >= trap.ExpiresAt)
            {
                RemoveHuntersSwiftnessTrapAt(trapIndex);
                continue;
            }

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyBaseController enemy = enemies[i];
                if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
                    continue;
                if (_huntersSwiftnessTrapVictimsThisCast.Contains(enemy))
                    continue;

                float distSq = (enemy.transform.position - trap.Position).sqrMagnitude;
                if (distSq > triggerRadiusSq)
                    continue;

                _huntersSwiftnessTrapVictimsThisCast.Add(enemy);
                TriggerHuntersSwiftnessTrapOnEnemy(enemy);
                RemoveHuntersSwiftnessTrapAt(trapIndex);
                break;
            }
        }
    }

    private void TriggerHuntersSwiftnessTrapOnEnemy(EnemyBaseController enemy)
    {
        if (enemy == null || enemy.IsDead || stats == null)
            return;

        enemy.TryApplyStun(
            AbilityCombatPower.HuntersSwiftnessTrapStunDurationSeconds,
            1f,
            transform);

        SplitDamage rolled = stats.RollSplitAttackDamage(out bool wasCrit);
        rolled = rolled * AbilityCombatPower.HuntersSwiftnessTrapWeaponDamageFraction;

        DealtHit dealt = ApplySplitDamageToEnemy(
            enemy,
            rolled,
            wasCrit,
            outgoingDamageSourceLabel: AbilityCombatPower.HuntersSwiftnessTrapOutgoingDamageSourceLabel);

        if (player != null && dealt.Total > 0f)
            player.ApplyLifeSteal(dealt.Total);
    }

    private void RemoveHuntersSwiftnessTrapAt(int trapIndex)
    {
        if (trapIndex < 0 || trapIndex >= _huntersSwiftnessTraps.Count)
            return;

        HuntersSwiftnessTrapState trap = _huntersSwiftnessTraps[trapIndex];
        if (trap.VisualRoot != null)
            abilityVfx?.DestroyHunterTrapFloorMark(trap.VisualRoot);

        _huntersSwiftnessTraps.RemoveAt(trapIndex);
    }

    private void ClearAllHuntersSwiftnessTraps()
    {
        for (int i = _huntersSwiftnessTraps.Count - 1; i >= 0; i--)
            RemoveHuntersSwiftnessTrapAt(i);

        _huntersSwiftnessTrapVictimsThisCast.Clear();
    }

    private void SyncHuntersSwiftnessHudBuff()
    {
        if (!buffController)
            return;

        string hudId = AbilityCombatPower.HuntersSwiftnessAbilityId;
        if (!IsHuntersSwiftnessActive)
        {
            if (buffController.IsHudAbilityBuffActive(hudId))
                buffController.ClearHudAbilityBuff(hudId);
            _lastSyncedHuntersSwiftnessHudEnd = float.NaN;
            return;
        }

        if (Mathf.Approximately(_lastSyncedHuntersSwiftnessHudEnd, _huntersSwiftnessEndsAt)
            && buffController.IsHudAbilityBuffActive(hudId))
            return;

        _lastSyncedHuntersSwiftnessHudEnd = _huntersSwiftnessEndsAt;
        buffController.SetHudAbilityBuff(hudId, 1, _huntersSwiftnessEndsAt, _huntersSwiftnessDuration);
    }

    private bool HasHuntersSwiftnessTrapsEnhancement() =>
        GetHuntersSwiftnessSelectedChoice() == AbilityCombatPower.HuntersSwiftnessEnh1HunterTrapsChoiceIndex;

    private int GetHuntersSwiftnessSelectedChoice()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Ranged,
            AbilityCombatPower.HuntersSwiftnessEnhancementParentSpineNodeId,
            -1);
    }
}
