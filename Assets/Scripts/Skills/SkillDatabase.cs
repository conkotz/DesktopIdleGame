using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillDatabase", menuName = "Game/Skills/Skill Database")]
public class SkillDatabase : ScriptableObject
{
    [SerializeField] private List<SkillDefinition> skills = new();

    private Dictionary<SkillType, SkillDefinition> _lookup;

    public IReadOnlyList<SkillDefinition> Skills => skills;

    public SkillDefinition Get(SkillType type)
    {
        BuildLookupIfNeeded();
        _lookup.TryGetValue(type, out var def);
        return def;
    }

    private void BuildLookupIfNeeded()
    {
        if (_lookup != null) return;

        _lookup = new Dictionary<SkillType, SkillDefinition>();
        foreach (var skill in skills)
        {
            if (skill == null) continue;
            _lookup[skill.skillType] = skill;
        }
    }
}