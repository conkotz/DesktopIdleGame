using System.Collections;
using TMPro;
using UnityEngine;

public class HUDToggle : MonoBehaviour
{
    [SerializeField] private GameObject hudRoot;
    [SerializeField] private TMP_Text arrowText;
    [Tooltip("Optional. Disabled while the left HUD is collapsed so bars and overlap fade do not update.")]
    [SerializeField] private HUDPresenter hudPresenter;

    private bool _isVisible = true;
    private bool _collapsedByAutoMinimizeRule;
    private bool _userExpandedHudOutOfCombat;
    private bool _userCollapsedHudThisCombat;
    private bool _wasInCombat;
    private bool _subscribedLevelStarted;
    private bool _subscribedCombatState;
    private PlayerCombatState _combatState;

    private void Awake()
    {
        if (!hudPresenter)
            hudPresenter = FindFirstObjectByType<HUDPresenter>(FindObjectsInactive.Include);
    }

    private void Start()
    {
        SyncPresenterToHudVisibility();
        UpdateVisual();
        ApplyAutoHudCombatRule();
    }

    private void OnEnable()
    {
        ToggleSettingsStore.Changed += OnToggleSettingsChanged;
        if (!TrySubscribeLevelStarted())
            StartCoroutine(WaitForBootstrapperThenApplyAutoHudRule());
        else
            ApplyAutoHudCombatRule();

        if (!TrySubscribeCombatState())
            StartCoroutine(WaitForCombatStateThenApplyAutoHudRule());
    }

    private void OnDisable()
    {
        ToggleSettingsStore.Changed -= OnToggleSettingsChanged;
        UnsubscribeLevelStarted();
        UnsubscribeCombatState();
    }

    public static void RefreshAllFromMinimiseHudSetting()
    {
        HUDToggle[] toggles = FindObjectsByType<HUDToggle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < toggles.Length; i++)
        {
            if (toggles[i])
                toggles[i].ApplyAutoHudCombatRule();
        }
    }

    public void Toggle()
    {
        bool opening = !_isVisible;
        SetHudVisible(opening, fromUser: true);

        if (!IsAutoHudCombatRuleEnabled())
            return;

        if (ResolvePlayerInCombat())
        {
            if (!opening)
                _userCollapsedHudThisCombat = true;
        }
        else if (opening)
        {
            _userExpandedHudOutOfCombat = true;
        }
    }

    public void SetHudVisible(bool visible, bool fromUser = false)
    {
        if (!hudRoot)
            return;

        if (_isVisible == visible && hudRoot.activeSelf == visible)
        {
            UpdateVisual();
            return;
        }

        _isVisible = visible;
        hudRoot.SetActive(visible);
        SyncPresenterToHudVisibility();

        if (visible && hudPresenter)
            hudPresenter.RefreshAll();

        UpdateVisual();
    }

    private void OnToggleSettingsChanged(ToggleSettingId id, bool _)
    {
        if (id == ToggleSettingId.MinimiseHud)
            ApplyAutoHudCombatRule();
    }

    private void OnPlayerCombatStateChanged(bool _)
    {
        ApplyAutoHudCombatRule();
    }

    private bool TrySubscribeLevelStarted()
    {
        if (_subscribedLevelStarted || GameplayLevelBootstrapper.Instance == null)
            return _subscribedLevelStarted;

        GameplayLevelBootstrapper.Instance.OnLevelStarted += OnLevelStarted;
        _subscribedLevelStarted = true;
        return true;
    }

    private IEnumerator WaitForBootstrapperThenApplyAutoHudRule()
    {
        const int maxFrames = 120;
        for (int i = 0; i < maxFrames; i++)
        {
            if (TrySubscribeLevelStarted())
            {
                ApplyAutoHudCombatRule();
                yield break;
            }

            yield return null;
        }
    }

    private bool TrySubscribeCombatState()
    {
        if (_subscribedCombatState)
            return true;

        _combatState = CombatPlayerRefs.CombatState;
        if (_combatState == null)
            return false;

        _combatState.OnCombatStateChanged += OnPlayerCombatStateChanged;
        _subscribedCombatState = true;
        return true;
    }

    private IEnumerator WaitForCombatStateThenApplyAutoHudRule()
    {
        const int maxFrames = 120;
        for (int i = 0; i < maxFrames; i++)
        {
            if (TrySubscribeCombatState())
            {
                ApplyAutoHudCombatRule();
                yield break;
            }

            yield return null;
        }
    }

    private void UnsubscribeLevelStarted()
    {
        if (!_subscribedLevelStarted || GameplayLevelBootstrapper.Instance == null)
            return;

        GameplayLevelBootstrapper.Instance.OnLevelStarted -= OnLevelStarted;
        _subscribedLevelStarted = false;
    }

    private void UnsubscribeCombatState()
    {
        if (!_subscribedCombatState || _combatState == null)
            return;

        _combatState.OnCombatStateChanged -= OnPlayerCombatStateChanged;
        _combatState = null;
        _subscribedCombatState = false;
    }

    private void OnLevelStarted(MapNodeDefinition _)
    {
        _userExpandedHudOutOfCombat = false;
        _userCollapsedHudThisCombat = false;
        _wasInCombat = false;
        _collapsedByAutoMinimizeRule = false;
        ApplyAutoHudCombatRule();
    }

    private void ApplyAutoHudCombatRule()
    {
        if (!IsAutoHudCombatRuleEnabled())
        {
            if (_collapsedByAutoMinimizeRule && !_isVisible)
                SetHudVisible(true, fromUser: false);

            _collapsedByAutoMinimizeRule = false;
            return;
        }

        bool inCombat = ResolvePlayerInCombat();
        if (inCombat && !_wasInCombat)
            _userCollapsedHudThisCombat = false;

        bool wantVisible = inCombat && !_userCollapsedHudThisCombat;
        if (!inCombat)
        {
            wantVisible = _userExpandedHudOutOfCombat;
            _userCollapsedHudThisCombat = false;
        }

        _wasInCombat = inCombat;
        _collapsedByAutoMinimizeRule = !wantVisible;

        if (_isVisible != wantVisible)
            SetHudVisible(wantVisible, fromUser: false);
    }

    private static bool IsAutoHudCombatRuleEnabled()
    {
        return ToggleSettingsStore.Get(ToggleSettingId.MinimiseHud);
    }

    private static bool ResolvePlayerInCombat()
    {
        PlayerCombatState combatState = CombatPlayerRefs.CombatState;
        return combatState != null && combatState.InCombat;
    }

    private void SyncPresenterToHudVisibility()
    {
        if (!hudPresenter)
            return;

        bool hudActive = hudRoot && hudRoot.activeSelf;
        hudPresenter.enabled = hudActive;
    }

    private void UpdateVisual()
    {
        if (!arrowText)
            return;

        arrowText.text = _isVisible ? "▼" : "▲";
    }
}
