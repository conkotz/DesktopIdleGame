using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space chest: click opens <see cref="StorageUI"/> and the character menu (inventory) like <see cref="MerchantClick"/>.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[DefaultExecutionOrder(-100)]
public class StorageClick : MonoBehaviour
{
    [SerializeField] private MainMenuWindowUI mainMenuWindowUI;

    [Header("Storage UI")]
    [SerializeField] private StorageUI storageUI;

    [Header("Chest sprite")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("Optional child transform for the chest sprite. Auto-created at runtime when the sprite lives on this root.")]
    [SerializeField] private Transform chestVisual;
    [Tooltip("Shown while this chest has storage open. Closed sprite is taken from the renderer at startup.")]
    [SerializeField] private Sprite openedSprite;

    [Header("Opened state layout")]
    [SerializeField] private Transform nameLabel;
    [SerializeField] private WorldTargetIndicatorAnchor targetIndicatorAnchor;
    [Tooltip("Applied to the chest sprite visual only (local space).")]
    [SerializeField] private Vector3 openedSpriteLocalOffset = new(0f, 0.15f, 0f);
    [Tooltip("Applied to the NameLabel transform when storage is open.")]
    [SerializeField] private Vector3 openedNameLabelLocalOffset = new(0f, 0.2f, 0f);
    [Tooltip("Added to the target-marker anchor offset while storage is open.")]
    [SerializeField] private Vector3 openedTargetMarkerLocalOffset = new(0f, 0.2f, 0f);

    [Header("Window positioning")]
    [Tooltip("Pinned to the left edge of the main menu (Character) window; flips to the right if it would leave the canvas.")]
    [SerializeField] private RectTransform storageRect;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private float pinGap = 8f;
    [SerializeField] private float pinCanvasEdgeMargin = 4f;

    private static StorageClick _active;
    private Sprite _closedSprite;
    private Vector3 _closedSpriteLocalPos;
    private Vector3 _closedNameLabelLocalPos;
    private Vector3 _closedTargetMarkerLocalOffset;
    private bool _openLayoutApplied;

    /// <summary>True when this chest last opened <see cref="StorageUI"/> and it is still open.</summary>
    public static bool IsActiveInstance(StorageClick click) =>
        click != null && _active == click && StorageUI.IsOpen;

    private void Awake()
    {
        EnsureChestVisualChild();
        CacheRefs();
        CacheClosedSprite();
        CacheClosedLayout();

        if (!GetComponent<Collider2D>())
            Debug.LogError("[StorageClick] Missing Collider2D.", this);
    }

    private void LateUpdate()
    {
        if (_active != this || openedSprite == null)
            return;

        if (!StorageUI.IsOpen)
            SetChestOpenVisual(false);
    }

    private void EnsureChestVisualChild()
    {
        if (chestVisual != null)
            return;

        if (!spriteRenderer)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (!spriteRenderer)
            return;

        if (spriteRenderer.transform != transform)
        {
            chestVisual = spriteRenderer.transform;
            return;
        }

        var visualGo = new GameObject("ChestVisual");
        chestVisual = visualGo.transform;
        chestVisual.SetParent(transform, false);
        chestVisual.localPosition = Vector3.zero;
        chestVisual.localRotation = Quaternion.identity;
        chestVisual.localScale = Vector3.one;

        SpriteRenderer childRenderer = visualGo.AddComponent<SpriteRenderer>();
        CopySpriteRenderer(spriteRenderer, childRenderer);
        Destroy(spriteRenderer);
        spriteRenderer = childRenderer;
    }

    private static void CopySpriteRenderer(SpriteRenderer source, SpriteRenderer destination)
    {
        destination.sprite = source.sprite;
        destination.color = source.color;
        destination.flipX = source.flipX;
        destination.flipY = source.flipY;
        destination.drawMode = source.drawMode;
        destination.size = source.size;
        destination.maskInteraction = source.maskInteraction;
        destination.spriteSortPoint = source.spriteSortPoint;
        destination.sortingLayerID = source.sortingLayerID;
        destination.sortingOrder = source.sortingOrder;
        destination.material = source.material;
    }

    private void CacheRefs()
    {
        if (!spriteRenderer)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (!chestVisual && spriteRenderer)
            chestVisual = spriteRenderer.transform;

        if (!nameLabel)
        {
            Transform label = transform.Find("NameLabel");
            if (label)
                nameLabel = label;
        }

        if (!targetIndicatorAnchor)
        {
            targetIndicatorAnchor = GetComponent<WorldTargetIndicatorAnchor>();
            if (!targetIndicatorAnchor)
                targetIndicatorAnchor = gameObject.AddComponent<WorldTargetIndicatorAnchor>();
        }

        if (!mainMenuWindowUI)
            mainMenuWindowUI = MainMenuWindowUI.Resolve();

        if (!storageUI)
            storageUI = FindFirstObjectByType<StorageUI>(FindObjectsInactive.Include);

        if (!storageRect && storageUI)
            storageRect = storageUI.GetComponent<RectTransform>();

        if (!canvasRect && storageRect)
        {
            Canvas c = storageRect.GetComponentInParent<Canvas>();
            if (c) canvasRect = c.transform as RectTransform;
        }
    }

    /// <summary>
    /// Called from <see cref="WorldInputRouter2D"/> when this chest is clicked.
    /// Re-clicking the same chest while storage is open keeps it open (no toggle-close).
    /// </summary>
    public void Open()
    {
        CacheRefs();

        if (!storageUI)
        {
            Debug.LogError("[StorageClick] StorageUI not found/assigned.", this);
            return;
        }

        if (StorageUI.IsOpen && _active == this)
        {
            PositionStorageUI();
            return;
        }

        MerchantClick.ForceCloseMerchantMode();
        NPCDialogueBoxUI.DismissAllActive();

        MainMenuWindowUI menu = mainMenuWindowUI != null ? mainMenuWindowUI : MainMenuWindowUI.Resolve();
        if (menu != null)
            menu.OpenCharacter();
        else
            Debug.LogWarning("[StorageClick] MainMenuWindowUI not assigned/found.", this);

        if (!storageUI.gameObject.activeSelf)
            storageUI.gameObject.SetActive(true);

        if (_active != null && _active != this)
            _active.SetChestOpenVisual(false);

        storageUI.Open();

        _active = this;
        SetChestOpenVisual(true);
        PositionStorageUI();
    }

    private void PositionStorageUI()
    {
        if (!storageRect || !canvasRect)
        {
            Debug.LogWarning("[StorageClick] Missing storageRect or canvasRect for positioning.", this);
            return;
        }

        MainMenuWindowUI menu = mainMenuWindowUI != null ? mainMenuWindowUI : MainMenuWindowUI.Resolve();
        UIPinNextToMenuWindow.PositionNextToMainMenu(
            storageRect, canvasRect, menu, pinGap, pinCanvasEdgeMargin, UIPinNextToMenuWindow.PinSide.Left);
    }

    private void CloseStorageMode()
    {
        SetChestOpenVisual(false);

        if (_active == this)
            _active = null;

        if (storageUI)
            storageUI.Close();
    }

    private void CacheClosedSprite()
    {
        if (!spriteRenderer)
            return;

        if (spriteRenderer.sprite != null && spriteRenderer.sprite != openedSprite)
            _closedSprite = spriteRenderer.sprite;
    }

    private void CacheClosedLayout()
    {
        if (chestVisual)
            _closedSpriteLocalPos = chestVisual.localPosition;

        if (nameLabel)
            _closedNameLabelLocalPos = nameLabel.localPosition;

        if (targetIndicatorAnchor)
            _closedTargetMarkerLocalOffset = targetIndicatorAnchor.LocalOffset;
    }

    private void SetChestOpenVisual(bool open)
    {
        if (spriteRenderer && openedSprite != null)
        {
            if (_closedSprite == null)
                CacheClosedSprite();

            spriteRenderer.sprite = open ? openedSprite : _closedSprite;
        }

        ApplyOpenedLayoutOffsets(open);
    }

    private void ApplyOpenedLayoutOffsets(bool open)
    {
        if (_openLayoutApplied == open)
            return;

        _openLayoutApplied = open;

        if (chestVisual)
        {
            chestVisual.localPosition = open
                ? _closedSpriteLocalPos + openedSpriteLocalOffset
                : _closedSpriteLocalPos;
        }

        if (nameLabel)
        {
            nameLabel.localPosition = open
                ? _closedNameLabelLocalPos + openedNameLabelLocalOffset
                : _closedNameLabelLocalPos;
        }

        if (targetIndicatorAnchor)
        {
            targetIndicatorAnchor.LocalOffset = open
                ? _closedTargetMarkerLocalOffset + openedTargetMarkerLocalOffset
                : _closedTargetMarkerLocalOffset;
        }
    }

    public static void ForceCloseStorageMode()
    {
        if (_active != null)
        {
            _active.CloseStorageMode();
            _active = null;
        }
        else
        {
            StorageUI ui = FindFirstObjectByType<StorageUI>(FindObjectsInactive.Include);
            if (ui != null)
                ui.Close();
        }
    }
}
