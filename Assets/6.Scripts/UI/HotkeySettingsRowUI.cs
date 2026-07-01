using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One row in settings: label + current key + rebind. Assign <see cref="bindId"/> and wire TMP/Button refs in the Inspector.
/// </summary>
[DisallowMultipleComponent]
public class HotkeySettingsRowUI : MonoBehaviour
{
    [SerializeField] private HotkeyBindId bindId = HotkeyBindId.ActionBar1;
    [Tooltip("Factory-reset default for this bind. KeyCode.None = use HotkeyBindingManager code default.")]
    [SerializeField] private KeyCode serializedDefaultKey = KeyCode.None;
    [SerializeField] private HotkeyModifier serializedDefaultModifiers = HotkeyModifier.None;
    [SerializeField] private TMP_Text actionNameText;
    [SerializeField] private TMP_Text currentKeyText;
    [Tooltip("Optional: separate label for \"Press any key…\". If null, the prompt is shown on Current Key Text.")]
    [SerializeField] private TMP_Text listeningText;
    [SerializeField] private Button rebindButton;

    [Tooltip("Shown while waiting for a key.")]
    [SerializeField] private string listeningPrompt = "Press any key…";

    private static readonly Color KeyButtonImageColor = Color.white;
    private static readonly Color KeyButtonTextColor = new(0.22f, 0.16f, 0.12f, 1f);

    private bool _listening;
    private Coroutine _deferredFocusCoroutine;
    private static KeyCode[] _keyScanOrder;
    private static HotkeySettingsRowUI _activeListener;
    private static readonly HashSet<HotkeySettingsRowUI> s_registeredRows = new();
    private static readonly Dictionary<HotkeyBindId, HotkeyChord> s_serializedDefaultChords = new();

    /// <summary>True once at least one row has registered inspector defaults (scene loaded or rebuilt).</summary>
    public static bool HasSerializedDefaultCatalog => s_serializedDefaultChords.Count > 0;

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

        EventSystem[] systems =
            FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        EventSystem prefer = EventSystem.current;
        if (prefer == null)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                EventSystem es = systems[i];
                if (es == null)
                    continue;
                Scene s = es.gameObject.scene;
                if (s.IsValid() && s.isLoaded && s.name.Equals("Bootstrap", StringComparison.OrdinalIgnoreCase))
                {
                    prefer = es;
                    break;
                }
            }
        }

        if (prefer == null && systems.Length > 0)
            prefer = systems[0];

        if (prefer != null && EventSystem.current != prefer)
            EventSystem.current = prefer;

        for (int si = 0; si < systems.Length; si++)
        {
            EventSystem es = systems[si];
            if (es == null)
                continue;

            es.enabled = true;
            BaseInputModule[] modules = es.GetComponents<BaseInputModule>();
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i] != null)
                    modules[i].enabled = true;
            }
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

    private void Awake() => RegisterSerializedDefault(this);

    private void OnEnable()
    {
        s_registeredRows.Add(this);

        if (rebindButton != null)
            rebindButton.onClick.AddListener(BeginListening);

        HotkeyBindingManager mgr = HotkeyBindingManager.Instance;
        if (mgr != null)
            mgr.OnBindingsChanged += RefreshDisplay;

        GlobalUserSettings.RestoredDefaults += OnGlobalRestoredDefaults;

        ApplyActionLabelFromBindId();
        ApplyNeutralRebindButtonStyle();
        RefreshDisplay();
    }

    private void OnDisable()
    {
        s_registeredRows.Remove(this);

        if (rebindButton != null)
            rebindButton.onClick.RemoveListener(BeginListening);

        HotkeyBindingManager mgr = HotkeyBindingManager.Instance;
        if (mgr != null)
            mgr.OnBindingsChanged -= RefreshDisplay;

        GlobalUserSettings.RestoredDefaults -= OnGlobalRestoredDefaults;

        if (_listening)
            StopListening();
    }

    private void OnGlobalRestoredDefaults() => RefreshDisplay();

    private void Update()
    {
        if (!_listening)
            return;

        float scrollY = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scrollY) > 0.01f)
        {
            TryApply(scrollY > 0f ? HotkeyChord.FromMouseScrollUp() : HotkeyChord.FromMouseScrollDown());
            return;
        }

        KeyCode[] order = KeyScanOrder;
        for (int i = 0; i < order.Length; i++)
        {
            if (!Input.GetKeyDown(order[i]))
                continue;

            KeyCode k = order[i];
            if (k == KeyCode.LeftShift || k == KeyCode.RightShift ||
                k == KeyCode.LeftControl || k == KeyCode.RightControl ||
                k == KeyCode.LeftAlt || k == KeyCode.RightAlt)
                continue;

            if (k == KeyCode.Escape)
            {
                StopListening();
                return;
            }

            if (k == KeyCode.Backspace || k == KeyCode.Delete)
            {
                TryApply(HotkeyChord.FromKeyCode(KeyCode.None));
                return;
            }

            HotkeyModifier mods = HotkeyModifier.None;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                mods |= HotkeyModifier.Shift;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                mods |= HotkeyModifier.Ctrl;
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                mods |= HotkeyModifier.Alt;

            TryApply(HotkeyChord.FromKeyCode(k, mods));
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

    private void TryApply(HotkeyChord chord)
    {
        HotkeyBindingManager mgr = HotkeyBindingManager.Instance;
        if (mgr == null)
        {
            StopListening();
            return;
        }

        if (!_listening)
            return;

        _listening = false;
        if (_activeListener == this)
            _activeListener = null;

        mgr.TrySetBinding(bindId, chord);
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

    public void SetBindId(HotkeyBindId id)
    {
        bindId = id;
        ApplyActionLabelFromBindId();
        RefreshDisplay();
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
        {
            ApplyRowVisualStyle();
            return;
        }

        HotkeyChord chord = mgr.GetChord(bindId);
        string s = HotkeyBindingManager.GetDisplayString(chord);
        currentKeyText.text = string.IsNullOrEmpty(s) ? "(unbound)" : s;
        ApplyRowVisualStyle();
    }

    private void ApplyRowVisualStyle()
    {
        ApplyNeutralRebindButtonStyle();
    }

    /// <summary>
    /// All hotkey rows share the same neutral white key button (not action-bar food/potion colors).
    /// </summary>
    private void ApplyNeutralRebindButtonStyle()
    {
        if (rebindButton == null)
            return;

        Image target = rebindButton.targetGraphic as Image;
        if (target != null)
            target.color = KeyButtonImageColor;

        Image[] images = rebindButton.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image img = images[i];
            if (img == null)
                continue;
            if (listeningText != null && img.gameObject == listeningText.gameObject)
                continue;
            img.color = KeyButtonImageColor;
        }

        ColorBlock colors = rebindButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.5f);
        colors.colorMultiplier = 1f;
        rebindButton.colors = colors;

        if (currentKeyText != null)
            currentKeyText.color = KeyButtonTextColor;
    }

    /// <summary>Optional: set from code; overrides automatic labels until next <see cref="ApplyActionLabelFromBindId"/>.</summary>
    public void SetActionLabel(string text)
    {
        if (actionNameText != null)
            actionNameText.text = text;
    }

    public HotkeyBindId BindId => bindId;
    public KeyCode SerializedDefaultKey => serializedDefaultKey;

    public static bool TryGetSerializedDefaultKey(HotkeyBindId id, out KeyCode key)
    {
        if (TryGetSerializedDefaultChord(id, out HotkeyChord chord))
        {
            key = chord.Key;
            return key != KeyCode.None;
        }

        key = KeyCode.None;
        return false;
    }

    /// <summary>Collects inspector defaults from every row in loaded scenes (including inactive).</summary>
    public static void RebuildSerializedDefaultCatalog()
    {
        s_serializedDefaultChords.Clear();
        HotkeySettingsRowUI[] rows =
            FindObjectsByType<HotkeySettingsRowUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] != null)
                RegisterSerializedDefault(rows[i]);
        }
    }

    private static void RegisterSerializedDefault(HotkeySettingsRowUI row)
    {
        if (row == null)
            return;

        if (row.serializedDefaultKey == KeyCode.None && row.serializedDefaultModifiers == HotkeyModifier.None)
            return;

        HotkeyChord chord = HotkeyChord.FromKeyCode(row.serializedDefaultKey, row.serializedDefaultModifiers);
        if (chord.IsEmpty)
            return;

        s_serializedDefaultChords[row.bindId] = chord;
    }

    public static bool TryGetSerializedDefaultChord(HotkeyBindId id, out HotkeyChord chord)
    {
        if (s_serializedDefaultChords.TryGetValue(id, out chord) && !chord.IsEmpty)
            return true;

        chord = default;
        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyActionLabelFromBindId();
        ApplyNeutralRebindButtonStyle();
    }
#endif
}
