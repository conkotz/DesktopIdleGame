using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Drag / double-click an ability icon onto the action bar (details panel, etc.).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class AbilityIconDragAssignUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [SerializeField] private Vector2 dragIconSize = new(48f, 48f);

    private AbilityDefinition _def;
    private bool _assignEnabled;
    private Canvas _rootCanvas;
    private Image _iconImage;

    private GameObject _dragIconGo;
    private RectTransform _dragIconRt;
    private Image _dragIconImage;

    public void Bind(AbilityDefinition def, bool assignEnabled)
    {
        _def = def;
        _assignEnabled = assignEnabled && def != null;

        EnsureIconImage();
        if (_iconImage != null)
            _iconImage.raycastTarget = _assignEnabled;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_assignEnabled || _def == null)
            return;
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        if (eventData.clickCount < 2)
            return;

        ActionBarUI bar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        if (bar == null)
            return;

        if (ActionBarUI.IsGatheringSkillType(_def.sourceSkill))
            bar.ShowGatheringBarForSkill(_def.sourceSkill, GatheringBarDriveKind.SkillsMenuSelection);

        bar.TryAssignAbilityToFirstEmptySlot(_def);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!_assignEnabled || _def == null)
            return;

        _rootCanvas ??= GetComponentInParent<Canvas>();
        if (_rootCanvas == null)
            _rootCanvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (_rootCanvas == null)
            return;

        string dragName = SkillsAbilityPresentationResolver.ResolveAbilityDisplayName(_def);
        string dragDesc = ResolveDragDescription(_def);
        AbilityDragState.BeginDrag(
            _def.abilityId,
            SkillsAbilityPresentationResolver.ResolveAbilityIcon(_def),
            dragName,
            dragDesc);
        CreateDragIcon();
        UpdateDragIconPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragIconRt == null)
            return;

        UpdateDragIconPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DestroyDragIcon();

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            StartCoroutine(CoDeferredEndAbilityDrag());
            return;
        }

        AbilityDragState.EndDrag();
    }

    private static IEnumerator CoDeferredEndAbilityDrag()
    {
        yield return null;
        AbilityDragState.EndDrag();
    }

    private void CreateDragIcon()
    {
        DestroyDragIcon();

        _dragIconGo = new GameObject("AbilityDragIcon");
        _dragIconGo.transform.SetParent(_rootCanvas.transform, false);

        _dragIconRt = _dragIconGo.AddComponent<RectTransform>();
        _dragIconImage = _dragIconGo.AddComponent<Image>();
        _dragIconImage.raycastTarget = false;
        _dragIconImage.sprite = _def != null ? SkillsAbilityPresentationResolver.ResolveAbilityIcon(_def) : null;
        _dragIconImage.preserveAspect = true;
        _dragIconRt.sizeDelta = dragIconSize;
    }

    private void DestroyDragIcon()
    {
        if (_dragIconGo != null)
            Destroy(_dragIconGo);

        _dragIconGo = null;
        _dragIconRt = null;
        _dragIconImage = null;
    }

    private void UpdateDragIconPosition(PointerEventData eventData)
    {
        if (_dragIconRt == null || _rootCanvas == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rootCanvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint);

        _dragIconRt.anchoredPosition = localPoint;
    }

    private void EnsureIconImage()
    {
        if (_iconImage == null)
            _iconImage = GetComponent<Image>();
    }

    private static string ResolveDragDescription(AbilityDefinition def)
    {
        if (!def)
            return "Ability";

        string intro = SkillsAbilityPresentationResolver.ResolveAbilityLeagueIntroParagraph(def);
        if (!string.IsNullOrWhiteSpace(intro) && intro != "No description.")
            return intro;

        string fallback = SkillsAbilityPresentationResolver.ResolveAbilityPrimaryDescription(def);
        return string.IsNullOrWhiteSpace(fallback) || fallback == "No description." ? "Ability" : fallback.Trim();
    }
}
