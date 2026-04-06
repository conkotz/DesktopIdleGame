using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space chest: click opens <see cref="StorageUI"/> and the character menu (inventory) like <see cref="MerchantClick"/>.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class StorageClick : MonoBehaviour
{
    [SerializeField] private MainMenuWindowUI mainMenuWindowUI;

    [Header("Storage UI")]
    [SerializeField] private StorageUI storageUI;

    [Header("Window positioning")]
    [SerializeField] private Transform storageAnchor;
    [SerializeField] private RectTransform storageRect;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private Camera uiCamera;
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 20f);

    private static StorageClick _active;

    private void Awake()
    {
        CacheRefs();

        if (!GetComponent<Collider2D>())
            Debug.LogError("[StorageClick] Missing Collider2D.", this);
    }

    private void CacheRefs()
    {
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

        if (!storageAnchor)
        {
            Transform found = transform.Find("StorageAnchor");
            storageAnchor = found ? found : transform;
        }
    }

    /// <summary>
    /// Called from <see cref="WorldInputRouter2D"/> when this chest is clicked. Toggle closes when clicking the same chest again.
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
            CloseStorageMode();
            return;
        }

        MainMenuWindowUI menu = mainMenuWindowUI != null ? mainMenuWindowUI : MainMenuWindowUI.Resolve();
        if (menu != null)
            menu.OpenCharacter();
        else
            Debug.LogWarning("[StorageClick] MainMenuWindowUI not assigned/found.", this);

        if (!storageUI.gameObject.activeSelf)
            storageUI.gameObject.SetActive(true);

        storageUI.Open();

        _active = this;
        PositionStorageUI();
    }

    private void PositionStorageUI()
    {
        if (!storageAnchor || !storageRect || !canvasRect)
        {
            Debug.LogWarning("[StorageClick] Missing storageAnchor, storageRect, or canvasRect for positioning.", this);
            return;
        }

        Canvas.ForceUpdateCanvases();

        Camera worldCam = Camera.main;
        Camera uiCam = uiCamera;

        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(worldCam, storageAnchor.position);
        screenPos += screenOffset;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvasRect,
                screenPos,
                uiCam,
                out Vector3 worldPoint))
        {
            storageRect.position = worldPoint;
            ClampToCanvas(storageRect, canvasRect);
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

    private void CloseStorageMode()
    {
        if (_active == this)
            _active = null;

        if (storageUI)
            storageUI.Close();
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
