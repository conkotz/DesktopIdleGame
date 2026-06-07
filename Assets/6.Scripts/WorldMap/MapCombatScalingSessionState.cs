using System;
using UnityEngine;

/// <summary>
/// Tracks the combat map scaling tier applied for the current gameplay session.
/// Changing scaling while still inside that map requires re-entering to apply bonuses.
/// </summary>
public static class MapCombatScalingSessionState
{
    public const string ReenterWarningMessage = "MUST RE-ENTER THE MAP TO GAIN UPDATED SCALING BONUS";

    private static string _sessionNodeId;
    private static int _appliedSliderTier;
    private static bool _needsReenterForScalingBonus;

    public static int AppliedSliderTier => _appliedSliderTier;

    public static void BeginSession(string nodeId, int appliedSliderTier)
    {
        _sessionNodeId = string.IsNullOrWhiteSpace(nodeId) ? null : nodeId.Trim();
        _appliedSliderTier = Mathf.Clamp(appliedSliderTier, MapCombatScaling.SliderMin, MapCombatScaling.SliderMax);
        _needsReenterForScalingBonus = false;
    }

    public static void ClearSession()
    {
        _sessionNodeId = null;
        _appliedSliderTier = MapCombatScaling.SliderMin;
        _needsReenterForScalingBonus = false;
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

    public static bool NeedsReenterWarningForNode(string nodeId) =>
        _needsReenterForScalingBonus && IsInGameplaySessionForNode(nodeId);

    public static bool NeedsReenterWarningInHud() =>
        _needsReenterForScalingBonus &&
        GameplayLevelBootstrapper.Instance != null &&
        GameplayLevelBootstrapper.Instance.ActiveDefinition != null;

    public static bool ShouldShowReenterWarningForSlider(string nodeId, int sliderValue)
    {
        if (!IsInGameplaySessionForNode(nodeId))
            return false;

        sliderValue = Mathf.Clamp(sliderValue, MapCombatScaling.SliderMin, MapCombatScaling.SliderMax);
        return sliderValue != _appliedSliderTier;
    }

    public static void NotifySelectedTierChangedWhileInMap(string nodeId, int newSliderTier)
    {
        if (!IsInGameplaySessionForNode(nodeId))
            return;

        newSliderTier = Mathf.Clamp(newSliderTier, MapCombatScaling.SliderMin, MapCombatScaling.SliderMax);
        _needsReenterForScalingBonus = newSliderTier != _appliedSliderTier;
    }
}
