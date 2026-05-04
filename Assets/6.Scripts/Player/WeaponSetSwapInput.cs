using UnityEngine;

[DisallowMultipleComponent]
public class WeaponSetSwapInput : MonoBehaviour
{
    [SerializeField] private EquipmentManager equipment;

    private void Awake()
    {
        if (!equipment)
            equipment = FindFirstObjectByType<EquipmentManager>();
    }

    private void Update()
    {
        if (!equipment) return;

        HotkeyBindingManager mgr = HotkeyBindingManager.Instance;
        KeyCode swapKey = mgr != null
            ? mgr.GetBinding(HotkeyBindId.SwapWeaponSet)
            : KeyCode.Tab;

        if (swapKey != KeyCode.None && Input.GetKeyDown(swapKey))
            equipment.ToggleWeaponSet();
    }
}