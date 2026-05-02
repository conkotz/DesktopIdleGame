using System;
using System.Collections.Generic;
using UnityEngine;

public class SkillsManager : MonoBehaviour, ISaveable
{
    public static SkillsManager Instance { get; private set; }

    [Serializable]
    public class SkillProgress
    {
        public int level = 1;
        public int xp = 0;   // XP into current level (you subtract req on level-up)
    }

    [Header("Starting Skills")]
    [SerializeField]
    private List<SkillSeed> starting = new()
    {
        new SkillSeed(SkillType.Mining, 1, 0),
        new SkillSeed(SkillType.Woodcutting, 1, 0),
        new SkillSeed(SkillType.Fishing, 1, 0),
        new SkillSeed(SkillType.Melee, 1, 0),
        new SkillSeed(SkillType.Ranged, 1, 0),
        new SkillSeed(SkillType.Magic, 1, 0),
        new SkillSeed(SkillType.Endurance, 1, 0),
    };

    [Serializable]
    public struct SkillSeed
    {
        public SkillType type;
        public int level;
        public int xp;

        public SkillSeed(SkillType t, int lvl, int x)
        {
            type = t; level = lvl; xp = x;
        }
    }

    private readonly Dictionary<SkillType, SkillProgress> _skills = new();
    private readonly Dictionary<SkillType, float> _xpRemainder = new();
    private readonly Dictionary<string, int> _skillChoiceSelections = new();
    private readonly Dictionary<string, int> _skillAbilityRowPicks = new();

    // ✅ Active XP display (drives XP bar label + color)
    public event Action<SkillType, string> OnActiveXpDisplayChanged;
    public SkillType ActiveSkill { get; private set; } = SkillType.Mining;
    public string ActiveSource { get; private set; } = "";
    /// <summary>XP per gain from the current source (e.g. node xpPerTick). -1 when unknown or N/A.</summary>
    public int ActiveSourceXpPerGain { get; private set; } = -1;

    // Hook this to your XP bar
    public event Action<SkillType, int, string> OnXpGained; // (skill, amount, source)
    public event Action<SkillType, int> OnLevelUp;          // (skill, newLevel)
    /// <summary>Fired when a skill level drops outside normal XP rules (e.g. debug). Does not fire <see cref="OnLevelUp"/>.</summary>
    public event Action<SkillType, int> OnSkillLevelDecreased; // (skill, newLevel)
    /// <summary>choiceIndex is -1 when the player cleared that branch (no active choice).</summary>
    public event Action<SkillType, int, int> OnSkillChoiceSelectionChanged; // (skill, sourceLevel, choiceIndex)

    /// <summary>pickIndex is -1 when cleared; otherwise sibling index among multiple abilities at the same level.</summary>
    public event Action<SkillType, int, int> OnSkillAbilityRowPickChanged; // (skill, requiredLevel, pickIndex)

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);

        BuildDefaultsIfEmpty();
    }

    private void BuildDefaultsIfEmpty()
    {
        _skills.Clear();
        foreach (var s in starting)
        {
            _skills[s.type] = new SkillProgress
            {
                level = Mathf.Max(1, s.level),
                xp = Mathf.Max(0, s.xp)
            };
        }
    }

    // -------------------------
    // Queries
    // -------------------------

    public int GetLevel(SkillType type) => Get(type).level;
    public int GetXpIntoLevel(SkillType type) => Get(type).xp;

    public int GetXpRequiredThisLevel(SkillType type)
    {
        var p = Get(type);
        return XpToNextLevel(p.level);
    }

    public int GetXpRemainingThisLevel(SkillType type)
    {
        var p = Get(type);
        int req = XpToNextLevel(p.level);
        return Mathf.Max(0, req - p.xp);
    }

    public int XpToNextLevel(int currentLevel)
    {
        currentLevel = Mathf.Max(1, currentLevel);
        return Mathf.RoundToInt(50f + (currentLevel - 1) * 30f + Mathf.Pow(currentLevel - 1, 1.35f) * 12f);
    }

    public float GetProgress01(SkillType type)
    {
        var p = Get(type);
        int req = XpToNextLevel(p.level);
        if (req <= 0) return 1f;
        return Mathf.Clamp01((float)p.xp / req);
    }

    /// <summary>True if this skill type has an entry in progression (seeded, loaded, or previously gained XP).</summary>
    public bool HasSkill(SkillType type) => _skills.ContainsKey(type);

    /// <summary>True when the player's current level for <paramref name="type"/> meets or exceeds <paramref name="requiredLevel"/>.</summary>
    public bool IsLevelUnlocked(SkillType type, int requiredLevel)
    {
        if (requiredLevel <= 0) return true;
        return GetLevel(type) >= requiredLevel;
    }

    /// <summary>All skill types currently stored in progression (enum order for stable UI lists).</summary>
    public IEnumerable<SkillType> GetAllTrackedSkills()
    {
        var list = new List<SkillType>(_skills.Keys);
        list.Sort((a, b) => ((int)a).CompareTo((int)b));
        return list;
    }

    private static string BuildChoiceKey(SkillType skillType, int sourceLevel)
    {
        return $"{skillType}:{Mathf.Max(1, sourceLevel)}";
    }

    /// <summary>
    /// Sets the active choice for a branch. Pass <paramref name="choiceIndex"/> &lt; 0 to clear (no selection).
    /// </summary>
    public void SetSkillChoiceSelection(SkillType skillType, int sourceLevel, int choiceIndex)
    {
        string key = BuildChoiceKey(skillType, sourceLevel);
        int src = Mathf.Max(1, sourceLevel);

        if (choiceIndex < 0)
        {
            if (!_skillChoiceSelections.Remove(key))
                return;
            OnSkillChoiceSelectionChanged?.Invoke(skillType, src, -1);
            return;
        }

        int clamped = Mathf.Max(0, choiceIndex);
        if (_skillChoiceSelections.TryGetValue(key, out int existing) && existing == clamped)
            return;

        _skillChoiceSelections[key] = clamped;
        OnSkillChoiceSelectionChanged?.Invoke(skillType, src, clamped);
    }

    public int GetSkillChoiceSelection(SkillType skillType, int sourceLevel, int defaultValue = -1)
    {
        string key = BuildChoiceKey(skillType, sourceLevel);
        return _skillChoiceSelections.TryGetValue(key, out int value) ? value : defaultValue;
    }

    public void ClearAllSkillChoiceSelections()
    {
        if (_skillChoiceSelections.Count == 0)
            return;
        var copy = new List<KeyValuePair<string, int>>(_skillChoiceSelections);
        _skillChoiceSelections.Clear();
        foreach (var kv in copy)
            TryInvokeChoiceClearFromKey(kv.Key);
    }

    private void TryInvokeChoiceClearFromKey(string key)
    {
        int idx = key.IndexOf(':');
        if (idx <= 0 || idx >= key.Length - 1)
            return;
        if (!Enum.TryParse(key.Substring(0, idx), out SkillType st))
            return;
        if (!int.TryParse(key.Substring(idx + 1), out int lvl))
            return;
        OnSkillChoiceSelectionChanged?.Invoke(st, Mathf.Max(1, lvl), -1);
    }

    /// <summary>Clears every passive-branch choice and every multi-ability row pick (global reset).</summary>
    public void ResetAllSkillTreeSelections()
    {
        ClearAllSkillChoiceSelections();
        ClearAllSkillAbilityRowPicks();
    }

    private static string BuildAbilityRowKey(SkillType skillType, int requiredLevel)
    {
        return $"{skillType}:abilityRow:{Mathf.Max(1, requiredLevel)}";
    }

    /// <summary>
    /// Commits which sibling ability is active at this level. Clearing is only done via
    /// <see cref="ClearSkillAbilityRowPicksForSkill"/> / <see cref="ClearAllSkillAbilityRowPicks"/> (e.g. Reset Tree), not by passing a negative index.
    /// </summary>
    public void SetSkillAbilityRowPick(SkillType skillType, int requiredLevel, int pickIndex)
    {
        if (pickIndex < 0)
            return;

        string key = BuildAbilityRowKey(skillType, requiredLevel);
        int lvl = Mathf.Max(1, requiredLevel);

        int clamped = Mathf.Max(0, pickIndex);
        if (_skillAbilityRowPicks.TryGetValue(key, out int existing) && existing == clamped)
            return;

        _skillAbilityRowPicks[key] = clamped;
        OnSkillAbilityRowPickChanged?.Invoke(skillType, lvl, clamped);
    }

    public int GetSkillAbilityRowPick(SkillType skillType, int requiredLevel, int defaultValue = -1)
    {
        string key = BuildAbilityRowKey(skillType, requiredLevel);
        return _skillAbilityRowPicks.TryGetValue(key, out int value) ? value : defaultValue;
    }

    public void ClearAllSkillAbilityRowPicks()
    {
        if (_skillAbilityRowPicks.Count == 0)
            return;
        var copy = new List<KeyValuePair<string, int>>(_skillAbilityRowPicks);
        _skillAbilityRowPicks.Clear();
        foreach (var kv in copy)
            TryInvokeAbilityRowClearFromKey(kv.Key);
    }

    /// <summary>Clears ability sibling picks for one skill (e.g. reset tree for current skill).</summary>
    public void ClearSkillAbilityRowPicksForSkill(SkillType skillType)
    {
        string prefix = $"{skillType}:abilityRow:";
        var toRemove = new List<string>();
        foreach (var kv in _skillAbilityRowPicks)
        {
            if (kv.Key.StartsWith(prefix, StringComparison.Ordinal))
                toRemove.Add(kv.Key);
        }

        foreach (string k in toRemove)
        {
            _skillAbilityRowPicks.Remove(k);
            TryInvokeAbilityRowClearFromKey(k);
        }
    }

    private void TryInvokeAbilityRowClearFromKey(string key)
    {
        int idx = key.IndexOf(":abilityRow:", StringComparison.Ordinal);
        if (idx <= 0)
            return;
        if (!Enum.TryParse(key.Substring(0, idx), out SkillType st))
            return;
        int levelStart = idx + ":abilityRow:".Length;
        if (levelStart >= key.Length || !int.TryParse(key.Substring(levelStart), out int lvl))
            return;
        OnSkillAbilityRowPickChanged?.Invoke(st, Mathf.Max(1, lvl), -1);
    }

    // -------------------------
    // XP Gain
    // -------------------------

    public void AddXp(SkillType type, int amount, string source = "")
    {
        if (amount <= 0) return;

        // ✅ "most recently gained" should become the active display (amount drives the bar hint after ticks)
        SetActiveXpDisplay(type, source, amount);

        var p = Get(type);
        p.xp += amount;

        OnXpGained?.Invoke(type, amount, source);

        // Level up loop
        while (true)
        {
            int req = XpToNextLevel(p.level);
            if (req <= 0) break;
            if (p.xp < req) break;

            p.xp -= req;
            p.level += 1;
            OnLevelUp?.Invoke(type, p.level);
        }
    }

    public void AddXpFloat(SkillType skill, float xp, string source)
    {
        if (xp <= 0f) return;

        if (!_xpRemainder.TryGetValue(skill, out float rem))
            rem = 0f;

        rem += xp;

        int whole = Mathf.FloorToInt(rem);
        rem -= whole;

        _xpRemainder[skill] = rem;

        if (whole > 0)
            AddXp(skill, whole, source);
    }

    /// <summary>
    /// Debug / testing: each tracked skill loses one level (minimum 1). XP into the current level is cleared.
    /// Fires <see cref="OnSkillLevelDecreased"/> per changed skill (not <see cref="OnLevelUp"/>).
    /// </summary>
    public void DebugDecreaseAllSkillsOneLevel()
    {
        foreach (SkillType t in GetAllTrackedSkills())
        {
            var p = Get(t);
            if (p.level <= 1)
                continue;

            p.level--;
            p.xp = 0;
            OnSkillLevelDecreased?.Invoke(t, p.level);
        }
    }

    /// <summary>
    /// Debug / testing: each tracked skill gains one level. XP into the current level is cleared.
    /// Fires <see cref="OnLevelUp"/> per skill (same hook as normal progression).
    /// </summary>
    public void DebugIncreaseAllSkillsOneLevel()
    {
        foreach (SkillType t in GetAllTrackedSkills())
        {
            var p = Get(t);
            p.level++;
            p.xp = 0;
            OnLevelUp?.Invoke(t, p.level);
        }
    }

    private SkillProgress Get(SkillType type)
    {
        if (_skills.TryGetValue(type, out var p) && p != null)
            return p;

        var created = new SkillProgress();
        _skills[type] = created;
        return created;
    }

    public void SetActiveXpDisplay(SkillType skill, string source, int sourceXpPerGain = -1)
    {
        ActiveSkill = skill;
        ActiveSource = source ?? "";
        if (string.IsNullOrWhiteSpace(ActiveSource))
            ActiveSourceXpPerGain = -1;
        else
            ActiveSourceXpPerGain = sourceXpPerGain;
        OnActiveXpDisplayChanged?.Invoke(ActiveSkill, ActiveSource);
    }

    // -------------------------
    // Combat helper (your existing one)
    // -------------------------

    public SkillType GetCombatSkillFromCurrentWeapon(PlayerController player, CharacterStats stats)
    {
        var inv = player ? player.GetComponent<Inventory>() : null;
        var eq = player ? player.GetComponent<EquipmentManager>() : null;
        if (!inv || !eq) return SkillType.Melee;

        string mainId = eq.MainHandItemId;
        if (string.IsNullOrWhiteSpace(mainId)) return SkillType.Melee;

        var def = inv.GetItemDef(mainId);
        if (!def || !def.IsWeapon) return SkillType.Melee;

        return def.weaponStats.attackSkill switch
        {
            AttackSkill.Ranged => SkillType.Ranged,
            AttackSkill.Magic => SkillType.Magic,
            _ => SkillType.Melee
        };
    }

    // -------------------------
    // Save / Load
    // -------------------------

    public void SaveInto(SaveData data)
    {
        if (data == null) return;

        data.skills.Clear();
        foreach (var kv in _skills)
        {
            data.skills.Add(new SaveData.SkillSave
            {
                type = kv.Key,
                level = kv.Value.level,
                xp = kv.Value.xp
            });
        }

        // ✅ Save last XP display
        data.lastXpSkill = ActiveSkill;
        data.lastXpSource = ActiveSource;

        data.skillChoiceSelectionKeys.Clear();
        data.skillChoiceSelectionValues.Clear();
        foreach (var kv in _skillChoiceSelections)
        {
            data.skillChoiceSelectionKeys.Add(kv.Key);
            data.skillChoiceSelectionValues.Add(kv.Value);
        }

        data.skillAbilityRowPickKeys ??= new List<string>();
        data.skillAbilityRowPickValues ??= new List<int>();
        data.skillAbilityRowPickKeys.Clear();
        data.skillAbilityRowPickValues.Clear();
        foreach (var kv in _skillAbilityRowPicks)
        {
            data.skillAbilityRowPickKeys.Add(kv.Key);
            data.skillAbilityRowPickValues.Add(kv.Value);
        }
    }

    public void LoadFrom(SaveData data)
    {
        if (data == null) return;

        if (data.skills == null || data.skills.Count == 0)
        {
            BuildDefaultsIfEmpty();
        }
        else
        {
            _skills.Clear();
            foreach (var s in data.skills)
            {
                _skills[s.type] = new SkillProgress
                {
                    level = Mathf.Max(1, s.level),
                    xp = Mathf.Max(0, s.xp)
                };
            }
        }

        // Restore which skill the bar tracks; omit source on load (no meaningful context until a node/menu sets it).
        ActiveSkill = data.lastXpSkill;
        ActiveSource = "";
        ActiveSourceXpPerGain = -1;

        _skillChoiceSelections.Clear();
        if (data.skillChoiceSelectionKeys != null && data.skillChoiceSelectionValues != null)
        {
            int count = Mathf.Min(data.skillChoiceSelectionKeys.Count, data.skillChoiceSelectionValues.Count);
            for (int i = 0; i < count; i++)
            {
                string key = data.skillChoiceSelectionKeys[i];
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                _skillChoiceSelections[key] = Mathf.Max(0, data.skillChoiceSelectionValues[i]);
            }
        }

        _skillAbilityRowPicks.Clear();
        if (data.skillAbilityRowPickKeys != null && data.skillAbilityRowPickValues != null)
        {
            int ac = Mathf.Min(data.skillAbilityRowPickKeys.Count, data.skillAbilityRowPickValues.Count);
            for (int i = 0; i < ac; i++)
            {
                string key = data.skillAbilityRowPickKeys[i];
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                _skillAbilityRowPicks[key] = Mathf.Max(0, data.skillAbilityRowPickValues[i]);
            }
        }

        // ✅ Push UI refresh immediately after load
        OnActiveXpDisplayChanged?.Invoke(ActiveSkill, ActiveSource);
    }
}