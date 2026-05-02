/// <summary>
/// Single formatter for consumable granted-effect lines (inventory tooltip, action bar, etc.).
/// <see cref="ConsumableGrantedEffect.magnitude"/> is a <b>fraction</b> for percent boosts (0.2 = +20%).
/// </summary>
public static class ConsumableEffectTooltip
{
    public static string Format(ConsumableGrantedEffect effect)
    {
        string magPct = $"{effect.magnitude * 100f:0.#}%";
        string dur = $"{effect.duration:0.#}s";

        return effect.effectType switch
        {
            ConsumableEffectType.PhysicalDamageBoost => $"+{magPct} Physical Damage for {dur}",
            ConsumableEffectType.MagicDamageBoost => $"+{magPct} Magic Damage for {dur}",
            ConsumableEffectType.AttackSpeed => $"+{magPct} Attack Speed for {dur}",
            ConsumableEffectType.MoveSpeed => $"+{magPct} Move Speed for {dur}",
            ConsumableEffectType.DefenseBoost => $"+{magPct} Defence for {dur}",
            ConsumableEffectType.EnergyRegen => $"+{effect.magnitude:0.##} Energy Regen for {dur}",
            ConsumableEffectType.EnergyRestore => $"+{effect.magnitude:0.##} Energy",
            ConsumableEffectType.HealOverTime => $"+{effect.magnitude:0.##} HP over {dur}",
            ConsumableEffectType.ManaRegenOverTime => $"+{effect.magnitude:0.##} Mana over {dur}",
            _ => $"{effect.effectType} for {dur}"
        };
    }
}
