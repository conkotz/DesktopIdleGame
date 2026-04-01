using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider2D))]
public class ItemDrop : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float lifetimeSeconds = 30f;

    [Header("Stack label")]
    [Tooltip("Optional. Shown only when Amount > 1. Assign a child TMP (world or UI); if empty, uses first TMP_Text under this object.")]
    [SerializeField] private TMP_Text stackAmountText;

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

        ResolveStackLabel();
        RefreshStackLabel();

        if (lifetimeSeconds > 0f)
            Destroy(gameObject, lifetimeSeconds);
    }

    private void ResolveStackLabel()
    {
        if (stackAmountText)
            return;
        Transform named = transform.Find("StackAmount");
        if (named)
            stackAmountText = named.GetComponent<TMP_Text>();
        if (!stackAmountText)
            stackAmountText = GetComponentInChildren<TMP_Text>(true);
    }

    private void RefreshStackLabel()
    {
        if (!stackAmountText)
            return;

        if (Amount <= 1)
        {
            stackAmountText.gameObject.SetActive(false);
            return;
        }

        stackAmountText.gameObject.SetActive(true);
        stackAmountText.text = Amount.ToString();
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
        RefreshStackLabel();
        return false;
    }
}