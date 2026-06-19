using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class PlayerAbilityController
{
    private Coroutine _penetratingShotRoutine;

    private static bool IsPenetratingShotAbilityId(string abilityId) =>
        string.Equals(abilityId, AbilityCombatPower.PenetratingShotAbilityId, System.StringComparison.OrdinalIgnoreCase);

    public bool IsPenetratingShotInFlight => _penetratingShotRoutine != null;

    private void BeginPenetratingShotCast(AbilityDefinition def)
    {
        if (def == null || player == null)
            return;

        if (_penetratingShotRoutine != null)
            StopCoroutine(_penetratingShotRoutine);

        _penetratingShotRoutine = StartCoroutine(CoPenetratingShot(def));
    }

    private void AbortPenetratingShotInFlight()
    {
        if (_penetratingShotRoutine != null)
        {
            StopCoroutine(_penetratingShotRoutine);
            _penetratingShotRoutine = null;
        }

        abilityVfx?.DestroyPenetratingShotVisual();
    }

    private float GetPenetratingShotReach()
    {
        if (stats == null)
            return 0.5f;

        return Mathf.Max(0.5f, stats.Range);
    }

    private int GetPenetratingShotSelectedChoice()
    {
        if (!skillsManager)
            skillsManager = SkillsManager.Instance;
        if (!skillsManager)
            return -1;

        return skillsManager.GetSkillChoiceSelection(
            SkillType.Ranged,
            AbilityCombatPower.PenetratingShotEnhancementParentSpineNodeId,
            -1);
    }

    private bool CanHitAnyEnemyWithPenetratingShot()
    {
        float reach = GetPenetratingShotReach();
        List<(EnemyBaseController enemy, float dist)> forwardHits = CollectPenetratingShotForwardHits(reach);
        return forwardHits.Count > 0;
    }

    private bool TryFindClosestEnemyInPenetratingShotLane(out EnemyBaseController target)
    {
        target = PickPreferredForwardArcEnemy(
            CollectPenetratingShotForwardHits(GetPenetratingShotReach()));
        return target != null;
    }

    private List<(EnemyBaseController enemy, float dist)> CollectPenetratingShotForwardHits(float reach)
    {
        IReadOnlyList<EnemyBaseController> allEnemies = CombatEnemyRegistry.GetLiveEnemies();
        List<(EnemyBaseController enemy, float dist)> forwardHits =
            new List<(EnemyBaseController enemy, float dist)>(allEnemies.Count);
        float facing = GetCombatFacingSign();
        Vector3 origin = transform.position;
        float laneWidth = Mathf.Max(0.6f, reach * 0.35f);

        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyBaseController enemy = allEnemies[i];
            if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
                continue;

            Vector3 to = enemy.transform.position - origin;
            float forwardDist = to.x * facing;
            if (forwardDist <= 0f || forwardDist > reach)
                continue;
            if (Mathf.Abs(to.y) > laneWidth)
                continue;

            forwardHits.Add((enemy, forwardDist));
        }

        return forwardHits;
    }

    private IEnumerator CoPenetratingShot(AbilityDefinition def)
    {
        if (def == null || stats == null || player == null)
        {
            _penetratingShotRoutine = null;
            yield break;
        }

        float reach = GetPenetratingShotReach();
        float facing = GetCombatFacingSign();
        Vector3 origin = transform.position;
        if (combat == null)
            combat = GetComponent<PlayerCombatController>();
        Vector3 spawnPos = combat != null
            ? combat.GetRangedProjectileSpawnPosition()
            : origin;
        int choice = GetPenetratingShotSelectedChoice();
        bool allInPath = choice == AbilityCombatPower.PenetratingShotEnh1AllInPathChoiceIndex;
        bool returningShot = choice == AbilityCombatPower.PenetratingShotEnh2ReturningShotChoiceIndex;
        int hitCap = allInPath ? int.MaxValue : AbilityCombatPower.PenetratingShotBaseMaxHits;

        List<(EnemyBaseController enemy, float dist)> forwardHits = CollectPenetratingShotForwardHits(reach);
        forwardHits.Sort((a, b) => a.dist.CompareTo(b.dist));

        float outboundEnd = reach;
        if (!allInPath && forwardHits.Count > 0)
        {
            int cappedEnemyIndex = Mathf.Min(hitCap, forwardHits.Count) - 1;
            if (cappedEnemyIndex >= 0)
                outboundEnd = Mathf.Min(reach, forwardHits[cappedEnemyIndex].dist);
        }

        GameObject vfx = abilityVfx != null
            ? abilityVfx.SpawnPenetratingShotVisual(spawnPos, facing)
            : null;

        float travelDist = 0f;
        float speed = AbilityCombatPower.PenetratingShotTravelSpeed;
        float returnSpeed = speed * AbilityCombatPower.PenetratingShotReturnTravelSpeedMultiplier;
        int nextEnemyIndex = 0;
        int outboundHitCount = 0;
        int pierceIndex = 0;
        var outboundHit = new HashSet<EnemyBaseController>();

        while (travelDist < outboundEnd)
        {
            travelDist += speed * Time.deltaTime;
            travelDist = Mathf.Min(travelDist, outboundEnd);
            Vector3 pos = spawnPos + Vector3.right * (facing * travelDist);
            abilityVfx?.UpdatePenetratingShotVisual(vfx, pos, facing);

            while (nextEnemyIndex < forwardHits.Count
                   && forwardHits[nextEnemyIndex].dist <= travelDist + 0.05f)
            {
                if (outboundHitCount < hitCap)
                {
                    EnemyBaseController enemy = forwardHits[nextEnemyIndex].enemy;
                    if (enemy != null && !enemy.IsDead && outboundHit.Add(enemy))
                    {
                        ApplyPenetratingShotHit(
                            enemy,
                            def,
                            GetPenetratingShotPierceDamageMultiplier(pierceIndex, allInPath),
                            AbilityCombatPower.PenetratingShotOutgoingDamageSourceLabel);
                        outboundHitCount++;
                        pierceIndex++;
                    }
                }

                nextEnemyIndex++;
            }

            yield return null;
        }

        if (returningShot)
        {
            var returnHit = new HashSet<EnemyBaseController>();
            int returnEnemyIndex = forwardHits.Count - 1;
            while (returnEnemyIndex >= 0 && forwardHits[returnEnemyIndex].dist > outboundEnd + 0.05f)
                returnEnemyIndex--;

            while (travelDist > 0f)
            {
                travelDist -= returnSpeed * Time.deltaTime;
                travelDist = Mathf.Max(0f, travelDist);
                Vector3 pos = spawnPos + Vector3.right * (facing * travelDist);
                abilityVfx?.UpdatePenetratingShotVisual(vfx, pos, -facing);

                while (returnEnemyIndex >= 0
                       && forwardHits[returnEnemyIndex].dist > outboundEnd + 0.05f)
                    returnEnemyIndex--;

                while (returnEnemyIndex >= 0
                       && forwardHits[returnEnemyIndex].dist >= travelDist - 0.05f)
                {
                    EnemyBaseController enemy = forwardHits[returnEnemyIndex].enemy;
                    if (enemy != null && !enemy.IsDead && returnHit.Add(enemy))
                    {
                        ApplyPenetratingShotHit(
                            enemy,
                            def,
                            AbilityCombatPower.PenetratingShotReturnDamageFraction,
                            AbilityCombatPower.PenetratingShotReturnOutgoingDamageSourceLabel);
                    }

                    returnEnemyIndex--;
                }

                yield return null;
            }
        }

        abilityVfx?.DestroyPenetratingShotVisual(vfx);
        _penetratingShotRoutine = null;
    }

    private void ApplyPenetratingShotHit(
        EnemyBaseController target,
        AbilityDefinition def,
        float damageMultiplier,
        string sourceLabelOverride)
    {
        if (target == null || target.IsDead || stats == null || def == null)
            return;

        BuildWhirlwindAbilityScaledSplit(def, target, out SplitDamage rolledNonCrit, out bool wasCrit, out float lightningMagNonCrit);
        float critMult = wasCrit ? Mathf.Max(1f, stats.CritMultiplier) : 1f;
        SplitDamage hitForTarget = new SplitDamage(
            rolledNonCrit.physical * critMult * damageMultiplier,
            rolledNonCrit.magic * critMult * damageMultiplier,
            rolledNonCrit.corruptionDamage * critMult * damageMultiplier);
        float lightningFrac = hitForTarget.magic > 1e-8f
            ? Mathf.Clamp01(lightningMagNonCrit * critMult * damageMultiplier / hitForTarget.magic)
            : 0f;

        DealtHit dealt = ApplyAbilitySplitDamageToEnemy(
            target,
            def,
            hitForTarget,
            wasCrit,
            lightningFrac,
            outgoingDamageSourceLabelOverride: sourceLabelOverride);
        ApplyOnHitEffects(target, dealt);

        if (player != null && dealt.Total > 0f)
            player.ApplyLifeSteal(dealt.Total);
    }

    private static float GetPenetratingShotPierceDamageMultiplier(int enemiesAlreadyHit, bool piercingPath)
    {
        if (!piercingPath || enemiesAlreadyHit <= 0)
            return 1f;

        float reduction = Mathf.Min(
            AbilityCombatPower.PenetratingShotPierceMaxDamageReduction,
            enemiesAlreadyHit * AbilityCombatPower.PenetratingShotPierceDamageReductionPerEnemy);
        return 1f - reduction;
    }
}
