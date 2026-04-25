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

    private void Awake()
    {
        if (!levelUpEffect)
            levelUpEffect = GetComponentInChildren<LevelUpEffect>(true);

        if (!popupSpawner)
            popupSpawner = FindFirstObjectByType<GoldPopupSpawner>(FindObjectsInactive.Include);
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
        ShowLevelUp(skill);
    }

    private void ShowLevelUp(SkillType skill)
    {
        if (levelUpEffect != null)
            levelUpEffect.PlayLevelUp();

        string message = $"{FormatSkillName(skill)} LEVEL UP !";
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
}