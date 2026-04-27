using System.Collections;
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
    private Rigidbody2D _rb;
    private Coroutine _launchRoutine;

    // Used by WorldClickPicker2D tie-breaker (newest drop wins)
    public int DropOrder { get; private set; }
    private static int _dropSeq;

    private void Awake()
    {
        _col = GetComponent<Collider2D>();
        _rb = GetComponent<Rigidbody2D>();
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

    public void SnapVisualBottomToWorldY(float worldY, float skin = 0.01f)
    {
        if (!spriteRenderer)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (!spriteRenderer)
            return;

        if (!_rb)
            _rb = GetComponent<Rigidbody2D>();

        if (_rb)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.gravityScale = 0f;
            _rb.bodyType = RigidbodyType2D.Kinematic;
        }

        float deltaY = (worldY + Mathf.Max(0f, skin)) - spriteRenderer.bounds.min.y;
        transform.position += new Vector3(0f, deltaY, 0f);

        if (_rb)
            _rb.position = transform.position;
    }

    public void LaunchToGround(Vector3 startWorldPosition, Vector3 targetWorldPosition, float groundY, float duration, float arcHeight, float skin = 0.01f)
    {
        if (!spriteRenderer)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (!_rb)
            _rb = GetComponent<Rigidbody2D>();

        if (!_col)
            _col = GetComponent<Collider2D>();

        if (_rb)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.gravityScale = 0f;
            _rb.bodyType = RigidbodyType2D.Kinematic;
        }

        // Trigger colliders still pick up mouse clicks, but do not physically stack or push other drops.
        if (_col)
            _col.isTrigger = true;

        transform.position = startWorldPosition;

        Vector3 endWorldPosition = targetWorldPosition;
        if (spriteRenderer)
        {
            float visualBottomOffset = spriteRenderer.bounds.min.y - transform.position.y;
            endWorldPosition.y = groundY + Mathf.Max(0f, skin) - visualBottomOffset;
        }

        if (_launchRoutine != null)
            StopCoroutine(_launchRoutine);

        _launchRoutine = StartCoroutine(CoLaunchToGround(startWorldPosition, endWorldPosition, Mathf.Max(0.01f, duration), Mathf.Max(0f, arcHeight)));
    }

    private IEnumerator CoLaunchToGround(Vector3 startWorldPosition, Vector3 endWorldPosition, float duration, float arcHeight)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 2f);

            Vector3 position = Vector3.Lerp(startWorldPosition, endWorldPosition, eased);
            position.y += Mathf.Sin(t * Mathf.PI) * arcHeight;

            transform.position = position;
            if (_rb)
                _rb.position = position;

            yield return null;
        }

        transform.position = endWorldPosition;
        if (_rb)
            _rb.position = endWorldPosition;

        _launchRoutine = null;
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