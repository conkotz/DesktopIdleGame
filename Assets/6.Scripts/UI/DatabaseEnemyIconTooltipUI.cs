using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Enemy portrait hover in the database enemies list.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class DatabaseEnemyIconTooltipUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private EnemyDefinition _enemy;
    private SharedTooltipUI _tooltip;

    public void Bind(EnemyDefinition enemy, SharedTooltipUI tooltip)
    {
        _enemy = enemy;
        _tooltip = tooltip;

        if (TryGetComponent(out Image image))
            image.raycastTarget = enemy != null;
    }

    public void OnPointerEnter(PointerEventData eventData) => ShowTooltipIfPossible();

    public void OnPointerExit(PointerEventData eventData) => _tooltip?.Hide();

    private void ShowTooltipIfPossible()
    {
        if (_tooltip == null || _enemy == null)
            return;

        if (!DatabaseEnemyStatsTooltipText.TryBuild(_enemy, out string title, out string body))
            return;

        _tooltip.ShowTextAt(
            transform,
            title,
            body,
            useStatsDisplayHeader: true);
    }
}
