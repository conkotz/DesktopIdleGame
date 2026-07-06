using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BlacksmithingClick : MonoBehaviour
{
    [SerializeField] private BlacksmithingStation station;
    [SerializeField] private PlayerController player;
    [SerializeField] private Collider2D rangeCollider;
    [SerializeField] private float openWhenWithinXDistance = 0.15f;
    [SerializeField] private float closeWhenBeyondDistance = 5f;

    private static BlacksmithingClick _active;
    private static BlacksmithingClick _pendingOpen;
    private Coroutine _openWhenArrivedRoutine;

    public static BlacksmithingClick PendingOpen => _pendingOpen;
    public static bool IsBlacksmithingOpen => _active != null && BlacksmithingUI.IsOpen;

    public bool IsEngagedWithPlayer() => _active == this && BlacksmithingUI.IsOpen;

    private void Awake()
    {
        CacheRefs();
        if (!GetComponent<Collider2D>())
            Debug.LogError("[BlacksmithingClick] Missing Collider2D.", this);
    }

    private void CacheRefs()
    {
        if (!station)
            station = GetComponent<BlacksmithingStation>();
        if (!player)
            player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (!rangeCollider)
            rangeCollider = GetComponent<Collider2D>() ?? GetComponentInChildren<Collider2D>(true);
    }

    private void OnDisable()
    {
        if (_active == this)
        {
            if (BlacksmithingUI.IsOpen && BlacksmithingUI.Instance != null)
                BlacksmithingUI.Instance.Close();
            _active = null;
        }

        if (_pendingOpen == this)
            CancelPendingOpen();
    }

    private void LateUpdate()
    {
        if (_active != this || !BlacksmithingUI.IsOpen)
            return;

        CacheRefs();
        if (player == null)
        {
            ForceClose();
            return;
        }

        if (GetPlayerDistance() > Mathf.Max(0.1f, closeWhenBeyondDistance))
            ForceClose();
    }

    private float GetPlayerDistance()
    {
        if (player == null)
            return float.MaxValue;

        Vector2 pos = rangeCollider != null ? rangeCollider.bounds.center : transform.position;
        return Vector2.Distance(player.transform.position, pos);
    }

    public void Open()
    {
        CacheRefs();
        if (!station)
        {
            Debug.LogError("[BlacksmithingClick] BlacksmithingStation not found.", this);
            return;
        }

        if (_active == this && BlacksmithingUI.IsOpen)
            return;

        CancelPendingOpen();

        if (player != null)
        {
            if (ShouldOpenImmediately(player))
            {
                OpenNow();
                return;
            }

            player.MoveToPointX(ComputeApproachTargetX(player));
            _pendingOpen = this;
            _openWhenArrivedRoutine = StartCoroutine(CoOpenWhenArrived());
            return;
        }

        OpenNow();
    }

    private IEnumerator CoOpenWhenArrived()
    {
        while (_pendingOpen == this)
        {
            if (player == null || player.IsDead)
            {
                _pendingOpen = null;
                yield break;
            }

            if (IsPlayerWithinArrivalRange())
            {
                _pendingOpen = null;
                OpenNow();
                yield break;
            }

            yield return null;
        }
    }

    private void OpenNow()
    {
        CacheRefs();
        MerchantClick.ForceCloseMerchantMode();
        FurnaceClick.ForceClose();
        CookingClick.ForceClose();
        StorageClick.ForceCloseStorageMode();

        _active = this;
        BlacksmithingUI.EnsureInstance().Open(station, this);
    }

    public static void ForceClose()
    {
        BlacksmithingUI ui = BlacksmithingUI.Instance;
        if (ui != null && BlacksmithingUI.IsOpen)
            ui.Close();

        _active = null;
        CancelPendingOpen();
    }

    public static void CancelPendingOpen()
    {
        if (_pendingOpen == null)
            return;

        BlacksmithingClick pending = _pendingOpen;
        _pendingOpen = null;
        if (pending != null && pending._openWhenArrivedRoutine != null)
        {
            pending.StopCoroutine(pending._openWhenArrivedRoutine);
            pending._openWhenArrivedRoutine = null;
        }
    }

    internal void NotifyClosed()
    {
        if (_active == this)
            _active = null;
    }

    private bool ShouldOpenImmediately(PlayerController p)
    {
        if (p == null)
            return false;

        if (IsPlayerWithinArrivalRange() ||
            WorldInteractRouter.IsPlayerWithinImmediateInteractRange(p, rangeCollider))
            return true;

        NPCInteractionSettings npc = GetComponent<NPCInteractionSettings>();
        return npc != null && npc.CanInteractImmediately(p);
    }

    private bool IsPlayerWithinArrivalRange()
    {
        if (player == null)
            return false;

        if (rangeCollider == null)
        {
            float dx = Mathf.Abs(player.transform.position.x - transform.position.x);
            return dx <= Mathf.Max(0.01f, openWhenWithinXDistance);
        }

        Bounds b = rangeCollider.bounds;
        float playerX = player.transform.position.x;
        float edgeX = playerX <= b.center.x ? b.min.x : b.max.x;
        float playerHalfWidth = ResolvePlayerColliderHalfWidth(player);
        float gap = Mathf.Abs(playerX - edgeX) - playerHalfWidth;
        return gap <= Mathf.Max(0.01f, openWhenWithinXDistance);
    }

    private float ComputeApproachTargetX(PlayerController p)
    {
        if (rangeCollider == null)
            return transform.position.x;

        Bounds b = rangeCollider.bounds;
        float playerX = p.transform.position.x;
        bool fromLeft = playerX <= b.center.x;
        float edgeX = fromLeft ? b.min.x : b.max.x;
        float sign = fromLeft ? -1f : 1f;
        float playerHalfWidth = ResolvePlayerColliderHalfWidth(p);
        return edgeX + sign * playerHalfWidth;
    }

    private static float ResolvePlayerColliderHalfWidth(PlayerController p)
    {
        if (p == null)
            return 0f;
        Collider2D col = p.GetComponent<Collider2D>() ?? p.GetComponentInChildren<Collider2D>(true);
        return col != null ? col.bounds.extents.x : 0f;
    }
}
