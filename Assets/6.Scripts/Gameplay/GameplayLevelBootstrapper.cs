using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Attach to an empty GameObject in the GamePlay scene (e.g. "EmptyGameplay") that acts as the level root.
/// On start it reads <see cref="ActiveLevelContext.Current"/> (<see cref="MapNodeDefinition"/>) and raises <see cref="OnLevelStarted"/>.
/// Use <see cref="ContentRoot"/> as the parent for runtime-spawned level content.
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

        // Spawn/enabled listeners run here — merchants often exist only after this event.
        OnLevelStarted?.Invoke(ActiveDefinition);

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.RehydrateMerchantStocksFromSave();
            SaveManager.Instance.ScheduleMerchantRehydrateFrames(2);
            SaveManager.Instance.RehydrateNpcDialogueStoresFromDiskPreferFile();
            // Saving here runs before default-order components' Start() (e.g. ActionBarUI). A save would snapshot an
            // empty action bar and overwrite SaveManager's in-memory payload, so food/potion slots never rehydrate.
            if (markedNodeEntered)
                StartCoroutine(CoSaveAfterGameplaySaveablesStart());
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

    private static IEnumerator CoSaveAfterGameplaySaveablesStart()
    {
        yield return null;
        yield return null;
        if (SaveManager.Instance != null)
            SaveManager.Instance.Save();
    }

}
