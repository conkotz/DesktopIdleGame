using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Warms main-menu tab content during initial gameplay load so the first open does not hitch.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuUIPrewarm : MonoBehaviour
{
    public static bool UseBatchedInstantiation { get; set; }
    public static bool IsComplete { get; private set; }
    public static bool IsRunning { get; private set; }

    private static bool s_registeredSceneHook;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsComplete = false;
        IsRunning = false;
        HorizontalSkillTreeScaffoldUI.ResetSkillTimelinePrewarmState();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        RegisterSceneHook();
        TryStartForScene(SceneManager.GetActiveScene());
    }

    private static void RegisterSceneHook()
    {
        if (s_registeredSceneHook)
            return;

        s_registeredSceneHook = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsGameplayScene(scene))
            IsComplete = false;

        TryStartForScene(scene);
    }

    private static void TryStartForScene(Scene scene)
    {
        if (!IsGameplayScene(scene))
            return;

        if (IsLoadPrewarmReady())
        {
            IsComplete = true;
            return;
        }

        if (IsRunning)
            return;

        IsComplete = false;

        var host = new GameObject(nameof(MainMenuUIPrewarm));
        host.AddComponent<MainMenuUIPrewarm>();
    }

    private static bool IsGameplayScene(Scene scene) =>
        scene.IsValid() && scene.name.Equals("GamePlay", System.StringComparison.OrdinalIgnoreCase);

    public static bool AreHeavyPagesPrewarmed()
    {
        MainMenuWindowUI menu = MainMenuWindowUI.Resolve();
        return menu != null && menu.AreHeavyPagesPrewarmed();
    }

    /// <summary>Menu pages plus town storage/shops when the active map is a town.</summary>
    public static bool IsLoadPrewarmReady()
    {
        if (!AreHeavyPagesPrewarmed())
            return false;

        if (!SkillsAbilityTimelinePrewarm.IsComplete)
            return false;

        if (GameplayLoadDisplayNames.IsActiveTownMap() && !AreTownServicesPrewarmed())
            return false;

        return true;
    }

    public static bool AreTownServicesPrewarmed()
    {
        StorageUI[] storageUis =
            Object.FindObjectsByType<StorageUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < storageUis.Length; i++)
        {
            StorageUI storageUi = storageUis[i];
            if (storageUi && !storageUi.IsGridPrewarmedForLoad())
                return false;
        }

        ShopUI[] shops = Object.FindObjectsByType<ShopUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < shops.Length; i++)
        {
            ShopUI shop = shops[i];
            if (shop && !shop.IsDisplayPrewarmedForLoad())
                return false;
        }

        return true;
    }

    /// <summary>Waits for menu (and town storage) prewarm while the load screen is black.</summary>
    public static IEnumerator CoWaitUntilComplete(float timeoutSeconds = 45f)
    {
        if (IsComplete || IsLoadPrewarmReady())
        {
            IsComplete = true;
            yield break;
        }

        float start = Time.unscaledTime;
        while (!IsComplete && Time.unscaledTime - start < timeoutSeconds)
        {
            if (IsLoadPrewarmReady())
            {
                IsComplete = true;
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator Start()
    {
        IsComplete = false;
        IsRunning = true;

        yield return null;

        yield return CoWaitForGameplayAndSaveReady();

        MainMenuWindowUI menu = MainMenuWindowUI.Resolve();
        if (menu != null && !AreHeavyPagesPrewarmed())
            yield return menu.CoPrewarmHeavyPages();

        if (!SkillsAbilityTimelinePrewarm.IsComplete)
            yield return SkillsAbilityTimelinePrewarm.CoPrewarmAllSkillTimelines();

        if (GameplayLoadDisplayNames.IsActiveTownMap() && !AreTownServicesPrewarmed())
        {
            yield return CoPrewarmTownStorage();
            yield return GameplayShopPrewarm.CoPrewarmAllShops();

            for (int i = 0; i < 5; i++)
                yield return null;
        }

        IsComplete = true;
        IsRunning = false;
        Destroy(gameObject);
    }

    private static IEnumerator CoPrewarmTownStorage()
    {
        StorageUI[] storageUis =
            FindObjectsByType<StorageUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < storageUis.Length; i++)
        {
            StorageUI storageUi = storageUis[i];
            if (!storageUi)
                continue;

            yield return storageUi.CoPrewarmForLoad();
        }
    }

    private static IEnumerator CoWaitForGameplayAndSaveReady()
    {
        const float timeoutSeconds = 20f;
        float start = Time.unscaledTime;

        while (Time.unscaledTime - start < timeoutSeconds)
        {
            SaveManager save = SaveManager.Instance;
            Inventory inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
            MainMenuWindowUI menu = MainMenuWindowUI.Resolve();

            if (menu != null && inventory != null && (save == null || save.IsGameFullyLoaded))
                break;

            yield return null;
        }

        int lastSlotCount = -1;
        int stableFrames = 0;
        while (stableFrames < 3 && Time.unscaledTime - start < timeoutSeconds)
        {
            Inventory inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
            int slotCount = inventory != null ? inventory.SlotCount : 0;

            if (slotCount > 0 && slotCount == lastSlotCount)
                stableFrames++;
            else
            {
                stableFrames = 0;
                lastSlotCount = slotCount;
            }

            yield return null;
        }

        yield return null;
    }
}
