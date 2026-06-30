using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EnhancementFlashUI : MonoBehaviour
{
    /// <summary>Above main menu windows (~10000) and tooltips (~10200); below level-load fader (short.MaxValue).</summary>
    private const int OverlaySortOrder = 32000;

    private static EnhancementFlashUI _instance;

    [SerializeField] private Color successColor = new Color(0.15f, 1f, 0.25f, 0.45f);
    [SerializeField] private Color failureColor = new Color(1f, 0.12f, 0.08f, 0.45f);
    [SerializeField] private float duration = 0.35f;

    private Image _image;
    private Coroutine _routine;

    public static void Flash(bool success)
    {
        EnhancementFlashUI ui = GetOrCreate();
        if (ui)
            ui.Play(success);
    }

    private static EnhancementFlashUI GetOrCreate()
    {
        if (_instance && _instance.gameObject.activeInHierarchy)
        {
            EnsureDedicatedOverlayParent(_instance.transform);
            return _instance;
        }

        if (_instance)
            Destroy(_instance.gameObject);

        Canvas canvas = GetOrCreateDedicatedCanvas();

        GameObject go = new GameObject("EnhancementFlashUI");
        go.transform.SetParent(canvas.transform, false);
        _instance = go.AddComponent<EnhancementFlashUI>();
        _instance.Build();
        return _instance;
    }

    private static Canvas GetOrCreateDedicatedCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.name == "EnhancementFlashCanvas")
            {
                canvas.gameObject.SetActive(true);
                ConfigureCanvas(canvas);
                return canvas;
            }
        }

        GameObject go = new GameObject("EnhancementFlashCanvas");
        Canvas createdCanvas = go.AddComponent<Canvas>();
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
        ConfigureCanvas(createdCanvas);
        DontDestroyOnLoad(go);
        return createdCanvas;
    }

    private static void ConfigureCanvas(Canvas canvas)
    {
        if (!canvas)
            return;

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = OverlaySortOrder;
    }

    private static void EnsureDedicatedOverlayParent(Transform flashTransform)
    {
        Canvas canvas = GetOrCreateDedicatedCanvas();
        if (flashTransform.parent != canvas.transform)
            flashTransform.SetParent(canvas.transform, false);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        Build();
    }

    private void Build()
    {
        if (_image)
            return;

        RectTransform rt = gameObject.GetComponent<RectTransform>();
        if (!rt)
            rt = gameObject.AddComponent<RectTransform>();

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();

        _image = gameObject.GetComponent<Image>();
        if (!_image)
            _image = gameObject.AddComponent<Image>();

        _image.raycastTarget = false;
        _image.color = Color.clear;
    }

    private void Play(bool success)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        EnsureDedicatedOverlayParent(transform);
        Build();
        transform.SetAsLastSibling();

        if (_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(FlashRoutine(success ? successColor : failureColor));
    }

    private IEnumerator FlashRoutine(Color color)
    {
        _image.color = color;

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            Color c = color;
            c.a = Mathf.Lerp(color.a, 0f, t);
            _image.color = c;
            yield return null;
        }

        _image.color = Color.clear;
        _routine = null;
    }
}
