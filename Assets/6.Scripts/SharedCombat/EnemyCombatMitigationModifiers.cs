using System.Collections;
using UnityEngine;

/// <summary>
/// Temporary armor / magic-resist rating multipliers on enemies (e.g. Executioner's Descent Sundering Impact).
/// </summary>
[DisallowMultipleComponent]
public class EnemyCombatMitigationModifiers : MonoBehaviour
{
    private float _armorRatingMultiplier = 1f;
    private float _magicResistRatingMultiplier = 1f;
    private Coroutine _shredRoutine;

    public float ArmorRatingMultiplier => _armorRatingMultiplier;
    public float MagicResistRatingMultiplier => _magicResistRatingMultiplier;

    public void ApplyArmorMrShred(float ratingMultiplier, float durationSeconds)
    {
        ratingMultiplier = Mathf.Clamp01(ratingMultiplier);
        durationSeconds = Mathf.Max(0.01f, durationSeconds);

        if (_shredRoutine != null)
            StopCoroutine(_shredRoutine);

        _armorRatingMultiplier = ratingMultiplier;
        _magicResistRatingMultiplier = ratingMultiplier;
        _shredRoutine = StartCoroutine(CoClearShredAfter(durationSeconds));
    }

    private IEnumerator CoClearShredAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _armorRatingMultiplier = 1f;
        _magicResistRatingMultiplier = 1f;
        _shredRoutine = null;
    }

    private void OnDisable()
    {
        _armorRatingMultiplier = 1f;
        _magicResistRatingMultiplier = 1f;
        _shredRoutine = null;
    }
}
