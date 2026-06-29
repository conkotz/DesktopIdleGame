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

    [Header("Gathering XP anchor")]
    [Tooltip("Extra world-space offset for +xp text vs gold (same base player anchor). E.g. slight -X / lower Y reads as behind the character in side view.")]
    [SerializeField] private Vector3 gatheringXpAnchorExtraWorld = new Vector3(-0.18f, -0.22f, 0f);

    [Header("Quest reward item anchor")]
    [Tooltip("Extra world-space offset for quest reward item popups — lower Y keeps text near the character, not the top of the strip.")]
    [SerializeField] private Vector3 questRewardAnchorExtraWorld = new Vector3(0f, -0.72f, 0f);
    [SerializeField] private float questRewardStackVerticalSpacing = 22f;

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
    [Tooltip("Gathering skill XP popup (+N xp) at the same world anchor as gold gains.")]
    [SerializeField] private Color gatheringXpTextColor = new Color(0.35f, 0.72f, 1f, 1f);

    private Camera _cam;
    private readonly List<bool> _stackSlotBusy = new List<bool>();
    private Canvas _topPopupCanvas;
    private bool _subscribedXpGained;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeGatheringXpPopups();
    }

    private void Awake()
    {
        Rebind();
    }

    private void Start()
    {
        TrySubscribeGatheringXpPopups();
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
        TrySubscribeGatheringXpPopups();
    }

    private static bool IsGatheringSkill(SkillType skill) =>
        skill == SkillType.Mining || skill == SkillType.Woodcutting || skill == SkillType.Fishing;

    private void TrySubscribeGatheringXpPopups()
    {
        if (_subscribedXpGained)
            return;
        if (SkillsManager.Instance == null)
            return;

        SkillsManager.Instance.OnXpGained += HandleGatheringXpGained;
        _subscribedXpGained = true;
    }

    private void UnsubscribeGatheringXpPopups()
    {
        if (!_subscribedXpGained)
            return;
        if (SkillsManager.Instance != null)
            SkillsManager.Instance.OnXpGained -= HandleGatheringXpGained;
        _subscribedXpGained = false;
    }

    private void HandleGatheringXpGained(SkillType skill, int amount, string _)
    {
        if (amount <= 0 || !IsGatheringSkill(skill))
            return;

        ShowGatheringXpGained(amount);
    }

    /// <summary>Quest reward items: slow green float at the same anchor as +gold (player + worldOffset).</summary>
    public void ShowQuestRewardItemGained(string itemId, int amount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemId))
            return;

        if (!popupPrefab)
            return;

        if (!canvas || !playerWorld)
            Rebind();

        if (!canvas || !playerWorld)
            return;

        string label = ItemGainPopupNotifier.ResolveDisplayLabel(itemId, amount);
        string text = amount > 1 ? $"+{amount} {label}" : $"+{label}";
        SpawnQuestRewardItemPopupAtWorld(playerWorld.position + worldOffset + questRewardAnchorExtraWorld, text);
    }

    /// <summary>World anchor = player + same base offset as gold + optional extra for XP; motion matches gold gains (rise + fade).</summary>
    public void ShowGatheringXpGained(int amount)
    {
        if (amount <= 0)
            return;

        if (!popupPrefab)
            return;

        if (!canvas || !playerWorld)
            Rebind();

        if (!canvas || !playerWorld)
            return;

        SpawnGatheringXpPopupAtWorld(playerWorld.position + worldOffset + gatheringXpAnchorExtraWorld, amount);
    }

    private void SpawnGatheringXpPopupAtWorld(Vector3 worldPos, int amount)
    {
        if (amount <= 0 || !popupPrefab)
            return;

        Rebind();

        Camera cam = ResolveWorldProjectionCamera();
        if (!cam)
            return;

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
        popup.PlayLocalTextWithGoldGainMotion(stackedLocal, $"+{amount} xp", gatheringXpTextColor, () => ReleaseStackSlot(slot));
    }

    private void SpawnQuestRewardItemPopupAtWorld(Vector3 worldPos, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || !popupPrefab)
            return;

        Rebind();

        Camera cam = ResolveWorldProjectionCamera();
        if (!cam)
            return;

        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        Canvas targetCanvas = GetPopupTargetCanvas();
        if (targetCanvas == null)
            return;
        RectTransform canvasRect = targetCanvas.transform as RectTransform;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, GetRectEventCamera(targetCanvas), out Vector2 localPoint))
            return;

        int slot = AcquireStackSlot();
        float stackSpacing = Mathf.Max(0f, questRewardStackVerticalSpacing);
        Vector2 stackedLocal = localPoint + Vector2.up * (slot * stackSpacing);
        var popup = Instantiate(popupPrefab, targetCanvas.transform);
        BringPopupToFront(popup);
        popup.PlayLocalQuestRewardItem(stackedLocal, text, () => ReleaseStackSlot(slot));
    }

    private void SpawnTextPopupAtWorld(Vector3 worldPos, string text, Color color)
    {
        if (string.IsNullOrWhiteSpace(text) || !popupPrefab)
            return;

        Rebind();

        Camera cam = ResolveWorldProjectionCamera();
        if (!cam)
            return;

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
        popup.PlayLocalText(stackedLocal, text, color, applyGoldStroke: false, () => ReleaseStackSlot(slot));
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

        // Dedicated top overlay canvas already sorts above the HUD. Adding a Canvas per popup
        // forces a full UI canvas rebuild and causes a visible hitch on every gold/item popup.
        if (useDedicatedTopPopupCanvas && targetCanvas == _topPopupCanvas)
        {
            EnsurePopupNonBlocking(popup);
            return;
        }

        var popupCanvas = popup.GetComponent<Canvas>();
        if (!popupCanvas)
            popupCanvas = popup.gameObject.AddComponent<Canvas>();

        int topLayerId = GetHighestSortingLayerId();
        int baseOrder = targetCanvas.sortingOrder;

        popupCanvas.overrideSorting = true;
        popupCanvas.sortingLayerID = topLayerId;
        popupCanvas.sortingOrder = baseOrder + Mathf.Max(0, popupSortingOrderOffset);

        EnsurePopupNonBlocking(popup);
    }

    private static void EnsurePopupNonBlocking(GoldPopup popup)
    {
        if (!popup)
            return;

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