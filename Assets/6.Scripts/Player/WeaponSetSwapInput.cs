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

        if (Input.GetKeyDown(KeyCode.Tab))
            equipment.ToggleWeaponSet();
    }
}