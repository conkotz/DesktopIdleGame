using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// One row in the Skills &amp; Abilities left panel: icon, name, level, selection, click.
/// </summary>
public class SkillListEntryUI : MonoBehaviour
{
    [Header("Row")]
    [Tooltip("Skill icon.")]
    [SerializeField] private Image icon;

    [Tooltip("Display name line.")]
    [SerializeField] private TMP_Text nameText;

    [Tooltip("Level line (e.g. Lv 12).")]
    [SerializeField] private TMP_Text levelText;

    [Header("XP Bar")]
    [Tooltip("XP bar fill image (uses Image.fillAmount).")]
    [SerializeField] private Image xpBarFill;

    [Tooltip("Whole-row click target.")]
    [SerializeField] private Button button;

    [Header("Selection")]
    [FormerlySerializedAs("background")]
    [Tooltip("Row highlight tint; script sets color from the fields below.")]
    [SerializeField] private Image selectionBackground;

    [Header("Selection colours")]
    [SerializeField] private Color normalColor = new Color(0.02f, 0.02f, 0.04f, 0.52f);
    [SerializeField] private Color selectedColor = new Color(0.07f, 0.09f, 0.13f, 0.58f);

    private SkillDefinition _definition;
    private Action<SkillDefinition> _onClicked;

    public SkillDefinition Definition => _definition;

    private void Awake()
    {
        if (!button)
            button = GetComponent<Button>();

        // Keep Button clicks/hover/press, but stop EventSystem "selected" focus from tinting only one row
        // (Selected Color on a list of Buttons). Selection visuals use selectionBackground + SetSelected instead.
        if (button)
        {
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            if (button.transition == Selectable.Transition.ColorTint)
            {
                ColorBlock cb = button.colors;
                cb.selectedColor = cb.normalColor;
                button.colors = cb;
            }
        }
    }

    public void Setup(
        SkillDefinition definition,
        int level,
        float progress01,
        bool selected,
        Action<SkillDefinition> onClicked)
    {
        _definition = definition;
        _onClicked = onClicked;

        if (!_definition)
            return;

        if (icon)
        {
            icon.sprite = _definition.icon;
            icon.enabled = _definition.icon != null;
        }

        if (nameText)
            nameText.text = string.IsNullOrWhiteSpace(_definition.displayName)
                ? _definition.skillType.ToString()
                : _definition.displayName;

        SetLevel(level);
        SetProgress(progress01);
        SetSelected(selected);

        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleClicked);
        }
    }

    public void SetLevel(int level)
    {
        if (levelText)
            levelText.text = $"Lv {Mathf.Max(1, level)}";
    }

    public void SetProgress(float progress01)
    {
        if (!xpBarFill) return;
        xpBarFill.fillAmount = Mathf.Clamp01(progress01);
    }

    public void SetSelected(bool isSelected)
    {
        if (!selectionBackground)
            return;
        selectionBackground.gameObject.SetActive(true);
        selectionBackground.color = isSelected ? selectedColor : normalColor;
    }

    private void HandleClicked()
    {
        if (_definition != null)
            _onClicked?.Invoke(_definition);
    }
}
