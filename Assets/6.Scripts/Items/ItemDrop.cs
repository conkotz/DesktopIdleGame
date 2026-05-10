using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider2D))]
public class ItemDrop : MonoBehaviour
{
    /// <summary>
    /// Raised after <see cref="Init"/> (enemy drops, player drops, level-placed pickups, legacy spawns).
    /// Argument is trimmed item id (not yet legacy-remapped — listeners should use <see cref="Inventory.RemapLegacyItemId"/>).
    /// </summary>
    public static event Action<string> OnWorldPickupSpawned;

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float lifetimeSeconds = 30f;

    [Header("Stack label")]
    [Tooltip("Optional. Shown only when Amount > 1. Assign a child TMP (world or UI); if empty, uses first TMP_Text under this object.")]
    [SerializeField] private TMP_Text stackAmountText;

    [Header("Click priority")]
    [Tooltip("Layers that compete for clicks (Pickup + Resource + NPC). Must include this object's layer.")]
    [SerializeField] private LayerMask interactMask = ~0;
    [Header("Auto-battle vacuum")]
    [SerializeField, Min(0.1f)] private float autoBattleVacuumSpeed = 9f;
    [SerializeField, Min(0f)] private float autoBattleVacuumAcceleration = 18f;
    [SerializeField, Min(0f)] private float autoBattleVacuumArcHeight = 1.25f;
    [SerializeField, Min(0.01f)] private float autoBattleVacuumTouchEpsilon = 0.04f;

    public string ItemId { get; private set; }
    public int Amount { get; private set; }
    /// <summary>Optional human-readable origin used by the session tracker (e.g. "Splitwood Tree", "Spider").</summary>
    public string SourceName { get; private set; }

    private string _levelOneShotPickupClaimKey;

    private Collider2D _col;
    private Rigidbody2D _rb;
    private Coroutine _launchRoutine;
    private Coroutine _autoBattleVacuumRoutine;

    // Used by WorldClickPicker2D tie-breaker (newest drop wins)
    public int DropOrder { get; private set; }
    private static int _dropSeq;

    private void Awake()
    {
        _col = GetComponent<Collider2D>();
        _rb = GetComponent<Rigidbody2D>();
        DropOrder = ++_dropSeq;
    }

    /// <param name="disableAutoDespawn">When true, the pickup never auto-destroys (e.g. level-placed one-shot loot).</param>
    public void Init(string itemId, int amount, Sprite icon, bool disableAutoDespawn = false)
    {
        ItemId = itemId;
        Amount = amount;

        if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer && icon) spriteRenderer.sprite = icon;

        ResolveStackLabel();
        RefreshStackLabel();

        if (!disableAutoDespawn && lifetimeSeconds > 0f)
            Destroy(gameObject, lifetimeSeconds);

        try
        {
            OnWorldPickupSpawned?.Invoke(ItemId);
        }
        catch (Exception)
        {
            // Never break loot spawning if a subscriber throws.
        }
    }

    /// <summary>When set, fully picking up this drop marks the key in save data (one-time level spawn reward).</summary>
    public void SetLevelOneShotPickupClaimKey(string saveKey)
    {
        _levelOneShotPickupClaimKey = string.IsNullOrWhiteSpace(saveKey) ? null : saveKey.Trim();
    }

    /// <summary>
    /// Tags the drop with the gameplay event that produced it so <see cref="SessionTrackerData"/> can attribute
    /// the loot to a source (e.g. enemy display name) once the player picks it up.
    /// </summary>
    public void SetSourceName(string sourceName)
    {
        SourceName = string.IsNullOrWhiteSpace(sourceName) ? null : sourceName.Trim();
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


    /// <param name="storage">When <paramref name="idleAutoBattleLoot"/> is true, overflow may be deposited here if the inventory cannot take the rest.</param>
    public bool TryPickup(Inventory inv, PlayerStorage storage = null, bool idleAutoBattleLoot = false)
    {
        if (inv == null) return false;
        if (Amount <= 0 || string.IsNullOrWhiteSpace(ItemId)) return false;

        List<int> invTouched = idleAutoBattleLoot ? new List<int>(4) : null;
        int added = inv.AddPartial(ItemId, Amount, null, true, invTouched);
        int left = Amount - added;

        if (added > 0)
            SessionTrackerData.Instance?.RegisterLootGain(SourceName, ItemId, added);

        if (idleAutoBattleLoot && invTouched != null)
        {
            for (int i = 0; i < invTouched.Count; i++)
                AutoBattleLootHighlight.MarkInventorySlot(invTouched[i]);
        }

        if (idleAutoBattleLoot && storage != null && left > 0 && inv.IsFull())
        {
            var stTouched = new List<int>(4);
            int dep = storage.TryDepositAmountFromExternal(ItemId, left, stTouched);
            left -= dep;
            for (int i = 0; i < stTouched.Count; i++)
                AutoBattleLootHighlight.MarkStorageSlot(stTouched[i]);

            if (dep > 0)
            {
                string label = ItemGainPopupNotifier.ResolveDisplayLabel(ItemId, dep);
                GameLog.ItemSentToStorageBecauseInventoryFull(label, dep);
            }

            if (left > 0)
            {
                string label = ItemGainPopupNotifier.ResolveDisplayLabel(ItemId, left);
                GameLog.CannotObtainInventoryAndStorageFull(label, left);
            }
        }

        if (idleAutoBattleLoot)
            AutoBattleLootHighlight.RefreshLootHighlightUIs();

        if (left <= 0)
        {
            if (!string.IsNullOrEmpty(_levelOneShotPickupClaimKey))
                SaveManager.Instance?.MarkLevelItemPickupOnceClaimed(_levelOneShotPickupClaimKey);

            Destroy(gameObject);
            return true;
        }

        Amount = left;
        RefreshStackLabel();
        return false;
    }

    public void BeginAutoBattleVacuum(
        Transform target,
        Collider2D targetCollider,
        Inventory inventory,
        PlayerStorage storage = null)
    {
        if (target == null || inventory == null || Amount <= 0)
            return;
        if (_autoBattleVacuumRoutine != null)
            return;

        _autoBattleVacuumRoutine = StartCoroutine(
            CoAutoBattleVacuum(target, targetCollider, inventory, storage));
    }

    private IEnumerator CoAutoBattleVacuum(
        Transform target,
        Collider2D targetCollider,
        Inventory inventory,
        PlayerStorage storage)
    {
        if (_launchRoutine != null)
        {
            StopCoroutine(_launchRoutine);
            _launchRoutine = null;
        }

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
        if (_col)
            _col.isTrigger = true;

        Vector3 startPos = transform.position;
        Vector3 linearPos = startPos;
        Vector3 initialTargetPoint = target.position;
        if (targetCollider != null)
            initialTargetPoint = targetCollider.ClosestPoint(startPos);
        float initialDistance = Mathf.Max(0.001f, Vector3.Distance(startPos, initialTargetPoint));
        float accelTime = 0f;
        float currentArcHeight = Mathf.Max(0f, autoBattleVacuumArcHeight);

        while (target != null && inventory != null && Amount > 0)
        {
            Vector3 targetPoint = target.position;
            if (targetCollider != null)
                targetPoint = targetCollider.ClosestPoint(linearPos);

            accelTime += Time.deltaTime;
            float speed = Mathf.Max(0.1f, autoBattleVacuumSpeed) + Mathf.Max(0f, autoBattleVacuumAcceleration) * accelTime;
            float step = speed * Time.deltaTime;

            linearPos = Vector3.MoveTowards(linearPos, targetPoint, step);

            // Arc lift fades toward the end so pickups "snap" into the player cleanly.
            float remaining = Vector3.Distance(linearPos, targetPoint);
            float progress01 = 1f - Mathf.Clamp01(remaining / initialDistance);
            float arcLift = Mathf.Sin(progress01 * Mathf.PI) * currentArcHeight;
            Vector3 next = linearPos + Vector3.up * arcLift;

            transform.position = next;
            if (_rb)
                _rb.position = next;

            if (HasReachedAutoBattleVacuumPickupRange(targetCollider))
            {
                bool picked = TryPickup(inventory, storage, idleAutoBattleLoot: true);
                if (!picked)
                {
                    // Inventory/storage constraints prevented full pickup; stop vacuuming this drop for now.
                    break;
                }
            }

            yield return null;
        }

        _autoBattleVacuumRoutine = null;
    }

    private bool HasReachedAutoBattleVacuumPickupRange(Collider2D targetCollider)
    {
        if (targetCollider == null)
            return false;

        if (_col != null)
        {
            ColliderDistance2D d = _col.Distance(targetCollider);
            if (d.isOverlapped || d.distance <= autoBattleVacuumTouchEpsilon)
                return true;
        }

        Vector3 closest = targetCollider.ClosestPoint(transform.position);
        return (transform.position - closest).sqrMagnitude <= autoBattleVacuumTouchEpsilon * autoBattleVacuumTouchEpsilon;
    }
}