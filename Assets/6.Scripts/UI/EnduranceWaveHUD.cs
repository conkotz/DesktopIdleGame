using TMPro;
using UnityEngine;

/// <summary>
/// Shows "Wave: n/total" only while an <see cref="MapNodeType.EnduranceTrial"/> level is active and not finished.
/// </summary>
[AddComponentMenu("Desktop Idle Game/UI/Endurance Wave HUD")]
public class EnduranceWaveHUD : MonoBehaviour
{
    [Tooltip("Object toggled on/off for endurance trials (e.g. panel). If unset, this component's GameObject is toggled.")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text waveText;

    [Header("Optional")]
    [SerializeField] private TMP_Text nextWaveCountdownText;

    [Tooltip("String.Format with {0} = whole seconds, e.g. \"Next wave in: {0}\"")]
    [SerializeField] private string nextWaveCountdownFormat = "Next wave in: {0}";

    private void Update()
    {
        EnduranceTrialDirector d = EnduranceTrialDirector.Instance;
        bool show = d != null && d.IsActive;

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
            int total = d.TotalWaves;
            if (total > 0)
                waveText.text = $"Wave: {d.CurrentWaveDisplay}/{total}";
            else
                waveText.text = $"Wave: {d.CurrentWaveDisplay}";
        }

        if (nextWaveCountdownText)
        {
            int cd = d.NextWaveCountdownSeconds;
            bool showCd = cd > 0;
            nextWaveCountdownText.gameObject.SetActive(showCd);
            if (showCd)
            {
                string fmt = string.IsNullOrEmpty(nextWaveCountdownFormat) ? "Next wave in: {0}" : nextWaveCountdownFormat;
                nextWaveCountdownText.text = string.Format(fmt, cd);
            }
        }
    }
}
