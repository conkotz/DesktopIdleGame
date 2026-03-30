public static class AbilityDragState
{
    public static bool HasDrag { get; private set; }

    public static string AbilityId { get; private set; }

    public static void BeginDrag(string abilityId)
    {
        AbilityId = abilityId;
        HasDrag = !string.IsNullOrWhiteSpace(abilityId);
    }

    public static void EndDrag()
    {
        HasDrag = false;
        AbilityId = null;
    }
}

