using System;
using System.Collections.Generic;
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

    [Header("Stacking")]
    [Tooltip("Extra vertical offset per concurrent popup so simultaneous messages do not overlap.")]
    [SerializeField] private float stackVerticalSpacing = 30f;

    [Header("Draw order")]
    [Tooltip("Added to the root UICanvas sorting order so popups render above the rest of the HUD.")]
    [SerializeField] private int popupSortingOrderOffset = 30000;

    [Header("Colours")]
    [SerializeField] private Color defaultMessageColor = Color.white;
    [SerializeField] private Color levelUpColor = new Color(0.35f, 0.8f, 1f, 1f);

    private Camera _cam;
    private readonly List<bool> _stackSlotBusy = new List<bool>();

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

    public void ShowGoldGained(int amount, string sourceLine = null)
    {
        if (amount <= 0 || !popupPrefab) return;

        if (!canvas || !playerWorld)
            Rebind();

        if (!canvas || !playerWorld) return;

        ShowGoldGainedAtWorld(playerWorld.position + worldOffset, amount, sourceLine);
    }

    public void ShowGoldGainedAtWorld(Vector3 worldPos, int amount, string sourceLine = null)
    {
        if (amount <= 0 || !popupPrefab) return;

        if (!canvas)
            Rebind();

        if (!canvas) return;

        Camera cam = canvas.worldCamera ? canvas.worldCamera : Camera.main;
        if (!cam) return;

        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        RectTransform canvasRect = canvas.transform as RectTransform;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, cam, out Vector2 localPoint))
            return;

        int slot = AcquireStackSlot();
        Vector2 stackedLocal = localPoint + Vector2.up * (slot * stackVerticalSpacing);
        var popup = Instantiate(popupPrefab, canvas.transform);
        BringPopupToFront(popup);
        popup.PlayLocal(stackedLocal, amount, sourceLine, () => ReleaseStackSlot(slot));
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
        RectTransform canvasRect = canvas.transform as RectTransform;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, cam, out Vector2 localPoint))
            return;

        int slot = AcquireStackSlot();
        Vector2 stackedLocal = localPoint + Vector2.up * (slot * stackVerticalSpacing);
        var popup = Instantiate(popupPrefab, canvas.transform);
        BringPopupToFront(popup);
        popup.PlayLocalText(stackedLocal, message, color, applyGoldStroke: false, () => ReleaseStackSlot(slot));
    }

    private void BringPopupToFront(GoldPopup popup)
    {
        if (!popup || !canvas)
            return;

        popup.transform.SetAsLastSibling();

        var popupCanvas = popup.GetComponent<Canvas>();
        if (!popupCanvas)
            popupCanvas = popup.gameObject.AddComponent<Canvas>();

        popupCanvas.overrideSorting = true;
        popupCanvas.sortingOrder = canvas.rootCanvas.sortingOrder + Mathf.Max(0, popupSortingOrderOffset);

        var cg = popup.GetComponent<CanvasGroup>();
        if (!cg)
            cg = popup.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;
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