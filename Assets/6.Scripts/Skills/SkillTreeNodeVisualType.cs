/// <summary>
/// Visual / logical skill tree node categories. Real skill data can map rows to these types per tree.
/// </summary>
public enum SkillTreeNodeVisualType
{
    /// <summary>Small stat bonus nodes.</summary>
    MinorPassive,

    /// <summary>Important passive milestone.</summary>
    MajorPassive,

    /// <summary>Gear / tool / weapon tier unlock.</summary>
    Unlock,

    /// <summary>Ability unlock.</summary>
    Ability,

    /// <summary>Specialization branch tied to an earlier milestone (unlocks at a higher level than that milestone).</summary>
    Choice,

    /// <summary>Final level 50 capstone passive.</summary>
    CapstonePassive,

    /// <summary>Small world unlock note (e.g. new fish). Sized between <see cref="MinorPassive"/> and milestone nodes; not on the vertical spine.</summary>
    MinorUnlock
}