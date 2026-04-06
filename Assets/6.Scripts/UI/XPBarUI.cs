using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class XPBarUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image fill;
    [SerializeField] private TMP_Text label;

    [Header("Behavior")]
    [Tooltip("If true, bar always shows SkillsManager.ActiveSkill/ActiveSource.")]
    [SerializeField] private bool followActiveDisplay = true;

    [Tooltip("Used only if followActiveDisplay = false.")]
    [SerializeField] private SkillType fixedSkill = SkillType.Mining;

    [Header("Skill Colours")]
    [SerializeField] private Color miningColor = new Color(0.8f, 0.8f, 0.8f, 1f);    // light grey
    [SerializeField] private Color woodcuttingColor = new Color(0.2f, 0.8f, 0.2f, 1f); // green
    [SerializeField] private Color fishingColor = new Color(0.2f, 0.5f, 0.95f, 1f);    // blue
    [SerializeField] private Color meleeColor = new Color(0.95f, 0.2f, 0.2f, 1f);      // red
    [SerializeField] private Color rangedColor = new Color(0.2f, 0.6f, 0.2f);
    [SerializeField] private Color magicColor = new Color(0.4f, 0.4f, 1f);
    [SerializeField] private Color enduranceColor = new Color(0.9f, 0.6f, 0.2f);
    private SkillType _currentSkill;

    private void Awake()
    {
        if (!fill) fill = transform.Find("Fill")?.GetComponent<Image>();
        if (!label) label = transform.Find("SourceText")?.GetComponent<TMP_Text>();

        _currentSkill = fixedSkill;
    }

    private void OnEnable()
    {
        var sm = SkillsManager.Instance;
        if (sm != null)
        {
            sm.OnXpGained += HandleXpGained;
            sm.OnLevelUp += HandleLevelUp;
            sm.OnActiveXpDisplayChanged += HandleActiveDisplayChanged;

            if (followActiveDisplay)
                _currentSkill = sm.ActiveSkill;
        }

        RefreshAll();
    }


    private void OnDisable()
    {
        var sm = SkillsManager.Instance;
        if (sm != null)
        {
            sm.OnXpGained -= HandleXpGained;
            sm.OnLevelUp -= HandleLevelUp;
            sm.OnActiveXpDisplayChanged -= HandleActiveDisplayChanged;
        }
    }

    // ✅ Called when you click a resource node (SetActiveXpDisplay in PlayerController)
    private void HandleActiveDisplayChanged(SkillType skill, string source)
    {
        if (!followActiveDisplay) return;
        if (skill == SkillType.Endurance) return;

        _currentSkill = skill;

        RefreshAll();
    }

    private void HandleXpGained(SkillType skill, int amount, string source)
    {
        // If we follow active display, SkillsManager already set ActiveSkill/Source in AddXp()
        if (!followActiveDisplay)
        {
            // If not following active display, only update if this skill is the fixed one
            if (_currentSkill != fixedSkill) _currentSkill = fixedSkill;
        }
        else
        {
            if (skill == SkillType.Endurance)
                return;

            _currentSkill = skill;
        }

        RefreshAll();
    }

    private void HandleLevelUp(SkillType skill, int newLevel)
    {
        if (!followActiveDisplay)
        {
            if (_currentSkill != fixedSkill) _currentSkill = fixedSkill;
        }
        else
        {
            _currentSkill = skill; // keep showing whichever just leveled / last active
        }

        RefreshAll();
    }

    private void RefreshAll()
    {
        var sm = SkillsManager.Instance;
        if (sm == null || fill == null) return;

        // Choose skill
        SkillType skill = followActiveDisplay ? _currentSkill : fixedSkill;

        int lvl = sm.GetLevel(skill);
        int cur = sm.GetXpIntoLevel(skill);
        int req = sm.GetXpRequiredThisLevel(skill);
        int remaining = Mathf.Max(0, req - cur);

        // Bar fill + color
        fill.fillAmount = sm.GetProgress01(skill);
        fill.color = GetColorForSkill(skill);

        // Label: "Mining level: 9 80/489 (Stone deposit - 2xp)" — source after cur/req; omit when no context.
        if (label != null)
        {
            string skillName = SkillLabel(skill);
            string tail = BuildActiveSourceTail(FormatXpSourceDisplay(sm.ActiveSource), sm.ActiveSourceXpPerGain);
            label.text = $"{skillName} level: {lvl} {cur}/{req}{tail}";
        }
    }

    private static string SkillLabel(SkillType s)
    {
        if (s == SkillType.Endurance)
            return "Combat";
        return s.ToString();
    }

    private static string BuildActiveSourceTail(string sourceLabel, int xpPerGain)
    {
        if (string.IsNullOrEmpty(sourceLabel))
            return "";

        if (xpPerGain > 0)
            return $" ({sourceLabel} - {xpPerGain}xp)";

        return $" ({sourceLabel})";
    }

    /// <summary>Empty = hide. Maps legacy "Endurance" source label to "Combat".</summary>
    private static string FormatXpSourceDisplay(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        raw = raw.Trim();
        if (raw.Equals("Endurance", StringComparison.OrdinalIgnoreCase))
            return "Combat";

        return raw;
    }

    private Color GetColorForSkill(SkillType s)
    {
        return s switch
        {
            SkillType.Mining => miningColor,
            SkillType.Woodcutting => woodcuttingColor,
            SkillType.Fishing => fishingColor,
            SkillType.Melee => meleeColor,
            SkillType.Ranged => rangedColor,
            SkillType.Magic => magicColor,
            SkillType.Endurance => enduranceColor,
            _ => miningColor
        };
    }
}