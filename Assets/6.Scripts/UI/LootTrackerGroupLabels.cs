using System;

public static class LootTrackerGroupLabels
{
    public const string BasicEnhancementScroll = "Basic Enhancement Scroll";
    public const string IntermediateEnhancementScroll = "Intermediate Enhancement Scroll";
    public const string AdvancedEnhancementScroll = "Advanced Enhancement Scroll";
    public const string ChaosEnhancementScroll = "Chaos Enhancement Scroll";
    public const string MapEnhancement = "Map Enhancement";

    public static bool TryGetGroupLabel(ItemDefinition def, out string label) =>
        TryGetGroupLabel(def != null ? def.itemId : null, def, out label);

    public static bool TryGetGroupLabel(string itemId, ItemDefinition def, out string label)
    {
        label = null;
        if (def == null && !string.IsNullOrWhiteSpace(itemId) && MapEnhancementRegistry.IsRuntimeItem(itemId))
        {
            def = MapEnhancementRegistry.TryGetRuntimeDefinition(itemId);
        }

        if (def == null)
            return false;

        if (def.IsEnhancementScroll)
        {
            EnhancementOptionEntry option = EnhancementOptionResolver.GetOptionForScroll(def);
            if (option == null || option.track == EnhancementTrack.Special)
                return false;

            if (option.track == EnhancementTrack.Corruption)
            {
                label = ChaosEnhancementScroll;
                return true;
            }

            label = option.tier switch
            {
                EnhancementTier.Basic => BasicEnhancementScroll,
                EnhancementTier.Intermediate => IntermediateEnhancementScroll,
                EnhancementTier.Advanced => AdvancedEnhancementScroll,
                _ => null,
            };
            return !string.IsNullOrWhiteSpace(label);
        }

        if (def.IsMapEnhancement || MapEnhancementRegistry.IsRuntimeItem(def.itemId))
        {
            label = MapEnhancement;
            return true;
        }

        return false;
    }

    public static int GetSortOrder(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return 99;

        if (string.Equals(label, MapEnhancement, StringComparison.OrdinalIgnoreCase))
            return 0;
        if (string.Equals(label, BasicEnhancementScroll, StringComparison.OrdinalIgnoreCase))
            return 1;
        if (string.Equals(label, IntermediateEnhancementScroll, StringComparison.OrdinalIgnoreCase))
            return 2;
        if (string.Equals(label, AdvancedEnhancementScroll, StringComparison.OrdinalIgnoreCase))
            return 3;
        if (string.Equals(label, ChaosEnhancementScroll, StringComparison.OrdinalIgnoreCase))
            return 4;

        return 50;
    }
}
