using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]
public class ItemDrop : MonoBehaviour
{
    /// <summary>
    /// Raised after <see cref="Init"/> (enemy drops, player drops, level-placed pickups, legacy spawns).
    /// Argument is trimmed item id (not yet legacy-remapped — listeners should use <see cref="Inventory.RemapLegacyItemId"/>).
    /// </summary>
    public static event Action<string> OnWorldPickupSpawned;

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Image iconImage;
    [Tooltip("World-space width/height of the icon box (inventory-style fill, independent of sprite PPU).")]
    [SerializeField] private float iconWorldSize = 1f;
    [SerializeField] private float lifetimeSeconds = 30f;

    [Header("Stack label")]
    [Tooltip("Optional. Shown only when Amount > 1. Assign a child TMP (world or UI); if empty, uses first TMP_Text under this object.")]
    [SerializeField] private TMP_Text stackAmountText;
    [Tooltip("Stack number height as a fraction of iconWorldSize (matches inventory corner labels).")]
    [SerializeField] private float stackLabelSizeFraction = 0.38f;

    [Header("Click priority")]
    [Tooltip("Layers that compete for clicks (Pickup + Resource + NPC). Must include this object's layer.")]
    [SerializeField] private LayerMask interactMask = ~0;
    [Header("Auto-battle vacuum")]
    [SerializeField, Min(0.1f)] private float autoBattleVacuumSpeed = 9f;
    [SerializeField, Min(0f)] private float autoBattleVacuumAcceleration = 18f;
    [SerializeField, Min(0f)] private float autoBattleVacuumArcHeight = 1.25f;
    [SerializeField, Min(0.01f)] private float autoBattleVacuumTouchEpsilon = 0.04f;

    public string ItemId { get; private set; }
    public int Amount { get; private set; }
    /// <summary>Optional human-readable origin used by the session tracker (e.g. "Splitwood Tree", "Spider").</summary>
    public string SourceName { get; private set; }

    private string _levelOneShotPickupClaimKey;

    private float _placedLevelRespawnDelay;
    private ItemDefinition _placedLevelRespawnItemDef;
    private int _placedLevelRespawnStack;
    private Transform _placedLevelRespawnParent;
    private bool _placedLevelRespawnAlignToGround;

    private Collider2D _col;
    private Rigidbody2D _rb;
    private Canvas _worldCanvas;
    private RectTransform _iconRect;
    private float _fittedIconWidth;
    private float _fittedIconHeight;
    private Color _stackLabelBaseColor = Color.white;
    private bool _stackLabelDimmed;
    private Coroutine _launchRoutine;
    private Coroutine _autoBattleVacuumRoutine;

    // Used by WorldClickPicker2D tie-breaker (newest drop wins)
    public int DropOrder { get; private set; }
    public bool HasVisibleStackLabel =>
        Amount > 1 && stackAmountText != null && stackAmountText.gameObject.activeInHierarchy;
    private static int _dropSeq;

    private void Awake()
    {
        _col = GetComponent<Collider2D>();
        _rb = GetComponent<Rigidbody2D>();
        DropOrder = ++_dropSeq;
    }

    private void OnEnable()
    {
        WorldFloorFollowerRegistry.Register(transform, WorldFloorFollowerRegistry.Category.ItemDrop);
        ItemDropStackLabelFocus.Register(this);
    }

    private void OnDisable()
    {
        WorldFloorFollowerRegistry.Unregister(transform);
        ItemDropStackLabelFocus.Unregister(this);
    }

    /// <param name="disableAutoDespawn">When true, the pickup never auto-destroys (e.g. level-placed one-shot loot).</param>
    public void Init(string itemId, int amount, Sprite icon, bool disableAutoDespawn = false)
    {
        ItemId = itemId;
        Amount = amount;

        ApplyIconVisual(icon ?? ResolveIconFromDatabase(itemId));
        ConfigurePickupLayout();
        ConfigureStackLabelLayout();

        ResolveStackLabel();
        RefreshStackLabel();

        if (!disableAutoDespawn && lifetimeSeconds > 0f)
            Destroy(gameObject, lifetimeSeconds);

        try
        {
            OnWorldPickupSpawned?.Invoke(ItemId);
        }
        catch (Exception)
        {
            // Never break loot spawning if a subscriber throws.
        }
    }

    /// <summary>When set, fully picking up this drop marks the key in save data (one-time level spawn reward).</summary>
    public void SetLevelOneShotPickupClaimKey(string saveKey)
    {
        _levelOneShotPickupClaimKey = string.IsNullOrWhiteSpace(saveKey) ? null : saveKey.Trim();
    }

    /// <summary>
    /// Level-placed pickups: after a full pickup, respawn the same stack at the same world position after <paramref name="delaySeconds"/>.
    /// </summary>
    public void ConfigurePlacedLevelRespawn(
        float delaySeconds,
        ItemDefinition itemDef,
        int stackAmount,
        Transform parent,
        bool alignToGround)
    {
        if (delaySeconds < 0.01f || !itemDef || stackAmount <= 0)
        {
            _placedLevelRespawnDelay = 0f;
            _placedLevelRespawnItemDef = null;
            return;
        }

        _placedLevelRespawnDelay = delaySeconds;
        _placedLevelRespawnItemDef = itemDef;
        _placedLevelRespawnStack = Mathf.Max(1, stackAmount);
        _placedLevelRespawnParent = parent;
        _placedLevelRespawnAlignToGround = alignToGround;
    }

    private void SchedulePlacedRespawnIfConfigured()
    {
        if (_placedLevelRespawnDelay < 0.01f || !_placedLevelRespawnItemDef)
            return;

        DropManager dm = DropManager.Instance;
        if (!dm)
            return;

        dm.SchedulePlacedLevelPickupRespawn(
            _placedLevelRespawnItemDef,
            _placedLevelRespawnStack,
            transform.position,
            _placedLevelRespawnParent,
            _placedLevelRespawnAlignToGround,
            _placedLevelRespawnDelay);
    }

    /// <summary>
    /// Tags the drop with the gameplay event that produced it so <see cref="SessionTrackerData"/> can attribute
    /// the loot to a source (e.g. enemy display name) once the player picks it up.
    /// </summary>
    public void SetSourceName(string sourceName)
    {
        SourceName = string.IsNullOrWhiteSpace(sourceName) ? null : sourceName.Trim();
    }

    public void SnapVisualBottomToWorldY(float worldY, float skin = 0.01f)
    {
        ResolveVisualRefs();

        if (!_rb)
            _rb = GetComponent<Rigidbody2D>();

        if (_rb)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.gravityScale = 0f;
            _rb.bodyType = RigidbodyType2D.Kinematic;
        }

        float deltaY = (worldY + Mathf.Max(0f, skin)) - GetVisualBottomWorldY();
        transform.position += new Vector3(0f, deltaY, 0f);

        if (_rb)
            _rb.position = transform.position;
    }

    public void LaunchToGround(Vector3 startWorldPosition, Vector3 targetWorldPosition, float groundY, float duration, float arcHeight, float skin = 0.01f)
    {
        ResolveVisualRefs();

        if (!_rb)
            _rb = GetComponent<Rigidbody2D>();

        if (!_col)
            _col = GetComponent<Collider2D>();

        if (_rb)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.gravityScale = 0f;
            _rb.bodyType = RigidbodyType2D.Kinematic;
        }

        // Trigger colliders still pick up mouse clicks, but do not physically stack or push other drops.
        if (_col)
            _col.isTrigger = true;

        transform.position = startWorldPosition;

        Vector3 endWorldPosition = targetWorldPosition;
        float visualBottomOffset = GetVisualBottomWorldY() - transform.position.y;
        endWorldPosition.y = groundY + Mathf.Max(0f, skin) - visualBottomOffset;

        if (_launchRoutine != null)
            StopCoroutine(_launchRoutine);

        _launchRoutine = StartCoroutine(CoLaunchToGround(startWorldPosition, endWorldPosition, Mathf.Max(0.01f, duration), Mathf.Max(0f, arcHeight)));
    }

    private void ResolveVisualRefs()
    {
        if (!iconImage)
        {
            Transform iconTransform = transform.Find("Icon");
            if (iconTransform)
                iconImage = iconTransform.GetComponent<Image>();
        }

        if (iconImage)
            _iconRect = iconImage.rectTransform;

        if (!spriteRenderer)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void ApplyIconVisual(Sprite icon)
    {
        ResolveVisualRefs();

        if (iconImage)
        {
            if (spriteRenderer)
                spriteRenderer.enabled = false;

            bool hasIcon = icon != null;
            iconImage.gameObject.SetActive(hasIcon);
            iconImage.enabled = hasIcon;
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.color = Color.white;
            iconImage.raycastTarget = false;
            return;
        }

        if (!spriteRenderer)
            return;

        spriteRenderer.enabled = true;
        if (!icon)
            return;

        spriteRenderer.sprite = icon;
        FitSpriteRendererToWorldSize(spriteRenderer, icon, iconWorldSize);
    }

    private void ConfigurePickupLayout()
    {
        ResolveVisualRefs();

        var rootRect = transform as RectTransform;
        if (rootRect)
        {
            float size = Mathf.Max(0.05f, iconWorldSize);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.sizeDelta = new Vector2(size, size);
        }

        if (iconImage)
        {
            EnsureWorldCanvas();
            BottomAlignIconInBox();
        }

        FitColliderToIcon();
        Canvas.ForceUpdateCanvases();
    }

    private void FitColliderToIcon()
    {
        if (!_col)
            _col = GetComponent<Collider2D>();

        float width = _fittedIconWidth > 0f ? _fittedIconWidth : iconWorldSize;
        float height = _fittedIconHeight > 0f ? _fittedIconHeight : iconWorldSize;

        if (_col is CircleCollider2D circle)
        {
            circle.radius = width * 0.5f;
            circle.offset = new Vector2(0f, height * 0.5f);
        }
        else if (_col is BoxCollider2D box)
        {
            box.size = new Vector2(width, height);
            box.offset = new Vector2(0f, height * 0.5f);
        }
    }

    private void EnsureWorldCanvas()
    {
        _worldCanvas = GetComponent<Canvas>();
        if (!_worldCanvas)
            _worldCanvas = gameObject.AddComponent<Canvas>();

        _worldCanvas.renderMode = RenderMode.WorldSpace;
        _worldCanvas.overrideSorting = true;
        _worldCanvas.worldCamera = null;

        if (spriteRenderer)
        {
            _worldCanvas.sortingLayerID = spriteRenderer.sortingLayerID;
            _worldCanvas.sortingOrder = spriteRenderer.sortingOrder;
        }
    }

    private void BottomAlignIconInBox()
    {
        if (!_iconRect || !iconImage || !iconImage.sprite)
        {
            _fittedIconWidth = iconWorldSize;
            _fittedIconHeight = iconWorldSize;
            StretchIconToFullBox();
            return;
        }

        Sprite sprite = iconImage.sprite;
        float box = Mathf.Max(0.05f, iconWorldSize);
        float spriteW = sprite.rect.width / sprite.pixelsPerUnit;
        float spriteH = sprite.rect.height / sprite.pixelsPerUnit;
        if (spriteW <= 0f || spriteH <= 0f)
        {
            _fittedIconWidth = box;
            _fittedIconHeight = box;
            StretchIconToFullBox();
            return;
        }

        float scale = Mathf.Min(box / spriteW, box / spriteH);
        _fittedIconWidth = spriteW * scale;
        _fittedIconHeight = spriteH * scale;

        iconImage.preserveAspect = false;

        _iconRect.anchorMin = new Vector2(0.5f, 0f);
        _iconRect.anchorMax = new Vector2(0.5f, 0f);
        _iconRect.pivot = new Vector2(0.5f, 0f);
        _iconRect.anchoredPosition = Vector2.zero;
        _iconRect.sizeDelta = new Vector2(_fittedIconWidth, _fittedIconHeight);
    }

    private void StretchIconToFullBox()
    {
        if (!_iconRect)
            return;

        _iconRect.anchorMin = Vector2.zero;
        _iconRect.anchorMax = Vector2.one;
        _iconRect.pivot = new Vector2(0.5f, 0f);
        _iconRect.anchoredPosition = Vector2.zero;
        _iconRect.sizeDelta = Vector2.zero;
        _iconRect.offsetMin = Vector2.zero;
        _iconRect.offsetMax = Vector2.zero;
    }

    private static void FitSpriteRendererToWorldSize(SpriteRenderer renderer, Sprite icon, float targetWorldSize)
    {
        if (!renderer || !icon || targetWorldSize <= 0f)
            return;

        Vector2 spriteSize = icon.bounds.size;
        float maxDim = Mathf.Max(spriteSize.x, spriteSize.y);
        if (maxDim <= 0.0001f)
            return;

        float scale = targetWorldSize / maxDim;
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private float GetVisualBottomWorldY()
    {
        if (iconImage && iconImage.enabled && iconImage.sprite && _iconRect)
        {
            var corners = new Vector3[4];
            _iconRect.GetWorldCorners(corners);
            return Mathf.Min(corners[0].y, corners[3].y);
        }

        if (!_col)
            _col = GetComponent<Collider2D>();
        if (_col)
            return _col.bounds.min.y;

        if (spriteRenderer && spriteRenderer.enabled)
            return spriteRenderer.bounds.min.y;

        return transform.position.y;
    }

    private static Sprite ResolveIconFromDatabase(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        Inventory inventory = UnityEngine.Object.FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        ItemDatabase db = inventory != null ? inventory.GetItemDatabase() : null;
        if (!db)
            db = UnityEngine.Object.FindFirstObjectByType<ItemDatabase>(FindObjectsInactive.Include);
        if (!db)
            db = Resources.Load<ItemDatabase>("Databases/ItemDatabase");

        return db != null ? db.Get(itemId)?.icon : null;
    }

    private IEnumerator CoLaunchToGround(Vector3 startWorldPosition, Vector3 endWorldPosition, float duration, float arcHeight)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 2f);

            Vector3 position = Vector3.Lerp(startWorldPosition, endWorldPosition, eased);
            position.y += Mathf.Sin(t * Mathf.PI) * arcHeight;

            transform.position = position;
            if (_rb)
                _rb.position = position;

            yield return null;
        }

        transform.position = endWorldPosition;
        if (_rb)
            _rb.position = endWorldPosition;

        _launchRoutine = null;
    }

    private void ResolveStackLabel()
    {
        if (stackAmountText)
            return;
        Transform named = transform.Find("StackAmount");
        if (named)
            stackAmountText = named.GetComponent<TMP_Text>();
        if (!stackAmountText)
            stackAmountText = GetComponentInChildren<TMP_Text>(true);
    }

    private void ConfigureStackLabelLayout()
    {
        ResolveStackLabel();
        if (!stackAmountText)
            return;

        float fontSize = Mathf.Max(0.06f, iconWorldSize * stackLabelSizeFraction);
        stackAmountText.fontSize = fontSize;
        stackAmountText.enableAutoSizing = false;
        stackAmountText.raycastTarget = false;
        stackAmountText.transform.SetAsLastSibling();
        _stackLabelBaseColor = stackAmountText.color;
        ApplyStackLabelDimState();
        // RectTransform anchor/position/size come from the prefab — Init only scales font size.
    }

    public bool TryGetStackLabelScreenRect(Camera cam, out Rect screenRect)
    {
        screenRect = default;
        if (!HasVisibleStackLabel || !cam)
            return false;

        stackAmountText.ForceMeshUpdate();
        Bounds bounds = stackAmountText.bounds;
        Vector3 min = cam.WorldToScreenPoint(bounds.min);
        Vector3 max = cam.WorldToScreenPoint(bounds.max);
        screenRect = Rect.MinMaxRect(
            Mathf.Min(min.x, max.x),
            Mathf.Min(min.y, max.y),
            Mathf.Max(min.x, max.x),
            Mathf.Max(min.y, max.y));
        return screenRect.width > 0.5f && screenRect.height > 0.5f;
    }

    public void SetStackLabelDimState(bool dimmed)
    {
        _stackLabelDimmed = dimmed;
        ApplyStackLabelDimState();
    }

    private void ApplyStackLabelDimState()
    {
        if (!stackAmountText)
            return;

        Color c = _stackLabelBaseColor;
        c.a = _stackLabelDimmed
            ? _stackLabelBaseColor.a * ItemDropStackLabelFocus.DimAlphaMultiplierForLabels
            : _stackLabelBaseColor.a;
        stackAmountText.color = c;
    }

    private void RefreshStackLabel()
    {
        if (!stackAmountText)
            return;

        if (Amount <= 1)
        {
            stackAmountText.gameObject.SetActive(false);
            return;
        }

        stackAmountText.gameObject.SetActive(true);
        stackAmountText.text = Amount.ToString();
        ApplyStackLabelDimState();
    }


    /// <param name="storage">When <paramref name="idleAutoBattleLoot"/> is true, overflow may be deposited here if the inventory cannot take the rest.</param>
    public bool TryPickup(Inventory inv, PlayerStorage storage = null, bool idleAutoBattleLoot = false)
    {
        if (inv == null) return false;
        if (Amount <= 0 || string.IsNullOrWhiteSpace(ItemId)) return false;

        List<int> invTouched = idleAutoBattleLoot ? new List<int>(4) : null;
        int added = inv.AddPartial(ItemId, Amount, null, true, invTouched);
        int left = Amount - added;

        if (added > 0)
            SessionTrackerData.EnsureInstance().RegisterLootGain(SourceName, ItemId, added);

        if (idleAutoBattleLoot && invTouched != null)
        {
            for (int i = 0; i < invTouched.Count; i++)
                AutoBattleLootHighlight.MarkInventorySlot(invTouched[i]);
        }

        if (idleAutoBattleLoot && storage != null && left > 0 && inv.IsFull())
        {
            var stTouched = new List<int>(4);
            int dep = storage.TryDepositAmountFromExternal(ItemId, left, stTouched);
            left -= dep;
            for (int i = 0; i < stTouched.Count; i++)
                AutoBattleLootHighlight.MarkStorageSlot(stTouched[i]);

            if (dep > 0)
            {
                string label = ItemGainPopupNotifier.ResolveDisplayLabel(ItemId, dep);
                GameLog.ItemSentToStorageBecauseInventoryFull(label, dep);
            }

            if (left > 0)
            {
                string label = ItemGainPopupNotifier.ResolveDisplayLabel(ItemId, left);
                GameLog.CannotObtainInventoryAndStorageFull(label, left);
            }
        }

        if (idleAutoBattleLoot)
            AutoBattleLootHighlight.RefreshLootHighlightUIs();

        if (left <= 0)
        {
            if (!string.IsNullOrEmpty(_levelOneShotPickupClaimKey))
                SaveManager.Instance?.MarkLevelItemPickupOnceClaimed(_levelOneShotPickupClaimKey);

            SchedulePlacedRespawnIfConfigured();

            Destroy(gameObject);
            return true;
        }

        Amount = left;
        RefreshStackLabel();
        return false;
    }

    /// <summary>
    /// Voluntary map leave: inventory first, then Main storage tab. Does not respawn placed pickups.
    /// </summary>
    public void CollectForVoluntaryMapExit(Inventory inv, PlayerStorage storage)
    {
        if (inv == null || storage == null || Amount <= 0 || string.IsNullOrWhiteSpace(ItemId))
        {
            Destroy(gameObject);
            return;
        }

        int addedToInventory = inv.AddPartial(ItemId, Amount, null, true, null);
        int remaining = Amount - addedToInventory;

        if (addedToInventory > 0)
            SessionTrackerData.EnsureInstance().RegisterLootGain(SourceName, ItemId, addedToInventory);

        if (remaining > 0)
        {
            int deposited = storage.TryDepositAmountToTab(ItemId, remaining, StorageTabKind.Main);
            remaining -= deposited;

            if (deposited > 0)
            {
                string label = ItemGainPopupNotifier.ResolveDisplayLabel(ItemId, deposited);
                GameLog.ItemRecoveredToMainStorageOnMapLeave(label, deposited);
            }

            if (remaining > 0)
            {
                string label = ItemGainPopupNotifier.ResolveDisplayLabel(ItemId, remaining);
                GameLog.CannotObtainInventoryAndStorageFull(label, remaining);
            }
        }

        if (!string.IsNullOrEmpty(_levelOneShotPickupClaimKey))
            SaveManager.Instance?.MarkLevelItemPickupOnceClaimed(_levelOneShotPickupClaimKey);

        Destroy(gameObject);
    }

    public void BeginAutoBattleVacuum(
        Transform target,
        Collider2D targetCollider,
        Inventory inventory,
        PlayerStorage storage = null)
    {
        if (target == null || inventory == null || Amount <= 0)
            return;
        if (_autoBattleVacuumRoutine != null)
            return;

        _autoBattleVacuumRoutine = StartCoroutine(
            CoAutoBattleVacuum(target, targetCollider, inventory, storage));
    }

    private IEnumerator CoAutoBattleVacuum(
        Transform target,
        Collider2D targetCollider,
        Inventory inventory,
        PlayerStorage storage)
    {
        if (_launchRoutine != null)
        {
            StopCoroutine(_launchRoutine);
            _launchRoutine = null;
        }

        if (!_rb)
            _rb = GetComponent<Rigidbody2D>();
        if (!_col)
            _col = GetComponent<Collider2D>();

        if (_rb)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.gravityScale = 0f;
            _rb.bodyType = RigidbodyType2D.Kinematic;
        }
        if (_col)
            _col.isTrigger = true;

        Vector3 startPos = transform.position;
        Vector3 linearPos = startPos;
        Vector3 initialTargetPoint = target.position;
        if (targetCollider != null)
            initialTargetPoint = targetCollider.ClosestPoint(startPos);
        float initialDistance = Mathf.Max(0.001f, Vector3.Distance(startPos, initialTargetPoint));
        float accelTime = 0f;
        float currentArcHeight = Mathf.Max(0f, autoBattleVacuumArcHeight);

        while (target != null && inventory != null && Amount > 0)
        {
            Vector3 targetPoint = target.position;
            if (targetCollider != null)
                targetPoint = targetCollider.ClosestPoint(linearPos);

            accelTime += Time.deltaTime;
            float speed = Mathf.Max(0.1f, autoBattleVacuumSpeed) + Mathf.Max(0f, autoBattleVacuumAcceleration) * accelTime;
            float step = speed * Time.deltaTime;

            linearPos = Vector3.MoveTowards(linearPos, targetPoint, step);

            // Arc lift fades toward the end so pickups "snap" into the player cleanly.
            float remaining = Vector3.Distance(linearPos, targetPoint);
            float progress01 = 1f - Mathf.Clamp01(remaining / initialDistance);
            float arcLift = Mathf.Sin(progress01 * Mathf.PI) * currentArcHeight;
            Vector3 next = linearPos + Vector3.up * arcLift;

            transform.position = next;
            if (_rb)
                _rb.position = next;

            if (HasReachedAutoBattleVacuumPickupRange(targetCollider))
            {
                bool picked = TryPickup(inventory, storage, idleAutoBattleLoot: true);
                if (!picked)
                {
                    // Inventory/storage constraints prevented full pickup; stop vacuuming this drop for now.
                    break;
                }
            }

            yield return null;
        }

        _autoBattleVacuumRoutine = null;
    }

    private bool HasReachedAutoBattleVacuumPickupRange(Collider2D targetCollider)
    {
        if (targetCollider == null)
            return false;

        if (_col != null)
        {
            ColliderDistance2D d = _col.Distance(targetCollider);
            if (d.isOverlapped || d.distance <= autoBattleVacuumTouchEpsilon)
                return true;
        }

        Vector3 closest = targetCollider.ClosestPoint(transform.position);
        return (transform.position - closest).sqrMagnitude <= autoBattleVacuumTouchEpsilon * autoBattleVacuumTouchEpsilon;
    }
}