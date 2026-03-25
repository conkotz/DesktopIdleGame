using System;
using UnityEngine;

[DisallowMultipleComponent]
public class MainHandEquipper : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private EquipmentManager equipment;
    [SerializeField] private Inventory inventory;

    [Header("Renderer (front arm held item)")]
    [SerializeField] private SpriteRenderer mainHandRenderer;

    [Header("Auto-find (optional)")]
    [Tooltip("Name of the held sprite object under the front arm (e.g. 'Weapon' or 'MainHandItem').")]
    [SerializeField] private string mainHandRendererObjectName = "Weapon";

    [Header("Defaults")]
    [SerializeField] private Vector3 defaultLocalPosition;
    [SerializeField] private Vector3 defaultLocalEulerAngles;
    [SerializeField] private bool defaultFlipX = false;
    [SerializeField] private bool defaultFlipY = false;

    private Action _cb;
    private ItemDefinition _currentDef;

    private void Awake()
    {
        if (!equipment) equipment = GetComponentInParent<EquipmentManager>();
        if (!inventory) inventory = equipment ? equipment.Inventory : GetComponentInParent<Inventory>();

        if (!mainHandRenderer)
            mainHandRenderer = FindRendererByName(mainHandRendererObjectName);

        if (mainHandRenderer)
        {
            defaultLocalPosition = mainHandRenderer.transform.localPosition;
            defaultLocalEulerAngles = mainHandRenderer.transform.localEulerAngles;
            defaultFlipX = mainHandRenderer.flipX;
            defaultFlipY = mainHandRenderer.flipY;
        }

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

    private void LateUpdate()
    {
        if (!mainHandRenderer || _currentDef == null)
            return;

        if (_currentDef.UseCustomEquippedPose)
        {
            mainHandRenderer.transform.localPosition = _currentDef.EquippedLocalOffset;
            mainHandRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, _currentDef.EquippedLocalRotationZ);
            mainHandRenderer.flipX = _currentDef.EquippedFlipX;
            mainHandRenderer.flipY = _currentDef.EquippedFlipY;
        }
    }

    private void Refresh()
    {
        if (!equipment || !inventory || !mainHandRenderer)
        {
            ApplyNone();
            return;
        }

        if (equipment.HideBothHandsOverride)
        {
            ApplyNone();
            return;
        }

        string itemId = equipment.VisualMainHandItemId;

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

        mainHandRenderer.sprite = spriteToUse;
        mainHandRenderer.enabled = true;

        if (def.UseCustomEquippedPose)
        {
            mainHandRenderer.transform.localPosition = def.EquippedLocalOffset;
            mainHandRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, def.EquippedLocalRotationZ);
            mainHandRenderer.flipX = def.EquippedFlipX;
            mainHandRenderer.flipY = def.EquippedFlipY;
        }
        else
        {
            mainHandRenderer.transform.localPosition = defaultLocalPosition;
            mainHandRenderer.transform.localEulerAngles = defaultLocalEulerAngles;
            mainHandRenderer.flipX = defaultFlipX;
            mainHandRenderer.flipY = defaultFlipY;
        }
    }

    private void ApplyNone()
    {
        _currentDef = null;

        if (!mainHandRenderer) return;

        mainHandRenderer.sprite = null;
        mainHandRenderer.enabled = false;
        mainHandRenderer.transform.localPosition = defaultLocalPosition;
        mainHandRenderer.transform.localEulerAngles = defaultLocalEulerAngles;
        mainHandRenderer.flipX = defaultFlipX;
        mainHandRenderer.flipY = defaultFlipY;
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

        return GetComponentInChildren<SpriteRenderer>(true);
    }
}