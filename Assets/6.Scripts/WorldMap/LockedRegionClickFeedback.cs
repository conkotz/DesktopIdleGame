using System;

/// <summary>
/// Shared activity-log lines when the player clicks a locked region (Level Select, Quest journal, etc.).
/// </summary>
public static class LockedRegionClickFeedback
{
    private const string GreenlandsRegionId = "greenlands";
    private const string EmberHollowRegionId = "emberhollow";

    public static void LogLockedRegionNotice(RegionDefinition region)
    {
        if (!region || string.IsNullOrWhiteSpace(region.regionId))
            return;

        string id = region.regionId.Trim();

        if (string.Equals(id, GreenlandsRegionId, StringComparison.OrdinalIgnoreCase))
        {
            GameLog.Add(
                "This Region is locked - accessible after finishing all tutorial quests",
                GameLog.CannotMessageColor);
            return;
        }

        if (string.Equals(id, EmberHollowRegionId, StringComparison.OrdinalIgnoreCase))
            GameLog.Add(
                "This Region is locked - accessible after defeating Greenlands Boss",
                GameLog.CannotMessageColor);
    }
}
