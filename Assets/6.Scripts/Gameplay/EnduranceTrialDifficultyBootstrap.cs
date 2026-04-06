using UnityEngine;

/// <summary>
/// Assign an <see cref="EnduranceTrialDifficultyScaling"/> asset here (e.g. on a DDOL systems object).
/// If unset, <see cref="EnduranceTrialTier"/> uses the built-in formula at runtime.
/// </summary>
public class EnduranceTrialDifficultyBootstrap : MonoBehaviour
{
    [SerializeField] private EnduranceTrialDifficultyScaling scaling;

    private void Awake()
    {
        EnduranceTrialTier.SetDifficultyScaling(scaling);
    }

    private void OnDestroy()
    {
        if (scaling != null && EnduranceTrialTier.ActiveScaling == scaling)
            EnduranceTrialTier.SetDifficultyScaling(null);
    }
}
