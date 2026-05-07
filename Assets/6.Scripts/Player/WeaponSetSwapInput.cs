using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class WeaponSetSwapInput : MonoBehaviour
{
    [SerializeField] private EquipmentManager equipment;
    [SerializeField] private ActionBarUI actionBar;
    private bool _swapInProgress;

    private void Awake()
    {
        if (!equipment)
            equipment = FindFirstObjectByType<EquipmentManager>();
        if (!actionBar)
            actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
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
            if (!_swapInProgress)
                StartCoroutine(CoSwapFullLoadout());
        }
    }

    private IEnumerator CoSwapFullLoadout()
    {
        _swapInProgress = true;
        equipment.ToggleWeaponSet();
        yield return null; // spread swap load across frames

        if (!actionBar)
            actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        actionBar?.SetCombatLoadoutSet(equipment.ActiveWeaponSetIndex);
        _swapInProgress = false;
    }
}