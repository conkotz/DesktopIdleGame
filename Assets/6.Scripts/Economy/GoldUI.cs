using TMPro;
using UnityEngine;

public class GoldUI : MonoBehaviour
{
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private CurrencyWallet wallet;

    private bool _refreshQueued;

    private void Awake()
    {
        if (!goldText) goldText = GetComponentInChildren<TMP_Text>(true);
        if (!wallet) wallet = FindFirstObjectByType<CurrencyWallet>(FindObjectsInactive.Include);

        Refresh();

        if (wallet != null)
            wallet.OnGoldChanged += QueueRefresh;
    }

    private void OnDestroy()
    {
        if (wallet != null)
            wallet.OnGoldChanged -= QueueRefresh;
    }

    private void QueueRefresh()
    {
        if (!isActiveAndEnabled || goldText == null || !goldText.gameObject.activeInHierarchy)
            return;

        _refreshQueued = true;
        InventoryUiRefreshCoordinator.MarkGoldLabelDirty(this);
    }

    internal void FlushCoalescedRefresh()
    {
        if (!_refreshQueued)
            return;

        _refreshQueued = false;
        Refresh();
    }

    private void Refresh()
    {
        int value = wallet != null ? wallet.Gold : 0;
        if (goldText) goldText.text = value.ToString("N0");
    }
}
