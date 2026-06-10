using UnityEngine;

/// <summary>Polls Alt and refreshes any item tooltip currently under the cursor.</summary>
[DefaultExecutionOrder(200)]
public sealed class ItemTooltipAltKeyWatcher : MonoBehaviour
{
    private static ItemTooltipAltKeyWatcher _instance;
    private bool _lastHeld;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Ensure()
    {
        if (_instance)
            return;

        var go = new GameObject(nameof(ItemTooltipAltKeyWatcher));
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<ItemTooltipAltKeyWatcher>();
    }

    private void Update()
    {
        bool held = ItemTooltipAdvancedInput.IsHeld;
        if (held == _lastHeld)
            return;

        _lastHeld = held;
        ItemTooltipHoverRegistry.RefreshAllHovered();
        MapEnhancementTooltipHover.NotifyAltChanged();
    }
}
