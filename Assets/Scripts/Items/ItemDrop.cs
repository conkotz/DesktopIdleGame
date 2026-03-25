using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider2D))]
public class ItemDrop : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float lifetimeSeconds = 30f;

    [Header("Click priority")]
    [Tooltip("Layers that compete for clicks (Pickup + Resource + NPC). Must include this object's layer.")]
    [SerializeField] private LayerMask interactMask = ~0;

    public string ItemId { get; private set; }
    public int Amount { get; private set; }

    private Collider2D _col;

    // Used by WorldClickPicker2D tie-breaker (newest drop wins)
    public int DropOrder { get; private set; }
    private static int _dropSeq;

    private void Awake()
    {
        _col = GetComponent<Collider2D>();
        DropOrder = ++_dropSeq;
    }

    public void Init(string itemId, int amount, Sprite icon)
    {
        ItemId = itemId;
        Amount = amount;

        if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer && icon) spriteRenderer.sprite = icon;

        if (lifetimeSeconds > 0f)
            Destroy(gameObject, lifetimeSeconds);
    }


    public bool TryPickup(Inventory inv)
    {
        if (inv == null) return false;
        if (Amount <= 0 || string.IsNullOrWhiteSpace(ItemId)) return false;

        int added = inv.AddPartial(ItemId, Amount);
        int left = Amount - added;

        if (left <= 0)
        {
            Destroy(gameObject);
            return true;
        }

        Amount = left;
        return false;
    }
}