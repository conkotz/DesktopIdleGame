using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Ability icon slot in the database enemy list; shows name and description on hover.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class DatabaseEnemyAbilityEntryUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private static readonly Color DefaultBackgroundColor = new Color(0.5622641f, 0.5309351f, 0.49542892f, 1f);

    private Image _backgroundImage;
    private EnemyAbilityDefinition _ability;
    private SharedTooltipUI _tooltip;

    private void Awake()
    {
        _backgroundImage = GetComponent<Image>();
    }

    public void Bind(EnemyAbilityDefinition ability, SharedTooltipUI tooltip)
    {
        _ability = ability;
        _tooltip = tooltip;

        if (!_backgroundImage)
            _backgroundImage = GetComponent<Image>();

        if (_backgroundImage)
        {
            _backgroundImage.color = DefaultBackgroundColor;
            _backgroundImage.raycastTarget = ability != null;
        }

        Transform icon = transform.Find("AbilityIcon");
        if (icon)
            icon.gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData) => ShowTooltipIfPossible();

    public void OnPointerExit(PointerEventData eventData) => _tooltip?.Hide();

    private void ShowTooltipIfPossible()
    {
        if (_tooltip == null || _ability == null)
            return;

        string title = string.IsNullOrWhiteSpace(_ability.displayName)
            ? _ability.abilityId ?? "Ability"
            : _ability.displayName.Trim();

        string body = _ability.description;
        if (string.IsNullOrWhiteSpace(body))
            body = "No description.";

        _tooltip.ShowTextAt(transform, title, body);
    }
}
