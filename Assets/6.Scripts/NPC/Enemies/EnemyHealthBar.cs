using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class EnemyHealthBarUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private EnemyBaseController enemy;
    [SerializeField] private EnemyHover2D hover;
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private CanvasGroup group;

    [Header("Show Rules")]
    [SerializeField] private bool showOnlyWhenDamaged = true;
    [SerializeField] private bool showOnHover = true;
    [SerializeField] private bool showMaxHP = true;

    [Header("Fade")]
    [SerializeField] private float fadeInSeconds = 0.08f;
    [SerializeField] private float visibleSecondsAfterDamage = 1.2f;
    [SerializeField] private float fadeOutSeconds = 0.2f;

    [Header("Hide")]
    [SerializeField] private bool hideWhenDead = true;

    private bool _hovered;
    private Coroutine _fadeRoutine;

    private void Awake()
    {
        if (!enemy) enemy = GetComponentInParent<EnemyBaseController>();
        if (!hover) hover = GetComponentInParent<EnemyHover2D>();

        if (!group) group = GetComponent<CanvasGroup>();
        if (!group) group = gameObject.AddComponent<CanvasGroup>();

        // IMPORTANT: start hidden but ACTIVE
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void OnEnable()
    {
        if (enemy != null)
            enemy.OnHealthChanged += HandleHealthChanged;

        if (hover != null)
            hover.OnHoverChanged += HandleHoverChanged;

        RefreshImmediate();
    }

    private void OnDisable()
    {
        if (enemy != null)
            enemy.OnHealthChanged -= HandleHealthChanged;

        if (hover != null)
            hover.OnHoverChanged -= HandleHoverChanged;
    }

    private void HandleHoverChanged(bool isHovered)
    {
        _hovered = isHovered;

        if (showOnHover && _hovered)
            ShowInstant();
        else
            EvaluateVisibility(enemy ? enemy.HP : 0, enemy ? enemy.MaxHP : 0, fromDamage: false);
    }

    private void HandleHealthChanged(int current, int max)
    {
        Refresh(current, max);
        EvaluateVisibility(current, max, fromDamage: true);
    }

    private void RefreshImmediate()
    {
        if (!enemy) return;
        Refresh(enemy.HP, enemy.MaxHP);
        EvaluateVisibility(enemy.HP, enemy.MaxHP, fromDamage: false);
    }

    private void Refresh(int current, int max)
    {
        float pct = (max <= 0) ? 0f : (float)current / max;

        if (fillImage)
            fillImage.fillAmount = Mathf.Clamp01(pct);

        if (hpText)
            hpText.text = showMaxHP ? $"{current}/{max}" : current.ToString();
    }

    private void EvaluateVisibility(int current, int max, bool fromDamage)
    {
        if (!enemy) return;

        if (hideWhenDead && current <= 0)
        {
            HideInstant();
            return;
        }

        bool isFull = (max > 0 && current >= max);
        bool damaged = !isFull;

        if (showOnHover && _hovered)
        {
            ShowInstant();
            return;
        }

        if (showOnlyWhenDamaged && !damaged)
        {
            HideInstant();
            return;
        }

        if (fromDamage)
            ShowThenAutoHide();
        else
            ShowInstant();
    }

    private void ShowInstant()
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        group.alpha = 1f;
    }

    private void HideInstant()
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        group.alpha = 0f;
    }

    private void ShowThenAutoHide()
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        yield return FadeTo(1f, fadeInSeconds);

        float t = 0f;
        while (t < visibleSecondsAfterDamage)
        {
            if (showOnHover && _hovered)
            {
                group.alpha = 1f;
                _fadeRoutine = null;
                yield break;
            }

            t += Time.deltaTime;
            yield return null;
        }

        if (!(showOnHover && _hovered))
            yield return FadeTo(0f, fadeOutSeconds);

        _fadeRoutine = null;
    }

    private IEnumerator FadeTo(float target, float seconds)
    {
        if (seconds <= 0f)
        {
            group.alpha = target;
            yield break;
        }

        float start = group.alpha;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(start, target, t / seconds);
            yield return null;
        }
        group.alpha = target;
    }
}