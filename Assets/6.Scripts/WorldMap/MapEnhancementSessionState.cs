using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks extra-spawn map enhancement layout for the current gameplay session.
/// Changing equipped enhancements that affect initial spawn counts while still in-map requires re-entering.
/// </summary>
public static class MapEnhancementSessionState
{
    public const string ReenterWarningMessage =
        "Must re-enter the map to apply extra enemy spawn bonuses.";

    private static string _sessionNodeId;
    private static ExtraSpawnSnapshot _extraSpawnsAtSessionEnter;

    public static void BeginSession(string nodeId)
    {
        _sessionNodeId = string.IsNullOrWhiteSpace(nodeId) ? null : nodeId.Trim();
        _extraSpawnsAtSessionEnter = CaptureExtraSpawnSnapshot(_sessionNodeId);
    }

    public static void ClearSession()
    {
        _sessionNodeId = null;
        _extraSpawnsAtSessionEnter = ExtraSpawnSnapshot.Empty;
    }

    public static bool IsInGameplaySessionForNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(_sessionNodeId))
            return false;

        if (GameplayLevelBootstrapper.Instance == null || GameplayLevelBootstrapper.Instance.ActiveDefinition == null)
            return false;

        string activeId = GameplayLevelBootstrapper.Instance.ActiveDefinition.nodeId;
        if (string.IsNullOrWhiteSpace(activeId))
            return false;

        return string.Equals(activeId.Trim(), nodeId.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldShowReenterWarningForNode(string nodeId)
    {
        if (!IsInGameplaySessionForNode(nodeId))
            return false;

        ExtraSpawnSnapshot current = CaptureExtraSpawnSnapshot(nodeId);
        return !current.Equals(_extraSpawnsAtSessionEnter);
    }

    public static ExtraSpawnSnapshot CaptureExtraSpawnSnapshot(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return ExtraSpawnSnapshot.Empty;

        MapEnhancementAggregate aggregate = MapEnhancementService.BuildAggregate(nodeId);
        if (aggregate == null || aggregate.extraSpawnsByEnemyId.Count == 0)
            return ExtraSpawnSnapshot.Empty;

        var copy = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, int> pair in aggregate.extraSpawnsByEnemyId)
        {
            if (pair.Value <= 0 || string.IsNullOrWhiteSpace(pair.Key))
                continue;
            copy[pair.Key.Trim()] = pair.Value;
        }

        return copy.Count == 0 ? ExtraSpawnSnapshot.Empty : new ExtraSpawnSnapshot(copy);
    }

    public readonly struct ExtraSpawnSnapshot : IEquatable<ExtraSpawnSnapshot>
    {
        public static readonly ExtraSpawnSnapshot Empty = new(null);

        private readonly Dictionary<string, int> _extraSpawnsByEnemyId;

        public ExtraSpawnSnapshot(Dictionary<string, int> extraSpawnsByEnemyId)
        {
            _extraSpawnsByEnemyId = extraSpawnsByEnemyId;
        }

        public bool Equals(ExtraSpawnSnapshot other)
        {
            if (ReferenceEquals(_extraSpawnsByEnemyId, other._extraSpawnsByEnemyId))
                return true;

            if (_extraSpawnsByEnemyId == null || _extraSpawnsByEnemyId.Count == 0)
                return other._extraSpawnsByEnemyId == null || other._extraSpawnsByEnemyId.Count == 0;

            if (other._extraSpawnsByEnemyId == null || _extraSpawnsByEnemyId.Count != other._extraSpawnsByEnemyId.Count)
                return false;

            foreach (KeyValuePair<string, int> pair in _extraSpawnsByEnemyId)
            {
                if (!other._extraSpawnsByEnemyId.TryGetValue(pair.Key, out int otherValue) || otherValue != pair.Value)
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj) => obj is ExtraSpawnSnapshot other && Equals(other);

        public override int GetHashCode()
        {
            if (_extraSpawnsByEnemyId == null || _extraSpawnsByEnemyId.Count == 0)
                return 0;

            int hash = 17;
            foreach (KeyValuePair<string, int> pair in _extraSpawnsByEnemyId)
                hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(pair.Key) ^ pair.Value;
            return hash;
        }
    }
}
