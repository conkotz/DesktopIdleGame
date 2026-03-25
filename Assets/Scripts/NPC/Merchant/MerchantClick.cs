using UnityEngine;

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
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 20f);

    public static bool MerchantModeOpen { get; private set; }

    // Tracks which merchant is currently "active" so we can toggle-close on re-click.
    private static MerchantClick _active;

    private void Awake()
    {
        CacheRefs();

        if (!GetComponent<Collider2D>())
            Debug.LogError("[MerchantClick] Missing Collider2D.");
    }

    private void CacheRefs()
    {
        if (!mainMenuWindowUI)
            mainMenuWindowUI = FindFirstObjectByType<MainMenuWindowUI>(FindObjectsInactive.Include);

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

        // Switch/open merchant mode
        MerchantModeOpen = true;
        _active = this;

        // Open the new Character page (inventory + equipment inside MainMenuWindow)
        if (mainMenuWindowUI)
            mainMenuWindowUI.OpenCharacter();
        else
            Debug.LogWarning("[MerchantClick] MainMenuWindowUI not assigned/found.");

        // Show banner
        if (merchantModeBanner)
            merchantModeBanner.SetActive(true);

        // Ensure shop shows immediately on first click
        if (!shopUI.gameObject.activeSelf)
            shopUI.gameObject.SetActive(true);

        shopUI.Open(merchant);

        // Position shop after opening so layout has a valid size
        PositionShopUI();
    }

    private void PositionShopUI()
    {
        if (!shopAnchor || !shopRect || !canvasRect)
        {
            Debug.LogWarning("[MerchantClick] Missing shopAnchor, shopRect, or canvasRect for positioning.");
            return;
        }

        Canvas.ForceUpdateCanvases();

        Camera worldCam = Camera.main;
        Camera uiCam = uiCamera; // null for Screen Space Overlay

        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(worldCam, shopAnchor.position);
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
            merchantModeBanner.SetActive(false);

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
}