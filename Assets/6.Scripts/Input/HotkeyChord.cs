using System;
using UnityEngine;

[Flags]
public enum HotkeyModifier
{
    None = 0,
    Shift = 1 << 0,
    Ctrl = 1 << 1,
    Alt = 1 << 2,
}

/// <summary>
/// Key plus optional modifiers (e.g. Shift+Q). Used by <see cref="HotkeyBindingManager"/> and the action bar.
/// </summary>
[Serializable]
public struct HotkeyChord
{
    public KeyCode Key;
    public HotkeyModifier Modifiers;

    public bool IsEmpty => Key == KeyCode.None;

    public static HotkeyChord FromKeyCode(KeyCode key, HotkeyModifier modifiers = HotkeyModifier.None) =>
        new() { Key = key, Modifiers = modifiers };

    public static bool MatchesModifiers(HotkeyModifier required)
    {
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        if (required.HasFlag(HotkeyModifier.Shift) != shift)
            return false;
        if (required.HasFlag(HotkeyModifier.Ctrl) != ctrl)
            return false;
        if (required.HasFlag(HotkeyModifier.Alt) != alt)
            return false;
        return true;
    }

    public static bool WasPressedThisFrame(HotkeyChord chord)
    {
        if (chord.IsEmpty)
            return false;

        if (!Input.GetKeyDown(chord.Key))
            return false;

        return MatchesModifiers(chord.Modifiers);
    }

    public static bool IsHeld(HotkeyChord chord)
    {
        if (chord.IsEmpty)
            return false;

        if (!Input.GetKey(chord.Key))
            return false;

        return MatchesModifiers(chord.Modifiers);
    }

    public bool Equals(HotkeyChord other) => Key == other.Key && Modifiers == other.Modifiers;

    public override bool Equals(object obj) => obj is HotkeyChord other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Key, Modifiers);
}
