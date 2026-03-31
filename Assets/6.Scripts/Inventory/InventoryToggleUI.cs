using UnityEngine;

public class InventoryToggleUI : MonoBehaviour
{
    [SerializeField] private MainMenuWindowUI mainMenuWindowUI;
    [SerializeField] private ShopUI shopUI;

    [Header("Merchant Mode")]
    [SerializeField] private GameObject merchantModeBanner;

    private void Awake()
    {
        if (!mainMenuWindowUI)
            mainMenuWindowUI = FindFirstObjectByType<MainMenuWindowUI>(FindObjectsInactive.Include);
        if (!shopUI)
            shopUI = FindFirstObjectByType<ShopUI>(FindObjectsInactive.Include);
        if (!merchantModeBanner)
            merchantModeBanner = FindSceneObjectByName("MerchantModeBanner");

        if (merchantModeBanner)
            merchantModeBanner.SetActive(false);
    }

    public bool IsOpen => mainMenuWindowUI != null && mainMenuWindowUI.IsOpen;

    private void Update()
    {
        if (!merchantModeBanner)
            merchantModeBanner = FindSceneObjectByName("MerchantModeBanner");
        if (!mainMenuWindowUI)
            mainMenuWindowUI = FindFirstObjectByType<MainMenuWindowUI>(FindObjectsInactive.Include);

        if (!merchantModeBanner || !mainMenuWindowUI)
            return;

        if (!shopUI)
            shopUI = FindFirstObjectByType<ShopUI>(FindObjectsInactive.Include);

        if (MerchantClick.MerchantModeOpen && !MerchantClick.IsShopOpen)
            MerchantClick.ForceCloseMerchantMode();

        bool shopOpen = shopUI != null && shopUI.IsOpen;
        bool shouldShow = MerchantClick.MerchantModeOpen && shopOpen && mainMenuWindowUI.IsOpen;
        if (merchantModeBanner.activeSelf != shouldShow)
            merchantModeBanner.SetActive(shouldShow);
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.hideFlags != HideFlags.None)
                continue;
            if (!t.gameObject.scene.IsValid())
                continue;

            if (string.Equals(t.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                return t.gameObject;
        }

        return null;
    }

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

