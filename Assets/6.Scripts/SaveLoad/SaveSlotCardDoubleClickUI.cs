using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Double-click the slot card to resume (same as LoadGameButton). Requires a Graphic on this object for raycasts.
/// </summary>
[DisallowMultipleComponent]
public class SaveSlotCardDoubleClickUI : MonoBehaviour, IPointerClickHandler
{
    [HideInInspector] public SaveSlotMenuUI menu;
    [HideInInspector] public int slotIndex;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.clickCount != 2 || !menu)
            return;
        if (menu.IsResumeTransitionActive)
            return;
        menu.OnClickLoadSlot(slotIndex);
    }
}
