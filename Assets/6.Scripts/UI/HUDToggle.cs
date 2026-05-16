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
    private bool _collapsedByTownRule;
    private bool _userExpandedHudInTown;
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
        ApplyTownHudRule();
    }

    private void OnEnable()
    {
        ToggleSettingsStore.Changed += OnToggleSettingsChanged;
        if (!TrySubscribeLevelStarted())
            StartCoroutine(WaitForBootstrapperThenApplyTownRule());
        else
            ApplyTownHudRule();
    }

    private void OnDisable()
    {
        ToggleSettingsStore.Changed -= OnToggleSettingsChanged;
        UnsubscribeLevelStarted();
    }

    public static void RefreshAllFromTownSetting()
    {
        HUDToggle[] toggles = FindObjectsByType<HUDToggle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < toggles.Length; i++)
        {
            if (toggles[i])
                toggles[i].ApplyTownHudRule();
        }
    }

    public void Toggle()
    {
        bool opening = !_isVisible;
        SetHudVisible(opening, fromUser: true);

        if (!IsCurrentNodeTown())
            return;

        if (opening)
            _userExpandedHudInTown = true;
        else
            _userExpandedHudInTown = false;
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
        if (id == ToggleSettingId.MinimiseHudDisplayInTown)
            ApplyTownHudRule();
    }

    private bool TrySubscribeLevelStarted()
    {
        if (_subscribedLevelStarted || GameplayLevelBootstrapper.Instance == null)
            return _subscribedLevelStarted;

        GameplayLevelBootstrapper.Instance.OnLevelStarted += OnLevelStarted;
        _subscribedLevelStarted = true;
        return true;
    }

    private IEnumerator WaitForBootstrapperThenApplyTownRule()
    {
        const int maxFrames = 120;
        for (int i = 0; i < maxFrames; i++)
        {
            if (TrySubscribeLevelStarted())
            {
                ApplyTownHudRule();
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
        _userExpandedHudInTown = false;
        _collapsedByTownRule = false;
        ApplyTownHudRule();
    }

    private void ApplyTownHudRule()
    {
        bool shouldAutoMinimize = ShouldAutoMinimizeInTown();

        if (shouldAutoMinimize && !_userExpandedHudInTown)
        {
            _collapsedByTownRule = true;
            if (_isVisible)
                SetHudVisible(false, fromUser: false);
            return;
        }

        if (_collapsedByTownRule && !_isVisible)
            SetHudVisible(true, fromUser: false);

        _collapsedByTownRule = false;
    }

    private static bool ShouldAutoMinimizeInTown()
    {
        if (!ToggleSettingsStore.Get(ToggleSettingId.MinimiseHudDisplayInTown))
            return false;

        return IsCurrentNodeTown();
    }

    private static bool IsCurrentNodeTown()
    {
        MapNodeDefinition def = ActiveLevelContext.Current;
        if (!def && GameplayLevelBootstrapper.Instance != null)
            def = GameplayLevelBootstrapper.Instance.ActiveDefinition;

        return def != null && def.nodeType == MapNodeType.Town;
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
