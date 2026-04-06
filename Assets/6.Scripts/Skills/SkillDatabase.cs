using System.Collections.Generic;
using UnityEngine;

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

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Invalidate so inspector edits to the list (add/remove/reorder) refresh the cache.
        _lookupByType = null;
    }
#endif
}
