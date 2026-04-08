using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Authoring-time registry of <see cref="SkillDefinition"/> assets for UI and lookups.
/// Does not store player progression; use <see cref="SkillsManager"/> for levels/XP.
/// </summary>
[CreateAssetMenu(fileName = "SkillDatabase", menuName = "Desktop Idle Game/Skills/Skill Database")]
public class SkillDatabase : ScriptableObject
{
    [Header("Skills")]
    [Tooltip("All skill definitions available in the game. Order is for display only unless UI sorts by SkillDefinition.listSortOrder.")]
    [SerializeField] private List<SkillDefinition> skills = new();

    private Dictionary<SkillType, SkillDefinition> _lookupByType;

    /// <summary>All configured skills (read-only). Null entries are skipped when building lookups.</summary>
    public IReadOnlyList<SkillDefinition> Skills => skills;

    /// <summary>Returns the definition for <paramref name="type"/>, or null if not present.</summary>
    public SkillDefinition Get(SkillType type)
    {
        EnsureLookupBuilt();
        return _lookupByType != null && _lookupByType.TryGetValue(type, out var def) ? def : null;
    }

    public static SkillDatabase LoadDefault()
    {
        SkillDatabase[] dbs = Resources.LoadAll<SkillDatabase>(string.Empty);
        SkillDatabase best = PickBestDatabase(dbs);
        if (best != null)
            return best;

        best = Resources.Load<SkillDatabase>("Databases/SkillDatabase");
        if (best != null)
            return best;

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:SkillDatabase");
        if (guids != null && guids.Length > 0)
        {
            List<SkillDatabase> found = new List<SkillDatabase>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                SkillDatabase db = AssetDatabase.LoadAssetAtPath<SkillDatabase>(path);
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

    private void EnsureLookupBuilt()
    {
        if (_lookupByType != null) return;

        _lookupByType = new Dictionary<SkillType, SkillDefinition>(skills.Count);
        foreach (var skill in skills)
        {
            if (skill == null) continue;
            _lookupByType[skill.skillType] = skill;
        }
    }

    private static SkillDatabase PickBestDatabase(IEnumerable<SkillDatabase> candidates)
    {
        if (candidates == null)
            return null;

        SkillDatabase best = null;
        int bestCount = -1;
        foreach (SkillDatabase db in candidates)
        {
            if (!db) continue;
            int count = db.skills != null ? db.skills.Count : 0;
            if (count > bestCount)
            {
                best = db;
                bestCount = count;
            }
        }

        return best;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Invalidate so inspector edits to the list (add/remove/reorder) refresh the cache.
        _lookupByType = null;
    }
#endif
}
