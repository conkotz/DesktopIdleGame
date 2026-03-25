using System;
using UnityEngine;

public class ToolSocketEquipper : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private EquipmentManager equipment;
    [SerializeField] private Inventory inventory;

    [Header("ToolSocket Children")]
    [SerializeField] private GameObject pickaxe;
    [SerializeField] private GameObject axe;
    [SerializeField] private GameObject rod;

    [Header("Sprite Swapping")]
    [Tooltip("If true, uses ItemDefinition.icon as the in-hand tool sprite.")]
    [SerializeField] private bool useItemIconAsHeldSprite = true;

    [Header("Sorting (fix 'behind hand')")]
    [SerializeField] private SpriteRenderer baselineRenderer; // eg your arm/body renderer
    [SerializeField] private int toolSortingOffset = 5;

    private Action _cb;
    private GameObject _current;

    private void Awake()
    {
        if (!equipment) equipment = GetComponentInParent<EquipmentManager>();
        if (!inventory) inventory = equipment ? equipment.Inventory : GetComponentInParent<Inventory>();

        if (!baselineRenderer)
            baselineRenderer = GetComponentInParent<SpriteRenderer>();

        Clear();
    }

    private void OnEnable()
    {
        if (!equipment) return;

        _cb ??= Refresh;
        equipment.OnVisualsChanged += _cb;

        Refresh();
    }

    private void OnDisable()
    {
        if (equipment != null && _cb != null)
            equipment.OnVisualsChanged -= _cb;

        if (equipment != null)
            equipment.SetOffHandVisible(true);
    }

    private void Refresh()
    {
        if (!equipment)
        {
            Clear();
            return;
        }

        ToolKey key = equipment.GetVisualMainHandToolKey();

        bool showingGatherTool =
            key == ToolKey.Pickaxe ||
            key == ToolKey.Axe ||
            key == ToolKey.FishingRod;

        // Hide offhand while gathering tool is visually active
        equipment.SetOffHandVisible(!showingGatherTool);

        switch (key)
        {
            case ToolKey.Pickaxe:
                ShowOnly(pickaxe);
                ApplyToolSpriteFromVisualItemId(pickaxe);
                break;

            case ToolKey.Axe:
                ShowOnly(axe);
                ApplyToolSpriteFromVisualItemId(axe);
                break;

            case ToolKey.FishingRod:
                ShowOnly(rod);
                ApplyToolSpriteFromVisualItemId(rod);
                break;

            default:
                Clear();
                break;
        }
    }

    private void ApplyToolSpriteFromVisualItemId(GameObject toolGo)
    {
        if (!toolGo || !inventory || !equipment) return;

        // ✅ This is the override item while gathering (toolbelt tool)
        string visualId = equipment.VisualMainHandItemId;
        if (string.IsNullOrWhiteSpace(visualId)) return;

        var def = inventory.GetItemDef(visualId);
        if (!def) return;

        if (useItemIconAsHeldSprite && def.icon != null)
        {
            var sr = toolGo.GetComponentInChildren<SpriteRenderer>(true);
            if (sr) sr.sprite = def.icon;
        }

        ForceSortingFront(toolGo);
    }

    private void ForceSortingFront(GameObject toolGo)
    {
        if (!toolGo || !baselineRenderer) return;

        var srs = toolGo.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            if (!srs[i]) continue;
            srs[i].sortingLayerID = baselineRenderer.sortingLayerID;
            srs[i].sortingOrder = baselineRenderer.sortingOrder + toolSortingOffset;
        }
    }

    private void ShowOnly(GameObject go)
    {
        if (!go) { Clear(); return; }
        if (_current == go && go.activeSelf) return;

        SetActiveSafe(pickaxe, false);
        SetActiveSafe(axe, false);
        SetActiveSafe(rod, false);

        SetActiveSafe(go, true);
        _current = go;

        ForceSortingFront(go);
    }

    private void Clear()
    {
        SetActiveSafe(pickaxe, false);
        SetActiveSafe(axe, false);
        SetActiveSafe(rod, false);
        _current = null;
    }

    private static void SetActiveSafe(GameObject go, bool active)
    {
        if (go && go.activeSelf != active)
            go.SetActive(active);
    }
}