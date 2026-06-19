using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LoadoutSetButtonBinder : MonoBehaviour
{
    [Header("Optional Explicit Button References")]
    [Tooltip("Preferred. Assign the real Set 1 button(s) here so no scene scan is needed.")]
    [SerializeField] private List<Button> setOneButtons = new();
    [Tooltip("Preferred. Assign the real Set 2 button(s) here so no scene scan is needed.")]
    [SerializeField] private List<Button> setTwoButtons = new();

    [Header("Fallback Exact Button Names")]
    [Tooltip("Used only when explicit refs are not assigned. Matches exact object name only.")]
    [SerializeField] private string setOneButtonName = "SetOneButton";
    [Tooltip("Used only when explicit refs are not assigned. Matches exact object name only.")]
    [SerializeField] private string setTwoButtonName = "SetTwoButton";

    private EquipmentManager _equipment;
    private ActionBarUI _actionBar;
    private CharacterStats _characterStats;
    private readonly List<Button> _setOneButtons = new();
    private readonly List<Button> _setTwoButtons = new();
    private bool _swapInProgress;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        if (FindFirstObjectByType<LoadoutSetButtonBinder>(FindObjectsInactive.Include) != null)
            return;

        EquipmentManager equipment = FindFirstObjectByType<EquipmentManager>(FindObjectsInactive.Include);
        if (equipment == null)
            return;

        equipment.gameObject.AddComponent<LoadoutSetButtonBinder>();
    }

    private void OnEnable()
    {
        BindRefs();
        RebuildButtonsAndWire();
        RefreshVisuals();

        if (_equipment != null)
            _equipment.OnActiveSetChanged += HandleActiveSetChanged;
    }

    private void OnDisable()
    {
        if (_equipment != null)
            _equipment.OnActiveSetChanged -= HandleActiveSetChanged;
    }

    private void Update()
    {
        if (_setOneButtons.Count == 0 && _setTwoButtons.Count == 0)
        {
            RebuildButtonsAndWire();
            RefreshVisuals();
        }
    }

    private void BindRefs()
    {
        if (_equipment == null)
            _equipment = FindFirstObjectByType<EquipmentManager>(FindObjectsInactive.Include);
        if (_actionBar == null)
            _actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        if (_characterStats == null && _equipment != null)
            _characterStats = _equipment.GetComponent<CharacterStats>();
    }

    private void RebuildButtonsAndWire()
    {
        _setOneButtons.Clear();
        _setTwoButtons.Clear();

        CollectAssignedButtons(setOneButtons, _setOneButtons);
        CollectAssignedButtons(setTwoButtons, _setTwoButtons);
        CollectButtonsFromActionBarRefs();

        if (_setOneButtons.Count == 0 || _setTwoButtons.Count == 0)
            CollectButtonsByExactName();

        WireButtonClickHandlers();
    }

    private void CollectButtonsFromActionBarRefs()
    {
        if (_actionBar == null)
            return;

        AddIfMissing(_setOneButtons, _actionBar.CombatSetOneButton);
        AddIfMissing(_setTwoButtons, _actionBar.CombatSetTwoButton);
    }

    private static bool CollectAssignedButtons(List<Button> source, List<Button> target)
    {
        bool any = false;
        if (source == null)
            return false;

        for (int i = 0; i < source.Count; i++)
        {
            Button b = source[i];
            if (b == null)
                continue;
            target.Add(b);
            any = true;
        }

        return any;
    }

    private static void AddIfMissing(List<Button> list, Button b)
    {
        if (list == null || b == null)
            return;
        if (!list.Contains(b))
            list.Add(b);
    }

    private void WireButtonClickHandlers()
    {
        for (int i = 0; i < _setOneButtons.Count; i++)
        {
            _setOneButtons[i].onClick.RemoveListener(HandleSetOneClicked);
            _setOneButtons[i].onClick.AddListener(HandleSetOneClicked);
        }

        for (int i = 0; i < _setTwoButtons.Count; i++)
        {
            _setTwoButtons[i].onClick.RemoveListener(HandleSetTwoClicked);
            _setTwoButtons[i].onClick.AddListener(HandleSetTwoClicked);
        }
    }

    private void HandleSetOneClicked() => ApplySet(0);
    private void HandleSetTwoClicked() => ApplySet(1);

    private void CollectButtonsByExactName()
    {
        if (_actionBar != null)
            CollectNamedButtonsFromScope(_actionBar.transform);

        if (_setOneButtons.Count > 0 && _setTwoButtons.Count > 0)
            return;

        Button[] allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allButtons.Length; i++)
            TryRegisterNamedButton(allButtons[i]);
    }

    private void CollectNamedButtonsFromScope(Transform scope)
    {
        if (scope == null)
            return;

        Button[] buttons = scope.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
            TryRegisterNamedButton(buttons[i]);
    }

    private void TryRegisterNamedButton(Button b)
    {
        if (b == null)
            return;

        if (_setOneButtons.Count == 0 && IsExactNameMatch(b.name, setOneButtonName))
            AddIfMissing(_setOneButtons, b);
        else if (_setTwoButtons.Count == 0 && IsExactNameMatch(b.name, setTwoButtonName))
            AddIfMissing(_setTwoButtons, b);
    }

    private static bool IsExactNameMatch(string actualName, string expectedName) =>
        !string.IsNullOrWhiteSpace(actualName) &&
        !string.IsNullOrWhiteSpace(expectedName) &&
        string.Equals(actualName.Trim(), expectedName.Trim(), System.StringComparison.Ordinal);

    private void HandleActiveSetChanged(int activeSet)
    {
        RefreshVisuals();
    }

    private void ApplySet(int setIndex)
    {
        if (_swapInProgress)
            return;

        BindRefs();
        if (_equipment == null)
            return;

        int targetSet = setIndex == 1 ? 1 : 0;
        if (!_equipment.TrySetActiveWeaponSet(targetSet))
            return;

        StartCoroutine(CoApplySet(targetSet));
    }

    private IEnumerator CoApplySet(int setIndex)
    {
        _swapInProgress = true;
        _equipment?.BeginWeaponSetSwapBatch();
        try
        {
            if (_actionBar == null)
                _actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
            _actionBar?.ExitGatheringBarToCombat();
            RefreshVisuals();
            yield return null;

            _actionBar?.SetCombatLoadoutSet(setIndex);
            yield return null;

            SkillsManager.Instance?.TryApplyLinkedPresetForWeaponSet(setIndex, _actionBar);
            yield return null;

            _characterStats?.NotifyWeaponSetSwapped();
            _actionBar?.CompleteWeaponSetSwapBatch();
            RefreshVisuals();
        }
        finally
        {
            _equipment?.EndWeaponSetSwapBatch();
            _swapInProgress = false;
        }
    }

    private void RefreshVisuals()
    {
        int active = _equipment != null ? (_equipment.ActiveWeaponSetIndex == 1 ? 1 : 0) : 0;
        ApplyButtonVisuals(_setOneButtons, active == 0);
        ApplyButtonVisuals(_setTwoButtons, active == 1);
    }

    private void ApplyButtonVisuals(List<Button> buttons, bool active)
    {
        if (buttons == null)
            return;

        for (int i = 0; i < buttons.Count; i++)
            UITabBarButtonVisuals.Apply(buttons[i], active);
    }
}
