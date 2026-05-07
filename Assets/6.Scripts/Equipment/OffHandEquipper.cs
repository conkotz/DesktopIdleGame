using System;
using UnityEngine;

[DisallowMultipleComponent]
public class OffHandEquipper : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private EquipmentManager equipment;
    [SerializeField] private Inventory inventory;

    [Header("Renderer (back arm held item)")]
    [SerializeField] private SpriteRenderer offHandRenderer;

    [Header("Auto-find (optional)")]
    [Tooltip("Name of the held sprite object under the back arm (e.g. 'Shield' or 'OffHandItem').")]
    [SerializeField] private string offHandRendererObjectName = "Shield";

    [Header("Defaults")]
    [SerializeField] private Vector3 defaultLocalPosition;
    [SerializeField] private Vector3 defaultLocalEulerAngles;
    [SerializeField] private bool defaultFlipX = false;
    [SerializeField] private bool defaultFlipY = false;

    private Action _cb;
    private ItemDefinition _currentDef;

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        if (!equipment) equipment = GetComponentInParent<EquipmentManager>();
        if (!inventory) inventory = equipment ? equipment.Inventory : GetComponentInParent<Inventory>();

        if (!offHandRenderer)
            offHandRenderer = FindRendererByName(offHandRendererObjectName);

        if (offHandRenderer)
        {
            defaultLocalPosition = offHandRenderer.transform.localPosition;
            defaultLocalEulerAngles = offHandRenderer.transform.localEulerAngles;
            defaultFlipX = offHandRenderer.flipX;
            defaultFlipY = offHandRenderer.flipY;
        }

        ApplyNone();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

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

    private void LateUpdate()
    {
        if (!offHandRenderer || _currentDef == null)
            return;

        if (_currentDef.UseCustomEquippedPose)
        {
            offHandRenderer.transform.localPosition = _currentDef.EquippedLocalOffset;
            offHandRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, _currentDef.EquippedLocalRotationZ);
            offHandRenderer.flipX = _currentDef.EquippedFlipX;
            offHandRenderer.flipY = _currentDef.EquippedFlipY;
        }
    }

    private void Refresh()
    {
        if (!equipment || !inventory || !offHandRenderer)
        {
            ApplyNone();
            return;
        }

        if (equipment.HideBothHandsOverride)
        {
            ApplyNone();
            return;
        }
        // Hide offhand whenever a gathering tool is being visually shown
        ToolKey visualKey = equipment.GetVisualMainHandToolKey();
        bool hideOffHandForGathering =
            visualKey == ToolKey.Pickaxe ||
            visualKey == ToolKey.Axe ||
            visualKey == ToolKey.FishingRod;

        if (hideOffHandForGathering)
        {
            ApplyNone();
            return;
        }

        string itemId = equipment.OffHandItemId;

        if (string.IsNullOrWhiteSpace(itemId))
        {
            ApplyNone();
            return;
        }

        var def = inventory.GetItemDef(itemId);
        if (!def)
        {
            ApplyNone();
            return;
        }

        Sprite spriteToUse = def.EquippedSprite ? def.EquippedSprite : def.HeldSprite;
        if (!spriteToUse)
        {
            ApplyNone();
            return;
        }

        _currentDef = def;

        offHandRenderer.sprite = spriteToUse;
        offHandRenderer.enabled = true;

        if (def.UseCustomEquippedPose)
        {
            offHandRenderer.transform.localPosition = def.EquippedLocalOffset;
            offHandRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, def.EquippedLocalRotationZ);
            offHandRenderer.flipX = def.EquippedFlipX;
            offHandRenderer.flipY = def.EquippedFlipY;
        }
        else
        {
            offHandRenderer.transform.localPosition = defaultLocalPosition;
            offHandRenderer.transform.localEulerAngles = defaultLocalEulerAngles;
            offHandRenderer.flipX = defaultFlipX;
            offHandRenderer.flipY = defaultFlipY;
        }
    }

    private void ApplyNone()
    {
        _currentDef = null;

        if (!offHandRenderer) return;

        offHandRenderer.sprite = null;
        offHandRenderer.enabled = false;
        offHandRenderer.transform.localPosition = defaultLocalPosition;
        offHandRenderer.transform.localEulerAngles = defaultLocalEulerAngles;
        offHandRenderer.flipX = defaultFlipX;
        offHandRenderer.flipY = defaultFlipY;
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