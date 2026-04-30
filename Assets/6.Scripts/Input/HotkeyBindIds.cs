using UnityEngine;

/// <summary>Helpers for <see cref="HotkeyBindId"/> action bar mapping.</summary>
public static class HotkeyBindIds
{
    public const int ActionBarSlotCount = 7;

    public static HotkeyBindId FromActionBarOrder(int slotOrderIndex)
    {
        int clamped = Mathf.Clamp(slotOrderIndex, 0, ActionBarSlotCount - 1);
        return (HotkeyBindId)clamped;
    }

    public static int ToActionBarOrder(HotkeyBindId id)
    {
        int v = (int)id;
        if (v < 0 || v >= ActionBarSlotCount)
            return 0;
        return v;
    }

    /// <summary>True if this id is one of the seven action bar slots.</summary>
    public static bool IsActionBarBind(HotkeyBindId id) =>
        id >= HotkeyBindId.ActionBar1 && id <= HotkeyBindId.ActionBar7;

    /// <summary>Human-readable row title for the hotkey settings UI (matches action bar slot order: abilities 1–5, then food, potion).</summary>
    public static string GetSettingsRowLabel(HotkeyBindId id)
    {
        switch (id)
        {
            case HotkeyBindId.ActionBar1:
                return "Ability Slot 1";
            case HotkeyBindId.ActionBar2:
                return "Ability Slot 2";
            case HotkeyBindId.ActionBar3:
                return "Ability Slot 3";
            case HotkeyBindId.ActionBar4:
                return "Ability Slot 4";
            case HotkeyBindId.ActionBar5:
                return "Ability Slot 5";
            case HotkeyBindId.ActionBar6:
                return "Food Slot";
            case HotkeyBindId.ActionBar7:
                return "Potion Slot";
            case HotkeyBindId.CloseAllWindows:
                return "Close All Windows";
            case HotkeyBindId.OpenCharacterPage:
                return "Open Character";
            case HotkeyBindId.OpenSkillsAbilities:
                return "Open Skills & Abilities";
            case HotkeyBindId.OpenLevelSelect:
                return "Open Level select";
            case HotkeyBindId.OpenQuestPage:
                return "Open Quests";
            case HotkeyBindId.ZoomIn:
                return "Zoom In";
            case HotkeyBindId.ZoomOut:
                return "Zoom Out";
            default:
                return id.ToString();
        }
    }
}
