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
