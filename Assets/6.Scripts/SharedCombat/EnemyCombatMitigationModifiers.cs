using System.Collections;
using UnityEngine;

/// <summary>
/// Temporary armour / magic-resist rating multipliers on enemies (e.g. Executioner's Descent Sundering Impact).
/// </summary>
[DisallowMultipleComponent]
public class EnemyCombatMitigationModifiers : MonoBehaviour
{
    private float _armourRatingMultiplier = 1f;
    private float _magicResistRatingMultiplier = 1f;
    private Coroutine _shredRoutine;

    public float ArmourRatingMultiplier => _armourRatingMultiplier;
    public float MagicResistRatingMultiplier => _magicResistRatingMultiplier;

    public void ApplyArmourMrShred(float ratingMultiplier, float durationSeconds)
    {
        ratingMultiplier = Mathf.Clamp01(ratingMultiplier);
        durationSeconds = Mathf.Max(0.01f, durationSeconds);

        if (_shredRoutine != null)
            StopCoroutine(_shredRoutine);

        _armourRatingMultiplier = ratingMultiplier;
        _magicResistRatingMultiplier = ratingMultiplier;
        _shredRoutine = StartCoroutine(CoClearShredAfter(durationSeconds));
    }

    private IEnumerator CoClearShredAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _armourRatingMultiplier = 1f;
        _magicResistRatingMultiplier = 1f;
        _shredRoutine = null;
    }

    private void OnDisable()
    {
        _armourRatingMultiplier = 1f;
        _magicResistRatingMultiplier = 1f;
        _shredRoutine = null;
    }
}
