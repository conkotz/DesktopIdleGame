using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Right-click relay for ability preset buttons. Left-click uses the normal <see cref="UnityEngine.UI.Button"/> onClick.
/// </summary>
[DisallowMultipleComponent]
public sealed class AbilityPresetButtonUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField, Min(0)] private int slotIndex;

    public int SlotIndex => slotIndex;

    public event Action<int, Vector2> RightClicked;

    public void Configure(int slot)
    {
        slotIndex = Mathf.Clamp(slot, 0, SkillsManager.AbilityPresetSlotCount - 1);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Right)
            return;

        eventData.Use();
        RightClicked?.Invoke(slotIndex, eventData.position);
    }
}
