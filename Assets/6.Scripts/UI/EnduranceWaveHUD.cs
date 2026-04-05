using TMPro;
using UnityEngine;

/// <summary>
/// Shows "Wave: n/total" only while an <see cref="MapNodeType.EnduranceTrial"/> level is active and not finished.
/// </summary>
[AddComponentMenu("Desktop Idle Game/UI/Endurance Wave HUD")]
public class EnduranceWaveHUD : MonoBehaviour
{
    [Header("Which trial (optional)")]
    [Tooltip("When set, this HUD only shows for this map node (matched by nodeId). Leave empty for a single-trial scene.")]
    [SerializeField] private MapNodeDefinition trialMap;

    [Tooltip("Object toggled on/off for endurance trials (e.g. panel). If unset, this component's GameObject is toggled.")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text waveText;

    [Header("Optional")]
    [SerializeField] private TMP_Text nextWaveCountdownText;

    [Tooltip("String.Format with {0} = whole seconds, e.g. \"Next wave in: {0}\"")]
    [SerializeField] private string nextWaveCountdownFormat = "Next wave in: {0}";

    [Tooltip("Shown on Wave text when all waves are cleared.")]
    [SerializeField] private string trialsCompleteLabel = "Trials complete";

    private void Update()
    {
        EnduranceTrialDirector d = EnduranceTrialDirector.Instance;
        MapNodeDefinition active = EnduranceTrialUIHelpers.TryGetActiveEnduranceMapNode();
        bool match = EnduranceTrialUIHelpers.MatchesAssignedTrial(trialMap, active);
        bool show = d != null && d.ShowEnduranceHud && match;

        GameObject r = panelRoot != null ? panelRoot : gameObject;
        r.SetActive(show);

        if (!show)
        {
            if (nextWaveCountdownText)
                nextWaveCountdownText.gameObject.SetActive(false);
            return;
        }

        if (waveText)
        {
            if (d.IsWaitingForPlayerBegin)
            {
                waveText.text = string.Empty;
            }
            else if (d.TrialCompleted)
            {
                waveText.text = string.IsNullOrEmpty(trialsCompleteLabel) ? "Trials complete" : trialsCompleteLabel;
            }
            else
            {
                int total = d.TotalWaves;
                if (total > 0)
                    waveText.text = $"Wave: {d.CurrentWaveDisplay}/{total}";
                else
                    waveText.text = $"Wave: {d.CurrentWaveDisplay}";
            }
        }

        if (nextWaveCountdownText)
        {
            int cd = d.NextWaveCountdownSeconds;
            bool showCd = cd > 0 && !d.TrialCompleted;
            nextWaveCountdownText.gameObject.SetActive(showCd);
            if (showCd)
            {
                string fmt = string.IsNullOrEmpty(nextWaveCountdownFormat) ? "Next wave in: {0}" : nextWaveCountdownFormat;
                nextWaveCountdownText.text = string.Format(fmt, cd);
            }
        }
    }
}
