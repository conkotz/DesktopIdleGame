using System;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Renders tracked quests in QuestTrackerWindow/Content, preserving track order.
/// </summary>
public class QuestTrackerWindowUI : MonoBehaviour
{
    private const string TrackerWindowName = "QuestTrackerWindow";
    private const string TrackerContentName = "Content";
    private const string TrackerTitleName = "QuestsTrackLabel";
    private const string RowNameTextChild = "QuestTrackerListName";
    private const string RowProgressTextChild = "QuestTrackerProgress";
    private static readonly Color TrackerDefaultTextColor = new Color(0.16f, 0.13f, 0.1f, 1f);
    private static bool s_hasRememberedWindowActiveState;
    private static bool s_rememberedWindowActive = true;
    private static bool s_hooksRegistered;

    [SerializeField] private RectTransform trackerContentRoot;
    [SerializeField] private TMP_Text trackerTitleText;
    [SerializeField] private QuestDatabase questDatabase;
    [SerializeField] private GameObject questTrackerRowPrefab;

    [Header("Row background (optional tint)")]
    [Tooltip(
        "Applied only while the quest objective is complete (ready to turn in). " +
        "Leave rows at the prefab Image color for in-progress quests.")]
    [SerializeField]
    private Color trackerObjectiveCompleteRowColor = new Color(0.82f, 0.96f, 0.82f, 1f);

    private CanvasGroup _canvasGroup;
    private QuestProgressManager _questProgress;
    private Inventory _inventory;
    private PlayerStorage _storage;
    private ItemDatabase _itemDatabase;
    private bool _isRefreshingRows;
    private bool _trackerRowsDirty;
    private bool _trackerStructureDirty;
    private Coroutine _deferredLayoutRebuild;

    private readonly List<GameObject> _spawnedRows = new();
    private readonly Dictionary<string, TrackerRowWidgets> _rowsByQuestId = new(StringComparer.Ordinal);
    private readonly List<string> _trackedQuestOrderScratch = new(8);
    private readonly List<string> _progressTextScratch = new(8);
    private bool _hasTrackedGatherQuest;

    private struct TrackerRowWidgets
    {
        public GameObject Row;
        public TMP_Text NameText;
        public TMP_Text ProgressText;
        public Image RowBg;
        public Color DefaultRowColor;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttachToTrackerWindow()
    {
        EnsureSceneLoadedHook();
        TryRestoreTrackerForCurrentScene();
    }

    private static void EnsureSceneLoadedHook()
    {
        if (s_hooksRegistered)
            return;
        s_hooksRegistered = true;
        SceneManager.sceneLoaded -= OnSceneLoadedRestoreTracker;
        SceneManager.sceneLoaded += OnSceneLoadedRestoreTracker;
    }

    private static void OnSceneLoadedRestoreTracker(Scene scene, LoadSceneMode mode)
    {
        TryRestoreTrackerForCurrentScene();
    }

    /// <summary>Re-show the tracker after a scene change when the player still has tracked quests (state is static; the window is recreated per scene).</summary>
    private static void TryRestoreTrackerForCurrentScene()
    {
        if (QuestTrackerState.TrackedCount <= 0)
            return;

        GameObject window = FindSceneObjectByName(TrackerWindowName);
        if (!window)
            return;

        if (!window.GetComponent<QuestTrackerWindowUI>())
            window.AddComponent<QuestTrackerWindowUI>();

        if (!window.activeSelf)
            window.SetActive(true);
    }

    private void Awake()
    {
        EnsureSceneLoadedHook();
        ResolveReferences();
        EnsureCanvasGroup();
        ApplyRememberedWindowActiveState();
    }

    private void OnEnable()
    {
        RememberWindowActiveState(true);
        ResolveReferences();
        QuestTrackerState.Changed += QueueTrackerStructureRefresh;

        if (_questProgress != null)
            _questProgress.ProgressChanged += QueueTrackerProgressRefresh;
        if (_inventory != null)
            _inventory.OnInventoryChanged += OnInventoryOrStorageChangedForTracker;
        if (_storage != null)
            _storage.OnStorageChanged += OnInventoryOrStorageChangedForTracker;

        _trackerStructureDirty = true;
        RefreshRows();
    }

    private void OnDisable()
    {
        if (_deferredLayoutRebuild != null)
        {
            StopCoroutine(_deferredLayoutRebuild);
            _deferredLayoutRebuild = null;
        }

        QuestTrackerState.Changed -= QueueTrackerStructureRefresh;

        if (_questProgress != null)
            _questProgress.ProgressChanged -= QueueTrackerProgressRefresh;
        if (_inventory != null)
            _inventory.OnInventoryChanged -= OnInventoryOrStorageChangedForTracker;
        if (_storage != null)
            _storage.OnStorageChanged -= OnInventoryOrStorageChangedForTracker;

        _trackerRowsDirty = false;
        _trackerStructureDirty = false;
    }

    private void QueueTrackerStructureRefresh()
    {
        _trackerStructureDirty = true;
        _trackerRowsDirty = true;
    }

    private void QueueTrackerProgressRefresh()
    {
        _trackerRowsDirty = true;
    }

    private void OnInventoryOrStorageChangedForTracker()
    {
        if (!_hasTrackedGatherQuest)
            return;

        QueueTrackerProgressRefresh();
    }

    private void LateUpdate()
    {
        if (!_trackerRowsDirty)
            return;
        _trackerRowsDirty = false;
        RefreshRows();
    }

    private void ResolveReferences()
    {
        if (!trackerContentRoot)
        {
            Transform content = transform.Find(TrackerContentName);
            if (content)
                trackerContentRoot = content as RectTransform;
        }

        if (!trackerTitleText)
            trackerTitleText = FindChildByName(transform, TrackerTitleName)?.GetComponent<TMP_Text>();

        if (!questDatabase)
            questDatabase = Resources.Load<QuestDatabase>("Databases/QuestDatabase_Main");

        if (!_itemDatabase)
            _itemDatabase = Resources.Load<ItemDatabase>("Databases/ItemDatabase");

        if (_questProgress == null)
            _questProgress = QuestProgressManager.Instance ?? FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);
        if (_inventory == null)
            _inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (_storage == null)
            _storage = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
    }

    private void RefreshRows()
    {
        if (_isRefreshingRows)
            return;
        _isRefreshingRows = true;

        ResolveReferences();

        if (!trackerContentRoot || !questDatabase)
        {
            ClearRows();
            SetTrackerVisible(false);
            _isRefreshingRows = false;
            return;
        }

        QuestTrackerState.PruneMissing(id =>
        {
            QuestDefinition def = FindQuestDefinition(id);
            if (!def)
                return false;
            bool permanentlyDone = _questProgress != null && _questProgress.IsPermanentlyComplete(def);
            return !permanentlyDone;
        });
        QuestTrackerState.PruneToMaxCount();

        CacheTrackedGatherQuestFlag();

        bool structureDirty = _trackerStructureDirty;
        _trackerStructureDirty = false;
        _trackerRowsDirty = false;

        if (structureDirty || !TryRefreshTrackedProgressInPlace())
            RebuildAllRows();

        _isRefreshingRows = false;
    }

    private void CacheTrackedGatherQuestFlag()
    {
        _hasTrackedGatherQuest = false;
        IReadOnlyList<string> tracked = QuestTrackerState.OrderedTrackedQuestIds;
        for (int i = 0; i < tracked.Count; i++)
        {
            QuestDefinition q = FindQuestDefinition(tracked[i]);
            if (q != null && q.objectiveKind == QuestObjectiveKind.GatherItem)
            {
                _hasTrackedGatherQuest = true;
                return;
            }
        }
    }

    private bool TryRefreshTrackedProgressInPlace()
    {
        if (_rowsByQuestId.Count == 0)
            return false;

        IReadOnlyList<string> tracked = QuestTrackerState.OrderedTrackedQuestIds;
        if (tracked.Count != _rowsByQuestId.Count)
            return false;

        _trackedQuestOrderScratch.Clear();
        _progressTextScratch.Clear();

        for (int i = 0; i < tracked.Count; i++)
        {
            string questId = tracked[i];
            if (!_rowsByQuestId.ContainsKey(questId))
                return false;

            _trackedQuestOrderScratch.Add(questId);

            QuestDefinition q = FindQuestDefinition(questId);
            if (!q)
                return false;

            int current = _questProgress != null ? _questProgress.GetDisplayProgress(q) : 0;
            int target = Mathf.Max(1, q.targetCount);
            _progressTextScratch.Add(FormatTrackerObjectiveProgress(q, current, target));
        }

        bool anyTextChanged = false;
        bool anyVisualChanged = false;

        for (int i = 0; i < _trackedQuestOrderScratch.Count; i++)
        {
            string questId = _trackedQuestOrderScratch[i];
            TrackerRowWidgets widgets = _rowsByQuestId[questId];
            if (!widgets.Row)
                return false;

            QuestDefinition q = FindQuestDefinition(questId);
            if (!q)
                return false;

            int current = _questProgress != null ? _questProgress.GetDisplayProgress(q) : 0;
            bool objectiveComplete = q.IsComplete(current);
            string progress = _progressTextScratch[i];

            if (widgets.ProgressText != null && widgets.ProgressText.text != progress)
            {
                widgets.ProgressText.text = progress;
                anyTextChanged = true;
            }

            if (widgets.RowBg != null)
            {
                Color want = objectiveComplete ? trackerObjectiveCompleteRowColor : widgets.DefaultRowColor;
                if (widgets.RowBg.color != want)
                {
                    widgets.RowBg.color = want;
                    anyVisualChanged = true;
                }
            }
        }

        if (!anyTextChanged && !anyVisualChanged)
            return true;

        if (anyTextChanged)
            RebuildTrackerLayoutImmediate();
        else if (anyVisualChanged)
            LayoutRebuilder.MarkLayoutForRebuild(trackerContentRoot);

        return true;
    }

    private void RebuildAllRows()
    {
        ClearRows();

        IReadOnlyList<string> tracked = QuestTrackerState.OrderedTrackedQuestIds;
        int renderedCount = 0;
        for (int i = 0; i < tracked.Count; i++)
        {
            string questId = tracked[i];
            QuestDefinition q = FindQuestDefinition(questId);
            if (!q)
                continue;

            int current = _questProgress != null ? _questProgress.GetDisplayProgress(q) : 0;
            int target = Mathf.Max(1, q.targetCount);
            bool objectiveComplete = q.IsComplete(current);
            string progress = FormatTrackerObjectiveProgress(q, current, target);

            CreateRow(q.questId, q.displayName, progress, objectiveComplete);
            renderedCount++;
        }

        RefreshTitle(renderedCount);
        SetTrackerVisible(renderedCount > 0);

        if (renderedCount > 0)
        {
            RebuildTrackerLayoutImmediate();
            ScheduleDeferredLayoutRebuild();
        }
    }

    private void ClearRows()
    {
        for (int i = 0; i < _spawnedRows.Count; i++)
        {
            if (_spawnedRows[i])
                Destroy(_spawnedRows[i]);
        }
        _spawnedRows.Clear();
        _rowsByQuestId.Clear();
        _trackedQuestOrderScratch.Clear();
        _progressTextScratch.Clear();
    }

    private void CreateRow(string questId, string questName, string progressText, bool objectiveComplete)
    {
        if (!questTrackerRowPrefab || string.IsNullOrWhiteSpace(questId))
            return;

        GameObject row = Instantiate(questTrackerRowPrefab, trackerContentRoot);
        row.name = "TrackedQuestRow";
        _spawnedRows.Add(row);

        RectTransform rowRt = row.transform as RectTransform;
        if (rowRt)
        {
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = Vector2.zero;
            rowRt.sizeDelta = new Vector2(0f, rowRt.sizeDelta.y);
        }

        TMP_Text nameText = row.transform.Find(RowNameTextChild)?.GetComponent<TMP_Text>();
        TMP_Text progressDisplayText = row.transform.Find(RowProgressTextChild)?.GetComponent<TMP_Text>();
        Image rowBg = row.GetComponent<Image>();
        Color defaultRowColor = rowBg != null ? rowBg.color : Color.white;
        if (rowBg != null && objectiveComplete)
            rowBg.color = trackerObjectiveCompleteRowColor;
        if (nameText)
        {
            nameText.text = questName;
            nameText.color = TrackerDefaultTextColor;
        }
        if (progressDisplayText)
        {
            progressDisplayText.text = progressText;
            progressDisplayText.color = TrackerDefaultTextColor;
        }

        Button rowButton = row.GetComponent<Button>();
        if (rowButton == null)
            rowButton = row.AddComponent<Button>();
        Image targetImage = row.GetComponent<Image>();
        if (targetImage != null)
            rowButton.targetGraphic = targetImage;
        rowButton.transition = Selectable.Transition.None;
        rowButton.onClick.RemoveAllListeners();
        rowButton.onClick.AddListener(() => OnTrackedQuestRowClicked(questId));

        _rowsByQuestId[questId.Trim()] = new TrackerRowWidgets
        {
            Row = row,
            NameText = nameText,
            ProgressText = progressDisplayText,
            RowBg = rowBg,
            DefaultRowColor = defaultRowColor
        };
    }

    private void RebuildTrackerLayoutImmediate()
    {
        if (!trackerContentRoot)
            return;

        Canvas.ForceUpdateCanvases();

        for (int i = 0; i < _spawnedRows.Count; i++)
        {
            if (!_spawnedRows[i])
                continue;
            RectTransform rowRt = _spawnedRows[i].transform as RectTransform;
            if (rowRt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rowRt);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(trackerContentRoot);

        RectTransform contentParent = trackerContentRoot.parent as RectTransform;
        if (contentParent)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent);

        SyncResizeHandlesToTrackerContent();
    }

    private void SyncResizeHandlesToTrackerContent()
    {
        if (!isActiveAndEnabled || !trackerContentRoot)
            return;

        UIWindowCornerResize resizer = GetComponent<UIWindowCornerResize>();
        if (resizer != null)
            resizer.SetBottomResizeHandleParent(trackerContentRoot);
    }

    private void ScheduleDeferredLayoutRebuild()
    {
        if (_deferredLayoutRebuild != null)
            StopCoroutine(_deferredLayoutRebuild);
        _deferredLayoutRebuild = StartCoroutine(DeferredLayoutRebuildRoutine());
    }

    private IEnumerator DeferredLayoutRebuildRoutine()
    {
        yield return null;
        _deferredLayoutRebuild = null;
        if (!this || !isActiveAndEnabled || !trackerContentRoot)
            yield break;

        for (int i = 0; i < _spawnedRows.Count; i++)
        {
            if (!_spawnedRows[i])
                continue;
            foreach (TMP_Text tmp in _spawnedRows[i].GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp)
                    tmp.ForceMeshUpdate(true);
            }
        }

        RebuildTrackerLayoutImmediate();
    }

    private QuestDefinition FindQuestDefinition(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId) || questDatabase == null || questDatabase.All == null)
            return null;

        string key = questId.Trim();
        IReadOnlyList<QuestDefinition> all = questDatabase.All;
        for (int i = 0; i < all.Count; i++)
        {
            QuestDefinition q = all[i];
            if (!q || string.IsNullOrWhiteSpace(q.questId))
                continue;
            if (string.Equals(q.questId.Trim(), key, System.StringComparison.Ordinal))
                return q;
        }

        return null;
    }

    private void RefreshTitle(int trackedCount)
    {
        if (!trackerTitleText)
            return;

        int current = Mathf.Clamp(trackedCount, 0, QuestTrackerState.MaxTrackedQuestCount);
        trackerTitleText.text = $"Quest Tracker ({current}/{QuestTrackerState.MaxTrackedQuestCount})";
    }

    private void OnTrackedQuestRowClicked(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId))
            return;

        MainMenuWindowUI menu = MainMenuWindowUI.Resolve();
        bool questPageAlreadyOpen =
            menu != null &&
            menu.IsOpen &&
            menu.CurrentPage != null &&
            string.Equals(menu.CurrentPage.name, "QuestPage", System.StringComparison.Ordinal);
        if (!questPageAlreadyOpen)
            menu?.OpenQuest();

        StartCoroutine(SelectQuestNextFrame(questId));
    }

    private static IEnumerator SelectQuestNextFrame(string questId)
    {
        yield return null;
        QuestPageUI questPage = FindFirstObjectByType<QuestPageUI>(FindObjectsInactive.Include);
        if (questPage != null)
            questPage.SelectQuestById(questId);
    }

    private void EnsureCanvasGroup()
    {
        if (!_canvasGroup)
            _canvasGroup = GetComponent<CanvasGroup>();
        if (!_canvasGroup)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void SetTrackerVisible(bool visible)
    {
        EnsureCanvasGroup();
        _canvasGroup.alpha = visible ? 1f : 0f;
        _canvasGroup.interactable = visible;
        _canvasGroup.blocksRaycasts = visible;
    }

    private void ApplyRememberedWindowActiveState()
    {
        if (QuestTrackerState.TrackedCount > 0)
        {
            gameObject.SetActive(true);
            return;
        }

        if (!s_hasRememberedWindowActiveState || s_rememberedWindowActive)
            return;

        gameObject.SetActive(false);
    }

    private void RememberWindowActiveState(bool active)
    {
        s_hasRememberedWindowActiveState = true;
        s_rememberedWindowActive = active;
    }

    public void RememberWindowClosedByUser()
    {
        RememberWindowActiveState(false);
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.hideFlags != HideFlags.None || !t.gameObject.scene.IsValid())
                continue;
            if (t.name == objectName)
                return t.gameObject;
        }

        return null;
    }

    /// <summary>
    /// Shows the quest tracker window after <see cref="QuestTrackerState.TrackQuest"/> so <see cref="OnEnable"/> refresh includes the new quest.
    /// </summary>
    public static void EnsureWindowOpenAfterTrack()
    {
        QuestTrackerWindowUI ui =
            FindFirstObjectByType<QuestTrackerWindowUI>(FindObjectsInactive.Include);
        if (!ui)
        {
            GameObject window = FindSceneObjectByName(TrackerWindowName);
            if (window)
                ui = window.GetComponent<QuestTrackerWindowUI>();
        }

        if (!ui)
            return;

        if (!ui.gameObject.activeSelf)
            ui.gameObject.SetActive(true);
    }

    private static string ResolveSpecialObjectiveTextOrDefault(QuestDefinition q, string fallback)
    {
        if (q != null && !string.IsNullOrWhiteSpace(q.specialObjectiveListText))
            return q.specialObjectiveListText.Trim();
        return fallback;
    }

    private string FormatTrackerObjectiveProgress(QuestDefinition q, int current, int target)
    {
        int c = Mathf.Clamp(current, 0, target);
        int t = Mathf.Max(1, target);

        return q.objectiveKind switch
        {
            QuestObjectiveKind.KillCount => FormatKillTrackerLine(q, c, t),
            QuestObjectiveKind.GatherItem => FormatGatherTrackerLine(q, c, t),
            QuestObjectiveKind.DieOnce => ResolveSpecialObjectiveTextOrDefault(q, "Die once"),
            _ => $"{c}/{t}"
        };
    }

    private string FormatKillTrackerLine(QuestDefinition q, int c, int t)
    {
        string id = q.ResolveKillDisplayEnemyId();
        if (string.IsNullOrEmpty(id))
            return $"{c}/{t} kills";

        EnemyDatabase ed = Resources.Load<EnemyDatabase>("Databases/EnemyDatabase");
        EnemyDefinition def = ed ? ed.Get(id) : null;
        string label = def && !string.IsNullOrWhiteSpace(def.displayName)
            ? def.displayName.Trim()
            : FormatTrackerEnemyIdFallback(id);
        return $"{c}/{t} {label}";
    }

    private string FormatGatherTrackerLine(QuestDefinition q, int c, int t)
    {
        if (string.IsNullOrWhiteSpace(q.objectiveId))
            return $"{c}/{t} gathered";

        ItemDefinition item = _itemDatabase ? _itemDatabase.Get(q.objectiveId.Trim()) : null;
        string label = item && !string.IsNullOrWhiteSpace(item.displayName)
            ? item.displayName.Trim()
            : FormatTrackerItemIdFallback(q.objectiveId.Trim());
        return $"{c}/{t} {label}";
    }

    private static string FormatTrackerItemIdFallback(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        string[] parts = raw.Trim().Split('_');
        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i];
            if (string.IsNullOrEmpty(p))
                continue;
            parts[i] = char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p.Substring(1).ToLowerInvariant() : "");
        }

        return string.Join(" ", parts);
    }

    private static string FormatTrackerEnemyIdFallback(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";
        if (raw.StartsWith("enemy_", System.StringComparison.Ordinal))
            raw = raw.Substring("enemy_".Length);
        return raw.Replace('_', ' ');
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
                return child;

            Transform nested = FindChildByName(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
