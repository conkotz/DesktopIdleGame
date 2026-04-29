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

    private SimpleHoverHighlight2D _currentHover; // or EdgeHighlight2D if you swap later

    [Header("Merchant open")]
    [Tooltip("Second left-click on the same merchant within this time (seconds) opens/closes, or hold Ctrl while clicking.")]
    [SerializeField, Min(0.05f)] private float merchantDoubleClickMaxDelay = 0.35f;

    private MerchantClick _lastMerchantClickTarget;
    private float _lastMerchantClickUnscaledTime = -999f;

    /// <summary>True when this router drives hover highlights (used to avoid duplicate HoverPicker2D).</summary>
    public bool HoverHighlightEnabled => enableHoverHighlight;

    private void Awake()
    {
        if (!cam) cam = Camera.main;
        if (!player) player = FindFirstObjectByType<PlayerController>();
    }

    private void Update()
    {
        if (!cam) cam = Camera.main;
        if (!cam) return;

        // UI blocks hover/click
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        // ----- Hover (winner under mouse) -----
        Collider2D winnerCol = null;
        if (!overUI)
            winnerCol = PickWinnerUnderMouse();

        if (enableHoverHighlight)
            UpdateHoverHighlight(winnerCol);

        // ----- Click -----
        if (!Input.GetMouseButtonDown(0)) return;
        if (overUI) return;

        if (restrictClicksToStrip && stripCamera &&
            !stripCamera.pixelRect.Contains(Input.mousePosition))
            return;

        if (winnerCol != null)
        {
            // Route by component type (priority order)
            // 1) Item drops
            var drop = winnerCol.GetComponentInParent<ItemDrop>();
            if (drop != null)
            {
                player.RequestPickup(drop);
                return;
            }

            // 2) Resources (trees/rocks/pond)
            var node = winnerCol.GetComponentInParent<ResourceNode>();
            if (node != null)
            {
                player.SelectNode(node);
                return;
            }

            // 3) Generic NPC dialogue / quest interactables
            var npc = winnerCol.GetComponentInParent<NPCInteractionSettings>();
            if (npc != null)
                npc.Interact();

            // 3b) Merchant / NPC interactables (Ctrl+click or double-click — not plain single click)
            var merchant = winnerCol.GetComponentInParent<MerchantClick>();
            if (merchant != null && TryConsumeMerchantOpenClick(merchant))
            {
                merchant.Open();
                return;
            }

            // 3c) Town storage chest
            var storage = winnerCol.GetComponentInParent<StorageClick>();
            if (storage != null)
            {
                storage.Open();
                return;
            }

            // 4) Enemy later (example)
            // var enemy = winnerCol.GetComponentInParent<Enemy>();
            // if (enemy != null) { player.Attack(enemy); return; }

            // If winner exists but isn't something we handle, do nothing
            return;
        }

     
    }

    private Collider2D PickWinnerUnderMouse()
    {
        Vector3 w3 = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 point = new Vector2(w3.x, w3.y);

        return WorldClickPicker2D.PickTopmostAtPoint(point, worldClickMask);
    }

    private void UpdateHoverHighlight(Collider2D winnerCol)
    {
        var newHover = winnerCol ? winnerCol.GetComponentInParent<SimpleHoverHighlight2D>() : null;

        if (_currentHover == newHover) return;

        if (_currentHover) _currentHover.SetHovered(false);
        _currentHover = newHover;
        if (_currentHover) _currentHover.SetHovered(true);
    }

    private bool IsCtrlHeldForWorldClick()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed);
#else
        return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
#endif
    }

    private void ResetMerchantDoubleClickState()
    {
        _lastMerchantClickTarget = null;
        _lastMerchantClickUnscaledTime = -999f;
    }

    /// <summary>True when this click should open/toggle the merchant (Ctrl held, or second click on same NPC in time window).</summary>
    private bool TryConsumeMerchantOpenClick(MerchantClick merchant)
    {
        if (merchant == null)
            return false;

        if (IsCtrlHeldForWorldClick())
        {
            ResetMerchantDoubleClickState();
            return true;
        }

        float t = Time.unscaledTime;
        if (_lastMerchantClickTarget == merchant &&
            (t - _lastMerchantClickUnscaledTime) <= merchantDoubleClickMaxDelay)
        {
            ResetMerchantDoubleClickState();
            return true;
        }

        _lastMerchantClickTarget = merchant;
        _lastMerchantClickUnscaledTime = t;
        return false;
    }
}