using UnityEngine;

/// <summary>
/// After the player claims <see cref="gateQuestId"/> (e.g. Basic Combat), spawns extra rogues on <see cref="mapNodeId"/>
/// so Basic Combat 2 can progress. Place on the Tutorial 2 scene and assign <see cref="rogueDefinition"/>.
/// </summary>
[DisallowMultipleComponent]
public class TutorialRogueReinforcementSpawner : MonoBehaviour
{
    [SerializeField] private string mapNodeId = "tutorial_2";
    [SerializeField] private string gateQuestId = "tutorial_basic_combat";
    [SerializeField] private EnemyDefinition rogueDefinition;
    [SerializeField] private int spawnCount = 5;
    [SerializeField] private string spawnPointGroupId = "CombatEnemies";

    private bool _spawned;
    private QuestProgressManager _quests;

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (_quests != null)
        {
            _quests.ProgressChanged -= OnQuestProgressChanged;
            _quests = null;
        }
    }

    private void TrySubscribe()
    {
        _quests = QuestProgressManager.Instance ??
            FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);
        if (_quests == null)
            return;

        _quests.ProgressChanged -= OnQuestProgressChanged;
        _quests.ProgressChanged += OnQuestProgressChanged;
        TrySpawnIfNeeded();
    }

    private void OnQuestProgressChanged()
    {
        TrySpawnIfNeeded();
    }

    private void TrySpawnIfNeeded()
    {
        if (_spawned || _quests == null || !rogueDefinition)
            return;

        MapNodeDefinition active = GameplayLevelBootstrapper.Instance != null
            ? GameplayLevelBootstrapper.Instance.ActiveDefinition
            : ActiveLevelContext.Current;
        if (active == null || string.IsNullOrEmpty(active.nodeId) ||
            !string.Equals(active.nodeId.Trim(), mapNodeId.Trim(), System.StringComparison.Ordinal))
            return;

        if (!_quests.IsRewardClaimed(gateQuestId))
            return;

        LevelSpawnDirector director = FindFirstObjectByType<LevelSpawnDirector>(FindObjectsInactive.Include);
        if (director == null)
            return;

        var plan = new LevelSpawnGroupPlan
        {
            groupId = spawnPointGroupId,
            shuffleSpawnPoints = true,
            spawns = new System.Collections.Generic.List<SpawnPrefabCount>
            {
                new SpawnPrefabCount
                {
                    spawnPointGroupId = "",
                    enemyDefinition = rogueDefinition,
                    count = Mathf.Max(1, spawnCount)
                }
            }
        };

        director.SpawnAdditionalGroupPlan(plan, active);
        _spawned = true;

        if (_quests != null)
        {
            _quests.ProgressChanged -= OnQuestProgressChanged;
            _quests = null;
        }
    }
}
