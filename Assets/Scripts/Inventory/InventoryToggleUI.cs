using UnityEngine;

public class InventoryToggleUI : MonoBehaviour
{
    [SerializeField] private MainMenuWindowUI mainMenuWindowUI;

    [Header("Merchant Mode")]
    [SerializeField] private GameObject merchantModeBanner;

    private void Awake()
    {
        if (!mainMenuWindowUI)
            mainMenuWindowUI = FindFirstObjectByType<MainMenuWindowUI>(FindObjectsInactive.Include);

        if (merchantModeBanner)
            merchantModeBanner.SetActive(false);
    }

    public bool IsOpen => mainMenuWindowUI != null && mainMenuWindowUI.IsOpen;

    public void Toggle()
    {
        if (!mainMenuWindowUI)
        {
            Debug.LogWarning("[InventoryToggleUI] MainMenuWindowUI is not assigned.");
            return;
        }

        mainMenuWindowUI.ToggleCharacter();

        bool isNowOpen = mainMenuWindowUI.IsOpen;

        if (!isNowOpen)
        {
            MerchantClick.ForceCloseMerchantMode();
            if (merchantModeBanner) merchantModeBanner.SetActive(false);
        }
        else
        {
            if (merchantModeBanner && !MerchantClick.MerchantModeOpen)
                merchantModeBanner.SetActive(false);
        }
    }

    public void Open()
    {
        if (!mainMenuWindowUI) return;

        mainMenuWindowUI.OpenCharacter();

        if (merchantModeBanner && !MerchantClick.MerchantModeOpen)
            merchantModeBanner.SetActive(false);
    }

    public void Close()
    {
        if (!mainMenuWindowUI) return;
        if (!mainMenuWindowUI.IsOpen) return;

        mainMenuWindowUI.Close();

        MerchantClick.ForceCloseMerchantMode();
        if (merchantModeBanner) merchantModeBanner.SetActive(false);
    }
}
