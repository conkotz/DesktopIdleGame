using UnityEngine;

/// <summary>
/// Inspector-only: hides the field unless the named serialized bool on the same component is true.
/// </summary>
public sealed class ShowWhenTrueAttribute : PropertyAttribute
{
    public readonly string ConditionBoolFieldName;

    public ShowWhenTrueAttribute(string conditionBoolFieldName) =>
        ConditionBoolFieldName = conditionBoolFieldName;
}
