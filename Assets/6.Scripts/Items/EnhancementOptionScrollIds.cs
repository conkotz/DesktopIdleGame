using System;

public static class EnhancementOptionScrollIds
{
    public static string TierScrollId(string basicScrollId, EnhancementTier tier)
    {
        if (string.IsNullOrWhiteSpace(basicScrollId))
            return null;

        return tier switch
        {
            EnhancementTier.Basic => basicScrollId.Trim(),
            EnhancementTier.Intermediate => ReplaceBasicPrefix(basicScrollId, "intermediate_"),
            EnhancementTier.Advanced => ReplaceBasicPrefix(basicScrollId, "advanced_"),
            _ => basicScrollId.Trim(),
        };
    }

    private static string ReplaceBasicPrefix(string basicScrollId, string replacementPrefix)
    {
        string id = basicScrollId.Trim();
        const string basicPrefix = "basic_";
        if (id.StartsWith(basicPrefix, StringComparison.OrdinalIgnoreCase))
            return replacementPrefix + id.Substring(basicPrefix.Length);

        return $"{replacementPrefix}{id}";
    }
}
