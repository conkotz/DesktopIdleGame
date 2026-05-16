using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared enter-node button label + non-faded disabled presentation for level select / world map details.
/// </summary>
public static class EnterNodeButtonPresenter
{
    public sealed class Cache
    {
        internal TMP_Text Label;
        internal Image ButtonImage;
        internal Color LabelColor;
        internal Color ImageColor;
        internal Selectable.Transition ButtonTransition = Selectable.Transition.ColorTint;
        internal bool Initialized;
    }

    public static void Refresh(
        Button button,
        TMP_Text serializedLabel,
        Cache cache,
        string enabledText,
        string blockedText,
        bool visible,
        bool canEnter)
    {
        if (!button)
            return;

        EnsureCached(button, serializedLabel, cache);

        button.gameObject.SetActive(visible);
        if (!visible)
            return;

        if (cache.Label)
            cache.Label.text = canEnter ? enabledText : blockedText;

        if (canEnter)
        {
            button.transition = cache.ButtonTransition;
            button.interactable = true;
        }
        else
        {
            button.transition = Selectable.Transition.None;
            button.interactable = false;
            ApplyBlockedPresentation(cache);
        }
    }

    private static void EnsureCached(Button button, TMP_Text serializedLabel, Cache cache)
    {
        if (cache.Initialized)
            return;

        cache.Label = serializedLabel;
        if (!cache.Label)
        {
            TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                TMP_Text label = labels[i];
                if (label && label.name == "EnterNodeText")
                {
                    cache.Label = label;
                    break;
                }
            }
        }

        cache.ButtonImage = button.targetGraphic as Image;
        if (cache.ButtonImage)
            cache.ImageColor = cache.ButtonImage.color;

        if (cache.Label)
            cache.LabelColor = cache.Label.color;

        cache.ButtonTransition = button.transition;
        cache.Initialized = true;
    }

    private static void ApplyBlockedPresentation(Cache cache)
    {
        if (cache.Label)
        {
            Color c = cache.LabelColor;
            cache.Label.color = new Color(c.r, c.g, c.b, 1f);
            cache.Label.alpha = 1f;
        }

        if (cache.ButtonImage)
        {
            Color c = cache.ImageColor;
            cache.ButtonImage.color = new Color(c.r, c.g, c.b, c.a > 0f ? c.a : 1f);
        }
    }
}
