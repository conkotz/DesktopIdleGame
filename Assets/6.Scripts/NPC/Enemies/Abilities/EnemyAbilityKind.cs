public enum EnemyAbilityKind
{
    /// <summary>Teleports behind the player on a combat timer (shadow-strike style smoke, no damage).</summary>
    ShadowDashBehindPlayer,

    /// <summary>Once per engagement, may leap backward when first struck in melee range.</summary>
    MeleeDisengage,

    /// <summary>When the player breaks leash range during combat, telegraphs then pounces to a locked ground point.</summary>
    PounceOnRangeBreak,

    /// <summary>Once per engagement below a health threshold, permanently buffs scale, attack speed, and move speed.</summary>
    LowHealthEnrage,

    /// <summary>Chance on auto-attacks to glow green, deal bonus corruption, and guarantee poison.</summary>
    ToxicFangsAttackProc,
}
