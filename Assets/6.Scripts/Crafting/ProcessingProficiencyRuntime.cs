using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Persistent crafting proficiencies (smelting, cooking placeholder). Simpler than gather/combat skill trees.</summary>
[DisallowMultipleComponent]
public sealed class ProcessingProficiencyRuntime : MonoBehaviour, ISaveable
{
    private static ProcessingProficiencyRuntime _instance;

    private const float ActiveWorkCooldownSeconds = 3f;

    private readonly Dictionary<ProcessingSkillType, SkillState> _skills = new();
    private float _nextActiveWorkUnscaledTime;

    public static ProcessingProficiencyRuntime Instance => _instance;

    public event Action Changed;

    public static ProcessingProficiencyRuntime EnsureInstance()
    {
        if (_instance != null)
            return _instance;

        var existing = FindFirstObjectByType<ProcessingProficiencyRuntime>(FindObjectsInactive.Include);
        if (existing != null)
        {
            _instance = existing;
            return _instance;
        }

        var host = new GameObject(nameof(ProcessingProficiencyRuntime));
        _instance = host.AddComponent<ProcessingProficiencyRuntime>();
        DontDestroyOnLoad(host);
        return _instance;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _instance = null;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureSkill(ProcessingSkillType.Smelting);
        EnsureSkill(ProcessingSkillType.Cooking);
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    public int GetLevel(ProcessingSkillType type) => EnsureSkill(type).Level;

    public int GetXp(ProcessingSkillType type) => EnsureSkill(type).Xp;

    public int GetXpToNextLevel(ProcessingSkillType type)
    {
        SkillState state = EnsureSkill(type);
        return state.Level >= ProcessingSkillCurves.MaxLevel
            ? 0
            : ProcessingSkillCurves.XpToNextLevel(state.Level);
    }

    public float GetProgress01(ProcessingSkillType type)
    {
        SkillState state = EnsureSkill(type);
        if (state.Level >= ProcessingSkillCurves.MaxLevel)
            return 1f;

        int needed = ProcessingSkillCurves.XpToNextLevel(state.Level);
        return needed > 0 ? Mathf.Clamp01(state.Xp / (float)needed) : 0f;
    }

    public SmeltingProficiencyBonuses GetSmeltingBonuses() =>
        SmeltingProficiencyBonuses.ForLevel(GetLevel(ProcessingSkillType.Smelting));

    public float GetActiveWorkCooldownRemaining() =>
        Mathf.Max(0f, _nextActiveWorkUnscaledTime - Time.unscaledTime);

    public bool TryApplyActiveWork(FurnaceSmeltingRuntime.FurnaceRow row, out string failureReason)
    {
        failureReason = null;
        if (row == null || !row.IsSmelting)
        {
            failureReason = "No smelting in progress.";
            return false;
        }

        if (Time.unscaledTime < _nextActiveWorkUnscaledTime)
        {
            failureReason = "Speed Up on cooldown.";
            return false;
        }

        float reduction = GetSmeltingBonuses().ActiveWorkSecondsPerClick;
        if (row.TickSmelting(reduction))
            RequestSaveDebounced();

        _nextActiveWorkUnscaledTime = Time.unscaledTime + ActiveWorkCooldownSeconds;
        Changed?.Invoke();
        return true;
    }

    public void AddSmeltingBarXp(SmeltingRecipe recipe) =>
        AddXp(ProcessingSkillType.Smelting, ProcessingSkillCurves.GetSmeltingBarXp(recipe));

    public void AddXp(ProcessingSkillType type, int amount)
    {
        if (amount <= 0)
            return;

        SkillState state = EnsureSkill(type);
        if (state.Level >= ProcessingSkillCurves.MaxLevel)
            return;

        state.Xp += amount;
        bool leveled = false;

        while (state.Level < ProcessingSkillCurves.MaxLevel)
        {
            int needed = ProcessingSkillCurves.XpToNextLevel(state.Level);
            if (state.Xp < needed)
                break;

            state.Xp -= needed;
            state.Level++;
            leveled = true;

            if (type == ProcessingSkillType.Smelting)
                GameLog.Add($"Smelting level {state.Level}!", GameLog.LevelAvailableColor);
        }

        if (state.Level >= ProcessingSkillCurves.MaxLevel)
            state.Xp = 0;

        if (leveled || amount > 0)
        {
            Changed?.Invoke();
            RequestSaveDebounced();
        }
    }

    public void SaveInto(SaveData data)
    {
        if (data == null)
            return;

        data.processingProficiency ??= new List<SaveData.ProcessingProficiencySave>();
        data.processingProficiency.Clear();

        foreach (KeyValuePair<ProcessingSkillType, SkillState> kv in _skills)
        {
            SkillState state = kv.Value;
            if (state == null)
                continue;

            data.processingProficiency.Add(new SaveData.ProcessingProficiencySave
            {
                skillType = (int)kv.Key,
                level = state.Level,
                xp = state.Xp
            });
        }
    }

    public void LoadFrom(SaveData data)
    {
        _skills.Clear();
        EnsureSkill(ProcessingSkillType.Smelting);
        EnsureSkill(ProcessingSkillType.Cooking);

        if (data?.processingProficiency == null)
            return;

        for (int i = 0; i < data.processingProficiency.Count; i++)
        {
            SaveData.ProcessingProficiencySave row = data.processingProficiency[i];
            if (row == null)
                continue;

            if (!Enum.IsDefined(typeof(ProcessingSkillType), row.skillType))
                continue;

            var type = (ProcessingSkillType)row.skillType;
            SkillState state = EnsureSkill(type);
            state.Level = Mathf.Clamp(row.level, 1, ProcessingSkillCurves.MaxLevel);
            state.Xp = Mathf.Max(0, Mathf.RoundToInt(row.xp));
            if (state.Level >= ProcessingSkillCurves.MaxLevel)
                state.Xp = 0;
        }

        Changed?.Invoke();
    }

    private SkillState EnsureSkill(ProcessingSkillType type)
    {
        if (!_skills.TryGetValue(type, out SkillState state) || state == null)
        {
            state = new SkillState();
            _skills[type] = state;
        }

        return state;
    }

    private static void RequestSaveDebounced()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.NotifyInventoryChangedDebounced();
    }

    private sealed class SkillState
    {
        public int Level = 1;
        public int Xp;
    }
}
