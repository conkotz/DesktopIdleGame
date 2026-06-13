using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Wires Aggressive / Assist / Passive minion commands on <see cref="ActionBarUI"/>.
/// Shown only while Soulforged Weapon or Warrior is on combat action-bar set 1 or 2.
/// </summary>
[DisallowMultipleComponent]
public class ActionBarMinionControlUI : MonoBehaviour
{
    [SerializeField] private ActionBarUI actionBar;
    [SerializeField] private GameObject minionControlRow;
    [SerializeField] private Button aggressiveButton;
    [SerializeField] private Button assistButton;
    [SerializeField] private Button passiveButton;

    [SerializeField, Range(0f, 1f)] private float inactiveButtonAlpha = 0.55f;

    private bool _wired;
    private bool _tooltipsConfigured;
    private SharedTooltipUI _sharedTooltip;

    private void Awake()
    {
        ResolveRefs();
        if (minionControlRow != null)
            minionControlRow.SetActive(false);
    }

    private void Start()
    {
        StartCoroutine(CoDeferredRefresh());
    }

    private void OnEnable()
    {
        WireButtons();
        ConfigureButtonTooltips();
        MinionControlService.OnStanceChanged += HandleStanceChanged;
        RefreshFromActionBar();
        RefreshStanceButtonVisuals();
    }

    private void OnDisable()
    {
        UnwireButtons();
        MinionControlService.OnStanceChanged -= HandleStanceChanged;
    }

    private IEnumerator CoDeferredRefresh()
    {
        const float savedStateTimeoutSeconds = 15f;
        float waitStart = Time.unscaledTime;

        while (actionBar != null &&
               actionBar.IsSavedStateApplyPending &&
               Time.unscaledTime - waitStart < savedStateTimeoutSeconds)
        {
            yield return null;
        }

        yield return null;
        RefreshFromActionBar();
    }

    public void RefreshFromActionBar()
    {
        ResolveRefs();
        bool show = ShouldShowMinionControls();

        if (minionControlRow != null)
            minionControlRow.SetActive(show);
    }

    private bool ShouldShowMinionControls()
    {
        return actionBar != null && actionBar.HasMinionAbilityOnCombatLoadouts();
    }

    private void ResolveRefs()
    {
        if (!actionBar)
            actionBar = GetComponent<ActionBarUI>() ?? FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);

        if (!minionControlRow)
        {
            Transform row = transform.Find("MinionControlButtonRow");
            if (row)
                minionControlRow = row.gameObject;
        }

        if (!aggressiveButton)
            aggressiveButton = FindButtonByName("AggressiveButton");
        if (!assistButton)
            assistButton = FindButtonByName("AssitButton") ?? FindButtonByName("AssistButton");
        if (!passiveButton)
            passiveButton = FindButtonByName("PassiveButton");
    }

    private Button FindButtonByName(string objectName)
    {
        Transform scope = minionControlRow != null ? minionControlRow.transform : transform;
        Button[] buttons = scope.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (b != null && string.Equals(b.gameObject.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                return b;
        }

        return null;
    }

    private void ConfigureButtonTooltips()
    {
        if (_tooltipsConfigured)
            return;

        _sharedTooltip ??= ResolveSharedTooltip();
        EnsureButtonTooltip(
            aggressiveButton,
            "Aggressive",
            "Minions automatically attack the nearest enemy in range.");
        EnsureButtonTooltip(
            assistButton,
            "Assist",
            "Minions only attack your current target. They stay idle near you when you are not fighting.");
        EnsureButtonTooltip(
            passiveButton,
            "Passive",
            "Minions never engage first. They only fight back after being hit by an enemy.");

        _tooltipsConfigured = true;
    }

    private static SharedTooltipUI ResolveSharedTooltip()
    {
        SharedTooltipUI[] allTooltips =
            FindObjectsByType<SharedTooltipUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allTooltips.Length; i++)
        {
            SharedTooltipUI t = allTooltips[i];
            if (t != null && t.name == "SharedToolTipInfoPanel")
                return t;
        }

        for (int i = 0; i < allTooltips.Length; i++)
        {
            if (allTooltips[i] != null)
                return allTooltips[i];
        }

        return null;
    }

    private void EnsureButtonTooltip(Button button, string title, string body)
    {
        if (!button)
            return;

        UIHoverTooltip hover = button.GetComponent<UIHoverTooltip>();
        if (!hover)
            hover = button.gameObject.AddComponent<UIHoverTooltip>();

        hover.ConfigureForEquipmentStatsFixedCopy(_sharedTooltip, title, body);
    }

    private void WireButtons()
    {
        if (_wired)
            return;

        Wire(aggressiveButton, OnAggressiveClicked);
        Wire(assistButton, OnAssistClicked);
        Wire(passiveButton, OnPassiveClicked);
        _wired = true;
    }

    private void UnwireButtons()
    {
        if (!_wired)
            return;

        Unwire(aggressiveButton, OnAggressiveClicked);
        Unwire(assistButton, OnAssistClicked);
        Unwire(passiveButton, OnPassiveClicked);
        _wired = false;
    }

    private static void Wire(Button b, UnityEngine.Events.UnityAction handler)
    {
        if (b == null || handler == null)
            return;
        b.onClick.RemoveListener(handler);
        b.onClick.AddListener(handler);
    }

    private static void Unwire(Button b, UnityEngine.Events.UnityAction handler)
    {
        if (b == null || handler == null)
            return;
        b.onClick.RemoveListener(handler);
    }

    private void OnAggressiveClicked() => MinionControlService.SetStance(MinionControlStance.Aggressive);
    private void OnAssistClicked() => MinionControlService.SetStance(MinionControlStance.Assist);
    private void OnPassiveClicked() => MinionControlService.SetStance(MinionControlStance.Passive);

    private void HandleStanceChanged(MinionControlStance _) => RefreshStanceButtonVisuals();

    private void RefreshStanceButtonVisuals()
    {
        MinionControlStance stance = MinionControlService.CurrentStance;
        ApplyStanceButtonVisual(aggressiveButton, stance == MinionControlStance.Aggressive);
        ApplyStanceButtonVisual(assistButton, stance == MinionControlStance.Assist);
        ApplyStanceButtonVisual(passiveButton, stance == MinionControlStance.Passive);
    }

    private void ApplyStanceButtonVisual(Button button, bool selected)
    {
        if (button == null)
            return;

        Outline outline = button.GetComponent<Outline>();
        if (selected)
        {
            if (outline == null)
                outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = UITabBarButtonVisuals.SelectedOutlineColor;
            outline.effectDistance = UITabBarButtonVisuals.SelectedOutlineDistance;
            outline.useGraphicAlpha = true;
            outline.enabled = true;
        }
        else if (outline != null)
        {
            outline.enabled = false;
        }

        CanvasGroup canvasGroup = button.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = button.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = selected ? 1f : inactiveButtonAlpha;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }
}
