using UnityEngine;

/// <summary>
/// Inspector-only: hides the field unless a sibling enum/int field equals <see cref="EnumValue"/>.
/// </summary>
public sealed class ShowWhenEnumAttribute : PropertyAttribute
{
    public readonly string EnumFieldName;
    public readonly int EnumValue;

    public ShowWhenEnumAttribute(string enumFieldName, int enumValue)
    {
        EnumFieldName = enumFieldName;
        EnumValue = enumValue;
    }
}
