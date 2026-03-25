using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillListEntryUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Button button;
    [SerializeField] private Image background;

    [Header("State Colors")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.08f);
    [SerializeField] private Color selectedColor = new Color(1f, 1f, 1f, 0.22f);

    public SkillDefinition Definition { get; private set; }

    private SkillsManager _skillsManager;
    private SkillsAbilitiesPageUI _page;

    public void Bind(SkillDefinition def, SkillsManager skillsManager, SkillsAbilitiesPageUI page)
    {
        Definition = def;
        _skillsManager = skillsManager;
        _page = page;

        if (icon)
        {
            icon.sprite = def.icon;
            icon.enabled = def.icon != null;
        }

        if (nameText)
            nameText.text = string.IsNullOrWhiteSpace(def.displayName) ? def.skillType.ToString() : def.displayName;

        if (levelText)
        {
            int level = _skillsManager ? _skillsManager.GetLevel(def.skillType) : 1;
            levelText.text = $"Lv {level}";
        }

        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClicked);
        }

        SetSelected(false);
    }

    private void OnClicked()
    {
        _page?.SelectSkill(Definition);
    }

    public void SetSelected(bool selected)
    {
        if (background)
            background.color = selected ? selectedColor : normalColor;
    }
}