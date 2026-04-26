using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NPCDialogueBoxUI : MonoBehaviour
{
    private static NPCDialogueBoxUI _activeBox;

    [Header("Layout")]
    [SerializeField] private Vector2 fixedSize = new(250f, 250f);
    [SerializeField] private float worldScale = 0.01f;

    [Header("Optional refs")]
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Button acceptButton;

    private Action _onAccept;
    private RectTransform _rectTransform;
    private Coroutine _autoCloseRoutine;

    private void Awake()
    {
        EnsureBuilt();
        gameObject.SetActive(false);
    }

    public void Show(Transform owner, Vector3 localOffset, string message, bool showAccept, Action onAccept, float autoCloseSeconds = 0f)
    {
        ShowAt(owner, owner, localOffset, message, showAccept, onAccept, autoCloseSeconds);
    }

    public void ShowAt(Transform parent, Transform anchor, Vector3 localOffset, string message, bool showAccept, Action onAccept, float autoCloseSeconds = 0f)
    {
        EnsureBuilt();

        if (_activeBox != null && _activeBox != this)
            _activeBox.Hide();
        _activeBox = this;

        if (parent)
        {
            transform.SetParent(parent, false);
            Vector3 anchorWorld = anchor ? anchor.position : parent.position;
            transform.position = anchorWorld + localOffset;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one * worldScale;
        }

        if (_rectTransform)
            _rectTransform.sizeDelta = fixedSize;

        if (dialogueText)
            dialogueText.text = message ?? "";

        _onAccept = onAccept;
        if (acceptButton)
        {
            acceptButton.gameObject.SetActive(showAccept);
            acceptButton.onClick.RemoveListener(HandleAcceptClicked);
            acceptButton.onClick.AddListener(HandleAcceptClicked);
        }

        gameObject.SetActive(true);
        StartAutoClose(autoCloseSeconds);
    }

    public void Hide()
    {
        if (_autoCloseRoutine != null)
        {
            StopCoroutine(_autoCloseRoutine);
            _autoCloseRoutine = null;
        }

        if (_activeBox == this)
            _activeBox = null;

        gameObject.SetActive(false);
    }

    private void StartAutoClose(float seconds)
    {
        if (_autoCloseRoutine != null)
            StopCoroutine(_autoCloseRoutine);

        _autoCloseRoutine = seconds > 0f ? StartCoroutine(AutoCloseAfter(seconds)) : null;
    }

    private IEnumerator AutoCloseAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _autoCloseRoutine = null;
        Hide();
    }

    private void HandleAcceptClicked()
    {
        _onAccept?.Invoke();
        Hide();
    }

    private void EnsureBuilt()
    {
        if (_rectTransform == null)
            _rectTransform = transform as RectTransform;
        if (_rectTransform == null)
            _rectTransform = gameObject.AddComponent<RectTransform>();

        _rectTransform.sizeDelta = fixedSize;

        Canvas canvas = GetComponent<Canvas>();
        if (!canvas)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        if (!canvas.worldCamera)
            canvas.worldCamera = Camera.main;
        canvas.overrideSorting = true;
        canvas.sortingLayerName = "UI";
        canvas.sortingOrder = 1000;

        if (!GetComponent<GraphicRaycaster>())
            gameObject.AddComponent<GraphicRaycaster>();

        Image bg = GetComponent<Image>();
        if (!bg)
            bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.08f, 0.94f);

        if (dialogueText != null && acceptButton != null)
            return;

        RectTransform closeRt = CreateButton("CloseButton", "X", _rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(24f, 24f), out Button closeButton);
        UIWindowCloseButton close = closeRt.gameObject.GetComponent<UIWindowCloseButton>();
        if (!close)
            close = closeRt.gameObject.AddComponent<UIWindowCloseButton>();
        close.Configure(gameObject);

        GameObject scrollGo = new("DialogueScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollGo.transform.SetParent(_rectTransform, false);
        RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0f, 0f);
        scrollRt.anchorMax = new Vector2(1f, 1f);
        scrollRt.offsetMin = new Vector2(10f, 45f);
        scrollRt.offsetMax = new Vector2(-10f, -35f);
        scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);

        GameObject viewportGo = new("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportGo.transform.SetParent(scrollRt, false);
        RectTransform viewportRt = viewportGo.GetComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        viewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
        viewportGo.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentGo = new("Content", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewportRt, false);
        RectTransform contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.offsetMin = new Vector2(6f, 0f);
        contentRt.offsetMax = new Vector2(-6f, 0f);

        dialogueText = contentGo.GetComponent<TMP_Text>();
        dialogueText.fontSize = 18f;
        dialogueText.color = new Color(0.93f, 0.86f, 0.72f, 1f);
        dialogueText.richText = true;
        dialogueText.textWrappingMode = TextWrappingModes.Normal;
        dialogueText.alignment = TextAlignmentOptions.TopLeft;

        ContentSizeFitter fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = viewportRt;
        scroll.content = contentRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        RectTransform acceptRt = CreateButton("AcceptButton", "Accept", _rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-45f, 22f), new Vector2(80f, 30f), out acceptButton);
        acceptRt.gameObject.SetActive(false);
    }

    private static RectTransform CreateButton(
        string name,
        string label,
        RectTransform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 size,
        out Button button)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.78f, 0.68f, 0.46f, 1f);
        button = go.GetComponent<Button>();

        GameObject textGo = new("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(rt, false);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        TMP_Text tmp = textGo.GetComponent<TMP_Text>();
        tmp.text = label;
        tmp.fontSize = 16f;
        tmp.color = new Color(0.12f, 0.1f, 0.08f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;

        return rt;
    }
}
