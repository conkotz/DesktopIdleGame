using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class PlayerConsumableController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private PlayerController player;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    [SerializeField] private ActionBarUI actionBar;

    // Cooldowns are now stored by shared group, not itemId
    private readonly Dictionary<string, float> cooldownEndTimes = new();

    private const string PotionCooldownGroup = "Potion";
    private const string FoodCooldownGroup = "Food";

    private void Awake()
    {
        if (!inventory) inventory = GetComponent<Inventory>();
        if (!player) player = GetComponent<PlayerController>();
        if (!actionBar) actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
    }

    public bool TryUseItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || inventory == null || player == null)
            return false;

        ItemDefinition def = inventory.GetItemDef(itemId);
        if (!def || !def.IsConsumable)
            return false;

        int totalCount = CountItem(itemId);
        if (totalCount <= 0)
        {
            player.ShowPopup("Item not available.");
            return false;
        }

        string cooldownKey = GetCooldownKey(def);

        bool onCooldown = IsOnCooldown(itemId, out float remaining);
        if (!onCooldown)
            player.ResetConsumableUnusableActivityLogLatchFor(def);

        if (onCooldown)
        {
            string displayName = GetCooldownDisplayName(def);
            player.ShowPopup($"{displayName} on cooldown ({remaining:0.#}s)");
            return false;
        }

        ApplyConsumable(def);

        if (def.ConsumeOnUse)
        {
            bool removed = RemoveOne(itemId);
            if (!removed)
            {
                player.ShowPopup("Could not consume item.");
                return false;
            }
        }

        if (def.UseCooldown > 0f && !string.IsNullOrWhiteSpace(cooldownKey))
            cooldownEndTimes[cooldownKey] = Time.time + def.UseCooldown;

        player.ResetConsumableUnusableActivityLogLatchFor(def);

        if (debugLogs)
            Debug.Log($"[Consumable] Used {def.displayName} (Cooldown Group: {cooldownKey})");

        return true;
    }

    public bool IsOnCooldown(string itemId, out float remaining)
    {
        remaining = 0f;

        if (string.IsNullOrWhiteSpace(itemId) || inventory == null)
            return false;

        ItemDefinition def = inventory.GetItemDef(itemId);
        if (!def || !def.IsConsumable)
            return false;

        string cooldownKey = GetCooldownKey(def);
        if (string.IsNullOrWhiteSpace(cooldownKey))
            return false;

        if (!cooldownEndTimes.TryGetValue(cooldownKey, out float endTime))
            return false;

        remaining = Mathf.Max(0f, endTime - Time.time);
        return remaining > 0f;
    }

    public float GetCooldownNormalized(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || inventory == null)
            return 0f;

        ItemDefinition def = inventory.GetItemDef(itemId);
        if (!def || !def.IsConsumable || def.UseCooldown <= 0f)
            return 0f;

        string cooldownKey = GetCooldownKey(def);
        if (string.IsNullOrWhiteSpace(cooldownKey))
            return 0f;

        if (!cooldownEndTimes.TryGetValue(cooldownKey, out float endTime))
            return 0f;

        float remaining = Mathf.Max(0f, endTime - Time.time);
        return remaining / def.UseCooldown;
    }

    public int CountItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return 0;

        if (!actionBar)
            actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        if (actionBar != null)
            return actionBar.CountSlottedItem(itemId);

        if (inventory == null)
            return 0;

        int total = 0;

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            var slot = inventory.GetSlot(i);
            if (!slot.IsEmpty && slot.itemId == itemId)
                total += slot.amount;
        }

        return total;
    }

    private void ApplyConsumable(ItemDefinition def)
    {
        if (def.HealAmount > 0)
            player.Heal(def.HealAmount);

        if (def.EnergyAmount > 0)
            player.AddEnergy(def.EnergyAmount);

        if (def.HasGrantedEffect)
            ApplyGrantedEffect(def);

        if (def.HasFoodTimedBuffs)
            ApplyFoodTimedBuffs(def);

        player.ShowPopup($"Used {def.displayName}");
    }

    private void ApplyFoodTimedBuffs(ItemDefinition def)
    {
        var buffController = GetComponent<PlayerBuffController>();
        if (!buffController)
        {
            Debug.LogWarning("No PlayerBuffController found for food timed buffs.");
            return;
        }

        ConsumableStats cs = def.consumableStats;
        float duration = Mathf.Max(0.01f, cs.foodEffectDurationSeconds);
        string itemId = def.itemId;

        if (cs.foodEnableOverheal && cs.foodOverhealMaxAboveMaxHp > 0)
        {
            var overheal = new ConsumableGrantedEffect
            {
                effectType = ConsumableEffectType.FoodOverheal,
                magnitude = cs.foodOverhealMaxAboveMaxHp,
                duration = duration,
                effectId = itemId
            };
            buffController.ApplyBuff(overheal);
        }

        if (cs.foodEnableRegen && cs.foodRegenTotalHeal > 0)
        {
            var hot = new ConsumableGrantedEffect
            {
                effectType = ConsumableEffectType.FoodHealOverTime,
                magnitude = cs.foodRegenTotalHeal,
                duration = duration,
                effectId = itemId
            };
            buffController.ApplyBuff(hot);
        }

        if (cs.foodEnableSwiftness && cs.foodSwiftnessPercentBonus > 0f)
        {
            var swift = new ConsumableGrantedEffect
            {
                effectType = ConsumableEffectType.FoodMoveSpeed,
                magnitude = cs.foodSwiftnessPercentBonus / 100f,
                duration = duration,
                effectId = itemId
            };
            buffController.ApplyBuff(swift);
        }

        if (cs.foodEnableFocused)
        {
            float mag = cs.foodFocusedDamageBonusFraction > 0f ? cs.foodFocusedDamageBonusFraction : 0.15f;
            var focused = new ConsumableGrantedEffect
            {
                effectType = ConsumableEffectType.FoodFocused,
                magnitude = mag,
                duration = duration,
                effectId = itemId
            };
            buffController.ApplyBuff(focused);
        }

        if (cs.foodEnableOverheal && cs.foodOverhealInstantHeal > 0)
            player.Heal(cs.foodOverhealInstantHeal);
    }

    private void ApplyGrantedEffect(ItemDefinition def)
    {
        var effect = def.GrantedEffect;

        var buffController = GetComponent<PlayerBuffController>();
        if (!buffController)
        {
            Debug.LogWarning("No PlayerBuffController found!");
            return;
        }

        effect.effectId = def.itemId; // 👈 THIS IS THE KEY FIX
        buffController.ApplyBuff(effect);

        player.ShowPopup(GetEffectPopupText(def.displayName, effect));
    }

    private string GetEffectPopupText(string itemName, ConsumableGrantedEffect effect)
    {
        string durationText = effect.duration > 0f ? $" for {effect.duration:0.#}s" : "";

        return effect.effectType switch
        {
            ConsumableEffectType.CleansePoison => $"{itemName}: Cleansed Poison",
            ConsumableEffectType.CleanseBleed => $"{itemName}: Cleansed Bleed",
            ConsumableEffectType.CleanseAllAilments => $"{itemName}: Cleansed Ailments",
            ConsumableEffectType.PoisonImmunity => $"{itemName}: Poison Immunity{durationText}",
            ConsumableEffectType.BleedImmunity => $"{itemName}: Bleed Immunity{durationText}",
            ConsumableEffectType.AbilityDamageBoost => $"{itemName}: Ability Damage Up{durationText}",
            ConsumableEffectType.PhysicalDamageBoost => $"{itemName}: Physical Damage Up{durationText}",
            ConsumableEffectType.MagicDamageBoost => $"{itemName}: Magic Damage Up{durationText}",
            ConsumableEffectType.AttackSpeed => $"{itemName}: Attack Speed Up{durationText}",
            ConsumableEffectType.MoveSpeed => $"{itemName}: Move Speed Up{durationText}",
            ConsumableEffectType.DefenseBoost => $"{itemName}: Defence Up{durationText}",
            ConsumableEffectType.ArmorBoost => $"{itemName}: Armour Up{durationText}",
            ConsumableEffectType.MagicResistBoost => $"{itemName}: Magic Resist Up{durationText}",
            ConsumableEffectType.DamageReduction => $"{itemName}: Damage Reduction{durationText}",
            ConsumableEffectType.EnergyRegen => $"{itemName}: Energy Regen Up{durationText}",
            ConsumableEffectType.HealOverTime => $"{itemName}: Regeneration{durationText}",
            ConsumableEffectType.ManaRegenOverTime => $"{itemName}: Mana Regeneration{durationText}",
            ConsumableEffectType.FoodHealOverTime => $"{itemName}: Food Regeneration{durationText}",
            ConsumableEffectType.FoodMoveSpeed => $"{itemName}: Swiftness{durationText}",
            ConsumableEffectType.FoodOverheal => $"{itemName}: Overheal{durationText}",
            ConsumableEffectType.FoodFocused => $"{itemName}: Focused{durationText}",
            _ => $"{itemName}: {effect.effectType}{durationText}"
        };
    }

    private string GetCooldownKey(ItemDefinition def)
    {
        if (def == null || !def.IsConsumable)
            return null;

        // Potions share one cooldown
        if (def.IsPotion)
            return PotionCooldownGroup;

        // Food can share its own cooldown group
        if (def.IsFood)
            return FoodCooldownGroup;

        // Fallback for other consumables
        return def.itemId;
    }

    private string GetCooldownDisplayName(ItemDefinition def)
    {
        if (def == null)
            return "Consumable";

        if (def.IsPotion)
            return "Potions";

        if (def.IsFood)
            return "Food";

        return def.displayName;
    }

    private bool RemoveOne(string itemId)
    {
        if (!actionBar)
            actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        if (actionBar != null)
            return actionBar.TryConsumeSlottedItem(itemId, 1);

        if (inventory == null) return false;

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            var slot = inventory.GetSlot(i);
            if (!slot.IsEmpty && slot.itemId == itemId)
                return inventory.RemoveAmountAtSlot(i, 1) == 1;
        }

        return false;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleCombatSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleCombatSceneLoaded;
    }

    private void HandleCombatSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
    }
}