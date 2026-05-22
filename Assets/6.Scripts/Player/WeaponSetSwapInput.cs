using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class WeaponSetSwapInput : MonoBehaviour
{
    [SerializeField] private EquipmentManager equipment;
    [SerializeField] private ActionBarUI actionBar;
    [SerializeField] private CharacterStats characterStats;
    private bool _swapInProgress;

    private void Awake()
    {
        if (!equipment)
            equipment = FindFirstObjectByType<EquipmentManager>();
        if (!actionBar)
            actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        if (!characterStats)
            characterStats = FindFirstObjectByType<CharacterStats>();
    }

    private void Update()
    {
        if (!equipment) return;

        HotkeyBindingManager mgr = HotkeyBindingManager.Instance;
        KeyCode swapKey = mgr != null
            ? mgr.GetBinding(HotkeyBindId.SwapWeaponSet)
            : KeyCode.Tab;

        if (swapKey != KeyCode.None && Input.GetKeyDown(swapKey))
        {
            if (_swapInProgress)
                return;

            if (!equipment.TryToggleWeaponSet())
                return;

            StartCoroutine(CoSwapFullLoadout());
        }
    }

    private IEnumerator CoSwapFullLoadout()
    {
        _swapInProgress = true;
        if (!actionBar)
            actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        actionBar?.ExitGatheringBarToCombat();
        if (characterStats)
            characterStats.NotifyWeaponSetSwapped();
        yield return null; // spread swap load across frames

        if (!actionBar)
            actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        actionBar?.SetCombatLoadoutSet(equipment.ActiveWeaponSetIndex);

        // Gear slot UI events are suppressed during swap; refresh panels after loadout bar catches up.
        characterStats?.NotifyStatsChanged();

        _swapInProgress = false;
    }
}