using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Endurance trial panel: pre-trial setup (Begin) and post-trial summary (Trial complete, obtained loot, tier unlock).
/// Assign <see cref="trialMap"/> so this popup only applies to that node when multiple trials exist.
/// </summary>
[AddComponentMenu("Desktop Idle Game/UI/Endurance Trials Begin Popup")]
public class EnduranceTrialsBeginPopup : MonoBehaviour
{
    [Header("Which trial (optional)")]
    [Tooltip("When set, this popup only shows for this map node (matched by nodeId). Leave empty for a single-trial scene.")]
    [SerializeField] private MapNodeDefinition trialMap;

    [Tooltip("If unset, this GameObject is toggled.")]
    [SerializeField] private GameObject popupRoot;

    [SerializeField] private Button beginButton;
    [Tooltip("Optional TMP on the Begin button. Pre-trial: Begin; completion: Continue.")]
    [SerializeField] private TMP_Text beginButtonLabel;
    [SerializeField] private string beginButtonTextPreTrial = "Begin";
    [SerializeField] private string beginButtonTextAfterTrial = "Continue";

    [Header("Content (TMP)")]
    [Tooltip("Large title (e.g. HeaderText). Set from the active map node's display name.")]
    [SerializeField] private TMP_Text trialsHeaderText;
    [SerializeField] private TMP_Text wavesText;
    [SerializeField] private TMP_Text recommendedCpText;
    [SerializeField] private TMP_Text finalEnemyText;
    [SerializeField] private TMP_Text lootText;

    [Header("Difficulty tier")]
    [SerializeField] private TMP_Text tierLabel;
    [SerializeField] private Button tierPrevButton;
    [SerializeField] private Button tierNextButton;
    [Tooltip("Optional: one TMP per tier (I–V). Locked tiers use tierLockedColor; selected uses tierSelectedColor.")]
    [SerializeField] private TMP_Text[] tierStepLabels;
    [SerializeField] private Color tierAvailableColor = Color.white;
    [SerializeField] private Color tierLockedColor = new Color32(120, 120, 120, 255);
    [SerializeField] private Color tierSelectedColor = new Color32(255, 220, 120, 255);
    [Tooltip("Shows the highest difficulty tier unlocked for this trial (from save).")]
    [SerializeField] private TMP_Text highestUnlockedText;

    [Header("Display format")]
    [SerializeField] private string wavesFormat = "Waves: {0}";
    [SerializeField] private string recommendedCpFormat = "Recommended CP: {0}";
    [Tooltip("{0} = enemy display name, {1} = combat power (rounded).")]
    [SerializeField] private string finalEnemyFormat = "Final: {0} (CP: {1})";
    [SerializeField] private string finalEnemyUnknown = "—";
    [SerializeField] private string tierFormat = "Tier {0}";
    [Tooltip("{0} = Roman numeral for highest unlocked tier (same as max selectable).")]
    [SerializeField] private string highestUnlockedFormat = "Highest unlocked: Tier {0}";

    [Header("Completion summary (after last wave)")]
    [SerializeField] private string trialCompleteHeader = "Trial complete";
    [Tooltip("{0} = Roman numeral of the tier that was just unlocked for selection.")]
    [SerializeField] private string tierUnlockedFormat = "Difficulty Tier {0} now unlocked";
    [Tooltip("Shown when no new tier was unlocked (e.g. already at max). {0} = Roman numeral of the tier you cleared.")]
    [SerializeField] private string tierUnlockedNoneFormat = "Completed at Tier {0}";

    private MapNodeDefinition _lastRefreshed;
    private int _selectedTier = 1;
    private WorldMapProgressManager _progressEventsTarget;
    private bool _wasWaitingForBeginVisible;
    private Coroutine _deferredLayoutCoroutine;
    private bool _completionSummaryDismissed;
    private bool _refreshedCompletionUi;

    private void Awake()
    {
        if (!popupRoot)
            popupRoot = gameObject;

        if (!beginButton)
            beginButton = GetComponentInChildren<Button>(true);

        if (beginButton != null)
            beginButton.onClick.AddListener(OnBeginClicked);

        if (tierPrevButton != null)
            tierPrevButton.onClick.AddListener(OnTierPrevClicked);
        if (tierNextButton != null)
            tierNextButton.onClick.AddListener(OnTierNextClicked);
    }

    private void OnDestroy()
    {
        if (beginButton != null)
            beginButton.onClick.RemoveListener(OnBeginClicked);
        if (tierPrevButton != null)
            tierPrevButton.onClick.RemoveListener(OnTierPrevClicked);
        if (tierNextButton != null)
            tierNextButton.onClick.RemoveListener(OnTierNextClicked);

        if (_deferredLayoutCoroutine != null)
        {
            StopCoroutine(_deferredLayoutCoroutine);
            _deferredLayoutCoroutine = null;
        }

        UnsubscribeProgress();
    }

    private void OnEnable()
    {
        TrySubscribeProgress();
    }

    private void OnDisable()
    {
        UnsubscribeProgress();
    }

    private void TrySubscribeProgress()
    {
        WorldMapProgressManager p = WorldMapProgressManager.Instance != null
            ? WorldMapProgressManager.Instance
            : FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        if (!p || p == _progressEventsTarget)
            return;

        UnsubscribeProgress();
        _progressEventsTarget = p;
        _progressEventsTarget.ProgressChanged += OnMapProgressChanged;
    }

    private void UnsubscribeProgress()
    {
        if (_progressEventsTarget != null)
        {
            _progressEventsTarget.ProgressChanged -= OnMapProgressChanged;
            _progressEventsTarget = null;
        }
    }

    private void OnMapProgressChanged()
    {
        // Same full path as tier arrows: progress can load after first LateUpdate, leaving _lastRefreshed null.
        TryFullRefreshForActiveTrial();
    }

    /// <summary>
    /// Called from <see cref="EnduranceTrialDirector"/> when the last wave is cleared. Required because while
    /// <see cref="popupRoot"/> is inactive, this behaviour does not receive <see cref="LateUpdate"/>, so the
    /// completion summary would never show otherwise.
    /// </summary>
    public void NotifyTrialCompletedFromDirector()
    {
        EnduranceTrialDirector d = EnduranceTrialDirector.Instance;
        if (d == null || !d.TrialCompleted)
            return;

        MapNodeDefinition active = EnduranceTrialUIHelpers.TryGetActiveEnduranceMapNode();
        if (!EnduranceTrialUIHelpers.MatchesAssignedTrial(trialMap, active))
            return;

        _completionSummaryDismissed = false;
        _wasWaitingForBeginVisible = false;

        if (popupRoot != null && !popupRoot.activeSelf)
            popupRoot.SetActive(true);

        RefreshCompletionSummary(d);
        _refreshedCompletionUi = true;
        ScheduleDeferredLayoutRebuild();
    }

    private void LateUpdate()
    {
        EnduranceTrialDirector d = EnduranceTrialDirector.Instance;
        MapNodeDefinition active = EnduranceTrialUIHelpers.TryGetActiveEnduranceMapNode();

        bool match = EnduranceTrialUIHelpers.MatchesAssignedTrial(trialMap, active);

        if (d != null && !d.TrialCompleted)
        {
            _completionSummaryDismissed = false;
            _refreshedCompletionUi = false;
        }

        bool postComplete = d != null && match && d.TrialCompleted && !_completionSummaryDismissed;
        bool preTrial = d != null && match && d.IsWaitingForPlayerBegin;
        bool show = d != null && match && (preTrial || postComplete);

        if (popupRoot != null && popupRoot.activeSelf != show)
            popupRoot.SetActive(show);

        if (!show)
        {
            _wasWaitingForBeginVisible = false;
            if (d == null || !d.ShowEnduranceHud || !match)
            {
                _lastRefreshed = null;
                _completionSummaryDismissed = false;
                _refreshedCompletionUi = false;
            }

            return;
        }

        if (_progressEventsTarget == null)
            TrySubscribeProgress();

        bool becameVisible = !_wasWaitingForBeginVisible;
        _wasWaitingForBeginVisible = true;

        if (active == null)
            return;

        if (postComplete)
        {
            if (!_refreshedCompletionUi)
            {
                RefreshCompletionSummary(d);
                _refreshedCompletionUi = true;
                ScheduleDeferredLayoutRebuild();
            }

            return;
        }

        // Pre-trial: first open or node changed: reset tier and full refresh. Re-show same node: refresh when panel becomes visible (layout/TMP).
        if (active != _lastRefreshed)
        {
            _lastRefreshed = active;
            _selectedTier = 1;
            RefreshContent(active);
            ScheduleDeferredLayoutRebuild();
            return;
        }

        if (becameVisible)
        {
            RefreshContent(active);
            ScheduleDeferredLayoutRebuild();
        }
    }

    /// <summary>Full content refresh when map progress updates while this trial is relevant (same path as tier buttons).</summary>
    private void TryFullRefreshForActiveTrial()
    {
        EnduranceTrialDirector d = EnduranceTrialDirector.Instance;
        MapNodeDefinition active = EnduranceTrialUIHelpers.TryGetActiveEnduranceMapNode();
        if (active == null)
            return;
        if (!EnduranceTrialUIHelpers.MatchesAssignedTrial(trialMap, active))
            return;
        if (d == null || !d.IsWaitingForPlayerBegin)
            return;

        _lastRefreshed = active;
        RefreshContent(active);
        ScheduleDeferredLayoutRebuild();
    }

    private void RefreshContent(MapNodeDefinition def)
    {
        if (trialsHeaderText)
        {
            string title = string.IsNullOrWhiteSpace(def.displayName) ? def.name : def.displayName.Trim();
            trialsHeaderText.text = title;
        }

        int totalWaves = def.enduranceWaves != null ? def.enduranceWaves.Count : 0;

        if (wavesText)
        {
            string fmt = string.IsNullOrEmpty(wavesFormat) ? "Waves: {0}" : wavesFormat;
            wavesText.text = string.Format(fmt, totalWaves);
        }

        RefreshTierRow(def);

        if (lootText)
            lootText.text = EnduranceTrialUIHelpers.BuildEnduranceCompletionLootSummary(def);

        if (beginButtonLabel)
            beginButtonLabel.text = beginButtonTextPreTrial;

        // Single layout pass after waves + tier row + loot (tier row alone calls RefreshTierRow + ForceLayoutRebuild on arrow clicks).
        ForceLayoutRebuild();
    }

    private void RefreshCompletionSummary(EnduranceTrialDirector d)
    {
        if (trialsHeaderText)
        {
            trialsHeaderText.text = string.IsNullOrEmpty(trialCompleteHeader)
                ? "Trial complete"
                : trialCompleteHeader;
        }

        if (wavesText)
            wavesText.text = string.Empty;
        if (recommendedCpText)
            recommendedCpText.text = string.Empty;
        if (finalEnemyText)
            finalEnemyText.text = string.Empty;

        if (tierLabel)
        {
            string roman = EnduranceTrialTier.ToRomanNumeral(d.LastCompletedRunTier);
            tierLabel.text = string.IsNullOrEmpty(tierFormat)
                ? $"Tier {roman}"
                : string.Format(tierFormat, roman);
        }

        if (tierPrevButton != null)
            tierPrevButton.interactable = false;
        if (tierNextButton != null)
            tierNextButton.interactable = false;

        if (highestUnlockedText)
        {
            if (d.LastRunUnlockedNextTier && d.LastUnlockedTier > 0)
            {
                string roman = EnduranceTrialTier.ToRomanNumeral(d.LastUnlockedTier);
                highestUnlockedText.text = string.IsNullOrEmpty(tierUnlockedFormat)
                    ? $"Difficulty Tier {roman} now unlocked"
                    : string.Format(tierUnlockedFormat, roman);
            }
            else if (string.IsNullOrEmpty(tierUnlockedNoneFormat))
            {
                highestUnlockedText.text = string.Empty;
            }
            else
            {
                string roman = EnduranceTrialTier.ToRomanNumeral(d.LastCompletedRunTier);
                highestUnlockedText.text = string.Format(tierUnlockedNoneFormat, roman);
            }
        }

        if (lootText)
            lootText.text = EnduranceTrialUIHelpers.BuildObtainedLootSummary(d.LastCompletionLootGrants);

        if (beginButtonLabel)
            beginButtonLabel.text = beginButtonTextAfterTrial;

        ForceLayoutRebuild();
    }

    private void ScheduleDeferredLayoutRebuild()
    {
        if (!isActiveAndEnabled)
            return;
        if (_deferredLayoutCoroutine != null)
            StopCoroutine(_deferredLayoutCoroutine);
        _deferredLayoutCoroutine = StartCoroutine(CoDeferredLayoutRebuild());
    }

    private IEnumerator CoDeferredLayoutRebuild()
    {
        // After SetActive + text changes, TMP and nested layout groups need a frame before sizes are final.
        yield return null;
        ForceLayoutRebuild();
        yield return new WaitForEndOfFrame();
        ForceLayoutRebuild();
        _deferredLayoutCoroutine = null;
    }

    /// <summary>
    /// Updates TMP meshes, then rebuilds layout. Nested Content → LeftSection/RightSection layouts require
    /// rebuilding child <see cref="LayoutGroup"/>s deepest-first; a single rebuild on the panel root is not enough.
    /// </summary>
    private void ForceLayoutRebuild()
    {
        if (!popupRoot)
            return;

        RectTransform rootRt = popupRoot.GetComponent<RectTransform>();
        if (rootRt == null)
            return;

        foreach (TMP_Text tmp in popupRoot.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp)
                tmp.ForceMeshUpdate(true);
        }

        Canvas.ForceUpdateCanvases();

        Transform rootT = popupRoot.transform;
        LayoutGroup[] groups = popupRoot.GetComponentsInChildren<LayoutGroup>(true);
        System.Array.Sort(groups, (a, b) =>
            DepthBelow(b.transform, rootT).CompareTo(DepthBelow(a.transform, rootT)));

        for (int i = 0; i < groups.Length; i++)
        {
            RectTransform rt = groups[i].transform as RectTransform;
            if (rt != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(rootRt);
    }

    private static int DepthBelow(Transform node, Transform ancestor)
    {
        int d = 0;
        Transform t = node;
        while (t != null && t != ancestor)
        {
            d++;
            t = t.parent;
        }

        return d;
    }

    private void RefreshRecommendedCp(MapNodeDefinition def)
    {
        if (!recommendedCpText)
            return;

        RecommendedCombatPower.Options opt = EnduranceTrialTier.BuildRecommendedCpOptions(_selectedTier);
        int recCp = RecommendedCombatPower.ComputeForEnduranceTrial(def, opt);
        string fmt = string.IsNullOrEmpty(recommendedCpFormat) ? "Recommended CP: {0}" : recommendedCpFormat;
        recommendedCpText.text = string.Format(fmt, recCp);
    }

    private void RefreshFinalEnemy(MapNodeDefinition def)
    {
        if (!finalEnemyText)
            return;

        EnemyDefinition lastEnemy = EnduranceTrialUIHelpers.GetLastEnemyDefinitionInEnduranceTrial(def);
        if (lastEnemy != null)
        {
            string name = string.IsNullOrWhiteSpace(lastEnemy.displayName)
                ? lastEnemy.name
                : lastEnemy.displayName.Trim();
            int cp = EnduranceTrialUIHelpers.GetEnemyCombatPowerRounded(lastEnemy);
            float dMult = EnduranceTrialTier.GetDamageMultiplier(_selectedTier);
            int displayCp = Mathf.Max(1, Mathf.RoundToInt(cp * dMult));
            string fmt = string.IsNullOrEmpty(finalEnemyFormat) ? "Final: {0} (CP: {1})" : finalEnemyFormat;
            finalEnemyText.text = string.Format(fmt, name, displayCp);
        }
        else
        {
            finalEnemyText.text = finalEnemyUnknown;
        }
    }

    private void RefreshTierRow(MapNodeDefinition def)
    {
        WorldMapProgressManager progress = WorldMapProgressManager.Instance != null
            ? WorldMapProgressManager.Instance
            : FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);

        int maxSel = progress != null
            ? progress.GetEnduranceMaxSelectableTier(def.nodeId)
            : 1;

        _selectedTier = Mathf.Clamp(_selectedTier, EnduranceTrialTier.MinTier, maxSel);

        if (tierLabel)
        {
            string roman = EnduranceTrialTier.ToRomanNumeral(_selectedTier);
            tierLabel.text = string.IsNullOrEmpty(tierFormat)
                ? $"Tier {roman}"
                : string.Format(tierFormat, roman);
        }

        if (tierPrevButton != null)
            tierPrevButton.interactable = _selectedTier > EnduranceTrialTier.MinTier;

        if (tierNextButton != null)
            tierNextButton.interactable = _selectedTier < maxSel;

        if (highestUnlockedText)
        {
            string highestRoman = EnduranceTrialTier.ToRomanNumeral(maxSel);
            highestUnlockedText.text = string.IsNullOrEmpty(highestUnlockedFormat)
                ? $"Highest unlocked: Tier {highestRoman}"
                : string.Format(highestUnlockedFormat, highestRoman);
        }

        if (tierStepLabels != null && tierStepLabels.Length > 0)
        {
            for (int i = 0; i < tierStepLabels.Length; i++)
            {
                TMP_Text t = tierStepLabels[i];
                if (!t)
                    continue;
                int tierNum = i + 1;
                bool locked = tierNum > maxSel;
                bool sel = tierNum == _selectedTier;
                t.color = locked ? tierLockedColor : (sel ? tierSelectedColor : tierAvailableColor);
            }
        }

        EnduranceTrialPendingTier.Tier = _selectedTier;
        RefreshRecommendedCp(def);
        RefreshFinalEnemy(def);
    }

    private void OnTierPrevClicked()
    {
        MapNodeDefinition def = EnduranceTrialUIHelpers.TryGetActiveEnduranceMapNode();
        if (def == null)
            return;
        _selectedTier = Mathf.Max(EnduranceTrialTier.MinTier, _selectedTier - 1);
        RefreshTierRow(def);
        ForceLayoutRebuild();
    }

    private void OnTierNextClicked()
    {
        MapNodeDefinition def = EnduranceTrialUIHelpers.TryGetActiveEnduranceMapNode();
        if (def == null)
            return;

        WorldMapProgressManager progress = WorldMapProgressManager.Instance != null
            ? WorldMapProgressManager.Instance
            : FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        int maxSel = progress != null
            ? progress.GetEnduranceMaxSelectableTier(def.nodeId)
            : 1;

        _selectedTier = Mathf.Min(maxSel, _selectedTier + 1);
        RefreshTierRow(def);
        ForceLayoutRebuild();
    }

    private void OnBeginClicked()
    {
        EnduranceTrialDirector d = EnduranceTrialDirector.Instance;
        if (d != null && d.TrialCompleted && !_completionSummaryDismissed)
        {
            _completionSummaryDismissed = true;
            if (popupRoot != null)
                popupRoot.SetActive(false);
            return;
        }

        EnduranceTrialPendingTier.Tier = _selectedTier;
        if (d != null)
            d.ConfirmBeginTrial();
    }
}
