using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class MerchantClick : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MainMenuWindowUI mainMenuWindowUI; // Opens Character page
    [SerializeField] private GameObject merchantModeBanner;

    [Header("Shop UI")]
    [SerializeField] private ShopUI shopUI;
    [SerializeField] private Merchant merchant; // on self or parent
    [SerializeField] private PlayerController player;
    [Tooltip("Extra padding added on top of merchant collider half-width when checking arrival.")]
    [SerializeField] private float openWhenWithinXDistance = 0.15f;
    [SerializeField] private Collider2D merchantCollider;

    [Header("Shop Positioning")]
    [Tooltip("Pinned to the right edge of the main menu (Character) window; flips to the left if it would leave the canvas.")]
    [SerializeField] private RectTransform shopRect;        // Shop window RectTransform
    [SerializeField] private RectTransform canvasRect;      // Root canvas RectTransform
    [SerializeField] private float pinGap = 8f;
    [SerializeField] private float pinCanvasEdgeMargin = 4f;

    public static bool MerchantModeOpen { get; private set; }
    public static bool IsShopOpen => _active != null && _active.shopUI != null && _active.shopUI.IsOpen;

    /// <summary>True when this merchant's shop UI is open (not while the player is only walking toward them).</summary>
    public bool IsShopEngagedWithPlayer()
    {
        CacheRefs();
        return _active == this && shopUI != null && shopUI.IsOpen;
    }

    // Tracks which merchant is currently "active" for shop mode and switching merchants.
    private static MerchantClick _active;
    private static MerchantClick _pendingOpen;
    private Coroutine _openWhenArrivedRoutine;

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

        if (!merchantCollider)
            merchantCollider = GetComponent<Collider2D>() ?? GetComponentInChildren<Collider2D>(true);

        if (!shopRect && shopUI)
            shopRect = shopUI.GetComponent<RectTransform>();

        if (!player)
            player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

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

        if (MerchantModeOpen && _active == this && shopUI.IsOpen)
        {
            PositionShopUI();
            return;
        }

        CancelPendingOpen();

        if (player != null)
        {
            // Walk to the merchant's near collider edge (offset back by player half-width + padding) instead of the
            // merchant's transform.position.x — otherwise the player overshoots into the merchant sprite before
            // arrival registers.
            player.MoveToPointX(ComputeApproachTargetX(player));
            _pendingOpen = this;
            _openWhenArrivedRoutine = StartCoroutine(CoOpenWhenArrived());
            return;
        }

        OpenNow();
    }

    private IEnumerator CoOpenWhenArrived()
    {
        while (_pendingOpen == this)
        {
            if (player == null || player.IsDead)
            {
                _pendingOpen = null;
                yield break;
            }

            if (IsPlayerWithinMerchantArrivalRange())
            {
                _pendingOpen = null;
                OpenNow();
                yield break;
            }

            yield return null;
        }
    }

    /// <summary>
    /// World-X the player should walk to so they stop at the nearest edge of the merchant's collider (not its center).
    /// Falls back to <see cref="Transform.position"/> when the merchant collider is missing.
    /// </summary>
    private float ComputeApproachTargetX(PlayerController p)
    {
        if (merchantCollider == null)
            return transform.position.x;

        Bounds b = merchantCollider.bounds;
        float playerX = p.transform.position.x;
        bool approachFromLeft = playerX <= b.center.x;
        float edgeX = approachFromLeft ? b.min.x : b.max.x;
        float sign = approachFromLeft ? -1f : 1f;

        // Walk flush against the merchant's near edge (no openWhenWithinXDistance baked in here — that field is the
        // arrival tolerance, not the walk offset). Previously the player stopped a few px short of the collider so the
        // proximity check still failed and the shop never opened.
        float playerHalfWidth = ResolvePlayerColliderHalfWidth(p);
        return edgeX + sign * playerHalfWidth;
    }

    private bool IsPlayerWithinMerchantArrivalRange()
    {
        if (player == null)
            return false;

        if (merchantCollider == null)
        {
            float dxCenter = Mathf.Abs(player.transform.position.x - transform.position.x);
            return dxCenter <= Mathf.Max(0.01f, openWhenWithinXDistance);
        }

        Bounds b = merchantCollider.bounds;
        float playerX = player.transform.position.x;
        float edgeX = playerX <= b.center.x ? b.min.x : b.max.x;
        float playerHalfWidth = ResolvePlayerColliderHalfWidth(player);
        float gapBetweenBodies = Mathf.Abs(playerX - edgeX) - playerHalfWidth;
        return gapBetweenBodies <= Mathf.Max(0.01f, openWhenWithinXDistance);
    }

    private static float ResolvePlayerColliderHalfWidth(PlayerController p)
    {
        if (p == null)
            return 0f;
        Collider2D pcol = p.GetComponent<Collider2D>();
        if (!pcol)
            pcol = p.GetComponentInChildren<Collider2D>(true);
        return pcol != null ? pcol.bounds.extents.x : 0f;
    }

    private void OpenNow()
    {
        CacheRefs();

        // Recover from stale merchant mode (e.g. window was closed through a generic close button path).
        if (MerchantModeOpen && !shopUI.IsOpen)
            ForceCloseMerchantMode();

        StorageClick.ForceCloseStorageMode();

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

    /// <summary>Re-pin the shop window next to the main menu (e.g. after returning from undo UI).</summary>
    public void RepositionShopNextToMenu() => PositionShopUI();

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
        CancelPendingOpen();
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
        CancelPendingOpen();
        MerchantModeOpen = false;

        UndoShopWindowUI undoWin = FindFirstObjectByType<UndoShopWindowUI>(FindObjectsInactive.Include);
        undoWin?.CloseWindow();

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

    public static void CancelPendingOpen()
    {
        if (_pendingOpen == null)
            return;

        if (_pendingOpen._openWhenArrivedRoutine != null)
        {
            _pendingOpen.StopCoroutine(_pendingOpen._openWhenArrivedRoutine);
            _pendingOpen._openWhenArrivedRoutine = null;
        }

        _pendingOpen = null;
    }
}