using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class FurnaceClick : MonoBehaviour
{
    [SerializeField] private FurnaceSmelter smelter;
    [SerializeField] private PlayerController player;
    [SerializeField] private Collider2D furnaceCollider;
    [SerializeField] private float openWhenWithinXDistance = 0.15f;
    [SerializeField] private float closeWhenBeyondDistance = 5f;

    private static FurnaceClick _active;
    private static FurnaceClick _pendingOpen;
    private Coroutine _openWhenArrivedRoutine;

    public static FurnaceClick PendingOpen => _pendingOpen;
    public static bool IsFurnaceOpen => _active != null && FurnaceUI.IsOpen;

    public bool IsEngagedWithPlayer() => _active == this && FurnaceUI.IsOpen;

    private void Awake()
    {
        CacheRefs();
        if (!GetComponent<Collider2D>())
            Debug.LogError("[FurnaceClick] Missing Collider2D.", this);
    }

    private void CacheRefs()
    {
        if (!smelter)
            smelter = GetComponent<FurnaceSmelter>();
        if (!player)
            player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (!furnaceCollider)
            furnaceCollider = GetComponent<Collider2D>() ?? GetComponentInChildren<Collider2D>(true);
    }

    private void OnDisable()
    {
        if (_active == this)
        {
            if (FurnaceUI.IsOpen && FurnaceUI.Instance != null)
                FurnaceUI.Instance.Close();
            _active = null;
        }

        if (_pendingOpen == this)
            CancelPendingOpen();
    }

    private void LateUpdate()
    {
        if (_active != this || !FurnaceUI.IsOpen)
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

        Vector2 furnacePos = furnaceCollider != null
            ? furnaceCollider.bounds.center
            : transform.position;
        return Vector2.Distance(player.transform.position, furnacePos);
    }

    public void Open()
    {
        CacheRefs();
        if (!smelter)
        {
            Debug.LogError("[FurnaceClick] FurnaceSmelter not found.", this);
            return;
        }

        if (_active == this && FurnaceUI.IsOpen)
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
        StorageClick.ForceCloseStorageMode();
        NPCDialogueBoxUI.DismissAllActive();

        _active = this;
        FurnaceUI.EnsureInstance().Open(smelter, this);
    }

    public static void ForceClose()
    {
        FurnaceUI ui = FurnaceUI.Instance;
        if (ui != null && FurnaceUI.IsOpen)
            ui.Close();

        _active = null;
        CancelPendingOpen();
    }

    public static void CancelPendingOpen()
    {
        if (_pendingOpen == null)
            return;

        FurnaceClick pending = _pendingOpen;
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
            WorldInteractRouter.IsPlayerWithinImmediateInteractRange(p, furnaceCollider))
            return true;

        NPCInteractionSettings npc = GetComponent<NPCInteractionSettings>();
        return npc != null && npc.CanInteractImmediately(p);
    }

    private bool IsPlayerWithinArrivalRange()
    {
        if (player == null)
            return false;

        if (furnaceCollider == null)
        {
            float dx = Mathf.Abs(player.transform.position.x - transform.position.x);
            return dx <= Mathf.Max(0.01f, openWhenWithinXDistance);
        }

        Bounds b = furnaceCollider.bounds;
        float playerX = player.transform.position.x;
        float edgeX = playerX <= b.center.x ? b.min.x : b.max.x;
        float playerHalfWidth = ResolvePlayerColliderHalfWidth(player);
        float gap = Mathf.Abs(playerX - edgeX) - playerHalfWidth;
        return gap <= Mathf.Max(0.01f, openWhenWithinXDistance);
    }

    private float ComputeApproachTargetX(PlayerController p)
    {
        if (furnaceCollider == null)
            return transform.position.x;

        Bounds b = furnaceCollider.bounds;
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
