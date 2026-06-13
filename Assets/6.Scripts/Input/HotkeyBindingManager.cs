using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Central keybind storage for UI and gameplay. Persists with <see cref="PlayerPrefs"/>.
/// Pair with <see cref="HotkeySettingsRowUI"/> and <see cref="ActionBarUI"/>.
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

    private readonly Dictionary<HotkeyBindId, HotkeyChord> _bindings = new();

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
                _bindings[id] = ResolveDefaultChord(id);
        }
    }

    /// <summary>Default layout: 1–5 on number row, Q and E for potion/food, Shift+1–5 for ability row 6–10.</summary>
    public static HotkeyChord GetDefaultChord(HotkeyBindId id)
    {
        return id switch
        {
            HotkeyBindId.ActionBar1 => HotkeyChord.FromKeyCode(KeyCode.Alpha1),
            HotkeyBindId.ActionBar2 => HotkeyChord.FromKeyCode(KeyCode.Alpha2),
            HotkeyBindId.ActionBar3 => HotkeyChord.FromKeyCode(KeyCode.Alpha3),
            HotkeyBindId.ActionBar4 => HotkeyChord.FromKeyCode(KeyCode.Alpha4),
            HotkeyBindId.ActionBar5 => HotkeyChord.FromKeyCode(KeyCode.Alpha5),
            HotkeyBindId.ActionBar6 => HotkeyChord.FromKeyCode(KeyCode.Q),
            HotkeyBindId.ActionBar7 => HotkeyChord.FromKeyCode(KeyCode.E),
            HotkeyBindId.ActionBarAbility6 => HotkeyChord.FromKeyCode(KeyCode.Alpha1, HotkeyModifier.Shift),
            HotkeyBindId.ActionBarAbility7 => HotkeyChord.FromKeyCode(KeyCode.Alpha2, HotkeyModifier.Shift),
            HotkeyBindId.ActionBarAbility8 => HotkeyChord.FromKeyCode(KeyCode.Alpha3, HotkeyModifier.Shift),
            HotkeyBindId.ActionBarAbility9 => HotkeyChord.FromKeyCode(KeyCode.Alpha4, HotkeyModifier.Shift),
            HotkeyBindId.ActionBarAbility10 => HotkeyChord.FromKeyCode(KeyCode.Alpha5, HotkeyModifier.Shift),
            HotkeyBindId.CloseAllWindows => HotkeyChord.FromKeyCode(KeyCode.Escape),
            HotkeyBindId.OpenCharacterPage => HotkeyChord.FromKeyCode(KeyCode.I),
            HotkeyBindId.OpenSkillsAbilities => HotkeyChord.FromKeyCode(KeyCode.S),
            HotkeyBindId.OpenLevelSelect => HotkeyChord.FromKeyCode(KeyCode.L),
            HotkeyBindId.OpenQuestPage => HotkeyChord.FromKeyCode(KeyCode.T),
            HotkeyBindId.SwapWeaponSet => HotkeyChord.FromKeyCode(KeyCode.Tab),
            HotkeyBindId.ZoomIn => HotkeyChord.FromMouseScrollUp(),
            HotkeyBindId.ZoomOut => HotkeyChord.FromMouseScrollDown(),
            HotkeyBindId.ReturnToTown => HotkeyChord.FromKeyCode(KeyCode.None),
            HotkeyBindId.Sprint => HotkeyChord.FromKeyCode(KeyCode.Space),
            HotkeyBindId.MoveLeft => HotkeyChord.FromKeyCode(KeyCode.A),
            HotkeyBindId.MoveRight => HotkeyChord.FromKeyCode(KeyCode.D),
            HotkeyBindId.Interact => HotkeyChord.FromKeyCode(KeyCode.F),
            HotkeyBindId.EnterArea => HotkeyChord.FromKeyCode(KeyCode.None),
            HotkeyBindId.OpenEnhancePage => HotkeyChord.FromKeyCode(KeyCode.U),
            HotkeyBindId.OpenDatabasePage => HotkeyChord.FromKeyCode(KeyCode.None),
            _ => HotkeyChord.FromKeyCode(KeyCode.None)
        };
    }

    public static KeyCode GetDefaultKey(HotkeyBindId id) => GetDefaultChord(id).Key;

    public HotkeyChord GetChord(HotkeyBindId id) =>
        _bindings.TryGetValue(id, out HotkeyChord chord) ? chord : ResolveDefaultChord(id);

    public KeyCode GetBinding(HotkeyBindId id) => GetChord(id).Key;

    public bool TrySetBinding(HotkeyBindId id, KeyCode newKey) =>
        TrySetBinding(id, HotkeyChord.FromKeyCode(newKey));

    /// <summary>Sets a binding; clears the same chord from other binds. Returns false if nothing changed.</summary>
    public bool TrySetBinding(HotkeyBindId id, HotkeyChord newChord)
    {
        HotkeyChord prev = GetChord(id);
        bool clearedDuplicateFromOther = false;

        if (!newChord.IsEmpty)
        {
            foreach (HotkeyBindId other in Enum.GetValues(typeof(HotkeyBindId)))
            {
                if (other == id)
                    continue;

                if (_bindings.TryGetValue(other, out HotkeyChord existing) && existing.Equals(newChord))
                {
                    _bindings[other] = HotkeyChord.FromKeyCode(KeyCode.None);
                    clearedDuplicateFromOther = true;
                }
            }
        }

        _bindings[id] = newChord;

        if (!clearedDuplicateFromOther && prev.Equals(newChord))
            return false;

        SaveToPlayerPrefs();
        OnBindingsChanged?.Invoke();
        return true;
    }

    public void SaveToPlayerPrefs()
    {
        foreach (HotkeyBindId id in Enum.GetValues(typeof(HotkeyBindId)))
        {
            HotkeyChord chord = GetChord(id);
            PlayerPrefs.SetInt(PrefKey(id), (int)chord.Key);
            PlayerPrefs.SetInt(PrefModKey(id), (int)chord.Modifiers);
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

            int code = PlayerPrefs.GetInt(key, (int)GetDefaultChord(id).Key);
            int mod = PlayerPrefs.HasKey(PrefModKey(id))
                ? PlayerPrefs.GetInt(PrefModKey(id), 0)
                : 0;

            _bindings[id] = new HotkeyChord
            {
                Key = (KeyCode)code,
                Modifiers = (HotkeyModifier)mod
            };
        }

        OnBindingsChanged?.Invoke();
    }

    public static void ResetPersistedBindingsToDefaults()
    {
        foreach (HotkeyBindId id in Enum.GetValues(typeof(HotkeyBindId)))
        {
            PlayerPrefs.DeleteKey(PrefKey(id));
            PlayerPrefs.DeleteKey(PrefModKey(id));
        }

        if (Instance != null)
        {
            Instance._bindings.Clear();
            foreach (HotkeyBindId id in Enum.GetValues(typeof(HotkeyBindId)))
                Instance._bindings[id] = ResolveDefaultChord(id);

            Instance.SaveToPlayerPrefs();
            Instance.OnBindingsChanged?.Invoke();
        }
        else
        {
            PlayerPrefs.Save();
        }
    }

    private static string PrefKey(HotkeyBindId id) => $"HotkeyBind_{id}";

    private static string PrefModKey(HotkeyBindId id) => $"HotkeyBindMod_{id}";

    private static HotkeyChord ResolveDefaultChord(HotkeyBindId id)
    {
        if (HotkeySettingsRowUI.TryGetSerializedDefaultChord(id, out HotkeyChord fromRow))
            return fromRow;
        return GetDefaultChord(id);
    }

    public static string GetDisplayString(KeyCode key) => GetDisplayString(HotkeyChord.FromKeyCode(key));

    public static string GetDisplayString(HotkeyChord chord)
    {
        if (chord.IsEmpty)
            return string.Empty;

        if (chord.IsMouseScrollUp)
            return "Scroll Wheel Up";
        if (chord.IsMouseScrollDown)
            return "Scroll Wheel Down";

        var sb = new StringBuilder(24);
        if (chord.Modifiers.HasFlag(HotkeyModifier.Shift))
            sb.Append("Shift+");
        if (chord.Modifiers.HasFlag(HotkeyModifier.Ctrl))
            sb.Append("Ctrl+");
        if (chord.Modifiers.HasFlag(HotkeyModifier.Alt))
            sb.Append("Alt+");

        sb.Append(GetKeyLabel(chord.Key));
        return sb.ToString();
    }

    private static string GetKeyLabel(KeyCode key)
    {
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
