using UnityEngine;

/// <summary>
/// Story beats after claiming specific tutorial quests (map unlocks, finale teleport).
/// </summary>
public static class TutorialQuestAfterClaim
{
    public const string LearningRopes2 = "tutorial_learning_ropes_2";
    public const string BasicCombat2 = "tutorial_basic_combat_2";

    public const string NodeTutorial1 = "tutorial_1";
    public const string NodeTutorial2 = "tutorial_2";

    private const string GameplaySceneName = "GamePlay";

    public static void Invoke(QuestDefinition q)
    {
        if (q == null || string.IsNullOrEmpty(q.questId))
            return;

        string id = q.questId.Trim();
        WorldMapProgressManager wmp = WorldMapProgressManager.Instance ??
            Object.FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        if (wmp == null)
            return;

        if (id == LearningRopes2)
        {
            wmp.UnlockNode(NodeTutorial2);
            return;
        }

        if (id != BasicCombat2)
            return;

        bool wasTutorial2Completed = wmp.IsNodeCompleted(NodeTutorial2);
        wmp.SetNodeCompleted(NodeTutorial2, true);
        if (!wasTutorial2Completed)
            GameLog.RegionUnlocked("Greenlands");

        if (SaveManager.Instance != null)
            SaveManager.Instance.Save();

        WorldMapDefinition map = wmp.WorldMap;
        if (map != null)
        {
            MapNodeDefinition dusk = map.FindNodeById("duskwood");
            if (dusk != null)
            {
                ActiveLevelContext.SetPendingLevel(dusk, logToConsole: false);
                PlayerLevelTransition.LoadSceneWithEffectOrImmediate(GameplaySceneName);
            }
        }
    }
}
