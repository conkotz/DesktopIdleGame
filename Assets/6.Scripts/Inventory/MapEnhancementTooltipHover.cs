using System;

/// <summary>Map scaling popup enhancement slots refresh their tooltip when Alt toggles.</summary>
public static class MapEnhancementTooltipHover
{
    private static Action _refreshCallback;

    public static void RegisterRefresh(Action refresh)
    {
        _refreshCallback = refresh;
    }

    public static void UnregisterRefresh(Action refresh)
    {
        if (_refreshCallback == refresh)
            _refreshCallback = null;
    }

    public static void NotifyAltChanged()
    {
        _refreshCallback?.Invoke();
    }
}
