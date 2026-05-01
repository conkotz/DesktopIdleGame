using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Estimates suggested player combat power from enemy spawn data (endurance waves or generic spawn group plans).
/// Uses the same per-enemy CP as <see cref="EnduranceTrialUIHelpers.GetEnemyCombatPowerRounded"/>.
/// <para>
/// <b>Formula:</b> weighted <b>average</b> CP over enemy instances (each row’s <c>count</c> expands instances), then wave or enemy stress.
/// Never recommend below the <b>strongest</b> enemy in the pull: <c>base = max(averageCp × stress, maxInstanceCp)</c>, then × <see cref="Options.TierMultiplier"/>.<br/>
/// <b>Endurance:</b> <c>waveStress = 1 + max(0, waveCount − 1) × WaveStressPerExtraWave</c>.<br/>
/// <b>Combat:</b> <c>enemyStress = 1 + max(0, instanceCount − 1) × EnemyStressPerExtraInstance</c>.<br/>
/// <c>recommended = max(Minimum, round(base × TierMultiplier))</c>
/// </para>
/// Rows without a resolvable <see cref="EnemyDefinition"/> are skipped: use <see cref="SpawnPrefabCount.enemyDefinition"/>,
/// or a <see cref="SpawnPrefabCount.prefab"/> whose root/children include <see cref="EnemyBaseController"/> with a definition assigned.
/// </summary>
public static class RecommendedCombatPower
{
    /// <summary>Set true to print verbose recommended-CP traces to the Console. Off by default.</summary>
    public static bool DiagnosticsEnabled = false;

    /// <summary>Tuning for endurance trials and combat spawn-plan estimates.</summary>
    public readonly struct Options
    {
        /// <summary>
        /// Endurance trials: extra multiplier per wave after the first. Default 0.18f → 7 waves ≈ 2.08× on the average term.
        /// </summary>
        public readonly float WaveStressPerExtraWave;

        /// <summary>
        /// Combat: extra multiplier per enemy instance after the first. Default 0.09f → 4 enemies ≈ 1.27× on the average term.
        /// </summary>
        public readonly float EnemyStressPerExtraInstance;

        /// <summary>Applied last — use for higher trial tiers (e.g. 1.25f = +25% recommended CP).</summary>
        public readonly float TierMultiplier;

        public readonly int MinimumRecommended;

        public Options(
            float waveStressPerExtraWave = 0.18f,
            float enemyStressPerExtraInstance = 0.09f,
            float tierMultiplier = 1f,
            int minimumRecommended = 1)
        {
            WaveStressPerExtraWave = Mathf.Max(0f, waveStressPerExtraWave);
            EnemyStressPerExtraInstance = Mathf.Max(0f, enemyStressPerExtraInstance);
            TierMultiplier = Mathf.Max(0f, tierMultiplier);
            MinimumRecommended = Mathf.Max(1, minimumRecommended);
        }

        /// <summary>Explicit values — avoids any ambiguity with <c>new Options()</c> / IL2CPP edge cases.</summary>
        public static Options Default => new Options(0.18f, 0.09f, 1f, 1);
    }

    static void DiagLog(string msg, UnityEngine.Object context = null)
    {
        if (!DiagnosticsEnabled)
            return;
        Debug.Log($"[RecommendedCombatPower] {msg}", context);
    }

    /// <summary>
    /// Endurance trial: all waves, all spawn rows. <paramref name="waveCount"/> should match endurance wave count for stress.
    /// </summary>
    public static int ComputeForEnduranceTrial(MapNodeDefinition def, Options options)
    {
        if (def?.enduranceWaves == null || def.enduranceWaves.Count == 0)
        {
            DiagLog($"ComputeForEnduranceTrial: no waves (nodeId={(def ? def.nodeId : "null")}).");
            return 0;
        }

        // Do not call EnduranceWavePlan.EnsureReady() here: it runs MigrateLegacyIfNeeded and can diverge from
        // the same serialized spawns UI helpers read (e.g. GetLastEnemyDefinitionInEnduranceTrial). Assets get
        // migration from OnAfterDeserialize / wave.ToSyntheticGroupPlan at spawn time when needed.
        List<SpawnPrefabCount> rows = CollectEnduranceSpawns(def.enduranceWaves);
        DiagLog(
            $"ComputeForEnduranceTrial start: nodeId='{def.nodeId}' waveCount={def.enduranceWaves.Count} collectedSpawnRows={rows.Count}",
            def);

        int result = ComputeFromSpawnRows(
            rows,
            def.enduranceWaves.Count,
            options,
            $"endurance '{def.nodeId}'",
            CpStressMode.EnduranceWaveStress);
        DiagLog($"ComputeForEnduranceTrial result={result} (0 = will use fallbackRecommendedCombatPower on node).", def);
        return result;
    }

    /// <summary>Overload with default tuning.</summary>
    public static int ComputeForEnduranceTrial(MapNodeDefinition def)
    {
        return ComputeForEnduranceTrial(def, Options.Default);
    }

    /// <summary>
    /// Combat zones: median instance CP from <see cref="MapNodeDefinition.spawnGroupPlans"/>, then enemy-count stress (not wave stress).
    /// </summary>
    public static int ComputeForCombatZone(MapNodeDefinition def, Options options)
    {
        if (def?.spawnGroupPlans == null || def.spawnGroupPlans.Count == 0)
        {
            DiagLog($"ComputeForCombatZone: no spawnGroupPlans (nodeId={(def ? def.nodeId : "null")}).");
            return 0;
        }

        DiagLog(
            $"ComputeForCombatZone start: nodeId='{def.nodeId}' spawnGroupPlans={def.spawnGroupPlans.Count} (median + per-enemy stress)",
            def);
        int result = ComputeFromSpawnGroupPlans(def.spawnGroupPlans, options);
        DiagLog($"ComputeForCombatZone result={result}", def);
        return result;
    }

    public static int ComputeForCombatZone(MapNodeDefinition def)
    {
        return ComputeForCombatZone(def, Options.Default);
    }

    /// <summary>All spawn rows across group plans: median instance CP + enemy-count stress (not wave stress).</summary>
    public static int ComputeFromSpawnGroupPlans(IReadOnlyList<LevelSpawnGroupPlan> plans, Options options)
    {
        if (plans == null || plans.Count == 0)
            return 0;

        var rows = new List<SpawnPrefabCount>();
        for (int i = 0; i < plans.Count; i++)
        {
            LevelSpawnGroupPlan plan = plans[i];
            if (plan?.spawns == null)
                continue;
            for (int j = 0; j < plan.spawns.Count; j++)
            {
                SpawnPrefabCount row = plan.spawns[j];
                if (row != null)
                    rows.Add(row);
            }
        }

        return ComputeFromSpawnRows(rows, waveCountForEndurance: 1, options, "spawnGroupPlans", CpStressMode.CombatEnemyCountStress);
    }

    public static int ComputeFromSpawnGroupPlans(IReadOnlyList<LevelSpawnGroupPlan> plans)
    {
        return ComputeFromSpawnGroupPlans(plans, Options.Default);
    }

    private enum CpStressMode
    {
        EnduranceWaveStress,
        CombatEnemyCountStress
    }

    /// <summary>
    /// UI / level select and trial popups: pick the formula from <b>serialized content</b> first, then <see cref="MapNodeDefinition.nodeType"/> when both waves and spawn plans exist.
    /// Waves only → <see cref="ComputeForEnduranceTrial"/>. Spawn plans only → <see cref="ComputeForCombatZone"/>.
    /// Both → EnduranceTrial type uses waves; otherwise spawn plans (combat field) so leftover wave data cannot steal the combat estimate.
    /// Otherwise <see cref="MapNodeDefinition.fallbackRecommendedCombatPower"/>.
    /// </summary>
    public static int GetRecommendedCombatPowerForDisplay(MapNodeDefinition def, Options options)
    {
        if (!def)
            return 1;

        bool hasWaves = def.enduranceWaves != null && def.enduranceWaves.Count > 0;
        bool hasSpawnPlans = def.spawnGroupPlans != null && def.spawnGroupPlans.Count > 0;

        int computed = 0;
        if (hasWaves && !hasSpawnPlans)
        {
            DiagLog($"GetRecommendedCombatPowerForDisplay: waves-only → endurance (nodeId='{def.nodeId}')", def);
            computed = ComputeForEnduranceTrial(def, options);
        }
        else if (!hasWaves && hasSpawnPlans)
        {
            DiagLog($"GetRecommendedCombatPowerForDisplay: spawn-plans-only → combat zone (nodeId='{def.nodeId}')", def);
            computed = ComputeForCombatZone(def, options);
        }
        else if (hasWaves && hasSpawnPlans)
        {
            if (def.nodeType == MapNodeType.EnduranceTrial)
            {
                DiagLog($"GetRecommendedCombatPowerForDisplay: both data → nodeType EnduranceTrial, using waves (nodeId='{def.nodeId}')", def);
                computed = ComputeForEnduranceTrial(def, options);
            }
            else
            {
                DiagLog($"GetRecommendedCombatPowerForDisplay: both data → not EnduranceTrial, using spawn plans only (nodeId='{def.nodeId}')", def);
                computed = ComputeForCombatZone(def, options);
            }
        }

        if (computed > 0)
            return computed;

        return def.fallbackRecommendedCombatPower;
    }

    public static int GetRecommendedCombatPowerForDisplay(MapNodeDefinition def)
    {
        return GetRecommendedCombatPowerForDisplay(def, Options.Default);
    }

    private static List<SpawnPrefabCount> CollectEnduranceSpawns(List<EnduranceWavePlan> waves)
    {
        var rows = new List<SpawnPrefabCount>();
        for (int w = 0; w < waves.Count; w++)
        {
            EnduranceWavePlan wave = waves[w];
            if (wave?.spawns == null)
                continue;
            for (int i = 0; i < wave.spawns.Count; i++)
            {
                SpawnPrefabCount row = wave.spawns[i];
                if (row != null)
                    rows.Add(row);
            }
        }

        return rows;
    }

    private static int ComputeFromSpawnRows(
        List<SpawnPrefabCount> rows,
        int waveCountForEndurance,
        Options options,
        string contextLabel,
        CpStressMode mode)
    {
        if (rows == null || rows.Count == 0)
        {
            DiagLog($"ComputeFromSpawnRows ({contextLabel}): no rows.");
            return 0;
        }

        var cpCache = new Dictionary<int, int>(8);
        long sumInstanceCp = 0;
        int nInstances = 0;
        int maxInstanceCp = 0;
        int skippedNullRow = 0;
        int skippedNoEnemyDef = 0;
        int skippedCpException = 0;
        int skipDetailBudget = 8;

        for (int i = 0; i < rows.Count; i++)
        {
            SpawnPrefabCount row = rows[i];
            if (row == null)
            {
                skippedNullRow++;
                continue;
            }

            EnemyDefinition enemyDef = TryResolveEnemyDefinitionFromSpawnRow(row);
            if (enemyDef == null)
            {
                skippedNoEnemyDef++;
                if (skipDetailBudget-- > 0)
                {
                    DiagLog(
                        $"  row[{i}] skip: no EnemyDefinition (prefab={(row.prefab ? row.prefab.name : "null")}, count={row.count})",
                        row.prefab);
                }

                continue;
            }

            int c = Mathf.Max(1, row.count);
            int cp;
            try
            {
                cp = GetCachedEnemyCp(enemyDef, cpCache);
            }
            catch (Exception e)
            {
                skippedCpException++;
                Debug.LogWarning(
                    $"[RecommendedCombatPower] CP estimate failed for enemy '{enemyDef.name}': {e.Message}",
                    enemyDef);
                continue;
            }

            for (int k = 0; k < c; k++)
            {
                sumInstanceCp += cp;
                nInstances++;
                if (cp > maxInstanceCp)
                    maxInstanceCp = cp;
            }

            DiagLog($"  row[{i}] scored: enemy='{enemyDef.name}' instanceId={enemyDef.GetInstanceID()} count={c} cp={cp}", enemyDef);
        }

        if (nInstances <= 0)
        {
            DiagLog(
                $"ComputeFromSpawnRows ({contextLabel}): nothing scored. rows={rows.Count} " +
                $"skippedNullRow={skippedNullRow} skippedNoEnemyDef={skippedNoEnemyDef} skippedCpException={skippedCpException}");
            return 0;
        }

        float averageCp = (float)sumInstanceCp / nInstances;
        float tier = options.TierMultiplier > 0f ? options.TierMultiplier : 1f;
        int minRec = Mathf.Max(1, options.MinimumRecommended);

        float stress;
        if (mode == CpStressMode.EnduranceWaveStress)
        {
            int waves = Mathf.Max(1, waveCountForEndurance);
            float kWave = Mathf.Max(0f, options.WaveStressPerExtraWave);
            stress = 1f + Mathf.Max(0, waves - 1) * kWave;
            DiagLog(
                $"ComputeFromSpawnRows ({contextLabel}): instances={nInstances} averageCp={averageCp:F2} maxCp={maxInstanceCp} " +
                $"mode=Endurance waves={waves} waveStress={stress:F3} (k={kWave}) tier={tier} minRec={minRec}");
        }
        else
        {
            float kEnemy = Mathf.Max(0f, options.EnemyStressPerExtraInstance);
            stress = 1f + Mathf.Max(0, nInstances - 1) * kEnemy;
            DiagLog(
                $"ComputeFromSpawnRows ({contextLabel}): instances={nInstances} averageCp={averageCp:F2} maxCp={maxInstanceCp} " +
                $"mode=Combat enemyStress={stress:F3} (k={kEnemy}) tier={tier} minRec={minRec}");
        }

        float stressedAverage = averageCp * stress;
        float baseCp = Mathf.Max(stressedAverage, maxInstanceCp);
        float raw = baseCp * tier;
        int rounded = Mathf.RoundToInt(raw);
        int final = Mathf.Max(minRec, rounded);
        DiagLog(
            $"ComputeFromSpawnRows ({contextLabel}): stressedAvg={stressedAverage:F2} base=max(stressedAvg,max)={baseCp:F2} raw={raw:F2} rounded={rounded} final={final}");
        return final;
    }

    private static int GetCachedEnemyCp(EnemyDefinition enemy, Dictionary<int, int> cacheByInstanceId)
    {
        int id = enemy.GetInstanceID();
        if (cacheByInstanceId.TryGetValue(id, out int cp))
            return cp;

        cp = EnduranceTrialUIHelpers.GetEnemyCombatPowerRounded(enemy);
        cacheByInstanceId[id] = cp;
        return cp;
    }

    /// <summary>
    /// Prefer explicit <see cref="SpawnPrefabCount.enemyDefinition"/>; otherwise read from spawn <see cref="SpawnPrefabCount.prefab"/>.
    /// </summary>
    private static EnemyDefinition TryResolveEnemyDefinitionFromSpawnRow(SpawnPrefabCount row)
    {
        if (row == null)
            return null;
        if (row.itemDefinition)
            return null;
        // UnityEngine.Object: use truthiness so missing/broken references match inspector behaviour.
        if (row.enemyDefinition)
            return row.enemyDefinition;
        if (row.prefab == null)
            return null;

        EnemyBaseController ec = row.prefab.GetComponent<EnemyBaseController>();
        if (ec == null)
            ec = row.prefab.GetComponentInChildren<EnemyBaseController>(true);

        return ec != null ? ec.Definition : null;
    }
}
