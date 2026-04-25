using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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

    [Header("Stacking")]
    [Tooltip("Extra vertical offset per concurrent popup so simultaneous messages do not overlap.")]
    [SerializeField] private float stackVerticalSpacing = 30f;

    [Header("Draw order")]
    [Tooltip("Added to the root UICanvas sorting order so popups render above the rest of the HUD.")]
    [SerializeField] private int popupSortingOrderOffset = 30000;
    [Tooltip("When enabled, popups are spawned under a dedicated top-most UI canvas.")]
    [SerializeField] private bool useDedicatedTopPopupCanvas = true;
    [SerializeField] private string topPopupCanvasName = "TopPopupCanvas";

    [Header("Colours")]
    [SerializeField] private Color defaultMessageColor = Color.white;
    [SerializeField] private Color levelUpColor = new Color(0.35f, 0.8f, 1f, 1f);

    private Camera _cam;
    private readonly List<bool> _stackSlotBusy = new List<bool>();
    private Canvas _topPopupCanvas;

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

        EnsureTopPopupCanvas();
    }

    public void ShowGoldGained(int amount, string sourceLine = null)
    {
        if (amount <= 0)
            return;

        GameLog.GoldGained(amount, sourceLine);
        if (!popupPrefab)
            return;

        if (!canvas || !playerWorld)
            Rebind();

        if (!canvas || !playerWorld) return;

        SpawnGoldPopupAtWorld(playerWorld.position + worldOffset, amount, sourceLine);
    }

    public void ShowGoldGainedAtWorld(Vector3 worldPos, int amount, string sourceLine = null)
    {
        if (amount <= 0)
            return;
        GameLog.GoldGained(amount, sourceLine);
        SpawnGoldPopupAtWorld(worldPos, amount, sourceLine);
    }

    private void SpawnGoldPopupAtWorld(Vector3 worldPos, int amount, string sourceLine)
    {
        if (amount <= 0 || !popupPrefab) return;

        Rebind();

        Camera cam = ResolveWorldProjectionCamera();
        if (!cam) return;

        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        Canvas targetCanvas = GetPopupTargetCanvas();
        if (targetCanvas == null)
            return;
        RectTransform canvasRect = targetCanvas.transform as RectTransform;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, GetRectEventCamera(targetCanvas), out Vector2 localPoint))
            return;

        int slot = AcquireStackSlot();
        Vector2 stackedLocal = localPoint + Vector2.up * (slot * stackVerticalSpacing);
        var popup = Instantiate(popupPrefab, targetCanvas.transform);
        BringPopupToFront(popup);
        popup.PlayLocal(stackedLocal, amount, sourceLine, () => ReleaseStackSlot(slot));
    }

    public void ShowNotEnoughGold()
    {
        ShowMessageAtWorld(Vector3.zero, "Not enough gold", defaultMessageColor);
    }

    public void ShowMessageAtWorld(Vector3 worldPos, string message)
    {
        ShowMessageAtWorld(worldPos, message, defaultMessageColor);
    }

    public void ShowMessageAtWorld(Vector3 worldPos, string message, Color color)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        GameLog.Add(message, color);
    }

    private void BringPopupToFront(GoldPopup popup)
    {
        if (!popup)
            return;

        Canvas targetCanvas = GetPopupTargetCanvas();
        if (targetCanvas == null)
            return;

        popup.transform.SetAsLastSibling();

        var popupCanvas = popup.GetComponent<Canvas>();
        if (!popupCanvas)
            popupCanvas = popup.gameObject.AddComponent<Canvas>();

        int topLayerId = GetHighestSortingLayerId();
        int baseOrder = targetCanvas.sortingOrder;

        popupCanvas.overrideSorting = true;
        popupCanvas.sortingLayerID = topLayerId;
        popupCanvas.sortingOrder = baseOrder + Mathf.Max(0, popupSortingOrderOffset);

        var cg = popup.GetComponent<CanvasGroup>();
        if (!cg)
            cg = popup.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;
    }

    private Canvas GetPopupTargetCanvas()
    {
        if (useDedicatedTopPopupCanvas && _topPopupCanvas != null)
            return _topPopupCanvas;
        return canvas;
    }

    private Camera ResolveWorldProjectionCamera()
    {
        if (canvas != null && canvas.worldCamera != null)
            return canvas.worldCamera;
        return Camera.main;
    }

    private static Camera GetRectEventCamera(Canvas target)
    {
        if (target == null || target.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;
        return target.worldCamera != null ? target.worldCamera : Camera.main;
    }

    private void EnsureTopPopupCanvas()
    {
        if (!useDedicatedTopPopupCanvas)
        {
            _topPopupCanvas = null;
            return;
        }

        if (_topPopupCanvas != null)
            return;

        GameObject existing = GameObject.Find(topPopupCanvasName);
        if (existing != null)
            _topPopupCanvas = existing.GetComponent<Canvas>();

        if (_topPopupCanvas == null)
        {
            GameObject go = new GameObject(topPopupCanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _topPopupCanvas = go.GetComponent<Canvas>();
            DontDestroyOnLoad(go);
        }

        if (_topPopupCanvas == null)
            return;

        _topPopupCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _topPopupCanvas.overrideSorting = true;
        _topPopupCanvas.sortingLayerID = GetHighestSortingLayerId();
        _topPopupCanvas.sortingOrder = short.MaxValue - 100;

        CanvasScaler scaler = _topPopupCanvas.GetComponent<CanvasScaler>();
        if (scaler != null)
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        GraphicRaycaster raycaster = _topPopupCanvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            raycaster.enabled = false;
    }

    private static int GetHighestSortingLayerId()
    {
        SortingLayer[] layers = SortingLayer.layers;
        if (layers == null || layers.Length == 0)
            return 0;

        int bestId = layers[0].id;
        int bestValue = layers[0].value;
        for (int i = 1; i < layers.Length; i++)
        {
            if (layers[i].value <= bestValue)
                continue;
            bestValue = layers[i].value;
            bestId = layers[i].id;
        }

        return bestId;
    }

    private int AcquireStackSlot()
    {
        for (int i = 0; i < _stackSlotBusy.Count; i++)
        {
            if (!_stackSlotBusy[i])
            {
                _stackSlotBusy[i] = true;
                return i;
            }
        }

        _stackSlotBusy.Add(true);
        return _stackSlotBusy.Count - 1;
    }

    private void ReleaseStackSlot(int slot)
    {
        if (slot < 0 || slot >= _stackSlotBusy.Count)
            return;
        _stackSlotBusy[slot] = false;
    }

    public void ShowLevelUpAtWorld(Vector3 worldPos, string message)
    {
        ShowMessageAtWorld(worldPos, message, levelUpColor);
    }
}