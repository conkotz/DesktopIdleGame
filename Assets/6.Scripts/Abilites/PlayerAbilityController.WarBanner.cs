using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class PlayerAbilityController
{
    private bool _warBannerActive;
    private bool _warBannerDeployed;
    private float _warBannerEndsAt;
    private float _warBannerDuration;
    private Vector3 _warBannerAnchor;
    private int _warBannerStacks;
    private int _warBannerMaxStacks;
    private int _warBannerEnh3KillProcs;
    private bool _warBannerEnh2Triggered;
    private bool _warBannerEnh2MoveSpeedActive;
    private float _warBannerNextStackTickAt;
    private float _warBannerEnh2NextHealTickAt;
    private float _warBannerNextAllyRefreshAt;
    private float _lastSyncedWarBannerHudEnd = float.NaN;
    private int _lastSyncedWarBannerHudStacks = int.MinValue;
    private Coroutine _warBannerCastRoutine;
    /// <summary>When the War Banner buff expires, this ability gets <see cref="StartCooldown"/> (not on cast).</summary>
    private AbilityDefinition _warBannerCooldownAbilityDef;
    private readonly HashSet<CharacterStats> _warBannerBuffedAlliesScratch = new();
    private readonly HashSet<CharacterStats> _warBannerCurrentlyBuffed = new();
    private readonly List<CharacterStats> _warBannerRemoveScratch = new();

    private bool IsWarBannerOnActionBar()
    {
        if (!actionBar)
            actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        if (!actionBar)
            return false;

        return actionBar.HasAbilityOnLoadout(AbilityCombatPower.WarBannerAbilityId);
    }

    private static bool IsWarBannerAbilityId(string abilityId) =>
        string.Equals(abilityId, AbilityCombatPower.WarBannerAbilityId, StringComparison.OrdinalIgnoreCase);

    public bool IsWarBannerActive =>
        _warBannerActive && Time.time < _warBannerEndsAt;

    public static void NotifyWarBannerKillFromEnemyDeath()
    {
        if (_instance == null || !_instance.IsWarBannerActive || !_instance._warBannerDeployed)
            return;

        _instance.NotifyWarBannerEnemyKilled();
    }

    private void NotifyWarBannerEnemyKilled()
    {
        if (!IsWarBannerActive || GetWarBannerSelectedChoice() != 2)
            return;

        if (_warBannerEnh3KillProcs >= AbilityCombatPower.WarBannerEnh3MaxKillProcs)
            return;

        _warBannerEnh3KillProcs++;
        _warBannerMaxStacks += AbilityCombatPower.WarBannerEnh3KillMaxStackBonus;
        _warBannerEndsAt += AbilityCombatPower.WarBannerEnh3KillDurationExtensionSeconds;
        _lastSyncedWarBannerHudEnd = float.NaN;
        SyncWarBannerHudBuff();
        RefreshWarBannerAllies();
    }

    private void BeginWarBannerCast(AbilityDefinition def)
    {
        _warBannerCooldownAbilityDef = def;

        if (_warBannerCastRoutine != null)
        {
            StopCoroutine(_warBannerCastRoutine);
            _warBannerCastRoutine = null;
        }

        if (IsWarBannerActive)
            ForceEndWarBannerEarly(applyCooldown: false, awardDeferredCooldown: false);

        _warBannerCastRoutine = StartCoroutine(CoDeployWarBanner(def));
    }

    private IEnumerator CoDeployWarBanner(AbilityDefinition def)
    {
        float anchorX = transform.position.x;
        float anchorY = LaneGroundEffectPlacement.GetLaneFloorTopWorldY();
        float descentSeconds = AbilityCombatPower.WarBannerDescentSeconds;
        abilityVfx?.BeginWarBannerDescent(new Vector3(anchorX, anchorY, 0f), descentSeconds);

        float elapsed = 0f;
        while (elapsed < descentSeconds)
        {
            elapsed += Time.deltaTime;
            abilityVfx?.UpdateWarBannerDescent(new Vector3(anchorX, anchorY, 0f), elapsed);
            yield return null;
        }

        Vector3 impactPoint = new Vector3(anchorX, anchorY, 0f);
        abilityVfx?.UpdateWarBannerDescent(impactPoint, descentSeconds);
        abilityVfx?.AnchorWarBannerAt(impactPoint);
        ActivateWarBannerAt(impactPoint, def);
        abilityVfx?.MaintainWarBannerAt(impactPoint);
        _warBannerCastRoutine = null;
    }

    private void ActivateWarBannerAt(Vector3 anchor, AbilityDefinition def)
    {
        _warBannerActive = true;
        _warBannerDeployed = true;
        _warBannerAnchor = anchor;
        _warBannerDuration = AbilityCombatPower.WarBannerBaseDurationSeconds;
        if (def != null && def.tooltipBuffMinionDurationSeconds > 0.01f)
            _warBannerDuration = def.tooltipBuffMinionDurationSeconds;

        _warBannerEndsAt = Time.time + _warBannerDuration;
        _warBannerStacks = 0;
        _warBannerMaxStacks = AbilityCombatPower.WarBannerBaseMaxStacks;
        _warBannerEnh3KillProcs = 0;
        _warBannerEnh2Triggered = false;
        _warBannerEnh2MoveSpeedActive = false;
        _warBannerNextStackTickAt = Time.time + AbilityCombatPower.WarBannerStackIntervalSeconds;
        _warBannerEnh2NextHealTickAt = 0f;
        _warBannerNextAllyRefreshAt = Time.time;
        _lastSyncedWarBannerHudEnd = float.NaN;
        _lastSyncedWarBannerHudStacks = int.MinValue;

        if (GetWarBannerSelectedChoice() == 0)
            ApplyWarBannerEnh1InstantCooldownReduction();

        RefreshWarBannerAllies();
        SyncWarBannerHudBuff();
    }

    private void ForceEndWarBannerEarly(bool applyCooldown, bool awardDeferredCooldown = true)
    {
        bool descentInProgress = _warBannerCastRoutine != null;
        if (_warBannerCastRoutine != null)
        {
            StopCoroutine(_warBannerCastRoutine);
            _warBannerCastRoutine = null;
        }

        if (!_warBannerActive && !IsWarBannerActive)
        {
            if (descentInProgress)
                abilityVfx?.StopWarBannerVfx();
            return;
        }

        AbilityDefinition defForCooldown = _warBannerCooldownAbilityDef;
        _warBannerCooldownAbilityDef = null;

        _warBannerActive = false;
        _warBannerDeployed = false;
        _warBannerEndsAt = 0f;
        _warBannerDuration = 0f;
        _warBannerStacks = 0;
        _warBannerMaxStacks = AbilityCombatPower.WarBannerBaseMaxStacks;
        _warBannerEnh3KillProcs = 0;
        _warBannerEnh2Triggered = false;
        _warBannerEnh2MoveSpeedActive = false;
        _warBannerEnh2NextHealTickAt = 0f;

        ClearWarBannerAllies();
        abilityVfx?.StopWarBannerVfx();
        _lastSyncedWarBannerHudEnd = float.NaN;
        _lastSyncedWarBannerHudStacks = int.MinValue;
        SyncWarBannerHudBuff();

        if (!awardDeferredCooldown)
            return;

        AbilityDefinition cooldownDef = applyCooldown
            ? defForCooldown ?? GetAbilityDefinition(AbilityCombatPower.WarBannerAbilityId)
            : defForCooldown;

        if (cooldownDef != null && cooldownDef.cooldown > 0f)
            StartCooldown(cooldownDef);
    }

    private void CleanupWarBannerIfExpired()
    {
        if (!_warBannerActive)
            return;

        if ((player != null && player.IsDead) || (stats != null && stats.IsDead))
        {
            _warBannerCooldownAbilityDef = null;
            ForceEndWarBannerEarly(applyCooldown: false, awardDeferredCooldown: false);
            return;
        }

        if (Time.time < _warBannerEndsAt)
            return;

        ForceEndWarBannerEarly(applyCooldown: false, awardDeferredCooldown: true);
    }

    private void TickWarBanner()
    {
        if (!_warBannerActive || !_warBannerDeployed)
            return;

        abilityVfx?.MaintainWarBannerAt(_warBannerAnchor);

        if (Time.time >= _warBannerNextStackTickAt)
        {
            _warBannerNextStackTickAt = Time.time + AbilityCombatPower.WarBannerStackIntervalSeconds;
            if (_warBannerStacks < _warBannerMaxStacks)
            {
                _warBannerStacks++;
                if (!_warBannerEnh2Triggered
                    && _warBannerStacks >= AbilityCombatPower.WarBannerBaseMaxStacks)
                    ActivateWarBannerEnh2AfterTenStacks();
                _lastSyncedWarBannerHudEnd = float.NaN;
                _lastSyncedWarBannerHudStacks = int.MinValue;
                SyncWarBannerHudBuff();
                RefreshWarBannerAllies();
            }
        }

        if (_warBannerEnh2Triggered && Time.time >= _warBannerEnh2NextHealTickAt)
        {
            _warBannerEnh2NextHealTickAt = Time.time + AbilityCombatPower.WarBannerStackIntervalSeconds;
            TickWarBannerEnh2Heal();
        }

        if (Time.time >= _warBannerNextAllyRefreshAt)
        {
            _warBannerNextAllyRefreshAt = Time.time + AbilityCombatPower.WarBannerStackIntervalSeconds;
            RefreshWarBannerAllies();
        }
    }

    private void ActivateWarBannerEnh2AfterTenStacks()
    {
        if (GetWarBannerSelectedChoice() != 1)
            return;

        _warBannerEnh2Triggered = true;
        _warBannerEnh2MoveSpeedActive = true;
        _warBannerEnh2NextHealTickAt = Time.time + AbilityCombatPower.WarBannerStackIntervalSeconds;
        RefreshWarBannerAllies();
    }

    private void TickWarBannerEnh2Heal()
    {
        if (GetWarBannerSelectedChoice() != 1)
            return;

        _warBannerRemoveScratch.Clear();
        CollectWarBannerAllies(_warBannerRemoveScratch);
        float healFraction = AbilityCombatPower.WarBannerEnh2MaxStacksHealPerSecondFraction;
        for (int i = 0; i < _warBannerRemoveScratch.Count; i++)
        {
            CharacterStats allyStats = _warBannerRemoveScratch[i];
            if (!allyStats)
                continue;

            int healAmount = Mathf.Max(1, Mathf.RoundToInt(allyStats.MaxHP * healFraction));
            allyStats.Heal(healAmount, PlayerCombatController.WarBannerTriumphantRallyHealingSourceLabel);
        }
    }

    private void RefreshWarBannerAllies()
    {
        if (!_warBannerDeployed)
            return;

        _warBannerRemoveScratch.Clear();
        CollectWarBannerAllies(_warBannerRemoveScratch);

        _warBannerBuffedAlliesScratch.Clear();
        foreach (CharacterStats previouslyBuffed in _warBannerCurrentlyBuffed)
        {
            if (previouslyBuffed && !_warBannerRemoveScratch.Contains(previouslyBuffed))
                previouslyBuffed.ClearWarBannerCombatModifiers();
        }

        _warBannerCurrentlyBuffed.Clear();
        for (int i = 0; i < _warBannerRemoveScratch.Count; i++)
        {
            CharacterStats allyStats = _warBannerRemoveScratch[i];
            if (!allyStats)
                continue;

            _warBannerCurrentlyBuffed.Add(allyStats);
            ApplyWarBannerModifiersToStats(allyStats);
        }
    }

    private void CollectWarBannerAllies(List<CharacterStats> dest)
    {
        dest.Clear();
        if (stats != null && !stats.IsDead)
        {
            if (!dest.Contains(stats))
                dest.Add(stats);
        }

        IReadOnlyList<MinionCombatTarget> allies = MinionCombatTarget.ActiveTargets;
        for (int i = 0; i < allies.Count; i++)
        {
            MinionCombatTarget ally = allies[i];
            if (!ally || !ally.IsAlive || ally.Stats == null)
                continue;

            if (!dest.Contains(ally.Stats))
                dest.Add(ally.Stats);
        }
    }

    private void ClearWarBannerAllies()
    {
        foreach (CharacterStats buffed in _warBannerCurrentlyBuffed)
            buffed?.ClearWarBannerCombatModifiers();
        _warBannerCurrentlyBuffed.Clear();
        stats?.ClearWarBannerCombatModifiers();
    }

    private void ApplyWarBannerModifiersToStats(CharacterStats targetStats)
    {
        if (!targetStats)
            return;

        float stackBonus = _warBannerStacks * AbilityCombatPower.WarBannerStackBonusPerStat;
        float attackSpeed = AbilityCombatPower.WarBannerBaseAttackSpeedBonus + stackBonus;
        float damageReduction = AbilityCombatPower.WarBannerBaseDamageReductionFraction + stackBonus;
        float globalPhysical = AbilityCombatPower.WarBannerBaseGlobalPhysicalDamageBonus + stackBonus;
        float cdr = GetWarBannerSelectedChoice() == 0 ? AbilityCombatPower.WarBannerEnh1CooldownReduction : 0f;
        float moveSpeed = _warBannerEnh2MoveSpeedActive ? AbilityCombatPower.WarBannerEnh2MoveSpeedBonus : 0f;

        targetStats.ApplyWarBannerCombatModifiers(
            attackSpeed,
            damageReduction,
            globalPhysical,
            cdr,
            moveSpeed);
    }

    private void ApplyWarBannerEnh1InstantCooldownReduction()
    {
        float fraction = AbilityCombatPower.WarBannerEnh1CooldownReduction;
        if (fraction <= 0f)
            return;

        var abilityIds = new List<string>(_cooldownEndsById.Keys);
        for (int i = 0; i < abilityIds.Count; i++)
        {
            string abilityId = abilityIds[i];
            if (IsWarBannerAbilityId(abilityId))
                continue;

            if (!_cooldownEndsById.TryGetValue(abilityId, out float end))
                continue;

            if (end <= Time.time)
                continue;

            AbilityDefinition abilityDef = GetAbilityDefinition(abilityId);
            if (!abilityDef || abilityDef.cooldown <= 0f)
                continue;

            ReduceAbilityCooldownBySeconds(abilityDef, abilityDef.cooldown * fraction);
        }
    }

    private void SyncWarBannerHudBuff()
    {
        if (!buffController)
            return;

        string hudId = ResolveWarBannerHudAbilityId();
        if (!IsWarBannerActive || !_warBannerDeployed)
        {
            if (buffController.IsHudAbilityBuffActive(hudId))
                buffController.ClearHudAbilityBuff(hudId);
            _lastSyncedWarBannerHudEnd = float.NaN;
            _lastSyncedWarBannerHudStacks = int.MinValue;
            return;
        }

        if (Mathf.Approximately(_lastSyncedWarBannerHudEnd, _warBannerEndsAt)
            && _lastSyncedWarBannerHudStacks == _warBannerStacks
            && buffController.IsHudAbilityBuffActive(hudId))
            return;

        _lastSyncedWarBannerHudEnd = _warBannerEndsAt;
        _lastSyncedWarBannerHudStacks = _warBannerStacks;
        buffController.SetHudAbilityBuff(hudId, _warBannerStacks, _warBannerEndsAt, _warBannerDuration);
    }

    private static string ResolveWarBannerHudAbilityId()
    {
        return AbilityCombatPower.WarBannerAbilityId;
    }

    private int GetWarBannerSelectedChoice()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Melee,
            AbilityCombatPower.WarBannerEnhancementParentSpineNodeId,
            -1);
    }
}
