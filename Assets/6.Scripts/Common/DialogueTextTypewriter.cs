using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Flowing dialogue reveal for <see cref="NPCDialogueBoxUI"/> and <see cref="HelperGameplayController"/>.
/// Uses TMP <see cref="TMP_Text.maxVisibleCharacters"/> so wrapping and spacing match the final paragraph while text appears gradually (character-wise in rendered output).
/// </summary>
public static class DialogueTextTypewriter
{
    public static void RestoreFullReveal(TMP_Text tmp)
    {
        if (!tmp)
            return;
        tmp.maxVisibleCharacters = int.MaxValue;
    }

    /// <param name="charactersPerSecond">&gt; 0 = timed; 0 = one visible character per frame.</param>
    public static IEnumerator RevealFlowingCharacters(TMP_Text tmp, string fullPlain, float charactersPerSecond)
    {
        if (!tmp)
            yield break;

        fullPlain ??= "";

        RestoreFullReveal(tmp);

        if (fullPlain.Length == 0)
        {
            tmp.text = "";
            yield break;
        }

        tmp.text = fullPlain;
        tmp.ForceMeshUpdate();

        int total = Mathf.Max(0, tmp.textInfo.characterCount);
        if (total == 0)
            yield break;

        tmp.maxVisibleCharacters = 0;

        float delay = charactersPerSecond > 1e-5f ? 1f / charactersPerSecond : 0f;
        bool timed = delay > 0f;

        for (int shown = 1; shown <= total; shown++)
        {
            tmp.maxVisibleCharacters = shown;
            if (shown >= total)
                break;

            if (timed)
                yield return new WaitForSeconds(delay);
            else
                yield return null;
        }

        RestoreFullReveal(tmp);
    }
}
