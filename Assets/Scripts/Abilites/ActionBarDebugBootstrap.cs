using UnityEngine;

public class ActionBarDebugBootstrap : MonoBehaviour
{
    [SerializeField] private ActionBarSlotUI foodSlot;
    [SerializeField] private ActionBarSlotUI potionSlot;
    [SerializeField] private ActionBarSlotUI ability1Slot;
    [SerializeField] private ActionBarSlotUI ability2Slot;

    private void Start()
    {
        if (foodSlot != null)
        {
            foodSlot.Assign(new ActionBarAssignment
            {
                id = "food_bread",
                displayName = "Bread"
            });
        }

        if (potionSlot != null)
        {
            potionSlot.Assign(new ActionBarAssignment
            {
                id = "potion_health_small",
                displayName = "Small Potion"
            });
        }

        if (ability1Slot != null)
        {
            ability1Slot.Assign(new ActionBarAssignment
            {
                id = "ability_slash",
                displayName = "Slash"
            });
        }

        if (ability2Slot != null)
        {
            ability2Slot.Assign(new ActionBarAssignment
            {
                id = "ability_bleed_strike",
                displayName = "Bleed Strike"
            });
        }
    }
}