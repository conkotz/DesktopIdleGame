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
    [Tooltip("Pinned to the right edge of the main menu (Character) window; flips to the left if it would leave the canvas.")]
    [SerializeField] private RectTransform shopRect;        // Shop window RectTransform
    [SerializeField] private RectTransform canvasRect;      // Root canvas RectTransform
    [SerializeField] private float pinGap = 8f;
    [SerializeField] private float pinCanvasEdgeMargin = 4f;

    public static bool MerchantModeOpen { get; private set; }
    public static bool IsShopOpen => _active != null && _active.shopUI != null && _active.shopUI.IsOpen;

    // Tracks which merchant is currently "active" for shop mode and switching merchants.
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
    /// Re-clicking the same merchant while shop mode is open keeps the shop open (dialogue still runs via <see cref="NPCInteractionSettings.Interact"/>).
    /// Clicking a different merchant replaces the active shop as before.
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

        if (MerchantModeOpen && _active == this)
        {
            PositionShopUI();
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

        MainMenuWindowUI menu = mainMenuWindowUI != null ? mainMenuWindowUI : MainMenuWindowUI.Resolve();
        UIPinNextToMenuWindow.PositionNextToMainMenu(shopRect, canvasRect, menu, pinGap, pinCanvasEdgeMargin);
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