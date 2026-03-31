using TMPro;
using UnityEngine;

public class GoldUI : MonoBehaviour
{
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private CurrencyWallet wallet;

    private void Awake()
    {
        if (!goldText) goldText = GetComponentInChildren<TMP_Text>(true);
        if (!wallet) wallet = FindFirstObjectByType<CurrencyWallet>(FindObjectsInactive.Include);

        Refresh();

        if (wallet != null)
            wallet.OnGoldChanged += Refresh;
    }

    private void OnDestroy()
    {
        if (wallet != null)
            wallet.OnGoldChanged -= Refresh;
    }

    private void Refresh()
    {
        int value = wallet != null ? wallet.Gold : 0;
        if (goldText) goldText.text = value.ToString("N0");
    }
}