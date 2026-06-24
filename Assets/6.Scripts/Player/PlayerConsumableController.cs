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

        int totalCount = CountItemIncludingInventory(itemId);
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

        float effectiveCooldown = GetEffectiveUseCooldown(def);
        if (effectiveCooldown > 0f && !string.IsNullOrWhiteSpace(cooldownKey))
            cooldownEndTimes[cooldownKey] = Time.time + effectiveCooldown;

        player.ResetConsumableUnusableActivityLogLatchFor(def);

        if (debugLogs)
            Debug.Log($"[Consumable] Used {def.displayName} (Cooldown Group: {cooldownKey})");

        return true;
    }

    /// <summary>Uses a consumable directly from a specific inventory slot (right-click Eat).</summary>
    public bool TryUseFromInventorySlot(int slotIndex)
    {
        if (inventory == null || player == null || slotIndex < 0 || slotIndex >= inventory.SlotCount)
            return false;

        var slot = inventory.GetSlot(slotIndex);
        if (slot.IsEmpty || string.IsNullOrWhiteSpace(slot.itemId))
            return false;

        ItemDefinition def = inventory.GetItemDef(slot.itemId);
        if (!def || !def.IsConsumable)
            return false;

        string cooldownKey = GetCooldownKey(def);

        bool onCooldown = IsOnCooldown(slot.itemId, out float remaining);
        if (!onCooldown)
            player.ResetConsumableUnusableActivityLogLatchFor(def);

        if (onCooldown)
        {
            player.ShowPopup($"{GetCooldownDisplayName(def)} on cooldown ({remaining:0.#}s)");
            return false;
        }

        ApplyConsumable(def);

        if (def.ConsumeOnUse && inventory.RemoveAmountAtSlot(slotIndex, 1) != 1)
        {
            player.ShowPopup("Could not consume item.");
            return false;
        }

        float effectiveCooldown = GetEffectiveUseCooldown(def);
        if (effectiveCooldown > 0f && !string.IsNullOrWhiteSpace(cooldownKey))
            cooldownEndTimes[cooldownKey] = Time.time + effectiveCooldown;

        player.ResetConsumableUnusableActivityLogLatchFor(def);
        return true;
    }

    private int CountItemIncludingInventory(string itemId)
    {
        int total = 0;

        if (!actionBar)
            actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        if (actionBar != null)
            total += actionBar.CountSlottedItem(itemId);

        if (inventory == null)
            return total;

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            var slot = inventory.GetSlot(i);
            if (!slot.IsEmpty && slot.itemId == itemId)
                total += slot.amount;
        }

        return total;
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

        float effectiveCooldown = GetEffectiveUseCooldown(def);
        if (effectiveCooldown <= 0f)
            return 0f;

        float remaining = Mathf.Max(0f, endTime - Time.time);
        return remaining / effectiveCooldown;
    }

    private float GetEffectiveUseCooldown(ItemDefinition def)
    {
        CharacterStats stats = player != null ? player.GetComponent<CharacterStats>() : null;
        return ConsumablePassiveModifiers.GetEffectiveUseCooldown(def, stats);
    }

    private CharacterStats ResolvePlayerStats() =>
        player != null ? player.GetComponent<CharacterStats>() : null;

    public int CountItem(string itemId) => CountItemIncludingInventory(itemId);

    private void ApplyConsumable(ItemDefinition def)
    {
        CharacterStats stats = ResolvePlayerStats();
        PlayerBuffController buffController = GetComponent<PlayerBuffController>();
        bool batchBuffs = buffController != null &&
                          (def.HasGrantedEffect || def.HasFoodTimedBuffs ||
                           (def.IsFood && ConsumablePassiveModifiers.GetEffectiveFoodOverhealCapFlat(def, stats) > 0));
        if (batchBuffs)
            buffController.BeginBuffBatch();

        try
        {
            if (def.IsFood)
                ApplyFoodOverhealBuffBeforeHeal(def, stats, buffController);

            int healAmount = ConsumablePassiveModifiers.GetEffectiveHealAmount(def, stats);
            if (healAmount > 0)
                player.Heal(healAmount, ResolveHealingSourceLabel(def));

            if (def.EnergyAmount > 0)
                player.AddEnergy(def.EnergyAmount);

            if (def.HasGrantedEffect)
                ApplyGrantedEffect(def, stats);

            if (def.HasFoodTimedBuffs)
                ApplyFoodTimedBuffs(def, stats);
        }
        finally
        {
            if (batchBuffs)
                buffController.EndBuffBatch();
        }

        player.ShowPopup($"Used {def.displayName}");
    }

    private static void ApplyFoodOverhealBuffBeforeHeal(
        ItemDefinition def,
        CharacterStats stats,
        PlayerBuffController buffController)
    {
        if (def == null || !def.IsFood || buffController == null)
            return;

        int overhealCap = ConsumablePassiveModifiers.GetEffectiveFoodOverhealCapFlat(def, stats);
        if (overhealCap <= 0)
            return;

        ConsumableStats cs = def.consumableStats;
        if (!cs.foodEnableOverheal && !ConsumablePassiveModifiers.IsAlchemistsBoonActive(stats))
            return;

        float duration = ConsumablePassiveModifiers.GetEffectiveFoodEffectDuration(def, stats);
        if (duration <= 0.001f && ConsumablePassiveModifiers.IsAlchemistsBoonActive(stats))
            duration = AbilityCombatPower.AlchemistsBoonDefaultFoodBuffDurationSeconds;
        if (duration <= 0.001f)
            return;

        var overheal = new ConsumableGrantedEffect
        {
            effectType = ConsumableEffectType.FoodOverheal,
            magnitude = overhealCap,
            duration = Mathf.Max(0.01f, duration),
            effectId = def.itemId
        };
        buffController.ApplyBuff(overheal);
    }

    private void ApplyFoodTimedBuffs(ItemDefinition def, CharacterStats stats)
    {
        var buffController = GetComponent<PlayerBuffController>();
        if (!buffController)
        {
            Debug.LogWarning("No PlayerBuffController found for food timed buffs.");
            return;
        }

        ConsumableStats cs = def.consumableStats;
        float duration = ConsumablePassiveModifiers.GetEffectiveFoodEffectDuration(def, stats);
        duration = Mathf.Max(0.01f, duration);
        string itemId = def.itemId;
        int regenTotal = ConsumablePassiveModifiers.GetEffectiveFoodRegenTotal(def, stats);

        if (cs.foodEnableRegen && regenTotal > 0)
        {
            var hot = new ConsumableGrantedEffect
            {
                effectType = ConsumableEffectType.FoodHealOverTime,
                magnitude = regenTotal,
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
        {
            player.Heal(
                ConsumablePassiveModifiers.ScaleFoodHealAmount(cs.foodOverhealInstantHeal, stats),
                PlayerCombatController.FoodHealingSourceLabel);
        }
    }

    private static string ResolveHealingSourceLabel(ItemDefinition def)
    {
        if (def == null)
            return PlayerCombatController.GenericHealingSourceLabel;
        if (def.IsFood)
            return PlayerCombatController.FoodHealingSourceLabel;
        if (def.IsPotion)
            return PlayerCombatController.PotionHealingSourceLabel;
        return PlayerCombatController.GenericHealingSourceLabel;
    }

    private void ApplyGrantedEffect(ItemDefinition def, CharacterStats stats)
    {
        ConsumableGrantedEffect effect = ConsumablePassiveModifiers.GetEffectiveGrantedEffect(def, stats);

        var buffController = GetComponent<PlayerBuffController>();
        if (!buffController)
        {
            Debug.LogWarning("No PlayerBuffController found!");
            return;
        }

        effect.effectId = def.itemId;
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