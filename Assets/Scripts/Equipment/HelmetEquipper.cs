using System;
using UnityEngine;

[DisallowMultipleComponent]
public class HelmetEquipper : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private EquipmentManager equipment;
    [SerializeField] private Inventory inventory;

    [Header("Renderers")]
    [SerializeField] private SpriteRenderer helmetRenderer;
    [SerializeField] private SpriteRenderer baseHeadRenderer;

    [Header("Auto-find (optional)")]
    [SerializeField] private string helmetRendererObjectName = "Helmet";
    [SerializeField] private string baseHeadRendererObjectName = "Head";

    private Action _cb;
    private Vector3 _defaultLocalPosition;

    private void Awake()
    {
        if (!equipment) equipment = GetComponentInParent<EquipmentManager>();
        if (!inventory) inventory = equipment ? equipment.Inventory : GetComponentInParent<Inventory>();

        if (!helmetRenderer)
            helmetRenderer = FindRendererByName(helmetRendererObjectName);

        if (!baseHeadRenderer)
            baseHeadRenderer = FindRendererByName(baseHeadRendererObjectName);

        if (helmetRenderer)
            _defaultLocalPosition = helmetRenderer.transform.localPosition;

        ApplyNone();
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
    }

    private void Refresh()
    {
        if (!equipment || !inventory || !helmetRenderer)
        {
            ApplyNone();
            return;
        }

        string itemId = equipment.HelmetItemId;

        if (string.IsNullOrWhiteSpace(itemId))
        {
            ApplyNone();
            return;
        }

        var def = inventory.GetItemDef(itemId);
        if (!def || def.EquippedSprite == null)
        {
            ApplyNone();
            return;
        }

        helmetRenderer.sprite = def.EquippedSprite;
        helmetRenderer.transform.localPosition = _defaultLocalPosition + (Vector3)def.EquippedLocalOffset;
        helmetRenderer.enabled = true;

        if (baseHeadRenderer)
            baseHeadRenderer.enabled = false;
    }

    private void ApplyNone()
    {
        if (helmetRenderer)
        {
            helmetRenderer.sprite = null;
            helmetRenderer.transform.localPosition = _defaultLocalPosition;
            helmetRenderer.enabled = false;
        }

        if (baseHeadRenderer)
            baseHeadRenderer.enabled = true;
    }

    private SpriteRenderer FindRendererByName(string childName)
    {
        if (string.IsNullOrWhiteSpace(childName)) return null;

        var root = transform.root;
        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (!t.name.Equals(childName, StringComparison.OrdinalIgnoreCase)) continue;

            var sr = t.GetComponent<SpriteRenderer>();
            if (sr) return sr;
        }

        return null;
    }
}