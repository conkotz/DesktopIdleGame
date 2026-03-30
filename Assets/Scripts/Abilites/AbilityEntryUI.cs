using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI row/button for an ability. Supports drag/drop into the action bar.
/// </summary>
public class AbilityEntryUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerClickHandler
{
    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text reqText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Drag")]
    [SerializeField] private Vector2 dragIconSize = new Vector2(48f, 48f);

    private AbilityDefinition _def;
    private bool _unlocked;

    private SharedTooltipUI _tooltip;
    private Canvas _rootCanvas;
    private RectTransform _tooltipBoundsRect;
    private FlipInsideBounds.PreferredSide _preferredSide = FlipInsideBounds.PreferredSide.Left;

    private GameObject _dragIconGO;
    private RectTransform _dragIconRT;
    private Image _dragIconImage;

    // Called by SkillsAbilitiesPageUI when creating runtime rows (no prefab).
    // Uses SendMessage to avoid making fields public.
    private void EditorAutoWire(object[] args)
    {
        if (args == null || args.Length < 4) return;
        icon = args[0] as Image;
        nameText = args[1] as TMP_Text;
        reqText = args[2] as TMP_Text;
        canvasGroup = args[3] as CanvasGroup;
    }

    public void Bind(AbilityDefinition def, bool unlocked, SharedTooltipUI tooltip, Canvas rootCanvas)
    {
        _def = def;
        _unlocked = unlocked;
        _tooltip = tooltip;
        _rootCanvas = rootCanvas;
        if (_rootCanvas == null)
            _rootCanvas = GetComponentInParent<Canvas>();
        if (_rootCanvas == null)
            _rootCanvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);

        if (icon)
        {
            icon.enabled = def != null && def.icon != null;
            icon.sprite = def ? def.icon : null;
            icon.preserveAspect = true;
        }

        if (nameText)
            nameText.text = def ? def.displayName : "—";

        if (reqText)
            reqText.text = def ? $"Lv {def.unlockLevel}" : "";

        if (!canvasGroup)
            canvasGroup = GetComponent<CanvasGroup>();
        if (!canvasGroup)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = unlocked ? 1f : 0.55f;
    }

    public void SetTooltipDocking(RectTransform tooltipBoundsRect, FlipInsideBounds.PreferredSide preferredSide)
    {
        _tooltipBoundsRect = tooltipBoundsRect;
        _preferredSide = preferredSide;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_tooltip == null || _def == null) return;

        string body = BuildLeagueStyleTooltip(_def);
        RectTransform measure = _tooltipBoundsRect ? _tooltipBoundsRect : transform.root as RectTransform;
        _tooltip.ShowTextAt(
            transform,
            _def.displayName,
            body,
            measureRect: measure,
            heightRect: measure,
            preferredSide: _preferredSide
        );
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _tooltip?.Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        // placeholder for future: click-to-equip default slot
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!_unlocked || _def == null) return;
        if (_rootCanvas == null)
            _rootCanvas = GetComponentInParent<Canvas>();
        if (_rootCanvas == null)
            return;

        AbilityDragState.BeginDrag(_def.abilityId);
        CreateDragIcon();
        UpdateDragIconPosition(eventData);
        if (canvasGroup) canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragIconRT == null) return;
        UpdateDragIconPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DestroyDragIcon();
        if (canvasGroup) canvasGroup.blocksRaycasts = true;
        AbilityDragState.EndDrag();
    }

    private void CreateDragIcon()
    {
        DestroyDragIcon();

        _dragIconGO = new GameObject("AbilityDragIcon");
        _dragIconGO.transform.SetParent(_rootCanvas.transform, false);

        _dragIconRT = _dragIconGO.AddComponent<RectTransform>();
        _dragIconImage = _dragIconGO.AddComponent<Image>();
        _dragIconImage.raycastTarget = false;
        _dragIconImage.sprite = _def != null ? _def.icon : null;
        _dragIconImage.preserveAspect = true;

        _dragIconRT.sizeDelta = dragIconSize;
    }

    private void UpdateDragIconPosition(PointerEventData eventData)
    {
        if (_dragIconRT == null || _rootCanvas == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rootCanvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint
        );

        _dragIconRT.anchoredPosition = localPoint;
    }

    private void DestroyDragIcon()
    {
        if (_dragIconGO) Destroy(_dragIconGO);
        _dragIconGO = null;
        _dragIconRT = null;
        _dragIconImage = null;
    }

    private static string BuildLeagueStyleTooltip(AbilityDefinition def)
    {
        if (!def) return "";

        string desc = string.IsNullOrWhiteSpace(def.description) ? "No description." : def.description.Trim();
        string scaling =
            $"Damage Scaling: Physical {(def.physicalDamageMultiplier * 100f):0.#}% + AP {(def.abilityPowerMultiplier * 100f):0.#}%";

        return
            $"{desc}\n\n" +
            $"{scaling}\n" +
            $"Energy Cost: {def.energyCost:0.#}\n" +
            $"Cooldown: {def.cooldown:0.#}s";
    }
}

