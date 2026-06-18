using System;
using UnityEngine;

/// <summary>
/// Persists conditional auto-battle loadout settings in <see cref="PlayerPrefs"/> (global, not save-slot).
/// </summary>
public static class ConditionalAutoBattleSettingsStore
{
    public const string DisplayName = "Set conditional auto battle settings";
    public const float MeleeRangeWorldUnits = 3f;
    public const float LowHealthFraction = 0.30f;
    public const float HighHealthFraction = 0.70f;

    private const string EnabledKey = "Settings.ConditionalAutoBattle.Enabled";
    private const string Set1ConditionKey = "Settings.ConditionalAutoBattle.Set1Condition";
    private const string Set2ConditionKey = "Settings.ConditionalAutoBattle.Set2Condition";

    public static event Action Changed;

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(EnabledKey, 0) != 0;
        set
        {
            if (Enabled == value)
                return;

            PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    public static ConditionalAutoBattleCondition GetSet1Condition() =>
        NormalizeCondition(PlayerPrefs.GetInt(Set1ConditionKey, 0));

    public static ConditionalAutoBattleCondition GetSet2Condition() =>
        NormalizeCondition(PlayerPrefs.GetInt(Set2ConditionKey, 0));

    public static void SetSet1Condition(ConditionalAutoBattleCondition condition)
    {
        condition = NormalizeCondition(condition);
        if (GetSet1Condition() == condition)
            return;

        PlayerPrefs.SetInt(Set1ConditionKey, (int)condition);
        ResolveConditionConflict(setOneChanged: true, condition);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void SetSet2Condition(ConditionalAutoBattleCondition condition)
    {
        condition = NormalizeCondition(condition);
        if (GetSet2Condition() == condition)
            return;

        PlayerPrefs.SetInt(Set2ConditionKey, (int)condition);
        ResolveConditionConflict(setOneChanged: false, condition);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    /// <summary>Returns set index 0 or 1 when a condition matches; -1 when neither applies.</summary>
    public static int EvaluateDesiredSetIndex(
        bool targetInMeleeRange,
        bool noMeleeTarget,
        float healthFraction01)
    {
        if (!Enabled)
            return -1;

        if (GetSet1Condition() == ConditionalAutoBattleCondition.DoNothing &&
            GetSet2Condition() == ConditionalAutoBattleCondition.DoNothing)
            return -1;

        bool set2 = IsConditionMet(GetSet2Condition(), targetInMeleeRange, noMeleeTarget, healthFraction01);
        if (set2)
            return 1;

        bool set1 = IsConditionMet(GetSet1Condition(), targetInMeleeRange, noMeleeTarget, healthFraction01);
        if (set1)
            return 0;

        return -1;
    }

    public static bool HasActiveConditions()
    {
        return GetSet1Condition() != ConditionalAutoBattleCondition.DoNothing ||
               GetSet2Condition() != ConditionalAutoBattleCondition.DoNothing;
    }

    public static bool IsConditionMet(
        ConditionalAutoBattleCondition condition,
        bool targetInMeleeRange,
        bool noMeleeTarget,
        float healthFraction01)
    {
        return condition switch
        {
            ConditionalAutoBattleCondition.TargetInMeleeRange => targetInMeleeRange,
            ConditionalAutoBattleCondition.NoMeleeTarget => noMeleeTarget,
            ConditionalAutoBattleCondition.LowHealth => healthFraction01 < LowHealthFraction,
            ConditionalAutoBattleCondition.HighHealth => healthFraction01 > HighHealthFraction,
            _ => false,
        };
    }

    public static string GetDisplayLabel(ConditionalAutoBattleCondition condition)
    {
        return condition switch
        {
            ConditionalAutoBattleCondition.DoNothing => "Do nothing",
            ConditionalAutoBattleCondition.TargetInMeleeRange =>
                "Activate set when target in melee range (3 range)",
            ConditionalAutoBattleCondition.NoMeleeTarget =>
                "Activate set when no current target melee target",
            ConditionalAutoBattleCondition.LowHealth =>
                "Activate set when dropping to low health (<30% HP)",
            ConditionalAutoBattleCondition.HighHealth =>
                "Activate set when above on high health (>70% HP)",
            _ => condition.ToString(),
        };
    }

    public static int ConditionCount => Enum.GetValues(typeof(ConditionalAutoBattleCondition)).Length;

    internal static void ClearStoredKeysAndReload()
    {
        PlayerPrefs.DeleteKey(EnabledKey);
        PlayerPrefs.DeleteKey(Set1ConditionKey);
        PlayerPrefs.DeleteKey(Set2ConditionKey);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    private static ConditionalAutoBattleCondition NormalizeCondition(int raw)
    {
        int max = ConditionCount - 1;
        int clamped = Mathf.Clamp(raw, 0, max);
        return (ConditionalAutoBattleCondition)clamped;
    }

    private static ConditionalAutoBattleCondition NormalizeCondition(ConditionalAutoBattleCondition condition)
    {
        int max = ConditionCount - 1;
        int clamped = Mathf.Clamp((int)condition, 0, max);
        return (ConditionalAutoBattleCondition)clamped;
    }

    private static void ResolveConditionConflict(bool setOneChanged, ConditionalAutoBattleCondition condition)
    {
        if (condition == ConditionalAutoBattleCondition.DoNothing)
            return;

        if (setOneChanged)
        {
            if (GetSet2Condition() == condition)
                PlayerPrefs.SetInt(Set2ConditionKey, (int)ConditionalAutoBattleCondition.DoNothing);
            return;
        }

        if (GetSet1Condition() == condition)
            PlayerPrefs.SetInt(Set1ConditionKey, (int)ConditionalAutoBattleCondition.DoNothing);
    }
}
