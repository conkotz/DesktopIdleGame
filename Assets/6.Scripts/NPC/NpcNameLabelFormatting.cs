using TMPro;
using UnityEngine;

/// <summary>
/// Shared world nameplate text for <see cref="NpcIdentity"/> / <see cref="Merchant"/> labels.
/// </summary>
public static class NpcNameLabelFormatting
{
    public static string Build(string characterName, string role)
    {
        string person = string.IsNullOrWhiteSpace(characterName) ? "" : characterName.Trim();
        string r = string.IsNullOrWhiteSpace(role) ? "" : role.Trim();

        if (string.IsNullOrWhiteSpace(person))
            return string.IsNullOrWhiteSpace(r)
                ? ""
                : $"<size=85%>{EscapeRichText(r)}</size>";

        if (string.IsNullOrWhiteSpace(r))
            return $"<b>{EscapeRichText(person)}</b>";

        return $"<b>{EscapeRichText(person)}</b>\n<size=85%>{EscapeRichText(r)}</size>";
    }

    public static void Apply(TMP_Text label, string characterName, string role)
    {
        if (!label)
            return;

        label.fontStyle = FontStyles.Normal;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.alignment = TextAlignmentOptions.Center;
        label.richText = true;
        label.text = Build(characterName, role);
        WorldNameLabelStyle.Apply(label);
    }

    private static string EscapeRichText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}
