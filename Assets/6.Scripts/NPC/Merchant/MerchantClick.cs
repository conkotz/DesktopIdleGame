using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]
public class MerchantClick : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MainMenuWindowUI mainMenuWindowUI; // Opens Character page
    [SerializeField] private GameObject merchantModeBanner;

    [Header("Shop UI")]
    [SerializeField] private ShopUI shopUI;
    [SerializeField] private Merchant merchant; // on self or parent

    [Header("Shop Positioning")]
    [SerializeField] private Transform shopAnchor;          // World-space point above merchant
    [SerializeField] private RectTransform shopRect;        // Shop window RectTransform
    [SerializeField] private RectTransform canvasRect;      // Root canvas RectTransform
    [SerializeField] private Camera uiCamera;               // Null for Screen Space Overlay
    [SerializeField] private Vector2 screenOffset = new Vector2(-220f, 80f);

    public static bool MerchantModeOpen { get; private set; }
    public static bool IsShopOpen => _active != null && _active.shopUI != null && _active.shopUI.IsOpen;

    // Tracks which merchant is currently "active" so we can toggle-close on re-click.
    private static MerchantClick _active;

    private void Awake()
    {
        CacheRefs();
        ApplyBannerInteractionState(false);

        if (!GetComponent<Collider2D>())
            Debug.LogError("[MerchantClick] Missing Collider2D.");
    }

    private void CacheRefs()
    {
        if (!mainMenuWindowUI)
            mainMenuWindowUI = MainMenuWindowUI.Resolve();

        if (!merchantModeBanner)
            merchantModeBanner = FindSceneObjectByName("MerchantModeBanner");

        if (!shopUI)
            shopUI = FindFirstObjectByType<ShopUI>(FindObjectsInactive.Include);

        if (!merchant)
        {
            merchant = GetComponent<Merchant>();
            if (!merchant) merchant = GetComponentInParent<Merchant>();
        }

        if (!shopRect && shopUI)
            shopRect = shopUI.GetComponent<RectTransform>();

        if (!canvasRect && shopRect)
        {
            Canvas c = shopRect.GetComponentInParent<Canvas>();
            if (c) canvasRect = c.transform as RectTransform;
        }

        if (!shopAnchor)
        {
            Transform found = transform.Find("ShopAnchor");
            if (!found)
            {
                // Optional fallback: try children recursively
                Transform[] children = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < children.Length; i++)
                {
                    if (children[i].name == "ShopAnchor")
                    {
                        found = children[i];
                        break;
                    }
                }
            }

            shopAnchor = found ? found : transform;
        }
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.hideFlags != HideFlags.None)
                continue;
            if (!t.gameObject.scene.IsValid())
                continue;

            if (string.Equals(t.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                return t.gameObject;
        }

        return null;
    }

    private void ApplyBannerInteractionState(bool enabled)
    {
        if (!merchantModeBanner)
            return;

        CanvasGroup cg = merchantModeBanner.GetComponent<CanvasGroup>();
        if (!cg)
            cg = merchantModeBanner.AddComponent<CanvasGroup>();

        // Banner is visual-only. Never let it consume input.
        cg.interactable = false;
        cg.blocksRaycasts = false;

        Graphic[] graphics = merchantModeBanner.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;
    }

    /// <summary>
    /// Called by WorldInputRouter when this merchant is clicked.
    /// Toggle behavior: clicking same merchant again closes shop + merchant mode (NOT main menu).
    /// </summary>
    public void Open()
    {
        CacheRefs();

        if (!shopUI)
        {
            Debug.LogError("[MerchantClick] ShopUI not found/assigned.");
            return;
        }

        if (!merchant)
        {
            Debug.LogError("[MerchantClick] Merchant not found on self/parent.");
            return;
        }

        // Toggle off if clicking the same merchant while already open
        if (MerchantModeOpen && _active == this)
        {
            CloseOnlyMerchantMode();
            return;
        }

        // Open the new Character page (inventory + equipment inside MainMenuWindow)
        MainMenuWindowUI menu = mainMenuWindowUI != null ? mainMenuWindowUI : MainMenuWindowUI.Resolve();
        if (menu != null)
            menu.OpenCharacter();
        else
            Debug.LogWarning("[MerchantClick] MainMenuWindowUI not assigned/found.", this);

        // Show banner
        if (merchantModeBanner)
        {
            merchantModeBanner.SetActive(true);
            ApplyBannerInteractionState(true);
        }

        // Ensure shop shows immediately on first click
        if (!shopUI.gameObject.activeSelf)
            shopUI.gameObject.SetActive(true);

        shopUI.Open(merchant);
        if (!shopUI.IsOpen)
        {
            MerchantModeOpen = false;
            if (_active == this)
                _active = null;

            if (merchantModeBanner)
            {
                merchantModeBanner.SetActive(false);
                ApplyBannerInteractionState(false);
            }
            return;
        }

        // Switch/open merchant mode only after shop successfully opened.
        MerchantModeOpen = true;
        _active = this;

        // Position shop after opening so layout has a valid size
        PositionShopUI();
    }

    private void PositionShopUI()
    {
        if (!shopRect || !canvasRect)
        {
            Debug.LogWarning("[MerchantClick] Missing shopRect or canvasRect for positioning.");
            return;
        }

        Canvas.ForceUpdateCanvases();

        Camera worldCam = Camera.main;
        Camera uiCam = uiCamera; // null for Screen Space Overlay

        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(worldCam, transform.position);
        screenPos += screenOffset;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvasRect,
                screenPos,
                uiCam,
                out Vector3 worldPoint))
        {
            shopRect.position = worldPoint;
            ClampToCanvas(shopRect, canvasRect);
        }
    }

    private static void ClampToCanvas(RectTransform rect, RectTransform canvas)
    {
        if (!rect || !canvas) return;

        Canvas.ForceUpdateCanvases();

        Vector3[] rectCorners = new Vector3[4];
        Vector3[] canvasCorners = new Vector3[4];

        rect.GetWorldCorners(rectCorners);
        canvas.GetWorldCorners(canvasCorners);

        Vector3 offset = Vector3.zero;

        float rectLeft = rectCorners[0].x;
        float rectBottom = rectCorners[0].y;
        float rectRight = rectCorners[2].x;
        float rectTop = rectCorners[2].y;

        float canvasLeft = canvasCorners[0].x;
        float canvasBottom = canvasCorners[0].y;
        float canvasRight = canvasCorners[2].x;
        float canvasTop = canvasCorners[2].y;

        if (rectLeft < canvasLeft)
            offset.x += canvasLeft - rectLeft;

        if (rectRight > canvasRight)
            offset.x -= rectRight - canvasRight;

        if (rectBottom < canvasBottom)
            offset.y += canvasBottom - rectBottom;

        if (rectTop > canvasTop)
            offset.y -= rectTop - canvasTop;

        rect.position += offset;
    }

    private void CloseOnlyMerchantMode()
    {
        MerchantModeOpen = false;

        if (_active == this)
            _active = null;

        if (merchantModeBanner)
        {
            merchantModeBanner.SetActive(false);
            ApplyBannerInteractionState(false);
        }

        if (shopUI)
        {
            var close = shopUI.GetType().GetMethod("Close");
            if (close != null) close.Invoke(shopUI, null);
            else shopUI.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Global hard close (ESC, clicking empty ground, etc.)
    /// </summary>
    public static void ForceCloseMerchantMode()
    {
        MerchantModeOpen = false;

        if (_active != null)
        {
            _active.CloseOnlyMerchantMode();
            _active = null;
        }
    }

    public static bool TryGetActiveMerchant(out Merchant merchant)
    {
        merchant = _active != null ? _active.merchant : null;
        return merchant != null;
    }
}