using UnityEngine;
using UnityEngine.SceneManagement;

public class GoldPopupSpawner : MonoBehaviour
{
    [Header("Prefab + Canvas")]
    [SerializeField] private GoldPopup popupPrefab;

    [Tooltip("Optional. If empty we auto-find a Canvas tagged 'UICanvas'.")]
    [SerializeField] private Canvas canvas;

    [Header("World Anchor")]
    [SerializeField] private Transform playerWorld;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.2f, 0f);

    [Header("Colors")]
    [SerializeField] private Color defaultMessageColor = Color.white;
    [SerializeField] private Color levelUpColor = new Color(0.35f, 0.8f, 1f, 1f);

    private Camera _cam;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Awake()
    {
        Rebind();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Rebind();
    }

    private void Rebind()
    {
        if (!playerWorld)
        {
            var player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (player) playerWorld = player.transform;
        }

        if (!canvas)
        {
            var go = GameObject.FindGameObjectWithTag("UICanvas");
            if (go) canvas = go.GetComponent<Canvas>();
        }

        _cam = Camera.main;

        if (canvas && canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            if (!canvas.worldCamera) canvas.worldCamera = _cam;
        }
    }

    public void ShowGoldGained(int amount)
    {
        if (amount <= 0 || !popupPrefab) return;

        if (!canvas || !playerWorld)
            Rebind();

        if (!canvas || !playerWorld) return;

        ShowGoldGainedAtWorld(playerWorld.position + worldOffset, amount);
    }

    public void ShowGoldGainedAtWorld(Vector3 worldPos, int amount)
    {
        if (amount <= 0 || !popupPrefab) return;

        if (!canvas)
            Rebind();

        if (!canvas) return;

        Camera cam = canvas.worldCamera ? canvas.worldCamera : Camera.main;
        if (!cam) return;

        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, worldPos);

        var popup = Instantiate(popupPrefab, canvas.transform);
        RectTransform canvasRect = canvas.transform as RectTransform;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, cam, out Vector2 localPoint))
        {
            popup.PlayLocal(localPoint, amount);
        }
    }

    public void ShowNotEnoughGold()
    {
        if (!popupPrefab) return;

        if (!canvas || !playerWorld)
            Rebind();

        if (!canvas || !playerWorld) return;

        ShowMessageAtWorld(playerWorld.position + worldOffset, "Not enough gold", defaultMessageColor);
    }

    public void ShowMessageAtWorld(Vector3 worldPos, string message)
    {
        ShowMessageAtWorld(worldPos, message, defaultMessageColor);
    }

    public void ShowMessageAtWorld(Vector3 worldPos, string message, Color color)
    {
        if (string.IsNullOrWhiteSpace(message) || !popupPrefab) return;

        if (!canvas)
            Rebind();

        if (!canvas) return;

        Camera cam = canvas.worldCamera ? canvas.worldCamera : Camera.main;
        if (!cam) return;

        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, worldPos);

        var popup = Instantiate(popupPrefab, canvas.transform);
        RectTransform canvasRect = canvas.transform as RectTransform;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, cam, out Vector2 localPoint))
        {
            popup.PlayLocalText(localPoint, message, color);
        }
    }

    public void ShowLevelUpAtWorld(Vector3 worldPos, string message)
    {
        ShowMessageAtWorld(worldPos, message, levelUpColor);
    }
}