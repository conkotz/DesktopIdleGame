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

    // ✅ Active XP display (drives XP bar label + color)
    public event Action<SkillType, string> OnActiveXpDisplayChanged;
    public SkillType ActiveSkill { get; private set; } = SkillType.Mining;
    public string ActiveSource { get; private set; } = "";
    /// <summary>XP per gain from the current source (e.g. node xpPerTick). -1 when unknown or N/A.</summary>
    public int ActiveSourceXpPerGain { get; private set; } = -1;

    // Hook this to your XP bar
    public event Action<SkillType, int, string> OnXpGained; // (skill, amount, source)
    public event Action<SkillType, int> OnLevelUp;          // (skill, newLevel)

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

        // ✅ Push UI refresh immediately after load
        OnActiveXpDisplayChanged?.Invoke(ActiveSkill, ActiveSource);
    }
}