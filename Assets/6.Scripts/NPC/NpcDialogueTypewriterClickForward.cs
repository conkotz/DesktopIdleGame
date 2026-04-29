using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Forwards pointer down to <see cref="NPCDialogueBoxUI"/> so clicks on scroll/viewport/content
/// complete any in-progress typewriter text.
/// </summary>
[DisallowMultipleComponent]
public sealed class NpcDialogueTypewriterClickForward : MonoBehaviour, IPointerDownHandler
{
    private NPCDialogueBoxUI _owner;

    public void Init(NPCDialogueBoxUI owner)
    {
        _owner = owner;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _owner?.CompleteAllTypewriters();
    }
}
