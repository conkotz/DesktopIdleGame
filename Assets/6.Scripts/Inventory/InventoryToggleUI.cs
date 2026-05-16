using UnityEngine;
using UnityEngine.UI;

public class InventoryToggleUI : MonoBehaviour
{
    [SerializeField] private MainMenuWindowUI mainMenuWindowUI;
    [SerializeField] private ShopUI shopUI;
    private Button _toolbarButton;

    [Header("Merchant Mode")]
    [SerializeField] private GameObject merchantModeBanner;

    private MainMenuWindowUI GetMenu()
    {
        if (mainMenuWindowUI != null)
            return mainMenuWindowUI;
        return MainMenuWindowUI.Resolve();
    }

    private void Awake()
    {
        if (!mainMenuWindowUI)
            mainMenuWindowUI = MainMenuWindowUI.Resolve();
        if (!shopUI)
            shopUI = FindFirstObjectByType<ShopUI>(FindObjectsInactive.Include);
        if (!merchantModeBanner)
            merchantModeBanner = FindSceneObjectByName("MerchantModeBanner");

        if (merchantModeBanner)
            merchantModeBanner.SetActive(false);

        _toolbarButton = GetComponent<Button>();
    }

    public bool IsOpen
    {
        get
        {
            MainMenuWindowUI menu = GetMenu();
            return menu != null && menu.IsOpen;
        }
    }

    private void Update()
    {
        if (!merchantModeBanner)
            merchantModeBanner = FindSceneObjectByName("MerchantModeBanner");

        MainMenuWindowUI menu = GetMenu();
        if (!merchantModeBanner || menu == null)
            return;

        if (!shopUI)
            shopUI = FindFirstObjectByType<ShopUI>(FindObjectsInactive.Include);

        if (MerchantClick.MerchantModeOpen && !MerchantClick.IsShopOpen)
            MerchantClick.ForceCloseMerchantMode();

        bool shopOpen = shopUI != null && shopUI.IsOpen;
        bool shouldShow = MerchantClick.MerchantModeOpen && shopOpen && menu.IsOpen;
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
        MainMenuWindowUI menu = GetMenu();
        if (menu == null)
        {
            Debug.LogWarning("[InventoryToggleUI] MainMenuWindowUI not assigned and not found in scene.", this);
            return;
        }

        menu.SelectTab(MainMenuTabId.Character);

        bool isNowOpen = menu.IsOpen;

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
        MainMenuWindowUI menu = GetMenu();
        if (menu == null) return;

        menu.SelectTab(MainMenuTabId.Character);

        if (merchantModeBanner && !MerchantClick.MerchantModeOpen)
            merchantModeBanner.SetActive(false);
    }

    public void Close()
    {
        MainMenuWindowUI menu = GetMenu();
        if (menu == null) return;
        if (!menu.IsOpen) return;

        menu.Close();

        MerchantClick.ForceCloseMerchantMode();
        if (merchantModeBanner) merchantModeBanner.SetActive(false);
    }
}

