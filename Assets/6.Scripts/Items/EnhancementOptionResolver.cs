using System;
using System.Collections.Generic;
using UnityEngine;

public static class EnhancementOptionResolver
{
    private static EnhancementOptionDatabase _database;
    private static Dictionary<string, EnhancementOptionEntry> _optionsByScrollId;
    private static Dictionary<string, EnhancementOptionEntry> _optionsByOptionId;

    public static EnhancementOptionDatabase GetDatabase()
    {
        if (_database == null)
        {
            _database = Resources.Load<EnhancementOptionDatabase>("Databases/EnhancementOptionDatabase");
            if (_database != null)
                _database.EnsureDefaults();
        }

        return _database;
    }

    private static readonly Dictionary<string, string> LegacyOptionIdAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "corruption_physical_gamble", "chaos_physical_gamble" },
            { "corruption_fire_gamble", "chaos_fire_gamble" },
            { "corruption_magic_gamble", "chaos_fire_gamble" },
            { "corruption_damage_gamble", "chaos_corruption_gamble" },
            { "corruption_health_gamble", "chaos_health_gamble" },
        };

    public static EnhancementOptionEntry GetOptionById(string optionId)
    {
        if (string.IsNullOrWhiteSpace(optionId))
            return null;

        EnsureScrollIndex();
        if (_optionsByOptionId == null)
            return null;

        string key = optionId.Trim();
        if (_optionsByOptionId.TryGetValue(key, out EnhancementOptionEntry option))
            return option;

        if (LegacyOptionIdAliases.TryGetValue(key, out string mappedId))
            _optionsByOptionId.TryGetValue(mappedId, out option);

        return option;
    }

    public static EnhancementOptionEntry GetOptionForScroll(ItemDefinition scroll)
    {
        if (scroll == null || scroll.itemKind != ItemKind.EnhancementScroll)
            return null;

        if (!string.IsNullOrWhiteSpace(scroll.enhancementOptionId))
        {
            EnhancementOptionEntry byOptionId = GetOptionById(scroll.enhancementOptionId);
            if (byOptionId != null)
                return byOptionId;
        }

        if (string.IsNullOrWhiteSpace(scroll.itemId))
            return null;

        EnsureScrollIndex();
        if (_optionsByScrollId == null)
            return null;

        _optionsByScrollId.TryGetValue(scroll.itemId.Trim(), out EnhancementOptionEntry option);
        return option;
    }

    public static EnhancementOptionEntry GetOptionForScrollId(string scrollItemId)
    {
        if (string.IsNullOrWhiteSpace(scrollItemId))
            return null;

        EnsureScrollIndex();
        if (_optionsByScrollId == null)
            return null;

        _optionsByScrollId.TryGetValue(scrollItemId.Trim(), out EnhancementOptionEntry option);
        return option;
    }

    public static bool ScrollMatchesDatabase(ItemDefinition scroll, EnhancementOptionEntry option)
    {
        if (scroll == null || option == null || scroll.itemKind != ItemKind.EnhancementScroll)
            return false;

        if (!string.IsNullOrWhiteSpace(scroll.enhancementOptionId))
            return string.Equals(scroll.enhancementOptionId.Trim(), option.optionId, StringComparison.OrdinalIgnoreCase);

        if (!string.Equals(scroll.itemId, option.linkedScrollItemId, StringComparison.OrdinalIgnoreCase))
            return false;

        EnhancementScrollStats expected = option.ToScrollStats();
        EnhancementScrollStats actual = scroll.GetEffectiveEnhancementScrollStats();

        if (actual.targetStat != expected.targetStat)
            return false;

        if (actual.modifierKind != expected.modifierKind)
            return false;

        if (!Mathf.Approximately(actual.modifierValue, expected.modifierValue))
            return false;

        if (actual.consumeSlotOnFailure != expected.consumeSlotOnFailure)
            return false;

        if (actual.failureOutcome != expected.failureOutcome)
            return false;

        if (!Mathf.Approximately(actual.destroyChanceOnFailure, expected.destroyChanceOnFailure))
            return false;

        EnhancementScrollGearMask expectedMask = EnhancementScrollGearRules.NormalizeMask(expected.allowedGearTypes);
        EnhancementScrollGearMask actualMask = EnhancementScrollGearRules.NormalizeMask(actual.allowedGearTypes);
        return expectedMask == actualMask;
    }

    public static EnhancementScrollStats BuildStatsForApply(EnhancementOptionEntry option, ItemDefinition gear)
    {
        if (option == null)
            return default;

        return option.ToScrollStats(gear);
    }

    public static void InvalidateCache()
    {
        _database = null;
        _optionsByScrollId = null;
        _optionsByOptionId = null;
    }

    private static void EnsureScrollIndex()
    {
        if (_optionsByScrollId != null && _optionsByOptionId != null)
            return;

        EnhancementOptionDatabase db = GetDatabase();
        var byScrollId = new Dictionary<string, EnhancementOptionEntry>(StringComparer.OrdinalIgnoreCase);
        var byOptionId = new Dictionary<string, EnhancementOptionEntry>(StringComparer.OrdinalIgnoreCase);
        if (db?.Options != null)
        {
            for (int i = 0; i < db.Options.Count; i++)
            {
                EnhancementOptionEntry entry = db.Options[i];
                if (entry == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(entry.optionId))
                    byOptionId[entry.optionId.Trim()] = entry;

                if (string.IsNullOrWhiteSpace(entry.linkedScrollItemId))
                    continue;

                byScrollId[entry.linkedScrollItemId.Trim()] = entry;
            }
        }

        _optionsByScrollId = byScrollId;
        _optionsByOptionId = byOptionId;
    }
}
