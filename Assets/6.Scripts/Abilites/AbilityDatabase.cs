using System.Collections.Generic;
using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Registry + lookup for ability definition assets.
/// Keep this asset in any Resources folder so runtime can load it.
/// </summary>
[CreateAssetMenu(fileName = "AbilityDatabase", menuName = "Desktop Idle Game/Abilities/Ability Database")]
public class AbilityDatabase : ScriptableObject
{
    [Header("Abilities")]
    [Tooltip("All abilities available in the game. Null entries are ignored.")]
    [SerializeField] private List<AbilityDefinition> abilities = new();

    public IReadOnlyList<AbilityDefinition> Abilities => abilities;

    private Dictionary<string, AbilityDefinition> _byId;

    public AbilityDefinition Get(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
            return null;

        EnsureLookupBuilt();
        return _byId != null && _byId.TryGetValue(abilityId, out var def) ? def : null;
    }

    public List<AbilityDefinition> GetBySkill(SkillType skill)
    {
        var list = new List<AbilityDefinition>();
        if (abilities == null || abilities.Count == 0)
            return list;

        for (int i = 0; i < abilities.Count; i++)
        {
            AbilityDefinition def = abilities[i];
            if (!def || def.sourceSkill != skill)
                continue;
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

    public static AbilityDatabase LoadDefault()
    {
        AbilityDatabase[] dbs = Resources.LoadAll<AbilityDatabase>(string.Empty);
        AbilityDatabase best = PickBestDatabase(dbs);
        if (best != null)
            return best;

        // Build player: Resources.LoadAll can miss edge cases; explicit path matches Assets/Resources/Databases/AbilityDatabase.asset
        best = Resources.Load<AbilityDatabase>("Databases/AbilityDatabase");
        if (best != null)
            return best;

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:AbilityDatabase");
        if (guids != null && guids.Length > 0)
        {
            List<AbilityDatabase> found = new List<AbilityDatabase>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                AbilityDatabase db = AssetDatabase.LoadAssetAtPath<AbilityDatabase>(path);
                if (db != null)
                    found.Add(db);
            }

            best = PickBestDatabase(found);
            if (best != null)
                return best;
        }
#endif

        return null;
    }

    /// <summary>
    /// Looks up an ability in <b>every</b> <see cref="AbilityDatabase"/> under Resources (then editor project in Edit mode).
    /// Use when <see cref="LoadDefault"/> picked a different DB (it chooses the largest list) than the one that contains a slotted ability.
    /// </summary>
    public static AbilityDefinition FindDefinitionById(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
            return null;

        AbilityDatabase[] dbs = Resources.LoadAll<AbilityDatabase>(string.Empty);
        if (dbs != null)
        {
            for (int i = 0; i < dbs.Length; i++)
            {
                if (!dbs[i])
                    continue;
                AbilityDefinition def = dbs[i].Get(abilityId);
                if (def)
                    return def;
            }
        }

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:AbilityDatabase");
        if (guids != null)
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                AbilityDatabase db = AssetDatabase.LoadAssetAtPath<AbilityDatabase>(path);
                if (!db)
                    continue;
                AbilityDefinition def = db.Get(abilityId);
                if (def)
                    return def;
            }
        }
#endif

        return null;
    }

    private void OnEnable()
    {
        _byId = null;
    }

    private void EnsureLookupBuilt()
    {
        if (_byId != null)
            return;

        _byId = new Dictionary<string, AbilityDefinition>(StringComparer.OrdinalIgnoreCase);
        if (abilities == null)
            return;

        for (int i = 0; i < abilities.Count; i++)
        {
            AbilityDefinition def = abilities[i];
            if (!def || string.IsNullOrWhiteSpace(def.abilityId))
                continue;
            _byId[def.abilityId] = def;
        }

        // Renamed ability ids — keep old keys working for saves / action-bar data.
        if (_byId.TryGetValue("rend", out AbilityDefinition rend) && rend)
            _byId["rending_strike"] = rend;
        if (_byId.TryGetValue("whirlwind", out AbilityDefinition whirl) && whirl)
            _byId["whirling_blade"] = whirl;
    }

    private static AbilityDatabase PickBestDatabase(IEnumerable<AbilityDatabase> candidates)
    {
        if (candidates == null)
            return null;

        AbilityDatabase best = null;
        int bestCount = -1;
        foreach (AbilityDatabase db in candidates)
        {
            if (!db) continue;
            int count = db.abilities != null ? db.abilities.Count : 0;
            if (count > bestCount)
            {
                best = db;
                bestCount = count;
            }
        }

        return best;
    }
}
