using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Enhancement choice icon button for the details panel (icon + level + name).</summary>
[DisallowMultipleComponent]
public sealed class SkillNodeDetailsEnhancementCardUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image highlightFrame;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text levelTagText;
    [SerializeField] private TMP_Text nameText;

    private static readonly Color NormalFrame = new(0.15f, 0.13f, 0.11f, 1f);
    private static readonly Color PreviewFrame = new(0.28f, 0.22f, 0.14f, 1f);
    private static readonly Color CommittedFrame = new(0.14f, 0.24f, 0.16f, 1f);
    private static readonly Color PreviewBorder = new(0.75f, 0.62f, 0.32f, 1f);
    private static readonly Color CommittedBorder = new(0.35f, 0.85f, 0.45f, 1f);
    private static readonly Color NormalBorder = new(0.35f, 0.3f, 0.24f, 0.65f);

    private string _description;
    private int _choiceIndex;

    public event Action<SkillNodeDetailsEnhancementCardUI> Clicked;

    public int ChoiceIndex => _choiceIndex;

    public string Description => _description;

    private void Awake()
    {
        if (button != null)
            button.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    public void Configure(
        int choiceIndex,
        Sprite icon,
        string title,
        string description,
        string levelLabel,
        bool previewSelected,
        bool committedSelected)
    {
        _choiceIndex = choiceIndex;
        _description = description ?? string.Empty;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            iconImage.color = Color.white;
        }

        if (nameText != null)
            nameText.text = title ?? string.Empty;

        if (levelTagText != null)
        {
            bool hasLevel = !string.IsNullOrWhiteSpace(levelLabel);
            levelTagText.gameObject.SetActive(hasLevel);
            if (hasLevel)
                levelTagText.text = levelLabel;
        }

        ApplyHighlight(previewSelected, committedSelected);
    }

    public void SetHighlight(bool previewSelected, bool committedSelected) =>
        ApplyHighlight(previewSelected, committedSelected);

    private void ApplyHighlight(bool previewSelected, bool committedSelected)
    {
        if (highlightFrame == null)
            return;

        highlightFrame.color = committedSelected
            ? CommittedFrame
            : previewSelected
                ? PreviewFrame
                : NormalFrame;

        Outline outline = highlightFrame.GetComponent<Outline>();
        if (outline == null)
            outline = highlightFrame.gameObject.AddComponent<Outline>();

        outline.effectColor = committedSelected
            ? CommittedBorder
            : previewSelected
                ? PreviewBorder
                : NormalBorder;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;
        outline.enabled = true;
    }

    private void HandleClick() => Clicked?.Invoke(this);
}
