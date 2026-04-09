using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ToolbeltManager : MonoBehaviour, ISaveable
{
    public const int SlotCount = 4;

    public event Action<int, string> OnToolSlotChanged; // (index, itemId)

    [Header("Refs")]
    [SerializeField] private Inventory inventory;

    [SerializeField] private string[] toolItemIds = new string[SlotCount];

    private void Awake()
    {
        if (!inventory)
            inventory = GetComponent<Inventory>();

        if (toolItemIds == null || toolItemIds.Length != SlotCount)
            toolItemIds = new string[SlotCount];
    }

    public string GetToolItemId(int index)
    {
        if (index < 0 || index >= SlotCount) return null;
        return toolItemIds[index];
    }

    public void SetToolItemId(int index, string itemId)
    {
        if (index < 0 || index >= SlotCount) return;

        string next = string.IsNullOrWhiteSpace(itemId) ? null : itemId;
        if (toolItemIds[index] == next) return;

        toolItemIds[index] = next;
        OnToolSlotChanged?.Invoke(index, next);

        RequestImmediateSave();
    }

    public void Clear(int index) => SetToolItemId(index, null);

    public bool Contains(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;

        for (int i = 0; i < SlotCount; i++)
        {
            if (toolItemIds[i] == itemId)
                return true;
        }

        return false;
    }

    public int FindItemSlot(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return -1;

        for (int i = 0; i < SlotCount; i++)
        {
            if (toolItemIds[i] == itemId)
                return i;
        }

        return -1;
    }

    public int FindFirstEmptySlot()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (string.IsNullOrWhiteSpace(toolItemIds[i]))
                return i;
        }

        return -1;
    }

    private ItemDefinition GetDef(string itemId)
    {
        if (!inventory) return null;
        if (string.IsNullOrWhiteSpace(itemId)) return null;
        return inventory.GetItemDef(itemId);
    }

    private ToolKey GetToolKey(string itemId)
    {
        var def = GetDef(itemId);
        if (!def) return ToolKey.None;
        return def.handVisualKey;
    }

    private bool IsValidToolKey(ToolKey key)
    {
        return key != ToolKey.None && key != ToolKey.Weapon;
    }

    public int FindSameToolTypeSlot(string itemId)
    {
        ToolKey wantedKey = GetToolKey(itemId);
        if (!IsValidToolKey(wantedKey))
            return -1;

        for (int i = 0; i < SlotCount; i++)
        {
            string equippedId = toolItemIds[i];
            if (string.IsNullOrWhiteSpace(equippedId))
                continue;

            ToolKey equippedKey = GetToolKey(equippedId);
            if (equippedKey == wantedKey)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Adds to first empty slot, unless a tool of the same ToolKey is already equipped.
    /// In that case, it replaces that existing slot.
    /// Returns true if placed/replaced, false if no valid slot available.
    /// </summary>
    public bool TryAddToFirstEmpty(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        // Exact same item already equipped somewhere: do nothing
        if (Contains(itemId))
            return false;

        ToolKey newKey = GetToolKey(itemId);
        if (!IsValidToolKey(newKey))
            return false;

        var def = GetDef(itemId);
        if (def && def.UsesEquipmentTierGating && !def.MeetsEquipmentTierRequirement(SkillsManager.Instance))
            return false;

        // First: replace same tool type (e.g. Axe replaces Axe)
        int sameTypeSlot = FindSameToolTypeSlot(itemId);
        if (sameTypeSlot >= 0)
        {
            SetToolItemId(sameTypeSlot, itemId);
            return true;
        }

        // Otherwise use first empty slot
        int emptySlot = FindFirstEmptySlot();
        if (emptySlot >= 0)
        {
            SetToolItemId(emptySlot, itemId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the slot that would be used for this item:
    /// same-type slot first, otherwise first empty slot, otherwise -1.
    /// </summary>
    public int GetPreferredSlotFor(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return -1;

        int sameTypeSlot = FindSameToolTypeSlot(itemId);
        if (sameTypeSlot >= 0)
            return sameTypeSlot;

        return FindFirstEmptySlot();
    }

    // -------------------------
    // Save / Load
    // -------------------------
    public void SaveInto(SaveData data)
    {
        if (data.toolbeltItemIds == null)
            data.toolbeltItemIds = new List<string>(new string[SlotCount]);

        while (data.toolbeltItemIds.Count < SlotCount)
            data.toolbeltItemIds.Add(null);

        if (data.toolbeltItemIds.Count > SlotCount)
            data.toolbeltItemIds.RemoveRange(SlotCount, data.toolbeltItemIds.Count - SlotCount);

        for (int i = 0; i < SlotCount; i++)
            data.toolbeltItemIds[i] = GetToolItemId(i);
    }

    public void LoadFrom(SaveData data)
    {
        if (toolItemIds == null || toolItemIds.Length != SlotCount)
            toolItemIds = new string[SlotCount];

        if (data == null || data.toolbeltItemIds == null)
        {
            for (int i = 0; i < SlotCount; i++)
                toolItemIds[i] = null;

            for (int i = 0; i < SlotCount; i++)
                OnToolSlotChanged?.Invoke(i, null);

            return;
        }

        for (int i = 0; i < SlotCount; i++)
        {
            string id = (i < data.toolbeltItemIds.Count) ? data.toolbeltItemIds[i] : null;
            toolItemIds[i] = string.IsNullOrWhiteSpace(id) ? null : id;
        }

        for (int i = 0; i < SlotCount; i++)
            OnToolSlotChanged?.Invoke(i, toolItemIds[i]);
    }

    // -------------------------
    // Save helper
    // -------------------------
    private void RequestImmediateSave()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.Save();
    }
}