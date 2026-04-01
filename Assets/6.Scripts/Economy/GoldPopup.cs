using System.Collections;
using TMPro;
using UnityEngine;

public class GoldPopup : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private float floatUpPx = 90f;
    [SerializeField] private float duration = 2f;

    [Header("Default Colours")]
    [SerializeField] private Color goldColor = new Color(0.72f, 0.52f, 0.06f, 1f);
    [SerializeField] private Color messageColor = Color.white;
    [SerializeField] private Color levelUpColor = new Color(0.35f, 0.8f, 1f, 1f);

    [Header("Gold readability")]
    [SerializeField] private float goldOutlineWidth = 0.28f;
    [SerializeField] private Color goldOutlineColor = new Color(0.12f, 0.08f, 0.02f, 1f);

    private RectTransform _rt;
    private Coroutine _co;

    private void Awake()
    {
        _rt = transform as RectTransform;
        if (!label) label = GetComponentInChildren<TMP_Text>(true);
    }

    public void PlayLocal(Vector2 startAnchoredPos, int amount)
    {
        PlayLocalText(startAnchoredPos, $"+{amount:N0}g", goldColor, applyGoldStroke: true);
    }

    public void PlayLocalText(Vector2 startAnchoredPos, string text)
    {
        PlayLocalText(startAnchoredPos, text, messageColor, applyGoldStroke: false);
    }

    public void PlayLocalText(Vector2 startAnchoredPos, string text, Color color)
    {
        PlayLocalText(startAnchoredPos, text, color, applyGoldStroke: false);
    }

    public void PlayLocalText(Vector2 startAnchoredPos, string text, Color color, bool applyGoldStroke)
    {
        gameObject.SetActive(true);

        if (label)
        {
            label.text = text;
            label.color = color;
            if (applyGoldStroke)
            {
                label.outlineWidth = goldOutlineWidth;
                label.outlineColor = goldOutlineColor;
            }
            else
            {
                label.outlineWidth = 0f;
            }
        }

        if (_rt != null)
            _rt.anchoredPosition = startAnchoredPos;

        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(AnimAnchored(startAnchoredPos, color));
    }

    public void PlayLocalLevelUp(Vector2 startAnchoredPos, string text)
    {
        PlayLocalText(startAnchoredPos, text, levelUpColor);
    }

    private IEnumerator AnimAnchored(Vector2 start, Color baseColor)
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duration);
            float ease = 1f - Mathf.Pow(1f - u, 2f);

            if (_rt != null)
                _rt.anchoredPosition = start + Vector2.up * (floatUpPx * ease);

            if (label)
            {
                var c = baseColor;
                c.a = 1f - u;
                label.color = c;
            }

            yield return null;
        }

        gameObject.SetActive(false);
    }
}