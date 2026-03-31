using UnityEngine;
using UnityEngine.EventSystems;

public class ClickResource : MonoBehaviour
{
    [Header("Layers")]
    [Tooltip("Layers to consider for click competition (e.g. Pickup + Resource + Merchant).")]
    [SerializeField] private LayerMask interactMask = ~0;

    private ResourceNode node;

    private void Awake()
    {
        node = GetComponent<ResourceNode>();
    }
}