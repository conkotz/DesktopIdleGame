using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central keybind storage for UI and gameplay. Persists with <see cref="PlayerPrefs"/>.
/// Pair with <see cref="HotkeySettingsRowUI"/> and <see cref="ActionBarUI"/> (first seven slots by list order).
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class HotkeyBindingManager : MonoBehaviour
{
    public static HotkeyBindingManager Instance { get; private set; }

    [Header("Bootstrap")]
    [Tooltip(
        "If true, this object is moved to a root transform and marked DontDestroyOnLoad so it survives scene unloads. " +
        "Not required: bindings are stored in PlayerPrefs, so a new manager in the next scene reloads the same keys. " +
        "Use DDOL only if you need a single live Instance across loads without the component being destroyed.")]
    [SerializeField] private bool dontDestroyOnLoad;

    /// <summary>Fired after any binding changes (including duplicate clears).</summary>
    public event Action OnBindingsChanged;

    private readonly Dictionary<HotkeyBindId, KeyCode> _bindings = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (dontDestroyOnLoad)
        {
            // Unity warns if DontDestroyOnLoad is used on a non-root GameObject; reparent to scene root first.
            transform.SetParent(null, worldPositionStays: true);
            DontDestroyOnLoad(gameObject);
        }

        ApplyDefaultsWhereMissing();
        LoadFromPlayerPrefs();
        OnBindingsChanged?.Invoke();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void ApplyDefaultsWhereMissing()
    {
        foreach (HotkeyBindId id in Enum.GetValues(typeof(HotkeyBindId)))
        {
            if (!_bindings.ContainsKey(id))
                _bindings[id] = ResolveDefaultKey(id);
        }
    }

    /// <summary>Default layout: 1–5 on number row, Q and E for slots 6–7 (matches common ARPG layouts).</summary>
    public static KeyCode GetDefaultKey(HotkeyBindId id)
    {
        return id switch
        {
            HotkeyBindId.ActionBar1 => KeyCode.Alpha1,
            HotkeyBindId.ActionBar2 => KeyCode.Alpha2,
            HotkeyBindId.ActionBar3 => KeyCode.Alpha3,
            HotkeyBindId.ActionBar4 => KeyCode.Alpha4,
            HotkeyBindId.ActionBar5 => KeyCode.Alpha5,
            HotkeyBindId.ActionBar6 => KeyCode.Q,
            HotkeyBindId.ActionBar7 => KeyCode.E,
            HotkeyBindId.CloseAllWindows => KeyCode.Escape,
            HotkeyBindId.OpenCharacterPage => KeyCode.I,
            HotkeyBindId.OpenSkillsAbilities => KeyCode.S,
            HotkeyBindId.OpenLevelSelect => KeyCode.L,
            HotkeyBindId.OpenQuestPage => KeyCode.T,
            HotkeyBindId.SwapWeaponSet => KeyCode.Tab,
            HotkeyBindId.ZoomIn => KeyCode.UpArrow,
            HotkeyBindId.ZoomOut => KeyCode.DownArrow,
            HotkeyBindId.ReturnToTown => KeyCode.None,
            HotkeyBindId.Sprint => KeyCode.Space,
            _ => KeyCode.None
        };
    }

    public KeyCode GetBinding(HotkeyBindId id)
    {
        return _bindings.TryGetValue(id, out KeyCode k) ? k : ResolveDefaultKey(id);
    }

    /// <summary>Sets a binding; clears the same key from other binds. Returns false if nothing changed.</summary>
    public bool TrySetBinding(HotkeyBindId id, KeyCode newKey)
    {
        KeyCode prev = GetBinding(id);
        bool clearedDuplicateFromOther = false;

        if (newKey != KeyCode.None)
        {
            foreach (HotkeyBindId other in Enum.GetValues(typeof(HotkeyBindId)))
            {
                if (other == id)
                    continue;
                if (_bindings.TryGetValue(other, out KeyCode existing) && existing == newKey)
                {
                    _bindings[other] = KeyCode.None;
                    clearedDuplicateFromOther = true;
                }
            }
        }

        _bindings[id] = newKey;

        if (!clearedDuplicateFromOther && prev == newKey)
            return false;

        SaveToPlayerPrefs();
        OnBindingsChanged?.Invoke();
        return true;
    }

    public void SaveToPlayerPrefs()
    {
        foreach (HotkeyBindId id in Enum.GetValues(typeof(HotkeyBindId)))
        {
            KeyCode k = GetBinding(id);
            PlayerPrefs.SetInt(PrefKey(id), (int)k);
        }

        PlayerPrefs.Save();
    }

    public void LoadFromPlayerPrefs()
    {
        foreach (HotkeyBindId id in Enum.GetValues(typeof(HotkeyBindId)))
        {
            string key = PrefKey(id);
            if (!PlayerPrefs.HasKey(key))
                continue;

            int code = PlayerPrefs.GetInt(key, (int)GetDefaultKey(id));
            _bindings[id] = (KeyCode)code;
        }

        OnBindingsChanged?.Invoke();
    }

    /// <summary>
    /// Clears hotkey prefs and restores in-memory bindings to <see cref="GetDefaultKey"/> values.
    /// Used by <see cref="GlobalUserSettings.RestoreAllToDefaults"/>.
    /// </summary>
    public static void ResetPersistedBindingsToDefaults()
    {
        foreach (HotkeyBindId id in Enum.GetValues(typeof(HotkeyBindId)))
            PlayerPrefs.DeleteKey(PrefKey(id));

        if (Instance != null)
        {
            Instance._bindings.Clear();
            foreach (HotkeyBindId id in Enum.GetValues(typeof(HotkeyBindId)))
                Instance._bindings[id] = ResolveDefaultKey(id);

            Instance.SaveToPlayerPrefs();
            Instance.OnBindingsChanged?.Invoke();
        }
        else
        {
            PlayerPrefs.Save();
        }
    }

    private static string PrefKey(HotkeyBindId id) => $"HotkeyBind_{id}";

    private static KeyCode ResolveDefaultKey(HotkeyBindId id)
    {
        if (HotkeySettingsRowUI.TryGetSerializedDefaultKey(id, out KeyCode fromRow))
            return fromRow;
        return GetDefaultKey(id);
    }

    public static string GetDisplayString(KeyCode key)
    {
        if (key == KeyCode.None)
            return "";

        return key switch
        {
            KeyCode.Alpha1 => "1",
            KeyCode.Alpha2 => "2",
            KeyCode.Alpha3 => "3",
            KeyCode.Alpha4 => "4",
            KeyCode.Alpha5 => "5",
            KeyCode.Alpha6 => "6",
            KeyCode.Alpha7 => "7",
            KeyCode.Alpha8 => "8",
            KeyCode.Alpha9 => "9",
            KeyCode.Alpha0 => "0",
            _ => key.ToString()
        };
    }
}
