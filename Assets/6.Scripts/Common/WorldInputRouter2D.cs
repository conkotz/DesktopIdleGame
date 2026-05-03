using UnityEngine;
using UnityEngine.EventSystems;

public class WorldInputRouter2D : MonoBehaviour
{
    [Header("Masks")]
    [Tooltip("All world-clickable layers (drops, resources, NPCs, enemies later).")]
    [SerializeField] private LayerMask worldClickMask = ~0;

    [Header("Refs")]
    [SerializeField] private Camera cam;
    [SerializeField] private PlayerController player;

    [Header("Hover Highlight (optional)")]
    [SerializeField] private bool enableHoverHighlight = true;

    [SerializeField] private bool restrictClicksToStrip = true;
    [SerializeField] private Camera stripCamera; // assign StripCamera in the scene

    private SimpleHoverHighlight2D _currentHover;

    public bool HoverHighlightEnabled => enableHoverHighlight;

    private void Awake()
    {
        if (!cam)
            cam = Camera.main;
        if (!player)
            player = FindFirstObjectByType<PlayerController>();
    }

    private void Update()
    {
        if (!cam)
            cam = Camera.main;
        if (!cam)
            return;

        bool gameplayLockedByHelperModal = HelperGameplayController.BlocksStripGameplay;
        bool whitelistTutorialRoutesWorld = HelperGameplayController.UsesWorldWhitelistRouting;

        bool overUI =
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        Collider2D winnerCol = null;
        if (!overUI)
            winnerCol = PickWinnerUnderMouse();
        else if (whitelistTutorialRoutesWorld && EventSystem.current != null)
            winnerCol = PickWinnerUnderMouse();

        if (enableHoverHighlight)
        {
            bool allowHoverWinner = winnerCol;
            if (whitelistTutorialRoutesWorld)
                allowHoverWinner = winnerCol && HelperGameplayController.IsWhitelistedWorldPick(winnerCol);

            UpdateHoverHighlight(allowHoverWinner ? winnerCol : null);
        }

        if (!Input.GetMouseButtonDown(0))
            return;

        bool preferWorldOverUi =
            HelperGameplayController.UsesWorldWhitelistRouting && EventSystem.current != null;

        if (!preferWorldOverUi && overUI)
            return;

        // If a fullscreen UI canvas is under the pointer but this helper needs map clicks, probe world first.
        Collider2D worldUnderPointer = null;
        if (preferWorldOverUi && overUI)
            worldUnderPointer = PickWinnerUnderMouse();

        // True click on real UI (menus) still blocks; whitelist only bypasses when there's a whitelisted collider.
        if (overUI && !(worldUnderPointer != null && HelperGameplayController.IsWhitelistedWorldPick(worldUnderPointer)))
            return;

        if (!player)
            return;

        bool skipStripForWhitelist = HelperGameplayController.UsesWorldWhitelistRouting;

        if (restrictClicksToStrip && stripCamera && !skipStripForWhitelist &&
            !stripCamera.pixelRect.Contains(Input.mousePosition))
            return;

        winnerCol = PickWinnerUnderMouse();

        if (whitelistTutorialRoutesWorld && gameplayLockedByHelperModal)
        {
            if (!winnerCol)
                return;

            if (!HelperGameplayController.IsWhitelistedWorldPick(winnerCol))
                return;

            RouteWorldClick(winnerCol);
            HelperGameplayController.NotifyWhitelistWorldRouteHandled(winnerCol);
            return;
        }

        if (whitelistTutorialRoutesWorld)
        {
            RouteWorldClick(winnerCol);

            if (winnerCol != null && HelperGameplayController.IsWhitelistedWorldPick(winnerCol))
                HelperGameplayController.NotifyWhitelistWorldRouteHandled(winnerCol);

            return;
        }

        RouteWorldClick(winnerCol);
    }

    private void RouteWorldClick(Collider2D winnerCol)
    {
        if (winnerCol == null)
            return;

        var drop = winnerCol.GetComponentInParent<ItemDrop>();
        if (drop != null)
        {
            player.RequestPickup(drop);
            return;
        }

        var node = winnerCol.GetComponentInParent<ResourceNode>();
        if (node != null)
        {
            player.SelectNode(node);
            return;
        }

        NPCInteractionSettings npc = winnerCol.GetComponentInParent<NPCInteractionSettings>();
        MerchantClick merchant = winnerCol.GetComponentInParent<MerchantClick>();

        if (merchant != null)
        {
            if (npc != null)
                npc.Interact();

            merchant.Open();
            return;
        }

        if (npc != null)
        {
            npc.Interact();
            return;
        }

        QuestGiver questGiverOnly = winnerCol.GetComponentInParent<QuestGiver>();
        if (questGiverOnly != null && questGiverOnly.TryClaimFirstReadyQuestReward())
            return;

        var storage = winnerCol.GetComponentInParent<StorageClick>();
        if (storage != null)
            storage.Open();
    }

    private Collider2D PickWinnerUnderMouse()
    {
        Vector3 w3 = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 point = new(w3.x, w3.y);

        return WorldClickPicker2D.PickTopmostAtPoint(point, worldClickMask);
    }

    private void UpdateHoverHighlight(Collider2D winnerCol)
    {
        var newHover = winnerCol ? winnerCol.GetComponentInParent<SimpleHoverHighlight2D>() : null;

        if (_currentHover == newHover)
            return;

        if (_currentHover)
            _currentHover.SetHovered(false);
        _currentHover = newHover;
        if (_currentHover)
            _currentHover.SetHovered(true);
    }
}
