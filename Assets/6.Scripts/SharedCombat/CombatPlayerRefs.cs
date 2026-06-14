using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Global player references for combat systems (e.g. enemies) without per-instance scene lookups.
/// Refreshes when <see cref="PlayerController"/> is enabled or recreated.
/// </summary>
public static class CombatPlayerRefs
{
    private static Transform _transform;
    private static PlayerController _controller;
    private static PlayerCombatState _combatState;
    private static CurrencyWallet _wallet;
    private static GoldPopupSpawner _goldPopupSpawner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Clear();

    static CombatPlayerRefs()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Clear();

    public static void Register(PlayerController player)
    {
        if (!player)
            return;

        _controller = player;
        _transform = player.transform;
        _combatState = player.GetComponent<PlayerCombatState>();

        _wallet = player.GetComponent<CurrencyWallet>();
        if (!_wallet)
            _wallet = Object.FindFirstObjectByType<CurrencyWallet>(FindObjectsInactive.Include);

        _goldPopupSpawner = Object.FindFirstObjectByType<GoldPopupSpawner>(FindObjectsInactive.Include);
    }

    public static void Unregister(PlayerController player)
    {
        if (_controller != player)
            return;

        Clear();
    }

    private static void Clear()
    {
        _transform = null;
        _controller = null;
        _combatState = null;
        _wallet = null;
        _goldPopupSpawner = null;
    }

    public static Transform Transform => _transform;
    public static PlayerController Controller => _controller;
    public static PlayerCombatState CombatState => _combatState;
    public static CurrencyWallet Wallet => _wallet;
    public static GoldPopupSpawner GoldPopupSpawner => _goldPopupSpawner;

    /// <summary>
    /// Populates enemy-local player refs from the global cache, with a one-time fallback find if unset.
    /// </summary>
    public static void ResolveForEnemy(
        out Transform playerTransform,
        out PlayerController controller,
        out PlayerCombatState combatState,
        out CurrencyWallet wallet,
        out GoldPopupSpawner goldPopup)
    {
        if (!_transform || !_controller)
        {
            PlayerController found =
                Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (found)
                Register(found);
        }

        playerTransform = _transform;
        controller = _controller;
        combatState = _combatState;
        wallet = _wallet;
        goldPopup = _goldPopupSpawner;

        if (!_wallet)
        {
            _wallet = Object.FindFirstObjectByType<CurrencyWallet>(FindObjectsInactive.Include);
            wallet = _wallet;
        }

        if (!_goldPopupSpawner)
        {
            _goldPopupSpawner =
                Object.FindFirstObjectByType<GoldPopupSpawner>(FindObjectsInactive.Include);
            goldPopup = _goldPopupSpawner;
        }
    }
}
