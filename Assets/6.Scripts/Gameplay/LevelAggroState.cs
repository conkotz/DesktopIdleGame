using UnityEngine;

/// <summary>
/// Session-scoped map aggro latch used by <see cref="LevelEnemyAggroMode.CalmUntilPlayerAggressive"/>.
/// Resets whenever the active level starts in <see cref="GameplayLevelBootstrapper"/>.
/// </summary>
public static class LevelAggroState
{
    private static string _activeNodeId = string.Empty;
    private static bool _waveAggroLatched;
    private static Transform _mapAggroInstigatorMinion;

    public static event System.Action<string> AggroPulseTriggered;

    /// <summary>Living minion that triggered calm-map aggression, if any.</summary>
    public static Transform MapAggroInstigatorMinion => _mapAggroInstigatorMinion;

    public static void ResetForLevel(MapNodeDefinition def)
    {
        _activeNodeId = def != null && !string.IsNullOrWhiteSpace(def.nodeId)
            ? def.nodeId.Trim()
            : string.Empty;
        _waveAggroLatched = false;
        _mapAggroInstigatorMinion = null;
    }

    public static bool IsWaveAggroLatched(MapNodeDefinition def)
    {
        string id = def != null && !string.IsNullOrWhiteSpace(def.nodeId)
            ? def.nodeId.Trim()
            : string.Empty;
        return !string.IsNullOrEmpty(id) &&
               string.Equals(_activeNodeId, id, System.StringComparison.Ordinal) &&
               _waveAggroLatched;
    }

    public static void TriggerPlayerAggression(MapNodeDefinition def)
    {
        TriggerAggression(def, null);
    }

    public static void TriggerAggression(MapNodeDefinition def, Transform attacker)
    {
        string id = def != null && !string.IsNullOrWhiteSpace(def.nodeId)
            ? def.nodeId.Trim()
            : string.Empty;
        if (string.IsNullOrEmpty(id))
            return;

        if (!string.Equals(_activeNodeId, id, System.StringComparison.Ordinal))
            _activeNodeId = id;

        _mapAggroInstigatorMinion = ResolveMapAggroInstigatorMinion(attacker);

        // Pulse active enemies right now (future spawns won't receive this past pulse).
        AggroPulseTriggered?.Invoke(id);

        // Persist aggression only when simple waves are currently running.
        if (SimpleCombatWaveDirector.IsSimpleWavesRunningFor(def))
            _waveAggroLatched = true;
    }

    private static Transform ResolveMapAggroInstigatorMinion(Transform attacker)
    {
        if (!attacker || EnemyAggro.IsDirectPlayerAttacker(attacker))
            return null;

        MinionCombatTarget mct = EnemyAggro.GetMinionCombatTargetFrom(attacker);
        if (mct == null || !mct.IsAlive)
            return null;

        return mct.transform;
    }
}
