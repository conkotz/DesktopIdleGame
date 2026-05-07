using UnityEngine;

public class PlayerLevelUpListener : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private LevelUpEffect levelUpEffect;
    [SerializeField] private GoldPopupSpawner popupSpawner;

    [Header("Popup")]
    [SerializeField] private Vector3 popupWorldOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Popup Colours")]
    [SerializeField] private Color miningColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    [SerializeField] private Color woodcuttingColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    [SerializeField] private Color fishingColor = new Color(0.2f, 0.5f, 0.95f, 1f);
    [SerializeField] private Color meleeColor = new Color(0.95f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color rangedColor = new Color(0.1f, 0.5f, 0.1f, 1f);
    [SerializeField] private Color magicColor = new Color(0.1f, 0.2f, 0.7f, 1f);

    [Header("Skill Tree Unlock Logging")]
    [Tooltip("When true, on level-up append newly unlocked skill tree nodes to the activity log.")]
    [SerializeField] private bool logUnlockedSkillTreeNodesOnLevelUp = true;
    [Tooltip("Optional. If empty, resolves via SkillDatabase.LoadDefault().")]
    [SerializeField] private SkillDatabase skillDatabase;

    private void Awake()
    {
        if (!levelUpEffect)
            levelUpEffect = GetComponentInChildren<LevelUpEffect>(true);

        if (!popupSpawner)
            popupSpawner = FindFirstObjectByType<GoldPopupSpawner>(FindObjectsInactive.Include);

        if (!skillDatabase)
            skillDatabase = SkillDatabase.LoadDefault();
    }

    private void Start()
    {
        if (SkillsManager.Instance != null)
            SkillsManager.Instance.OnLevelUp += HandleLevelUp;
        else
            Debug.LogWarning("PlayerLevelUpListener: SkillsManager.Instance is null in Start");
    }

    private void OnDestroy()
    {
        if (SkillsManager.Instance != null)
            SkillsManager.Instance.OnLevelUp -= HandleLevelUp;
    }

    private void HandleLevelUp(SkillType skill, int newLevel)
    {
        Debug.Log($"Listener received level up: {skill} -> {newLevel}");
        ShowLevelUp(skill, newLevel);
    }

    private void ShowLevelUp(SkillType skill, int newLevel)
    {
        if (levelUpEffect != null)
            levelUpEffect.PlayLevelUp();

        string message = $"{FormatSkillName(skill)} LEVEL UP !";
        if (logUnlockedSkillTreeNodesOnLevelUp)
        {
            string unlocked = BuildUnlockedNodesMessage(skill, newLevel);
            if (!string.IsNullOrWhiteSpace(unlocked))
                message = $"{message} {unlocked}";
        }
        GameLog.Add(message, GetColorForSkill(skill));
    }

    private static string FormatSkillName(SkillType skill)
    {
        return skill.ToString().ToUpperInvariant();
    }

    private Color GetColorForSkill(SkillType skill)
    {
        return skill switch
        {
            SkillType.Mining => miningColor,
            SkillType.Woodcutting => woodcuttingColor,
            SkillType.Fishing => fishingColor,
            SkillType.Melee => meleeColor,
            SkillType.Ranged => rangedColor,
            SkillType.Magic => magicColor,
            _ => Color.white
        };
    }

    private string BuildUnlockedNodesMessage(SkillType skillType, int newLevel)
    {
        if (newLevel <= 1)
            return "";

        if (!skillDatabase)
            skillDatabase = SkillDatabase.LoadDefault();

        SkillDefinition def = skillDatabase ? skillDatabase.Get(skillType) : null;
        if (def == null || def.unlocks == null || def.unlocks.Count == 0)
            return "";

        int oldLevel = Mathf.Max(1, newLevel - 1);
        var unlockedNow = new System.Collections.Generic.List<string>(4);

        for (int i = 0; i < def.unlocks.Count; i++)
        {
            SkillUnlockDefinition u = def.unlocks[i];
            if (u == null)
                continue;

            int req = Mathf.Max(1, u.requiredLevel);
            if (oldLevel < req && newLevel >= req)
            {
                string title = !string.IsNullOrWhiteSpace(u.title) ? u.title.Trim() : "Untitled";
                unlockedNow.Add($"{title} ({UnlockTypeLabel(u.unlockType)})");
            }

            // Choice nodes: appear later (usually +3 levels) unless overridden per choice.
            if (u.choices != null && u.choices.Count > 0)
            {
                int parentUnlockLevel = ResolveChoiceUnlockLevel(req, u.unlockType);
                for (int c = 0; c < u.choices.Count; c++)
                {
                    SkillChoiceDefinition choice = u.choices[c];
                    if (choice == null)
                        continue;

                    int choiceReq = choice.requiredLevel > 0 ? choice.requiredLevel : parentUnlockLevel;
                    if (oldLevel < choiceReq && newLevel >= choiceReq)
                    {
                        string ct = !string.IsNullOrWhiteSpace(choice.title)
                            ? choice.title.Trim()
                            : (!string.IsNullOrWhiteSpace(u.title) ? u.title.Trim() : "Untitled");
                        unlockedNow.Add($"{ct} (Enhancement)");
                    }
                }
            }
        }

        if (unlockedNow.Count <= 0)
            return "";

        // Keep it compact: "Unlocked: A (...), B (...)".
        string joined = string.Join(", ", unlockedNow);
        return $"Unlocked: {joined}";
    }

    private static int ResolveChoiceUnlockLevel(int sourceLevel, SkillUnlockType sourceType)
    {
        // Matches SkillTreeViewUI: capstone choice branches appear at 50; otherwise +3.
        if (sourceType == SkillUnlockType.CapstonePassive)
            return 50;
        return Mathf.Max(1, sourceLevel) + 3;
    }

    private static string UnlockTypeLabel(SkillUnlockType type)
    {
        return type switch
        {
            SkillUnlockType.MinorPassive => "Minor Passive",
            SkillUnlockType.MajorPassive => "Major Passive",
            SkillUnlockType.Unlock => "Unlock",
            SkillUnlockType.Ability => "Ability",
            SkillUnlockType.CapstonePassive => "Capstone",
            _ => "Node"
        };
    }
}