using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Drag-and-place mode for <see cref="WorldObjectMovable"/> interactables along the lane X axis.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldObjectMoveModeController : MonoBehaviour
{
    private const int OverlayCanvasSortOrder = 10004;
    private static readonly Color GhostValidTint = new(1f, 1f, 1f, 0.45f);
    private static readonly Color GhostInvalidTint = new(1f, 0.25f, 0.25f, 0.55f);

    private static WorldObjectMoveModeController _instance;

    public static bool IsActive => _instance != null && _instance._active;

    /// <summary>True for the rest of the frame after place/cancel so the same click cannot walk or interact.</summary>
    public static bool SuppressesWorldLeftClick =>
        Time.frameCount == s_suppressWorldLeftClickUntilFrame;

    private static int s_suppressWorldLeftClickUntilFrame = -1;

    private WorldObjectMovable _target;
    private GameObject _ghostRoot;
    private readonly List<SpriteRenderer> _ghostSprites = new(16);
    private LineRenderer _gridLine;
    private LineRenderer _gridTicks;
    private Camera _worldCamera;
    private PlayerController _lockedPlayer;
    private bool _appliedMovementLock;
    private bool _releaseMovementLockNextLateUpdate;
    private float _startX;
    private float _currentSnappedX;
    private Vector3 _lockedLanePosition;
    private bool _overlap;
    private bool _active;

    private RectTransform _overlayRoot;
    private Button _cancelButton;
    private Button _saveButton;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.name.Equals("GamePlay", System.StringComparison.OrdinalIgnoreCase))
            return;

        EnsureExists();
    }

    public static void EnsureExists()
    {
        if (_instance != null)
            return;

        var host = new GameObject(nameof(WorldObjectMoveModeController));
        _instance = host.AddComponent<WorldObjectMoveModeController>();
    }

    public static void BeginMove(WorldObjectMovable target)
    {
        if (!target || !target.CanShowMoveMenu)
            return;

        EnsureExists();
        if (_instance == null)
            return;

        _instance.EnterMoveMode(target);
    }

    public static bool TryConsumeWorldLeftClick()
    {
        if (!IsActive || _instance == null)
            return false;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return true;

        _instance.TryPlaceAtCurrentPosition();
        return true;
    }

    public static bool TryConsumeWorldRightClick()
    {
        if (!IsActive || _instance == null)
            return false;

        _instance.CancelMoveMode(logMessage: true);
        return true;
    }

    public static void CancelIfActive()
    {
        if (!IsActive || _instance == null)
            return;

        _instance.CancelMoveMode(logMessage: false, deferMovementUnlock: false);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        BuildOverlayUi();
        BuildGridVisuals();
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (scene.name.Equals("GamePlay", System.StringComparison.OrdinalIgnoreCase))
            CancelMoveMode(logMessage: false, deferMovementUnlock: false);
    }

    private void OnDestroy()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        if (!_active || !_target)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelMoveMode(logMessage: true);
            return;
        }

        UpdateGhostFromMouse();
    }

    private void LateUpdate()
    {
        if (_releaseMovementLockNextLateUpdate)
        {
            _releaseMovementLockNextLateUpdate = false;
            ReleaseMoveModeGameplayLock();
        }
    }

    private static void SuppressWorldLeftClickForThisFrame()
    {
        s_suppressWorldLeftClickUntilFrame = Time.frameCount;
    }

    private void FinishMoveModeSession(bool deferMovementUnlock)
    {
        if (deferMovementUnlock)
            SuppressWorldLeftClickForThisFrame();

        if (deferMovementUnlock)
            _releaseMovementLockNextLateUpdate = _appliedMovementLock;
        else
            ReleaseMoveModeGameplayLock();

        _active = false;
    }

    private void EnterMoveMode(WorldObjectMovable target)
    {
        if (_active)
            CancelMoveMode(logMessage: false, deferMovementUnlock: false);

        DismissGameplayUi();
        _target = target;
        _lockedLanePosition = target.transform.position;
        _startX = _lockedLanePosition.x;
        _currentSnappedX = WorldObjectMovable.SnapWorldX(_startX);
        _active = true;

        ApplyMoveModeGameplayLock();
        CreateGhost(target);
        UpdateGridVisuals();
        UpdateGhostFromMouse();
        SetOverlayVisible(true);
        GameLog.Add("Move object: click to place, right-click or Cancel to stop.");
    }

    private void CancelMoveMode(bool logMessage, bool deferMovementUnlock = true)
    {
        if (!_active)
            return;

        if (_target)
            _target.RestoreSpriteColors();

        DestroyGhost();
        SetGridVisible(false);
        SetOverlayVisible(false);
        _target = null;
        FinishMoveModeSession(deferMovementUnlock);

        if (logMessage)
            GameLog.Add("Move object cancelled.");
    }

    private void TryPlaceAtCurrentPosition()
    {
        if (!_target)
            return;

        if (_overlap)
        {
            GameLog.Add("Cannot be placed here as its overlapping something else", GameLog.CannotMessageColor);
            return;
        }

        Vector3 placed = _lockedLanePosition;
        placed.x = _currentSnappedX;
        _target.transform.position = placed;
        WorldObjectPositionStore.RecordWorldX(_target.PositionKey, placed.x);
        SaveManager.Instance?.Save();

        DestroyGhost();
        SetGridVisible(false);
        SetOverlayVisible(false);
        _target.RestoreSpriteColors();
        _target = null;
        FinishMoveModeSession(deferMovementUnlock: true);

        GameLog.Add("Object position saved.");
    }

    private void ApplyMoveModeGameplayLock()
    {
        _lockedPlayer = FindFirstObjectByType<PlayerController>();
        if (!_lockedPlayer)
            return;

        _lockedPlayer.StopMovementAndCombatFromHotkey();
        _lockedPlayer.SetMovementLocked(true);
        _appliedMovementLock = true;
    }

    private void ReleaseMoveModeGameplayLock()
    {
        if (_appliedMovementLock && _lockedPlayer)
            _lockedPlayer.SetMovementLocked(false);

        _appliedMovementLock = false;
        _lockedPlayer = null;
    }

    private void UpdateGhostFromMouse()
    {
        if (!_ghostRoot || !_target)
            return;

        if (!TryGetMouseWorldX(out float mouseX))
            return;

        if (PlayAreaBounds.TryGetClampXForWorldX(_target.transform.position.x, 0.25f, out float minX, out float maxX))
            mouseX = Mathf.Clamp(mouseX, minX, maxX);

        _currentSnappedX = WorldObjectMovable.SnapWorldX(mouseX);
        Vector3 pos = _lockedLanePosition;
        pos.x = _currentSnappedX;
        _ghostRoot.transform.position = pos;

        _overlap = _target.WouldOverlapAtX(_currentSnappedX, ignore: _target);
        ApplyGhostTint(_overlap ? GhostInvalidTint : GhostValidTint);
    }

    private bool TryGetMouseWorldX(out float worldX)
    {
        worldX = 0f;
        Camera cam = ResolveWorldCamera();
        if (!cam)
            return false;

        Vector3 screen = Input.mousePosition;
        screen.z = Mathf.Abs(cam.transform.position.z);
        worldX = cam.ScreenToWorldPoint(screen).x;
        return true;
    }

    private Camera ResolveWorldCamera()
    {
        if (_worldCamera)
            return _worldCamera;

        _worldCamera = GameplayScreenOverlayLayout.TryResolveStripCamera();
        if (!_worldCamera)
            _worldCamera = Camera.main;
        return _worldCamera;
    }

    private void CreateGhost(WorldObjectMovable target)
    {
        DestroyGhost();
        target.CacheSpriteRenderers();
        target.SetSpriteTint(new Color(1f, 1f, 1f, 0.35f));

        _ghostRoot = Instantiate(target.gameObject);
        _ghostRoot.name = $"{target.name}_MoveGhost";
        DisableGhostInteractivity(_ghostRoot);
        _ghostSprites.Clear();
        _ghostRoot.GetComponentsInChildren(true, _ghostSprites);
        ApplyGhostTint(GhostValidTint);
        _ghostRoot.transform.position = _lockedLanePosition;
    }

    private static void DisableGhostInteractivity(GameObject root)
    {
        if (!root)
            return;

        WorldObjectMovable movable = root.GetComponentInChildren<WorldObjectMovable>(true);
        if (movable)
            Destroy(movable);

        Collider2D[] cols = root.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i])
                cols[i].enabled = false;
        }

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour b = behaviours[i];
            if (!b)
                continue;
            b.enabled = false;
        }
    }

    private void DestroyGhost()
    {
        if (_ghostRoot)
            Destroy(_ghostRoot);
        _ghostRoot = null;
        _ghostSprites.Clear();
    }

    private void ApplyGhostTint(Color tint)
    {
        for (int i = 0; i < _ghostSprites.Count; i++)
        {
            SpriteRenderer sr = _ghostSprites[i];
            if (sr)
                sr.color = tint;
        }
    }

    private void BuildGridVisuals()
    {
        var gridRoot = new GameObject("MoveObjectGrid");
        gridRoot.transform.SetParent(transform, false);

        _gridLine = gridRoot.AddComponent<LineRenderer>();
        ConfigureGridLine(_gridLine, 0.08f);

        var ticksRoot = new GameObject("MoveObjectGridTicks");
        ticksRoot.transform.SetParent(gridRoot.transform, false);
        _gridTicks = ticksRoot.AddComponent<LineRenderer>();
        ConfigureGridLine(_gridTicks, 0.05f);

        SetGridVisible(false);
    }

    private static void ConfigureGridLine(LineRenderer lr, float width)
    {
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.widthMultiplier = width;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = new Color(0.35f, 0.85f, 1f, 0.85f);
        lr.endColor = lr.startColor;
        lr.sortingOrder = 5000;
    }

    private void UpdateGridVisuals()
    {
        if (!_target || !_gridLine || !_gridTicks)
            return;

        float centerY = _lockedLanePosition.y;

        float minX = _target.transform.position.x - 12f;
        float maxX = _target.transform.position.x + 12f;
        if (PlayAreaBounds.TryGetClampXForWorldX(_target.transform.position.x, 0.25f, out float clampMin, out float clampMax))
        {
            minX = clampMin;
            maxX = clampMax;
        }

        minX = WorldObjectMovable.SnapWorldX(minX);
        maxX = WorldObjectMovable.SnapWorldX(maxX);
        if (maxX < minX)
            (minX, maxX) = (maxX, minX);

        _gridLine.positionCount = 2;
        _gridLine.SetPosition(0, new Vector3(minX, centerY, 0f));
        _gridLine.SetPosition(1, new Vector3(maxX, centerY, 0f));

        int tickCount = Mathf.Max(2, Mathf.RoundToInt((maxX - minX) / WorldObjectMovable.GridSnapSize) + 1);
        _gridTicks.positionCount = tickCount * 2;
        int write = 0;
        for (int i = 0; i < tickCount; i++)
        {
            float x = minX + i * WorldObjectMovable.GridSnapSize;
            _gridTicks.SetPosition(write++, new Vector3(x, centerY - 0.15f, 0f));
            _gridTicks.SetPosition(write++, new Vector3(x, centerY + 0.15f, 0f));
        }

        SetGridVisible(true);
    }

    private void SetGridVisible(bool visible)
    {
        if (_gridLine)
            _gridLine.enabled = visible;
        if (_gridTicks)
            _gridTicks.enabled = visible;
    }

    private void BuildOverlayUi()
    {
        var canvasGo = new GameObject("MoveObjectOverlayCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OverlayCanvasSortOrder;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        _overlayRoot = new GameObject("Buttons", typeof(RectTransform)).GetComponent<RectTransform>();
        _overlayRoot.SetParent(canvasGo.transform, false);
        StretchTopCenter(_overlayRoot, 520f, 52f, 12f);

        var hlg = _overlayRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;

        _cancelButton = CreateOverlayButton("Cancel", new Color(0.48f, 0.22f, 0.22f, 0.92f), OnCancelClicked);
        _saveButton = CreateOverlayButton("Place", new Color(0.2f, 0.42f, 0.28f, 0.92f), OnPlaceClicked);

        SetOverlayVisible(false);
    }

    private Button CreateOverlayButton(string label, Color bg, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(_overlayRoot, false);
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = 220f;
        le.preferredHeight = 44f;
        go.GetComponent<Image>().color = bg;
        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(onClick);

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        StretchFull(textRt);
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;

        return btn;
    }

    private void OnCancelClicked() => CancelMoveMode(logMessage: true);

    private void OnPlaceClicked() => TryPlaceAtCurrentPosition();

    private void SetOverlayVisible(bool visible)
    {
        if (_overlayRoot)
            _overlayRoot.gameObject.SetActive(visible);
    }

    private static void StretchTopCenter(RectTransform rt, float width, float height, float topInset)
    {
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(0f, -topInset);
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void DismissGameplayUi()
    {
        MerchantClick.ForceCloseMerchantMode();
        FurnaceClick.ForceClose();
        CookingClick.ForceClose();
        BlacksmithingClick.ForceClose();
        StorageClick.ForceCloseStorageMode();
        ContextMenuUI.EnsureInstance().Hide();
    }
}
