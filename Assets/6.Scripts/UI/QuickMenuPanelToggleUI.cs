using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Toggles <see cref="quickMenuPanel"/> from a toolbar button (e.g. UIButton_QuickMenu on BottomGameBar).
/// </summary>
[RequireComponent(typeof(Button))]
public class QuickMenuPanelToggleUI : MonoBehaviour
{
    private const string FullWindowCanvasName = "FullWindowCanvas";
    private const int OverlaySortOrderAboveWindowsFallback = 10050;
    private const int OverlaySortOrderAboveWindowsPadding = 50;

    [SerializeField] private GameObject quickMenuPanel;
    [SerializeField] private string quickMenuPanelName = "QuickMenuPanel";

    private static GameObject s_cachedPanel;

    private Button _button;

    public static bool IsOpen
    {
        get
        {
            GameObject panel = ResolvePanelStatic();
            return panel != null && panel.activeInHierarchy;
        }
    }

    /// <summary>Hides the quick menu if it is currently open.</summary>
    public static void HideIfOpen()
    {
        GameObject panel = ResolvePanelStatic();
        if (panel == null || !panel.activeSelf)
            return;

        panel.SetActive(false);
        RestoreOverlaySortOrder(panel);
    }

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.RemoveListener(Toggle);
        _button.onClick.AddListener(Toggle);
    }

    private void OnEnable()
    {
        if (!_button)
            _button = GetComponent<Button>();
        _button.onClick.RemoveListener(Toggle);
        _button.onClick.AddListener(Toggle);
    }

    private void OnDisable()
    {
        if (_button)
            _button.onClick.RemoveListener(Toggle);
    }

    public void Toggle()
    {
        GameObject panel = ResolvePanel();
        if (panel == null)
        {
            Debug.LogWarning(
                $"[{nameof(QuickMenuPanelToggleUI)}] Assign Quick Menu Panel or ensure a GameObject named '{quickMenuPanelName}' exists in the scene.",
                this);
            return;
        }

        bool opening = !panel.activeSelf;
        panel.SetActive(opening);
        if (opening)
            BringToFront(panel);
        else
            RestoreOverlaySortOrder(panel);
    }

    /// <summary>
    /// Quick menu lives under <see cref="GameplayScreenOverlayLayout.StripUiCanvasObjectName"/> while draggable
    /// windows draw on FullWindowCanvas — boost nested canvas sort order so the panel renders above windows.
    /// </summary>
    private static void BringToFront(GameObject panel)
    {
        if (!panel)
            return;

        GameplayScreenOverlayLayout.EnsureNestedOverlayCanvas(panel, ResolveOverlaySortOrderAboveWindows());
        if (!panel.TryGetComponent(out GraphicRaycaster _))
            panel.AddComponent<GraphicRaycaster>();

        Transform t = panel.transform;
        if (t.parent != null)
            t.SetAsLastSibling();
    }

    private static void RestoreOverlaySortOrder(GameObject panel)
    {
        if (!panel || !panel.TryGetComponent(out Canvas canvas))
            return;

        canvas.overrideSorting = false;
        canvas.sortingOrder = 0;
    }

    private static int ResolveOverlaySortOrderAboveWindows()
    {
        GameObject fullWindowCanvas = GameplayScreenOverlayLayout.FindSceneObjectByName(FullWindowCanvasName);
        if (fullWindowCanvas != null && fullWindowCanvas.TryGetComponent(out Canvas canvas))
            return canvas.sortingOrder + OverlaySortOrderAboveWindowsPadding;

        return OverlaySortOrderAboveWindowsFallback;
    }

    private GameObject ResolvePanel()
    {
        if (quickMenuPanel != null)
        {
            s_cachedPanel = quickMenuPanel;
            return quickMenuPanel;
        }

        GameObject resolved = ResolvePanelStatic();
        if (resolved != null)
            quickMenuPanel = resolved;
        return resolved;
    }

    private static GameObject ResolvePanelStatic()
    {
        if (s_cachedPanel != null)
            return s_cachedPanel;

        QuickMenuPanelToggleUI[] toggles = FindObjectsByType<QuickMenuPanelToggleUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < toggles.Length; i++)
        {
            QuickMenuPanelToggleUI t = toggles[i];
            if (t != null && t.quickMenuPanel != null)
            {
                s_cachedPanel = t.quickMenuPanel;
                return s_cachedPanel;
            }
        }

        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.hideFlags != HideFlags.None || !t.gameObject.scene.IsValid())
                continue;
            if (t.name != "QuickMenuPanel")
                continue;

            s_cachedPanel = t.gameObject;
            return s_cachedPanel;
        }

        return null;
    }
}
