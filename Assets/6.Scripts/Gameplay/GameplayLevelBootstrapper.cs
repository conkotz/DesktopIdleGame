using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Attach to an empty GameObject in the GamePlay scene (e.g. "EmptyGameplay") that acts as the level root.
/// On start it reads <see cref="ActiveLevelContext.Current"/> (<see cref="MapNodeDefinition"/>) and raises <see cref="OnLevelStarted"/>.
/// Use <see cref="ContentRoot"/> as the parent for spawned prefabs from <see cref="MapNodeDefinition.prefabGroups"/>.
/// </summary>
[AddComponentMenu("Desktop Idle Game/Gameplay Level Bootstrapper")]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public class GameplayLevelBootstrapper : MonoBehaviour
{
    public static GameplayLevelBootstrapper Instance { get; private set; }

    [Header("Hierarchy")]
    [Tooltip("Parent for spawned level content. Leave empty to use this GameObject (your EmptyGameplay root).")]
    [SerializeField] private Transform contentRoot;

    [Header("Optional")]
    [Tooltip("If no ActiveLevelContext (e.g. Play Mode on GamePlay scene directly), use this for quick tests.")]
    [SerializeField] private MapNodeDefinition devFallbackNode;

    [Header("Level content — dev spawn test")]
    [Tooltip("Instantiate prefabs from prefabGroups under ContentRoot to verify references (layout test only).")]
    [SerializeField] private bool devInstantiatePrefabGroups;
    [SerializeField] private Vector3 devSpawnGridStep = new Vector3(2f, 0f, 0f);

    /// <summary>Map node from level select (or dev fallback). All playable data is on this asset.</summary>
    public MapNodeDefinition ActiveDefinition { get; private set; }
    /// <summary>True only when this Start() marked the active node as entered for the first time in this save.</summary>
    public bool ActiveLevelWasFirstVisit { get; private set; }

    /// <summary>Where to parent spawned encounters; defaults to this transform.</summary>
    public Transform ContentRoot => contentRoot != null ? contentRoot : transform;

    public event Action<MapNodeDefinition> OnLevelStarted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Never Destroy(gameObject) here — a duplicate on EventSystem/UI would delete input or the whole menu.
            Debug.LogWarning(
                "[GameplayLevelBootstrapper] Multiple instances — remove the extra component. " +
                "Only one bootstrapper should exist (e.g. on EmptyGameplay). Removing this duplicate component only.",
                this);
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        MapNodeDefinition node = ActiveLevelContext.Current;
        if (!node && devFallbackNode)
            node = devFallbackNode;

        if (!node)
        {
            Debug.LogWarning(
                "[GameplayLevelBootstrapper] No MapNodeDefinition. Enter from level select, or assign Dev Fallback Node, or set ActiveLevelContext in code.");
            return;
        }

        ActiveDefinition = node;
        ActiveLevelContext.SetPendingLevel(node, logToConsole: false);
        LevelAggroState.ResetForLevel(node);
        GameLog.EnteringMap(ResolveMapDisplayName(node));

        WorldMapProgressManager wmp = WorldMapProgressManager.Instance ??
            FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        bool markedNodeEntered =
            wmp != null &&
            !string.IsNullOrEmpty(node.nodeId) &&
            wmp.MarkNodeEntered(node.nodeId.Trim());
        ActiveLevelWasFirstVisit = markedNodeEntered;

        // Suppress startup spam logs during normal gameplay.

        if (devInstantiatePrefabGroups)
            DevInstantiatePrefabGroups(node);

        // Spawn/enabled listeners run here — merchants often exist only after this event.
        OnLevelStarted?.Invoke(ActiveDefinition);

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.RehydrateMerchantStocksFromSave();
            SaveManager.Instance.ScheduleMerchantRehydrateFrames(2);
            SaveManager.Instance.RehydrateNpcDialogueStoresFromDiskPreferFile();
            if (markedNodeEntered)
                SaveManager.Instance.Save();
        }

        if (FindFirstObjectByType<HelperGameplayController>(FindObjectsInactive.Include) == null)
            GameplayRespawnHelperPersistence.ClearStaleKeepOverlayFlagIfPresent();
    }

    private static string ResolveMapDisplayName(MapNodeDefinition node)
    {
        if (node == null)
            return "";
        if (!string.IsNullOrWhiteSpace(node.displayName))
            return node.displayName;
        return node.nodeId;
    }

    private static void LogPrefabSpawnPlan(MapNodeDefinition node)
    {
        if (node == null)
            return;

        if (node.prefabGroups == null || node.prefabGroups.Count == 0)
        {
            Debug.Log("[GameplayLevelBootstrapper] Spawn plan: no prefabGroups on this MapNodeDefinition — add EncounterPrefabGroup rows on the asset when ready.");
            return;
        }

        var sb = new StringBuilder(256);
        sb.AppendLine("[GameplayLevelBootstrapper] Spawn plan (data only — production spawners should subscribe to OnLevelStarted):");

        for (int g = 0; g < node.prefabGroups.Count; g++)
        {
            EncounterPrefabGroup group = node.prefabGroups[g];
            if (group == null)
            {
                sb.AppendLine($"  group[{g}]: <null>");
                continue;
            }

            int n = group.prefabs != null ? group.prefabs.Count : 0;
            sb.Append($"  group '{group.groupId}' ({n} prefabs): ");

            if (group.prefabs == null || n == 0)
            {
                sb.AppendLine("<empty>");
                continue;
            }

            var names = new List<string>(n);
            for (int p = 0; p < group.prefabs.Count; p++)
            {
                GameObject prefab = group.prefabs[p];
                names.Add(prefab ? prefab.name : "<null ref>");
            }

            sb.AppendLine(string.Join(", ", names));
        }

        if (!string.IsNullOrWhiteSpace(node.contentDesignerNotes))
            sb.Append("  designer notes: ").AppendLine(node.contentDesignerNotes.Trim());

        Debug.Log(sb.ToString().TrimEnd());
    }

    private void DevInstantiatePrefabGroups(MapNodeDefinition node)
    {
        if (node == null || node.prefabGroups == null)
            return;

        int idx = 0;
        Transform parent = ContentRoot;

        foreach (EncounterPrefabGroup group in node.prefabGroups)
        {
            if (group == null || group.prefabs == null)
                continue;

            foreach (GameObject prefab in group.prefabs)
            {
                if (!prefab)
                    continue;

                Vector3 pos = parent.position + devSpawnGridStep * idx;
                GameObject inst = Instantiate(prefab, pos, Quaternion.identity, parent);
                Debug.Log(
                    $"[GameplayLevelBootstrapper] Dev spawn [{idx}] group='{group.groupId}' prefab='{prefab.name}' " +
                    $"-> instance '{inst.name}' at {pos}",
                    inst);

                idx++;
            }
        }

        if (idx == 0)
            Debug.LogWarning("[GameplayLevelBootstrapper] Dev instantiate: no valid prefabs in prefabGroups.");
    }
}
