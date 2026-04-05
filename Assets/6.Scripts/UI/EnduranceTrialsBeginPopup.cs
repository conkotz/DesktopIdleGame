using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pre-trial panel for endurance maps: visible until the player clicks Begin, then
/// <see cref="EnduranceTrialDirector.ConfirmBeginTrial"/> runs and the first wave spawns.
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

    [Header("Content (TMP)")]
    [SerializeField] private TMP_Text wavesText;
    [SerializeField] private TMP_Text recommendedCpText;
    [SerializeField] private TMP_Text finalEnemyText;
    [SerializeField] private TMP_Text lootText;

    [Header("Display format")]
    [SerializeField] private string wavesFormat = "Waves: {0}";
    [SerializeField] private string recommendedCpFormat = "Recommended CP: {0}";
    [Tooltip("{0} = enemy display name, {1} = combat power (rounded).")]
    [SerializeField] private string finalEnemyFormat = "Final: {0} (CP: {1})";
    [SerializeField] private string finalEnemyUnknown = "—";

    private MapNodeDefinition _lastRefreshed;

    private void Awake()
    {
        if (!popupRoot)
            popupRoot = gameObject;

        if (!beginButton)
            beginButton = GetComponentInChildren<Button>(true);

        if (beginButton != null)
            beginButton.onClick.AddListener(OnBeginClicked);
    }

    private void OnDestroy()
    {
        if (beginButton != null)
            beginButton.onClick.RemoveListener(OnBeginClicked);
    }

    private void LateUpdate()
    {
        EnduranceTrialDirector d = EnduranceTrialDirector.Instance;
        MapNodeDefinition active = EnduranceTrialUIHelpers.TryGetActiveEnduranceMapNode();

        bool match = EnduranceTrialUIHelpers.MatchesAssignedTrial(trialMap, active);
        bool show = d != null && d.IsWaitingForPlayerBegin && match;

        if (popupRoot != null && popupRoot.activeSelf != show)
            popupRoot.SetActive(show);

        if (!show)
        {
            _lastRefreshed = null;
            return;
        }

        if (active != null && active != _lastRefreshed)
        {
            _lastRefreshed = active;
            RefreshContent(active);
        }
    }

    private void RefreshContent(MapNodeDefinition def)
    {
        int totalWaves = def.enduranceWaves != null ? def.enduranceWaves.Count : 0;

        if (wavesText)
        {
            string fmt = string.IsNullOrEmpty(wavesFormat) ? "Waves: {0}" : wavesFormat;
            wavesText.text = string.Format(fmt, totalWaves);
        }

        if (recommendedCpText)
        {
            string fmt = string.IsNullOrEmpty(recommendedCpFormat) ? "Recommended CP: {0}" : recommendedCpFormat;
            recommendedCpText.text = string.Format(fmt, def.recommendedCombatPower);
        }

        if (finalEnemyText)
        {
            EnemyDefinition lastEnemy = EnduranceTrialUIHelpers.GetLastEnemyDefinitionInEnduranceTrial(def);
            if (lastEnemy != null)
            {
                string name = string.IsNullOrWhiteSpace(lastEnemy.displayName)
                    ? lastEnemy.name
                    : lastEnemy.displayName.Trim();
                int cp = EnduranceTrialUIHelpers.GetEnemyCombatPowerRounded(lastEnemy);
                string fmt = string.IsNullOrEmpty(finalEnemyFormat) ? "Final: {0} (CP: {1})" : finalEnemyFormat;
                finalEnemyText.text = string.Format(fmt, name, cp);
            }
            else
            {
                finalEnemyText.text = finalEnemyUnknown;
            }
        }

        if (lootText)
            lootText.text = EnduranceTrialUIHelpers.BuildEnduranceCompletionLootSummary(def);
    }

    private void OnBeginClicked()
    {
        EnduranceTrialDirector d = EnduranceTrialDirector.Instance;
        if (d != null)
            d.ConfirmBeginTrial();
    }
}
