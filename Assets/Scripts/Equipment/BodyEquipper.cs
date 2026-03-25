using System;
using UnityEngine;

[DisallowMultipleComponent]
public class BodyEquipper : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private EquipmentManager equipment;
    [SerializeField] private Inventory inventory;

    [Header("Renderer")]
    [SerializeField] private SpriteRenderer bodyRenderer;

    [Header("Auto-find (optional)")]
    [Tooltip("Name of the body armor overlay object under the character.")]
    [SerializeField] private string bodyRendererObjectName = "BodyArmor";

    [Header("Base Body (optional)")]
    [Tooltip("Base body renderer (e.g. 'Body') that should be hidden when BodyArmor is equipped.")]
    [SerializeField] private SpriteRenderer baseBodyRenderer;

    [Tooltip("Name of base body renderer object under the character.")]
    [SerializeField] private string baseBodyRendererObjectName = "Body";

    private Action _cb;
    private Vector3 _defaultLocalPosition;

    private void Awake()
    {
        if (!equipment) equipment = GetComponentInParent<EquipmentManager>();
        if (!inventory) inventory = equipment ? equipment.Inventory : GetComponentInParent<Inventory>();

        if (!bodyRenderer)
            bodyRenderer = FindRendererByName(bodyRendererObjectName);

        if (!baseBodyRenderer)
            baseBodyRenderer = FindRendererByName(baseBodyRendererObjectName);

        if (bodyRenderer)
            _defaultLocalPosition = bodyRenderer.transform.localPosition;

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
        if (!equipment || !inventory || !bodyRenderer)
        {
            ApplyNone();
            return;
        }

        string itemId = equipment.BodyItemId;

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

        bodyRenderer.sprite = def.EquippedSprite;
        bodyRenderer.transform.localPosition = _defaultLocalPosition + (Vector3)def.EquippedLocalOffset;
        bodyRenderer.enabled = true;

        // Avoid double visuals by hiding the base body whenever an armor overlay is equipped.
        if (baseBodyRenderer)
            baseBodyRenderer.enabled = false;
    }

    private void ApplyNone()
    {
        if (!bodyRenderer) return;
        bodyRenderer.sprite = null;
        bodyRenderer.transform.localPosition = _defaultLocalPosition;
        bodyRenderer.enabled = false;

        // Restore base body when no armor overlay is equipped.
        if (baseBodyRenderer)
            baseBodyRenderer.enabled = true;
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