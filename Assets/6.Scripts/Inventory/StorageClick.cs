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
    [Tooltip("Pinned to the right edge of the main menu (Character) window; flips to the left if it would leave the canvas.")]
    [SerializeField] private RectTransform storageRect;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private float pinGap = 8f;
    [SerializeField] private float pinCanvasEdgeMargin = 4f;

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
        if (!storageRect || !canvasRect)
        {
            Debug.LogWarning("[StorageClick] Missing storageRect or canvasRect for positioning.", this);
            return;
        }

        MainMenuWindowUI menu = mainMenuWindowUI != null ? mainMenuWindowUI : MainMenuWindowUI.Resolve();
        UIPinNextToMenuWindow.PositionNextToMainMenu(storageRect, canvasRect, menu, pinGap, pinCanvasEdgeMargin);
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
