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
    private bool _userExpandedHudThisMap;
    private bool _subscribedLevelStarted;

    private void Awake()
    {
        if (!hudPresenter)
            hudPresenter = FindFirstObjectByType<HUDPresenter>(FindObjectsInactive.Include);
    }

    private void Start()
    {
        SyncPresenterToHudVisibility();
        UpdateVisual();
        ApplyMinimiseHudRule();
    }

    private void OnEnable()
    {
        ToggleSettingsStore.Changed += OnToggleSettingsChanged;
        if (!TrySubscribeLevelStarted())
            StartCoroutine(WaitForBootstrapperThenApplyMinimiseHudRule());
        else
            ApplyMinimiseHudRule();
    }

    private void OnDisable()
    {
        ToggleSettingsStore.Changed -= OnToggleSettingsChanged;
        UnsubscribeLevelStarted();
    }

    public static void RefreshAllFromMinimiseHudSetting()
    {
        HUDToggle[] toggles = FindObjectsByType<HUDToggle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < toggles.Length; i++)
        {
            if (toggles[i])
                toggles[i].ApplyMinimiseHudRule();
        }
    }

    public void Toggle()
    {
        bool opening = !_isVisible;
        SetHudVisible(opening, fromUser: true);
        _userExpandedHudThisMap = opening;
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
            ApplyMinimiseHudRule();
    }

    private bool TrySubscribeLevelStarted()
    {
        if (_subscribedLevelStarted || GameplayLevelBootstrapper.Instance == null)
            return _subscribedLevelStarted;

        GameplayLevelBootstrapper.Instance.OnLevelStarted += OnLevelStarted;
        _subscribedLevelStarted = true;
        return true;
    }

    private IEnumerator WaitForBootstrapperThenApplyMinimiseHudRule()
    {
        const int maxFrames = 120;
        for (int i = 0; i < maxFrames; i++)
        {
            if (TrySubscribeLevelStarted())
            {
                ApplyMinimiseHudRule();
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

    private void OnLevelStarted(MapNodeDefinition _)
    {
        _userExpandedHudThisMap = false;
        _collapsedByAutoMinimizeRule = false;
        ApplyMinimiseHudRule();
    }

    private void ApplyMinimiseHudRule()
    {
        bool shouldAutoMinimize = ShouldAutoMinimizeOnMapLoad();

        if (shouldAutoMinimize && !_userExpandedHudThisMap)
        {
            _collapsedByAutoMinimizeRule = true;
            if (_isVisible)
                SetHudVisible(false, fromUser: false);
            return;
        }

        if (_collapsedByAutoMinimizeRule && !_isVisible)
            SetHudVisible(true, fromUser: false);

        _collapsedByAutoMinimizeRule = false;
    }

    private static bool ShouldAutoMinimizeOnMapLoad()
    {
        return ToggleSettingsStore.Get(ToggleSettingId.MinimiseHud);
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
