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

    public const int AbilityPresetSlotCount = 2;
    public const int AbilityPresetMaxDisplayNameLength = 12;

    private readonly Dictionary<SkillType, SkillAbilityPresetSave[]> _abilityPresetsBySkill = new();

    /// <summary>Last loaded preset slot per skill; highlight only while the live tree still matches that snapshot.</summary>
    private readonly Dictionary<SkillType, int> _activeAbilityPresetSlotBySkill = new();

    private readonly SkillAbilityPresetWeaponSetLinkSave[] _weaponSetPresetLinks =
    {
        new SkillAbilityPresetWeaponSetLinkSave(),
        new SkillAbilityPresetWeaponSetLinkSave()
    };

    public const int WeaponSetPresetLinkCount = 2;

    public event Action OnAbilityPresetWeaponSetLinksChanged;

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

    /// <summary>Fired after <see cref="LoadFrom"/> applies saved levels/XP (and related choice data) to runtime progression.</summary>
    public event Action OnSkillProgressionLoaded;

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);

        BuildDefaultsIfEmpty();

        if (GetComponent<AbilityPresetWeaponSetLabelUI>() == null)
            gameObject.AddComponent<AbilityPresetWeaponSetLabelUI>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
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

    public int XpToNextLevel(int currentLevel) => SkillCurves.XpToNextLevel(currentLevel);

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

    private static string BuildChoiceKeyLegacyLevel(SkillType skillType, int sourceLevel)
    {
        return $"{skillType}:{Mathf.Max(1, sourceLevel)}";
    }

    /// <summary>Choice storage keyed by the parent spine node id (e.g. <c>Lv15_2</c>) so multiple branches at the same level do not collide.</summary>
    public static string BuildChoiceKeyFromParentSpine(SkillType skillType, string parentSpineNodeId)
    {
        return $"{skillType}:{parentSpineNodeId}";
    }

    /// <summary>
    /// Sets the active choice for a branch. Pass <paramref name="choiceIndex"/> &lt; 0 to clear (no selection).
    /// </summary>
    public void SetSkillChoiceSelection(SkillType skillType, int sourceLevel, int choiceIndex)
    {
        string key = BuildChoiceKeyLegacyLevel(skillType, sourceLevel);
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

    /// <summary>
    /// Sets the active choice for a specific parent spine row (preferred for multi-branch levels like Woodcutting Lv15).
    /// </summary>
    public void SetSkillChoiceSelection(SkillType skillType, string parentSpineNodeId, int choiceIndex)
    {
        if (string.IsNullOrWhiteSpace(parentSpineNodeId))
            return;

        string key = BuildChoiceKeyFromParentSpine(skillType, parentSpineNodeId.Trim());
        int src = 1;
        if (TryParseSourceLevelFromChoiceStorageKey(key, out int parsedLvl))
            src = parsedLvl;

        if (choiceIndex < 0)
        {
            bool removed = _skillChoiceSelections.Remove(key);
            if (parentSpineNodeId.IndexOf('_') > 0 && TryParseSourceLevelFromChoiceStorageKey(key, out int lvlLeg))
                removed |= _skillChoiceSelections.Remove(BuildChoiceKeyLegacyLevel(skillType, lvlLeg));
            if (!removed)
                return;
            OnSkillChoiceSelectionChanged?.Invoke(skillType, src, -1);
            return;
        }

        int clamped = Mathf.Max(0, choiceIndex);
        if (_skillChoiceSelections.TryGetValue(key, out int existing) && existing == clamped)
            return;

        // Remove legacy shared-level key so old saves / UI paths cannot return the wrong branch's pick.
        if (TryParseSourceLevelFromChoiceStorageKey(key, out int lvlForLegacy) && parentSpineNodeId.IndexOf('_') > 0)
            _skillChoiceSelections.Remove(BuildChoiceKeyLegacyLevel(skillType, lvlForLegacy));

        _skillChoiceSelections[key] = clamped;
        OnSkillChoiceSelectionChanged?.Invoke(skillType, src, clamped);
    }

    public int GetSkillChoiceSelection(SkillType skillType, int sourceLevel, int defaultValue = -1)
    {
        string key = BuildChoiceKeyLegacyLevel(skillType, sourceLevel);
        if (_skillChoiceSelections.TryGetValue(key, out int value))
            return value;
        // Spine keys (e.g. Melee:Lv15_1) — legacy callers pass level only; return first populated slot.
        int lvl = Mathf.Max(1, sourceLevel);
        for (int slot = 0; slot < 8; slot++)
        {
            string sk = BuildChoiceKeyFromParentSpine(skillType, $"Lv{lvl}_{slot}");
            if (_skillChoiceSelections.TryGetValue(sk, out int v))
                return v;
        }

        return defaultValue;
    }

    /// <summary>Reads the choice for a specific parent spine row, falling back to legacy level-only keys when present.</summary>
    public int GetSkillChoiceSelection(SkillType skillType, string parentSpineNodeId, int defaultValue = -1)
    {
        if (string.IsNullOrWhiteSpace(parentSpineNodeId))
            return defaultValue;

        string spineKey = BuildChoiceKeyFromParentSpine(skillType, parentSpineNodeId.Trim());
        if (_skillChoiceSelections.TryGetValue(spineKey, out int v))
            return v;

        if (TryParseSourceLevelFromChoiceStorageKey(spineKey, out int lvl))
        {
            string legacy = BuildChoiceKeyLegacyLevel(skillType, lvl);
            if (_skillChoiceSelections.TryGetValue(legacy, out int legacyVal))
                return legacyVal;
        }

        return defaultValue;
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
        if (!TryParseChoiceEventFromStorageKey(key, out SkillType st, out int lvl))
            return;
        OnSkillChoiceSelectionChanged?.Invoke(st, Mathf.Max(1, lvl), -1);
    }

    private static bool TryParseChoiceEventFromStorageKey(string key, out SkillType skillType, out int sourceLevel)
    {
        skillType = default;
        sourceLevel = 1;
        int idx = key.IndexOf(':');
        if (idx <= 0 || idx >= key.Length - 1)
            return false;
        if (!Enum.TryParse(key.Substring(0, idx), out skillType))
            return false;
        string rest = key.Substring(idx + 1);
        if (int.TryParse(rest, out int legacyLvl))
        {
            sourceLevel = Mathf.Max(1, legacyLvl);
            return true;
        }

        // Spine id: "Lv15_2" → event level 15
        if (rest.Length >= 4 && rest.StartsWith("Lv", StringComparison.Ordinal))
        {
            int u = rest.IndexOf('_');
            if (u > 2 && int.TryParse(rest.Substring(2, u - 2), out int spineLvl))
            {
                sourceLevel = Mathf.Max(1, spineLvl);
                return true;
            }
        }

        return false;
    }

    /// <summary>Parses level from keys like <c>Melee:Lv10_0</c> or legacy <c>Melee:10</c>.</summary>
    public static bool TryParseSourceLevelFromChoiceStorageKey(string key, out int level)
    {
        level = 1;
        int idx = key.IndexOf(':');
        if (idx <= 0 || idx >= key.Length - 1)
            return false;
        string rest = key.Substring(idx + 1);
        if (int.TryParse(rest, out int legacyLvl))
        {
            level = Mathf.Max(1, legacyLvl);
            return true;
        }

        if (rest.Length >= 4 && rest.StartsWith("Lv", StringComparison.Ordinal))
        {
            int u = rest.IndexOf('_');
            if (u > 2 && int.TryParse(rest.Substring(2, u - 2), out int spineLvl))
            {
                level = Mathf.Max(1, spineLvl);
                return true;
            }
        }

        return false;
    }

    /// <summary>Clears every passive-branch choice and every multi-ability row pick (global reset).</summary>
    public void ResetAllSkillTreeSelections()
    {
        ClearAllSkillChoiceSelections();
        ClearAllSkillAbilityRowPicks();
    }

    /// <summary>Clears passive-branch choices for one skill only (keys are prefixed with <c>SkillType:</c>).</summary>
    public void ClearSkillChoiceSelectionsForSkill(SkillType skillType)
    {
        string prefix = $"{skillType}:";
        var toRemove = new List<string>();
        foreach (var kv in _skillChoiceSelections)
        {
            if (kv.Key.StartsWith(prefix, StringComparison.Ordinal))
                toRemove.Add(kv.Key);
        }

        foreach (string k in toRemove)
        {
            _skillChoiceSelections.Remove(k);
            TryInvokeChoiceClearFromKey(k);
        }
    }

    /// <summary>Clears choice branches and multi-ability row picks for a single skill (used by Reset Tree on the skill tree UI).</summary>
    public void ResetSkillTreeSelectionsForSkill(SkillType skillType)
    {
        ClearSkillChoiceSelectionsForSkill(skillType);
        ClearSkillAbilityRowPicksForSkill(skillType);
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

    /// <summary>
    /// Clears the committed sibling pick for one required level (fires <see cref="OnSkillAbilityRowPickChanged"/> with pick -1 if a pick existed).
    /// Used when resetting a single skill-tree row without wiping the whole skill.
    /// </summary>
    public void ClearSkillAbilityRowPickForLevel(SkillType skillType, int requiredLevel)
    {
        string key = BuildAbilityRowKey(skillType, requiredLevel);
        if (_skillAbilityRowPicks.Remove(key))
            TryInvokeAbilityRowClearFromKey(key);
    }

    /// <summary>Clears the passive-branch choice for one parent spine row (same semantics as <see cref="SetSkillChoiceSelection"/> with a negative index).</summary>
    public void ClearSkillChoiceSelectionForParentSpine(SkillType skillType, string parentSpineNodeId)
    {
        if (string.IsNullOrWhiteSpace(parentSpineNodeId))
            return;
        SetSkillChoiceSelection(skillType, parentSpineNodeId.Trim(), -1);
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
            SkillsAbilitiesColdStartLevelUpGlow.Append(type, p.level);
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
            SkillsAbilitiesColdStartLevelUpGlow.Append(t, p.level);
            OnLevelUp?.Invoke(t, p.level);
        }
    }

    /// <summary>Dev/testing: set every tracked skill to <paramref name="targetLevel"/> (clamped 1–<paramref name="maxLevel"/>), XP into level cleared.</summary>
    public void DebugSetAllTrackedSkillsLevel(int targetLevel, int maxLevel = 50)
    {
        targetLevel = Mathf.Clamp(targetLevel, 1, Mathf.Max(1, maxLevel));
        foreach (SkillType t in GetAllTrackedSkills())
        {
            var p = Get(t);
            int old = p.level;
            if (old == targetLevel)
            {
                p.xp = 0;
                continue;
            }

            p.level = targetLevel;
            p.xp = 0;
            if (targetLevel > old)
            {
                SkillsAbilitiesColdStartLevelUpGlow.Append(t, p.level);
                OnLevelUp?.Invoke(t, p.level);
            }
            else
            {
                OnSkillLevelDecreased?.Invoke(t, p.level);
            }
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
    // Ability presets (skills page)
    // -------------------------

    public static string GetAbilityPresetDefaultDisplayName(int slotIndex) =>
        $"Preset {Mathf.Clamp(slotIndex + 1, 1, AbilityPresetSlotCount)}";

    public string GetAbilityPresetResolvedDisplayName(SkillType skillType, int slotIndex)
    {
        SkillAbilityPresetSave preset = GetAbilityPresetSlot(skillType, slotIndex);
        if (!string.IsNullOrWhiteSpace(preset.displayName))
            return SanitizeAbilityPresetDisplayName(preset.displayName);
        return GetAbilityPresetDefaultDisplayName(slotIndex);
    }

    public bool AbilityPresetHasSnapshot(SkillType skillType, int slotIndex) =>
        GetAbilityPresetSlot(skillType, slotIndex).hasSnapshot;

    public SkillAbilityPresetSave GetAbilityPresetSlotSnapshot(SkillType skillType, int slotIndex) =>
        GetAbilityPresetSlot(skillType, slotIndex);

    public void SaveCurrentSkillTreeToAbilityPreset(SkillType skillType, int slotIndex)
    {
        SkillAbilityPresetSave preset = GetAbilityPresetSlot(skillType, slotIndex);
        preset.hasSnapshot = true;
        CopySkillFilteredDictionaryToLists(_skillChoiceSelections, skillType, preset.choiceKeys, preset.choiceValues, abilityRowsOnly: false);
        CopySkillFilteredDictionaryToLists(_skillAbilityRowPicks, skillType, preset.abilityRowKeys, preset.abilityRowValues, abilityRowsOnly: true);
        _activeAbilityPresetSlotBySkill[skillType] = Mathf.Clamp(slotIndex, 0, AbilityPresetSlotCount - 1);
    }

    public bool TryLoadAbilityPreset(SkillType skillType, int slotIndex)
    {
        SkillAbilityPresetSave preset = GetAbilityPresetSlot(skillType, slotIndex);
        if (!preset.hasSnapshot)
            return false;

        ApplyAbilityPresetSnapshotForSkill(skillType, preset);
        _activeAbilityPresetSlotBySkill[skillType] = Mathf.Clamp(slotIndex, 0, AbilityPresetSlotCount - 1);
        return true;
    }

    public bool IsAbilityPresetActiveAndMatching(SkillType skillType, int slotIndex)
    {
        if (!_activeAbilityPresetSlotBySkill.TryGetValue(skillType, out int activeSlot))
            return false;
        if (activeSlot != Mathf.Clamp(slotIndex, 0, AbilityPresetSlotCount - 1))
            return false;

        return DoesCurrentSkillTreeMatchPreset(skillType, slotIndex);
    }

    public bool TryGetActiveAbilityPresetSlot(SkillType skillType, out int slotIndex)
    {
        if (_activeAbilityPresetSlotBySkill.TryGetValue(skillType, out int active))
        {
            slotIndex = Mathf.Clamp(active, 0, AbilityPresetSlotCount - 1);
            return true;
        }

        slotIndex = -1;
        return false;
    }

    public bool DoesCurrentSkillTreeMatchPreset(SkillType skillType, int slotIndex)
    {
        SkillAbilityPresetSave saved = GetAbilityPresetSlot(skillType, slotIndex);
        if (!saved.hasSnapshot)
            return false;

        var current = new SkillAbilityPresetSave();
        CopySkillFilteredDictionaryToLists(_skillChoiceSelections, skillType, current.choiceKeys, current.choiceValues, abilityRowsOnly: false);
        CopySkillFilteredDictionaryToLists(_skillAbilityRowPicks, skillType, current.abilityRowKeys, current.abilityRowValues, abilityRowsOnly: true);

        return PresetSelectionListsEqual(saved.choiceKeys, saved.choiceValues, current.choiceKeys, current.choiceValues)
            && PresetSelectionListsEqual(saved.abilityRowKeys, saved.abilityRowValues, current.abilityRowKeys, current.abilityRowValues);
    }

    public void ClearActiveAbilityPresetForSkill(SkillType skillType)
    {
        _activeAbilityPresetSlotBySkill.Remove(skillType);
    }

    public void ResetAbilityPreset(SkillType skillType, int slotIndex)
    {
        SkillAbilityPresetSave preset = GetAbilityPresetSlot(skillType, slotIndex);
        preset.hasSnapshot = false;
        preset.displayName = string.Empty;
        preset.choiceKeys.Clear();
        preset.choiceValues.Clear();
        preset.abilityRowKeys.Clear();
        preset.abilityRowValues.Clear();
        ClearWeaponSetLinksForPreset(skillType, slotIndex);
        if (_activeAbilityPresetSlotBySkill.TryGetValue(skillType, out int activeSlot) && activeSlot == slotIndex)
            _activeAbilityPresetSlotBySkill.Remove(skillType);
        NotifyWeaponSetPresetLinksChanged();
    }

    public void SetAbilityPresetDisplayName(SkillType skillType, int slotIndex, string displayName)
    {
        SkillAbilityPresetSave preset = GetAbilityPresetSlot(skillType, slotIndex);
        preset.displayName = SanitizeAbilityPresetDisplayName(displayName);
    }

    public static bool IsCombatSkillType(SkillType skillType) =>
        skillType == SkillType.Melee
        || skillType == SkillType.Ranged
        || skillType == SkillType.Magic
        || skillType == SkillType.Endurance;

    public SkillAbilityPresetWeaponSetLinkSave GetWeaponSetPresetLink(int weaponSetIndex) =>
        _weaponSetPresetLinks[ClampWeaponSetIndex(weaponSetIndex)];

    public bool TryGetWeaponSetIndexForPreset(SkillType skillType, int presetSlotIndex, out int weaponSetIndex)
    {
        for (int i = 0; i < WeaponSetPresetLinkCount; i++)
        {
            SkillAbilityPresetWeaponSetLinkSave link = _weaponSetPresetLinks[i];
            if (!link.enabled)
                continue;
            if (link.skillType == skillType && link.presetSlotIndex == presetSlotIndex)
            {
                weaponSetIndex = i;
                return true;
            }
        }

        weaponSetIndex = -1;
        return false;
    }

    public void GetAbilityPresetWeaponSetAssignment(
        SkillType skillType,
        int presetSlotIndex,
        out bool enabled,
        out int weaponSetIndex)
    {
        if (TryGetWeaponSetIndexForPreset(skillType, presetSlotIndex, out int setIndex))
        {
            enabled = true;
            weaponSetIndex = setIndex;
            return;
        }

        enabled = false;
        weaponSetIndex = 0;
    }

    public void SetAbilityPresetWeaponSetAssignment(
        SkillType skillType,
        int presetSlotIndex,
        bool enabled,
        int weaponSetIndex)
    {
        if (!IsCombatSkillType(skillType))
            return;

        ClearWeaponSetLinksForPreset(skillType, presetSlotIndex);

        if (!enabled)
        {
            NotifyWeaponSetPresetLinksChanged();
            return;
        }

        int setIndex = ClampWeaponSetIndex(weaponSetIndex);
        ClearWeaponSetLinkAtIndex(setIndex);

        SkillAbilityPresetWeaponSetLinkSave link = _weaponSetPresetLinks[setIndex];
        link.enabled = true;
        link.skillType = skillType;
        link.presetSlotIndex = Mathf.Clamp(presetSlotIndex, 0, AbilityPresetSlotCount - 1);

        NotifyWeaponSetPresetLinksChanged();
    }

    public string GetWeaponSetPresetDisplayLabel(int weaponSetIndex)
    {
        SkillAbilityPresetWeaponSetLinkSave link = GetWeaponSetPresetLink(weaponSetIndex);
        if (!link.enabled)
            return "None Assigned";

        return GetAbilityPresetResolvedDisplayName(link.skillType, link.presetSlotIndex);
    }

    /// <summary>True when assigning the given preset to this weapon set would replace a different preset.</summary>
    public bool WillAssigningPresetOverwriteWeaponSet(
        int weaponSetIndex,
        SkillType skillType,
        int presetSlotIndex,
        out string existingPresetDisplayName)
    {
        SkillAbilityPresetWeaponSetLinkSave link = GetWeaponSetPresetLink(weaponSetIndex);
        if (!link.enabled)
        {
            existingPresetDisplayName = null;
            return false;
        }

        if (link.skillType == skillType && link.presetSlotIndex == presetSlotIndex)
        {
            existingPresetDisplayName = null;
            return false;
        }

        existingPresetDisplayName = GetAbilityPresetResolvedDisplayName(link.skillType, link.presetSlotIndex);
        return true;
    }

    public bool TryApplyLinkedPresetForWeaponSet(int weaponSetIndex, ActionBarUI actionBar = null)
    {
        SkillAbilityPresetWeaponSetLinkSave link = GetWeaponSetPresetLink(weaponSetIndex);
        if (!link.enabled || !IsCombatSkillType(link.skillType))
            return false;

        if (!TryLoadAbilityPreset(link.skillType, link.presetSlotIndex))
            return false;

        actionBar?.ApplyCombatLoadoutFromSkillRowPicks(link.skillType);
        return true;
    }

    private void ClearWeaponSetLinksForPreset(SkillType skillType, int presetSlotIndex)
    {
        for (int i = 0; i < WeaponSetPresetLinkCount; i++)
        {
            SkillAbilityPresetWeaponSetLinkSave link = _weaponSetPresetLinks[i];
            if (!link.enabled)
                continue;
            if (link.skillType == skillType && link.presetSlotIndex == presetSlotIndex)
                ClearWeaponSetLinkAtIndex(i);
        }
    }

    private void ClearWeaponSetLinkAtIndex(int weaponSetIndex)
    {
        SkillAbilityPresetWeaponSetLinkSave link = _weaponSetPresetLinks[ClampWeaponSetIndex(weaponSetIndex)];
        link.enabled = false;
        link.skillType = default;
        link.presetSlotIndex = 0;
    }

    private static int ClampWeaponSetIndex(int weaponSetIndex) =>
        Mathf.Clamp(weaponSetIndex, 0, WeaponSetPresetLinkCount - 1);

    private void NotifyWeaponSetPresetLinksChanged()
    {
        OnAbilityPresetWeaponSetLinksChanged?.Invoke();
        AbilityPresetWeaponSetLabelUI.RefreshAll();
    }

    private SkillAbilityPresetSave[] EnsurePresetSlotsForSkill(SkillType skillType)
    {
        if (!_abilityPresetsBySkill.TryGetValue(skillType, out SkillAbilityPresetSave[] slots))
        {
            slots = new SkillAbilityPresetSave[AbilityPresetSlotCount];
            for (int i = 0; i < AbilityPresetSlotCount; i++)
                slots[i] = new SkillAbilityPresetSave();
            _abilityPresetsBySkill[skillType] = slots;
        }

        return slots;
    }

    private SkillAbilityPresetSave GetAbilityPresetSlot(SkillType skillType, int slotIndex)
    {
        SkillAbilityPresetSave[] slots = EnsurePresetSlotsForSkill(skillType);
        return slots[Mathf.Clamp(slotIndex, 0, AbilityPresetSlotCount - 1)];
    }

    private static string BuildSkillSelectionKeyPrefix(SkillType skillType) => $"{skillType}:";

    private static void CopySkillFilteredDictionaryToLists(
        Dictionary<string, int> source,
        SkillType skillType,
        List<string> keys,
        List<int> values,
        bool abilityRowsOnly)
    {
        keys.Clear();
        values.Clear();
        if (source == null || source.Count == 0)
            return;

        string prefix = BuildSkillSelectionKeyPrefix(skillType);
        const string abilityRowMarker = ":abilityRow:";

        foreach (KeyValuePair<string, int> kv in source)
        {
            if (!kv.Key.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            bool isAbilityRow = kv.Key.IndexOf(abilityRowMarker, StringComparison.Ordinal) >= 0;
            if (abilityRowsOnly != isAbilityRow)
                continue;

            keys.Add(kv.Key);
            values.Add(kv.Value);
        }
    }

    private static void RemoveSkillKeysFromDictionary(Dictionary<string, int> dict, SkillType skillType)
    {
        if (dict == null || dict.Count == 0)
            return;

        string prefix = BuildSkillSelectionKeyPrefix(skillType);
        var toRemove = new List<string>();
        foreach (KeyValuePair<string, int> kv in dict)
        {
            if (kv.Key.StartsWith(prefix, StringComparison.Ordinal))
                toRemove.Add(kv.Key);
        }

        for (int i = 0; i < toRemove.Count; i++)
            dict.Remove(toRemove[i]);
    }

    private static void ApplyParallelListsToDictionary(
        List<string> keys,
        List<int> values,
        Dictionary<string, int> destination)
    {
        if (destination == null || keys == null || values == null)
            return;

        int count = Mathf.Min(keys.Count, values.Count);
        for (int i = 0; i < count; i++)
        {
            string key = keys[i];
            if (string.IsNullOrWhiteSpace(key))
                continue;
            destination[key] = values[i];
        }
    }

    private static string SanitizeAbilityPresetDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string trimmed = value.Trim();
        if (trimmed.Length > AbilityPresetMaxDisplayNameLength)
            trimmed = trimmed.Substring(0, AbilityPresetMaxDisplayNameLength);

        return trimmed.Replace("<", string.Empty).Replace(">", string.Empty);
    }

    private void ApplyAbilityPresetSnapshotForSkill(SkillType skillType, SkillAbilityPresetSave preset)
    {
        RemoveSkillKeysFromDictionary(_skillChoiceSelections, skillType);
        RemoveSkillKeysFromDictionary(_skillAbilityRowPicks, skillType);

        ApplyParallelListsToDictionary(preset.choiceKeys, preset.choiceValues, _skillChoiceSelections);
        ApplyParallelListsToDictionary(preset.abilityRowKeys, preset.abilityRowValues, _skillAbilityRowPicks);

        OnSkillProgressionLoaded?.Invoke();

        CharacterStats stats = FindFirstObjectByType<CharacterStats>(FindObjectsInactive.Exclude);
        stats?.NotifyStatsChanged();
    }

    private static void CopyPresetToSaveSlot(SkillAbilityPresetSave source, SkillAbilityPresetSave dest)
    {
        if (source == null || dest == null)
            return;

        dest.hasSnapshot = source.hasSnapshot;
        dest.displayName = source.displayName ?? string.Empty;
        CopyList(source.choiceKeys, dest.choiceKeys);
        CopyList(source.choiceValues, dest.choiceValues);
        CopyList(source.abilityRowKeys, dest.abilityRowKeys);
        CopyList(source.abilityRowValues, dest.abilityRowValues);
    }

    private static void CopyList<T>(List<T> source, List<T> dest)
    {
        dest.Clear();
        if (source == null || source.Count == 0)
            return;
        dest.AddRange(source);
    }

    private static bool PresetSelectionListsEqual(
        List<string> keysA,
        List<int> valuesA,
        List<string> keysB,
        List<int> valuesB)
    {
        int countA = Mathf.Min(keysA?.Count ?? 0, valuesA?.Count ?? 0);
        int countB = Mathf.Min(keysB?.Count ?? 0, valuesB?.Count ?? 0);
        if (countA != countB)
            return false;

        var lookupB = new Dictionary<string, int>();
        for (int i = 0; i < countB; i++)
        {
            string key = keysB[i];
            if (string.IsNullOrWhiteSpace(key))
                continue;
            lookupB[key] = valuesB[i];
        }

        for (int i = 0; i < countA; i++)
        {
            string key = keysA[i];
            if (string.IsNullOrWhiteSpace(key))
                continue;
            if (!lookupB.TryGetValue(key, out int valueB) || valueB != valuesA[i])
                return false;
        }

        return true;
    }

    private static void EnsurePresetGroupSlots(SkillAbilityPresetGroupSave group)
    {
        if (group == null)
            return;

        group.slots ??= new List<SkillAbilityPresetSave>();
        while (group.slots.Count < AbilityPresetSlotCount)
            group.slots.Add(new SkillAbilityPresetSave());
        while (group.slots.Count > AbilityPresetSlotCount)
            group.slots.RemoveAt(group.slots.Count - 1);
    }

    // -------------------------
    // Save / Load
    // -------------------------

    public void SaveInto(SaveData data)
    {
        if (data == null) return;

        data.skills ??= new List<SaveData.SkillSave>();
        data.skillChoiceSelectionKeys ??= new List<string>();
        data.skillChoiceSelectionValues ??= new List<int>();

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

        data.skillAbilityPresetGroups ??= new List<SkillAbilityPresetGroupSave>();
        data.skillAbilityPresetGroups.Clear();
        foreach (KeyValuePair<SkillType, SkillAbilityPresetSave[]> kv in _abilityPresetsBySkill)
        {
            var group = new SkillAbilityPresetGroupSave { skillType = kv.Key };
            for (int i = 0; i < AbilityPresetSlotCount; i++)
            {
                var slot = new SkillAbilityPresetSave();
                CopyPresetToSaveSlot(kv.Value[i], slot);
                group.slots.Add(slot);
            }

            data.skillAbilityPresetGroups.Add(group);
        }

        data.skillAbilityPresetWeaponSetLinks ??= new List<SkillAbilityPresetWeaponSetLinkSave>();
        data.skillAbilityPresetWeaponSetLinks.Clear();
        for (int i = 0; i < WeaponSetPresetLinkCount; i++)
        {
            SkillAbilityPresetWeaponSetLinkSave link = _weaponSetPresetLinks[i];
            data.skillAbilityPresetWeaponSetLinks.Add(new SkillAbilityPresetWeaponSetLinkSave
            {
                enabled = link.enabled,
                skillType = link.skillType,
                presetSlotIndex = link.presetSlotIndex
            });
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

        _abilityPresetsBySkill.Clear();
        if (data.skillAbilityPresetGroups != null && data.skillAbilityPresetGroups.Count > 0)
        {
            for (int g = 0; g < data.skillAbilityPresetGroups.Count; g++)
            {
                SkillAbilityPresetGroupSave group = data.skillAbilityPresetGroups[g];
                if (group == null)
                    continue;

                EnsurePresetGroupSlots(group);
                SkillAbilityPresetSave[] slots = EnsurePresetSlotsForSkill(group.skillType);
                for (int i = 0; i < AbilityPresetSlotCount; i++)
                    CopyPresetToSaveSlot(group.slots[i], slots[i]);
            }
        }

        for (int i = 0; i < WeaponSetPresetLinkCount; i++)
            ClearWeaponSetLinkAtIndex(i);

        if (data.skillAbilityPresetWeaponSetLinks != null)
        {
            int count = Mathf.Min(data.skillAbilityPresetWeaponSetLinks.Count, WeaponSetPresetLinkCount);
            for (int i = 0; i < count; i++)
            {
                SkillAbilityPresetWeaponSetLinkSave loaded = data.skillAbilityPresetWeaponSetLinks[i];
                if (loaded == null || !loaded.enabled || !IsCombatSkillType(loaded.skillType))
                    continue;

                SkillAbilityPresetWeaponSetLinkSave link = _weaponSetPresetLinks[i];
                link.enabled = true;
                link.skillType = loaded.skillType;
                link.presetSlotIndex = Mathf.Clamp(loaded.presetSlotIndex, 0, AbilityPresetSlotCount - 1);
            }
        }

        OnActiveXpDisplayChanged?.Invoke(ActiveSkill, ActiveSource);
        MagicStarterSpellRules.TryEnsureDefaultStarterSpellCommitted(this);
        OnSkillProgressionLoaded?.Invoke();
        NotifyWeaponSetPresetLinksChanged();
    }
}