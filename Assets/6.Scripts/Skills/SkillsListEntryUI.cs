using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// One row in the Skills &amp; Abilities left panel: icon, name, level, selection, click.
/// </summary>
public class SkillListEntryUI : MonoBehaviour, IPointerEnterHandler
{
    [Header("Row")]
    [Tooltip("Drag a sprite asset here (SkillsPageIcons, etc.). Applied to Icon Image at runtime.")]
    [SerializeField] private Sprite skillIconSprite;

    [Tooltip("Icon Image target that displays skillIconSprite.")]
    [SerializeField] private Image icon;

    [Tooltip("Display name line.")]
    [SerializeField] private TMP_Text nameText;

    [Tooltip("Level line (e.g. Lv 12).")]
    [SerializeField] private TMP_Text levelText;

    [Tooltip("Current XP line (e.g. 450 XP).")]
    [SerializeField] private TMP_Text xpText;

    [Tooltip("Shown on processing skills that are not implemented yet.")]
    [SerializeField] private GameObject notCompleteText;

    [Header("XP Bar")]
    [Tooltip("XP bar fill image (uses Image.fillAmount).")]
    [SerializeField] private Image xpBarFill;

    [Tooltip("Whole-row click target.")]
    [SerializeField] private Button button;

    [Header("Display")]
    [Tooltip("Square skill icons include the name; hide the separate name line.")]
    [SerializeField] private bool hideNameText = true;

    [Tooltip("Keep the sprite assigned on the Icon image in the inspector instead of overwriting from SkillDefinition.")]
    [SerializeField] private bool preferInspectorAssignedIcon = true;

    [Header("Selection")]
    [FormerlySerializedAs("background")]
    [Tooltip("Row highlight tint; script sets color from the fields below.")]
    [SerializeField] private Image selectionBackground;

    [Header("Selection colours")]
    [SerializeField] private Color normalColor = new Color(0.02f, 0.02f, 0.04f, 0.52f);
    [SerializeField] private Color selectedColor = new Color(0.07f, 0.09f, 0.13f, 0.58f);

    [Header("Level-up highlight")]
    [Tooltip("Row background pulses toward this colour while a new level unlock is pending (very obvious on the strip).")]
    [SerializeField] private Color unlockPulsePeakColor = new Color(0.92f, 0.78f, 0.18f, 0.92f);

    private SkillDefinition _definition;
    private Action<SkillDefinition> _onClicked;
    private UIPulseGlowOverlay _unlockGlow;
    private Action<SkillDefinition> _onHoverAcknowledge;
    private bool _selected;
    private bool _unlockRowPulseActive;
    private Coroutine _unlockRowPulseCo;
    private bool _deferUnlockGlowUntilActive;

    public SkillDefinition Definition => _definition;

    /// <summary>Resolved list-row icon (prefab / inspector sprite, then optional fallback).</summary>
    public Sprite GetDisplayIcon(Sprite fallbackSprite = null)
    {
        Sprite sprite = skillIconSprite;
        if (sprite == null && preferInspectorAssignedIcon && icon != null && icon.sprite != null)
            sprite = icon.sprite;
        if (sprite == null)
            sprite = fallbackSprite;

        return ShouldShowIcon(sprite) ? sprite : null;
    }

    /// <summary>Processing rows without a <see cref="SkillDefinition"/>.</summary>
    public void SetupProcessingRow(
        int level,
        float progress01,
        int currentXp,
        bool selected,
        bool interactable,
        bool showNotComplete,
        Action onClicked)
    {
        _definition = null;
        _onClicked = null;
        _onHoverAcknowledge = null;

        ApplyIconResolve(null);

        if (nameText && hideNameText)
            nameText.gameObject.SetActive(false);

        SetLevel(level);
        SetProgress(progress01);
        SetXp(currentXp);
        SetSelected(selected);
        SetNotCompleteVisible(showNotComplete);

        if (button)
        {
            button.onClick.RemoveAllListeners();
            if (onClicked != null)
                button.onClick.AddListener(() =>
                {
                    ClearUnlockGlow();
                    onClicked.Invoke();
                });
            button.interactable = interactable;
        }
    }

    private void Awake()
    {
        if (!button)
            button = GetComponent<Button>();

        if (!xpText)
            xpText = transform.Find("XpBar/ExpText")?.GetComponent<TMP_Text>();

        if (!icon)
            icon = transform.Find("Icon")?.GetComponent<Image>();

        if (!notCompleteText)
            notCompleteText = transform.Find("NotCompleteText")?.gameObject;

        ApplyIconResolve(null);

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

    private void OnValidate()
    {
        if (!icon)
            icon = transform.Find("Icon")?.GetComponent<Image>();

        ApplyIconResolve(null);
    }

    private void OnEnable()
    {
        if (_deferUnlockGlowUntilActive && isActiveAndEnabled)
        {
            _deferUnlockGlowUntilActive = false;
            ShowUnlockGlow();
        }
    }

    public void ShowUnlockGlow()
    {
        if (!isActiveAndEnabled)
        {
            _deferUnlockGlowUntilActive = true;
            return;
        }

        RectTransform rt = transform as RectTransform;
        if (!rt)
            rt = GetComponent<RectTransform>();
        if (!rt)
            return;

        _deferUnlockGlowUntilActive = false;
        _unlockGlow = UIPulseGlowOverlay.Show(rt);

        StopUnlockRowPulseCoroutine();
        _unlockRowPulseActive = true;
        _unlockRowPulseCo = StartCoroutine(UnlockRowPulseRoutine());
    }

    public void ClearUnlockGlow()
    {
        _deferUnlockGlowUntilActive = false;
        StopUnlockRowPulseCoroutine();
        if (_unlockGlow != null)
            _unlockGlow.Clear();
        _unlockGlow = null;

        if (selectionBackground)
        {
            selectionBackground.gameObject.SetActive(true);
            selectionBackground.color = _selected ? selectedColor : normalColor;
        }
    }

    private void StopUnlockRowPulseCoroutine()
    {
        _unlockRowPulseActive = false;
        if (_unlockRowPulseCo != null)
        {
            StopCoroutine(_unlockRowPulseCo);
            _unlockRowPulseCo = null;
        }
    }

    private IEnumerator UnlockRowPulseRoutine()
    {
        float t = 0f;
        while (_unlockRowPulseActive && selectionBackground)
        {
            t += Time.unscaledDeltaTime * Mathf.PI * 2.6f;
            float s = (Mathf.Sin(t) + 1f) * 0.5f;
            Color from = _selected ? selectedColor : normalColor;
            selectionBackground.color = Color.Lerp(from, unlockPulsePeakColor, s);
            yield return null;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Remove the "new unlock" highlight as soon as the player acknowledges it.
        ClearUnlockGlow();
        if (_definition != null)
            _onHoverAcknowledge?.Invoke(_definition);
    }

    public void Setup(
        SkillDefinition definition,
        int level,
        float progress01,
        bool selected,
        Action<SkillDefinition> onClicked,
        Action<SkillDefinition> onHoverAcknowledge = null)
    {
        _definition = definition;
        _onClicked = onClicked;
        _onHoverAcknowledge = onHoverAcknowledge;

        if (!_definition)
            return;

        ApplyIconResolve(_definition != null ? _definition.icon : null);

        if (nameText)
        {
            if (hideNameText)
            {
                nameText.gameObject.SetActive(false);
            }
            else
            {
                string name = SkillsAbilityPresentationResolver.ResolveSkillDisplayName(_definition);
                nameText.text = string.IsNullOrWhiteSpace(name)
                    ? _definition.skillType.ToString()
                    : name;
                nameText.gameObject.SetActive(true);
            }
        }

        SetLevel(level);
        SetProgress(progress01);
        SetSelected(selected);
        SetNotCompleteVisible(false);

        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleClicked);
            button.interactable = true;
        }
    }

    /// <summary>Updates level/xp/progress without changing hierarchy icons or reparenting.</summary>
    public void RefreshDisplay(
        int level,
        float progress01,
        int currentXp,
        bool selected,
        bool interactable)
    {
        ApplyIconResolve(null);

        if (nameText && hideNameText)
            nameText.gameObject.SetActive(false);

        SetLevel(level);
        SetProgress(progress01);
        SetXp(currentXp);
        SetSelected(selected);
        SetNotCompleteVisible(false);

        if (button)
            button.interactable = interactable;
    }

    public void SetNotCompleteVisible(bool visible)
    {
        if (!notCompleteText)
            notCompleteText = transform.Find("NotCompleteText")?.gameObject;

        if (notCompleteText)
            notCompleteText.SetActive(visible);
    }

    public void SetupWithXp(
        SkillDefinition definition,
        int level,
        float progress01,
        int currentXp,
        bool selected,
        Action<SkillDefinition> onClicked,
        Action<SkillDefinition> onHoverAcknowledge = null)
    {
        Setup(definition, level, progress01, selected, onClicked, onHoverAcknowledge);
        SetXp(currentXp);
    }

    private void ApplyIconResolve(Sprite fallbackSprite)
    {
        if (!icon)
            return;

        Sprite sprite = skillIconSprite;
        if (sprite == null && preferInspectorAssignedIcon && icon.sprite != null)
            sprite = icon.sprite;
        if (sprite == null)
            sprite = fallbackSprite;

        ApplyIcon(sprite);
    }

    private void ApplyIcon(Sprite iconSprite)
    {
        if (!icon)
            return;

        bool show = ShouldShowIcon(iconSprite);
        icon.sprite = show ? iconSprite : null;
        icon.enabled = show;
    }

    private static bool ShouldShowIcon(Sprite iconSprite)
    {
        if (iconSprite == null)
            return false;

        Texture2D tex = iconSprite.texture;
        return tex != null;
    }

    public void SetInteractable(bool interactable)
    {
        if (button)
            button.interactable = interactable;
    }

    public void SetLevel(int level)
    {
        if (levelText)
            levelText.text = $"Lv {Mathf.Max(1, level)}";
    }

    public void SetXp(int currentXp)
    {
        if (!xpText)
            return;

        xpText.gameObject.SetActive(true);
        xpText.text = $"{Mathf.Max(0, currentXp)} XP";
    }

    public void SetProgress(float progress01)
    {
        if (!xpBarFill) return;
        xpBarFill.fillAmount = Mathf.Clamp01(progress01);
    }

    public void SetSelected(bool isSelected)
    {
        _selected = isSelected;
        if (_unlockRowPulseActive)
            return;

        if (!selectionBackground)
            return;
        selectionBackground.gameObject.SetActive(true);
        selectionBackground.color = isSelected ? selectedColor : normalColor;
    }

    private void HandleClicked()
    {
        ClearUnlockGlow();
        if (_definition != null)
            _onHoverAcknowledge?.Invoke(_definition);
        if (_definition != null)
            _onClicked?.Invoke(_definition);
    }
}
