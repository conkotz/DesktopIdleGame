/// <summary>
/// Strongly typed hotkey bind slots. Action bar uses <see cref="ActionBar1"/>–<see cref="ActionBar7"/> in <b>slot order</b>
/// (first seven entries in <see cref="ActionBarUI"/> slot list). Add future binds (inventory, map, etc.) here.
/// </summary>
public enum HotkeyBindId
{
    ActionBar1 = 0,
    ActionBar2 = 1,
    ActionBar3 = 2,
    ActionBar4 = 3,
    ActionBar5 = 4,
    ActionBar6 = 5,
    ActionBar7 = 6,
    CloseAllWindows = 7,
    OpenCharacterPage = 8,
    OpenSkillsAbilities = 9,
    OpenLevelSelect = 10,
    OpenQuestPage = 11,
}
