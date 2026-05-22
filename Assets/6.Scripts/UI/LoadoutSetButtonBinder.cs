using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LoadoutSetButtonBinder : MonoBehaviour
{
    [Header("Optional Explicit Button References")]
    [Tooltip("If assigned, these are used first and no name/text matching is required.")]
    [SerializeField] private List<Button> setOneButtons = new();
    [Tooltip("If assigned, these are used first and no name/text matching is required.")]
    [SerializeField] private List<Button> setTwoButtons = new();

    [Header("Button Names")]
    [SerializeField] private string setOneButtonName = "SetOneButton";
    [SerializeField] private string setTwoButtonName = "SetTwoButton";

    [Header("Visuals (fallback if no ActionBarUI)")]
    [Tooltip("Used only when ActionBarUI is missing. Prefer Action Bar UI → Combat loadout set buttons on ActionBarWindow.")]
    [SerializeField] private Color activeBackgroundColor = new Color(0.97f, 0.82f, 0.34f, 1f);
    [SerializeField] private Color inactiveBackgroundColor = new Color(1f, 1f, 1f, 0.45f);
    [SerializeField] private Color activeTextColor = new Color(0.12f, 0.10f, 0.05f, 1f);
    [SerializeField] private Color inactiveTextColor = new Color(1f, 1f, 1f, 0.85f);

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

        Button[] allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allButtons.Length; i++)
        {
            Button b = allButtons[i];
            if (b == null)
                continue;

            if (IsSetOneButton(b))
                AddIfMissing(_setOneButtons, b);
            else if (IsSetTwoButton(b))
                AddIfMissing(_setTwoButtons, b);
        }

        WireButtonClickHandlers();
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

    private bool IsSetOneButton(Button b)
    {
        if (b == null)
            return false;
        if (b.name == setOneButtonName)
            return true;
        string txt = GetButtonText(b);
        return txt == "1" || txt == "Set 1" || txt == "Set1";
    }

    private bool IsSetTwoButton(Button b)
    {
        if (b == null)
            return false;
        if (b.name == setTwoButtonName)
            return true;
        string txt = GetButtonText(b);
        return txt == "2" || txt == "Set 2" || txt == "Set2";
    }

    private static string GetButtonText(Button b)
    {
        TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
        return t != null ? (t.text ?? "").Trim() : "";
    }

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
        if (_actionBar == null)
            _actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        _actionBar?.ExitGatheringBarToCombat();
        _characterStats?.NotifyWeaponSetSwapped();
        RefreshVisuals();
        yield return null; // spread swap load across frames
        _actionBar?.SetCombatLoadoutSet(setIndex);
        _characterStats?.NotifyStatsChanged();
        RefreshVisuals();
        _swapInProgress = false;
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

        Color bg;
        Color txt;
        if (_actionBar != null)
        {
            bg = active ? _actionBar.CombatSetActiveBackgroundColor : _actionBar.CombatSetInactiveBackgroundColor;
            txt = active ? _actionBar.CombatSetActiveTextColor : _actionBar.CombatSetInactiveTextColor;
        }
        else
        {
            bg = active ? activeBackgroundColor : inactiveBackgroundColor;
            txt = active ? activeTextColor : inactiveTextColor;
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            Button b = buttons[i];
            if (b == null)
                continue;

            ColorBlock cb = b.colors;
            cb.normalColor = bg;
            cb.highlightedColor = active
                ? Color.Lerp(bg, Color.white, 0.08f)
                : Color.Lerp(bg, Color.white, 0.14f);
            cb.pressedColor = active
                ? Color.Lerp(bg, Color.black, 0.18f)
                : Color.Lerp(bg, Color.black, 0.24f);
            cb.selectedColor = cb.highlightedColor;
            cb.disabledColor = new Color(bg.r * 0.6f, bg.g * 0.6f, bg.b * 0.6f, Mathf.Clamp01(bg.a * 0.8f));
            b.colors = cb;

            Graphic g = b.targetGraphic;
            if (g != null)
                g.color = bg;

            Image ownImage = b.GetComponent<Image>();
            if (ownImage != null)
                ownImage.color = bg;

            TMP_Text[] labels = b.GetComponentsInChildren<TMP_Text>(true);
            for (int t = 0; t < labels.Length; t++)
                labels[t].color = txt;
        }
    }
}
