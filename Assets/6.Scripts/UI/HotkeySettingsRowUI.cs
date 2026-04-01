using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One row in settings: label + current key + rebind. Assign <see cref="bindId"/> and wire TMP/Button refs in the Inspector.
/// </summary>
[DisallowMultipleComponent]
public class HotkeySettingsRowUI : MonoBehaviour
{
    [SerializeField] private HotkeyBindId bindId = HotkeyBindId.ActionBar1;
    [SerializeField] private TMP_Text actionNameText;
    [SerializeField] private TMP_Text currentKeyText;
    [Tooltip("Optional: separate label for \"Press any key…\". If null, the prompt is shown on Current Key Text.")]
    [SerializeField] private TMP_Text listeningText;
    [SerializeField] private Button rebindButton;

    [Tooltip("Shown while waiting for a key.")]
    [SerializeField] private string listeningPrompt = "Press any key…";

    private bool _listening;
    private Coroutine _deferredFocusCoroutine;
    private static KeyCode[] _keyScanOrder;
    private static HotkeySettingsRowUI _activeListener;

    /// <summary>True while a row is waiting for a key — use to avoid action bar / gameplay consuming the same keys.</summary>
    public static bool IsRebinding => _activeListener != null;

    /// <summary>Frame when a bind was committed; <see cref="ActionBarUI"/> skips hotkey polls on this frame to avoid duplicate <see cref="ActionBarSlotUI.Press"/>.</summary>
    public static int SuppressActionBarHotkeyPollFrame { get; private set; } = -1;

    /// <summary>
    /// Gameplay uses legacy <see cref="Input"/> for rebinding, but UI uses <see cref="InputSystemUIInputModule"/>.
    /// While both run, Submit/Navigate can corrupt focus. Suspend UI modules for the listen window only.
    /// </summary>
    private static bool _uiInputModulesSuspended;
    private static BaseInputModule[] _suspendedInputModules;
    private static bool[] _suspendedInputModuleStates;

    private static void SuspendUiInputModules()
    {
        if (_uiInputModulesSuspended || EventSystem.current == null)
            return;

        BaseInputModule[] modules = EventSystem.current.GetComponents<BaseInputModule>();
        if (modules == null || modules.Length == 0)
            return;

        _suspendedInputModules = modules;
        _suspendedInputModuleStates = new bool[modules.Length];
        for (int i = 0; i < modules.Length; i++)
        {
            _suspendedInputModuleStates[i] = modules[i].enabled;
            modules[i].enabled = false;
        }

        _uiInputModulesSuspended = true;
    }

    private static void ResumeUiInputModules()
    {
        if (!_uiInputModulesSuspended || _suspendedInputModules == null)
            return;

        for (int i = 0; i < _suspendedInputModules.Length; i++)
        {
            if (_suspendedInputModules[i] != null && _suspendedInputModuleStates != null && i < _suspendedInputModuleStates.Length)
                _suspendedInputModules[i].enabled = _suspendedInputModuleStates[i];
        }

        _suspendedInputModules = null;
        _suspendedInputModuleStates = null;
        _uiInputModulesSuspended = false;
    }

    /// <summary>
    /// Rebind listen mode can leave <see cref="UnityEngine.EventSystems.BaseInputModule"/> disabled if flow desyncs.
    /// Call when opening/closing the main menu so keyboard Submit works on the bottom bar again.
    /// </summary>
    public static void EnsureUiInputModulesEnabled()
    {
        ResumeUiInputModules();

        EventSystem es = EventSystem.current;
        if (es == null)
            return;

        BaseInputModule[] modules = es.GetComponents<BaseInputModule>();
        for (int i = 0; i < modules.Length; i++)
        {
            if (modules[i] != null)
                modules[i].enabled = true;
        }
    }

    /// <summary>Keep focus on the row's button so keyboard Submit/navigation still works; clearing selection breaks toolbar after rebinding.</summary>
    private void FocusRebindButtonForKeyboard()
    {
        if (rebindButton == null || EventSystem.current == null)
            return;
        if (!rebindButton.gameObject.activeInHierarchy || !rebindButton.IsInteractable())
            return;
        rebindButton.Select();
    }

    /// <summary>Input System UI needs one frame after modules re-enable before Select() sticks for keyboard.</summary>
    private void ScheduleFocusRebindButtonNextFrame()
    {
        if (_deferredFocusCoroutine != null)
            StopCoroutine(_deferredFocusCoroutine);
        _deferredFocusCoroutine = StartCoroutine(DeferredFocusRebindRoutine());
    }

    private IEnumerator DeferredFocusRebindRoutine()
    {
        yield return null;
        FocusRebindButtonForKeyboard();
        _deferredFocusCoroutine = null;
    }

    private static void MarkActionBarSuppressThisFrame()
    {
        SuppressActionBarHotkeyPollFrame = Time.frameCount;
    }

    private static KeyCode[] KeyScanOrder
    {
        get
        {
            if (_keyScanOrder != null)
                return _keyScanOrder;

            var list = new List<KeyCode>();
            foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
            {
                if (k == KeyCode.None)
                    continue;
                if (k >= KeyCode.Mouse0 && k <= KeyCode.Mouse6)
                    continue;
                int i = (int)k;
                if (i >= (int)KeyCode.JoystickButton0 && i <= (int)KeyCode.Joystick8Button19)
                    continue;
                list.Add(k);
            }

            list.Sort((a, b) => ((int)a).CompareTo((int)b));
            _keyScanOrder = list.ToArray();
            return _keyScanOrder;
        }
    }

    private void OnEnable()
    {
        if (rebindButton != null)
            rebindButton.onClick.AddListener(BeginListening);

        HotkeyBindingManager mgr = HotkeyBindingManager.Instance;
        if (mgr != null)
            mgr.OnBindingsChanged += RefreshDisplay;

        ApplyActionLabelFromBindId();
        RefreshDisplay();
    }

    private void OnDisable()
    {
        if (rebindButton != null)
            rebindButton.onClick.RemoveListener(BeginListening);

        HotkeyBindingManager mgr = HotkeyBindingManager.Instance;
        if (mgr != null)
            mgr.OnBindingsChanged -= RefreshDisplay;

        if (_listening)
            StopListening();
    }

    private void Update()
    {
        if (!_listening)
            return;

        KeyCode[] order = KeyScanOrder;
        for (int i = 0; i < order.Length; i++)
        {
            if (!Input.GetKeyDown(order[i]))
                continue;

            KeyCode k = order[i];
            if (k == KeyCode.Escape)
            {
                StopListening();
                return;
            }

            if (k == KeyCode.Backspace || k == KeyCode.Delete)
            {
                TryApply(KeyCode.None);
                return;
            }

            TryApply(k);
            return;
        }
    }

    private void BeginListening()
    {
        HotkeyBindingManager mgr = HotkeyBindingManager.Instance;
        if (mgr == null)
        {
            Debug.LogWarning("[HotkeySettingsRowUI] HotkeyBindingManager not found in scene.");
            return;
        }

        if (_listening)
            return;

        if (_activeListener != null && _activeListener != this)
            _activeListener.StopListening();

        _activeListener = this;
        _listening = true;

        if (listeningText != null)
        {
            listeningText.gameObject.SetActive(true);
            listeningText.text = listeningPrompt;
            if (currentKeyText != null)
                currentKeyText.text = string.Empty;
        }
        else if (currentKeyText != null)
        {
            currentKeyText.text = listeningPrompt;
        }

        SuspendUiInputModules();
    }

    private void StopListening()
    {
        if (!_listening)
            return;

        _listening = false;
        if (_activeListener == this)
            _activeListener = null;

        RefreshDisplay();
        ResumeUiInputModules();
        ScheduleFocusRebindButtonNextFrame();
    }

    private void TryApply(KeyCode k)
    {
        HotkeyBindingManager mgr = HotkeyBindingManager.Instance;
        if (mgr == null)
        {
            StopListening();
            return;
        }

        if (!_listening)
            return;

        // Exit listen mode before TrySetBinding so OnBindingsChanged subscribers do not see stale "listening" UI state.
        _listening = false;
        if (_activeListener == this)
            _activeListener = null;

        mgr.TrySetBinding(bindId, k);
        MarkActionBarSuppressThisFrame();
        RefreshDisplay();
        ResumeUiInputModules();
        ScheduleFocusRebindButtonNextFrame();
    }

    /// <summary>Sets <see cref="actionNameText"/> from <see cref="bindId"/> (e.g. Ability Slot 1…5, Food, Potion).</summary>
    public void ApplyActionLabelFromBindId()
    {
        if (actionNameText != null)
            actionNameText.text = HotkeyBindIds.GetSettingsRowLabel(bindId);
    }

    public void RefreshDisplay()
    {
        HotkeyBindingManager mgr = HotkeyBindingManager.Instance;
        if (_listening)
        {
            if (listeningText != null)
            {
                listeningText.gameObject.SetActive(true);
                listeningText.text = listeningPrompt;
            }

            if (currentKeyText != null)
            {
                if (listeningText != null)
                    currentKeyText.text = string.Empty;
                else
                    currentKeyText.text = listeningPrompt;
            }

            return;
        }

        if (listeningText != null)
            listeningText.gameObject.SetActive(false);

        if (mgr == null || currentKeyText == null)
            return;

        KeyCode k = mgr.GetBinding(bindId);
        string s = HotkeyBindingManager.GetDisplayString(k);
        currentKeyText.text = string.IsNullOrEmpty(s) ? "(unbound)" : s;
    }

    /// <summary>Optional: set from code; overrides automatic labels until next <see cref="ApplyActionLabelFromBindId"/>.</summary>
    public void SetActionLabel(string text)
    {
        if (actionNameText != null)
            actionNameText.text = text;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyActionLabelFromBindId();
    }
#endif
}
