using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime lookup for <see cref="AbilityDefinition"/> assets.
/// Loads from Resources/Abilities by default.
/// </summary>
public static class AbilityLibrary
{
    private static Dictionary<string, AbilityDefinition> _byId;

    public static AbilityDefinition Get(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
            return null;

        EnsureLoaded();
        return _byId != null && _byId.TryGetValue(abilityId, out var def) ? def : null;
    }

    public static List<AbilityDefinition> GetBySkill(SkillType skill)
    {
        EnsureLoaded();
        var list = new List<AbilityDefinition>();
        if (_byId == null) return list;

        foreach (var kv in _byId)
        {
            var def = kv.Value;
            if (!def) continue;
            if (def.sourceSkill != skill) continue;
            list.Add(def);
        }

        list.Sort((a, b) =>
        {
            int byLvl = a.unlockLevel.CompareTo(b.unlockLevel);
            if (byLvl != 0) return byLvl;
            return string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase);
        });

        return list;
    }

    private static void EnsureLoaded()
    {
        if (_byId != null)
            return;

        _byId = new Dictionary<string, AbilityDefinition>(StringComparer.OrdinalIgnoreCase);
        AbilityDefinition[] defs = Resources.LoadAll<AbilityDefinition>("Abilities");
        if (defs == null) return;

        foreach (var def in defs)
        {
            if (!def) continue;
            if (string.IsNullOrWhiteSpace(def.abilityId)) continue;
            _byId[def.abilityId] = def;
        }
    }
}

