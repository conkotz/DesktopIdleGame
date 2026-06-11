using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One storage tab: click selects, 0.5s hold enlarges then horizontal reorder, drop target for dragged items.
/// </summary>
[RequireComponent(typeof(Button))]
public class StorageTabButtonUI : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
    IDropHandler
{
    private const float HoldSeconds = 0.5f;
    private const float ReorderScale = 1.15f;

    [SerializeField] private StorageTabKind tabKind = StorageTabKind.Main;
    [SerializeField] private GameObject affinityBar;

    private StorageTabBarUI _bar;
    private Button _button;
    private Vector3 _baseScale = Vector3.one;
    private bool _pointerDown;
    private bool _reorderMode;
    private bool _suppressClick;
    private int _reorderPointerId = -1;
    private Coroutine _holdRoutine;
    private Inventory _inventory;
    private PlayerStorage _storage;

    public StorageTabKind TabKind => tabKind;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _baseScale = transform.localScale;
        tabKind = InferTabKindFromName(gameObject.name, tabKind);
        ResolveAffinityBarIfNeeded();
    }

    private void OnEnable()
    {
        ResolveStorageRefs();
        if (_storage != null)
            _storage.OnTabAffinityChanged += RefreshAffinityBar;

        RefreshAffinityBar();
    }

    private void OnDisable()
    {
        if (_storage != null)
            _storage.OnTabAffinityChanged -= RefreshAffinityBar;
    }

    public void Initialize(StorageTabBarUI bar)
    {
        _bar = bar;
        ResolveStorageRefs();
        RefreshAffinityBar();
    }

    public void RefreshAffinityBar()
    {
        ResolveAffinityBarIfNeeded();

        if (!affinityBar || tabKind == StorageTabKind.Main)
        {
            if (affinityBar)
                affinityBar.SetActive(false);
            return;
        }

        bool show = _storage != null && _storage.IsTabAffinityEnabled(tabKind);
        affinityBar.SetActive(show);
    }

    public void ApplySelectedVisual(bool selected)
    {
        UITabBarButtonVisuals.Apply(_button, selected);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            ShowTabContextMenu();
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        _pointerDown = true;
        _suppressClick = false;
        _reorderPointerId = eventData.pointerId;
        if (_holdRoutine != null)
            StopCoroutine(_holdRoutine);
        _holdRoutine = StartCoroutine(HoldToReorderRoutine());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (eventData.pointerId != _reorderPointerId && _reorderPointerId >= 0)
            return;

        _pointerDown = false;
        if (_holdRoutine != null)
        {
            StopCoroutine(_holdRoutine);
            _holdRoutine = null;
        }

        if (_reorderMode)
            FinishReorder();
    }

    private void Update()
    {
        if (!_reorderMode)
            return;

        if (!IsReorderPointerStillDown())
            FinishReorder();
        else
            ProcessReorderAtScreenPosition(Input.mousePosition);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        if (_suppressClick || _reorderMode || InventoryDragState.HasDrag)
            return;

        _bar?.SelectTab(tabKind);
    }

    public void OnDrop(PointerEventData eventData)
    {
        ResolveStorageRefs();
        if (_storage == null)
            return;

        if (EquipmentSlotUI.TryConsumeEquipDrag(out var fromSlotType, out string equipItemId, out int equipAmount))
        {
            if (_inventory == null || string.IsNullOrWhiteSpace(equipItemId))
                return;

            ItemDefinition equipDef = _storage.GetItemDef(equipItemId);
            if (!StorageTabFilters.PassesTab(equipDef, tabKind))
                return;

            var equipment = FindFirstObjectByType<EquipmentManager>(FindObjectsInactive.Include);
            var toolbelt = FindFirstObjectByType<ToolbeltManager>(FindObjectsInactive.Include);
            if (equipment == null)
                return;

            int placed = _storage.TryDepositAmountToTab(equipItemId, equipAmount, tabKind);
            int remainder = equipAmount - placed;
            if (remainder > 0)
            {
                if (!_inventory.Add(equipItemId, remainder, null, notifyItemGainPopup: false))
                {
                    if (placed > 0)
                        _storage.RemoveItemAmountAcrossSlots(equipItemId, placed);
                    return;
                }
            }

            if (placed > 0 || remainder > 0)
            {
                EquipmentSlotUI.UnequipDragSource(fromSlotType, equipment, toolbelt);
                _bar?.HandleItemDroppedOnTab(tabKind);
            }

            return;
        }

        if (!InventoryDragState.HasDrag)
            return;

        if (InventoryDragState.Source == InventoryDragState.SourceKind.Inventory)
        {
            if (_inventory == null)
                return;

            int fromInv = InventoryDragState.FromSlotIndex;
            int amount = InventoryDragState.IsSplit
                ? InventoryDragState.CarriedAmount
                : _inventory.GetSlot(fromInv).amount;

            if (amount <= 0)
                return;

            int moved = _storage.TryMoveFromInventoryToTab(_inventory, fromInv, tabKind, amount);
            if (moved > 0)
            {
                InventoryDragState.EndDrag();
                _bar?.HandleItemDroppedOnTab(tabKind);
            }

            return;
        }

        if (InventoryDragState.Source == InventoryDragState.SourceKind.Storage)
        {
            int fromSlot = InventoryDragState.FromSlotIndex;
            int amount = InventoryDragState.IsSplit
                ? InventoryDragState.CarriedAmount
                : _storage.GetSlot(fromSlot).amount;

            if (amount <= 0)
                return;

            int moved = _storage.TryMoveFromStorageSlotToTab(fromSlot, tabKind, amount);
            if (moved > 0)
            {
                InventoryDragState.EndDrag();
                _bar?.HandleItemDroppedOnTab(tabKind);
            }
        }
    }

    private IEnumerator HoldToReorderRoutine()
    {
        yield return new WaitForSecondsRealtime(HoldSeconds);
        if (!_pointerDown || InventoryDragState.HasDrag)
            yield break;

        _reorderMode = true;
        _suppressClick = true;
        if (_button)
            _button.interactable = false;
        transform.localScale = _baseScale * ReorderScale;
        transform.SetAsLastSibling();
    }

    private void FinishReorder()
    {
        if (!_reorderMode)
            return;

        _reorderMode = false;
        _reorderPointerId = -1;
        transform.localScale = _baseScale;
        if (_button)
            _button.interactable = true;

        var parent = transform.parent as RectTransform;
        if (parent)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);

        _bar?.NotifyTabOrderChanged();
    }

    private bool IsReorderPointerStillDown()
    {
        if (_reorderPointerId < 0)
            return Input.GetMouseButton(0);

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId == _reorderPointerId)
                return touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
        }

        return Input.GetMouseButton(0);
    }

    private void ProcessReorderAtScreenPosition(Vector2 screenPosition)
    {
        var rt = transform as RectTransform;
        var parent = rt != null ? rt.parent as RectTransform : null;
        if (rt == null || parent == null)
            return;

        Camera eventCamera = null;
        Canvas canvas = parent.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCamera = canvas.worldCamera;

        int myIndex = rt.GetSiblingIndex();
        for (int i = 0; i < parent.childCount; i++)
        {
            if (i == myIndex)
                continue;

            var sibling = parent.GetChild(i) as RectTransform;
            if (sibling == null)
                continue;

            if (!RectTransformUtility.RectangleContainsScreenPoint(sibling, screenPosition, eventCamera))
                continue;

            rt.SetSiblingIndex(i);
            break;
        }
    }

    private void ShowTabContextMenu()
    {
        ResolveStorageRefs();

        var entries = new List<ContextMenuEntry>(2);
        if (tabKind != StorageTabKind.Main && _storage != null)
        {
            bool enabled = _storage.IsTabAffinityEnabled(tabKind);
            Sprite checkIcon = enabled ? ContextMenuUI.GetCheckmarkIcon() : null;
            entries.Add(new ContextMenuEntry(
                "Affinity Tab",
                () =>
                {
                    _storage.SetTabAffinityEnabled(tabKind, !enabled);
                    RefreshAffinityBar();
                },
                trailingIcon: checkIcon));
        }

        entries.Add(new ContextMenuEntry("Reposition", () => _bar?.RepositionTabToFirst(tabKind)));

        string headerTitle = GetTabDisplayName();
        ContextMenuUI menu = ContextMenuUI.EnsureInstance();
        menu.ShowAtScreen(entries, Input.mousePosition, headerTitle);
    }

    private string GetTabDisplayName()
    {
        if (_button != null)
        {
            TMP_Text label = _button.GetComponentInChildren<TMP_Text>(true);
            if (label != null && !string.IsNullOrWhiteSpace(label.text))
                return label.text.Trim();
        }

        return StorageTabFilters.GetDisplayName(tabKind);
    }

    private void ResolveAffinityBarIfNeeded()
    {
        if (affinityBar)
            return;

        Transform bar = transform.Find("AffinityBar");
        if (bar != null)
            affinityBar = bar.gameObject;
    }

    private void ResolveStorageRefs()
    {
        StorageGridUI grid = FindFirstObjectByType<StorageGridUI>(FindObjectsInactive.Include);
        if (grid != null && grid.PlayerStorage != null)
            _storage = grid.PlayerStorage;

        if (!_storage)
            _storage = FindFirstObjectByType<PlayerStorage>(FindObjectsInactive.Include);
        if (!_inventory)
            _inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
    }

    private static StorageTabKind InferTabKindFromName(string objectName, StorageTabKind fallback)
    {
        if (string.IsNullOrEmpty(objectName))
            return fallback;

        string n = objectName.ToLowerInvariant();
        if (n.Contains("main")) return StorageTabKind.Main;
        if (n.Contains("resource")) return StorageTabKind.Resources;
        if (n.Contains("equip")) return StorageTabKind.Equips;
        if (n.Contains("consum")) return StorageTabKind.Consumables;
        if (n.Contains("enhanc") || n.Contains("enahnc")) return StorageTabKind.Enhance;
        return fallback;
    }
}
