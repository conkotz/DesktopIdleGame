/// <summary>
/// Migrates legacy serialized world-map copy without editing GamePlay.unity (scene edits have corrupted the file before).
/// </summary>
internal static class WorldMapUiTextMigration
{
    public const string EnterAreaEnabledText = "Enter area \u2192";
    private const string LegacyEnterMapEnabledText = "Enter map \u2192";

    public static string MigrateEnterNodeEnabledText(string current) =>
        current == LegacyEnterMapEnabledText ? EnterAreaEnabledText : current;
}
