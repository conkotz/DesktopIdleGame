using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Drag a committed timeline ability node onto the action bar.</summary>
[DisallowMultipleComponent]
public sealed class SkillTimelineNodeAbilityDragUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [SerializeField] private Vector2 dragIconSize = new(48f, 48f);

    private SkillTimelineNodeUI _node;
    private AbilityDefinition _ability;
    private bool _canDrag;
    private Canvas _rootCanvas;
    private GameObject _dragIconGo;
    private RectTransform _dragIconRt;
    private Image _dragIconImage;

    public void Bind(SkillTimelineNodeUI node)
    {
        _node = node;
        RefreshDragState();
    }

    public void RefreshDragState()
    {
        _ability = null;
        _canDrag = false;

        SkillTimelineNodeBinding binding = _node != null ? _node.Binding : null;
        if (binding == null)
            return;

        bool abilityLike =
            binding.TimelineNodeType == SkillTimelineNodeUI.SkillTimelineNodeType.Ability
            || (binding.TimelineNodeType == SkillTimelineNodeUI.SkillTimelineNodeType.Capstone
                && binding.Unlock?.ability != null);
        if (!abilityLike)
            return;

        _ability = ResolveAbility(binding);
        if (_ability == null)
            return;

        SkillsManager skillsManager = SkillsManager.Instance
            ?? FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);
        _canDrag = SkillAbilityCommitRules.IsAbilityFullyUnlockedForGameplay(
            binding.Skill, _ability, skillsManager);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        RefreshDragState();
        if (!_canDrag || _ability == null)
            return;
        if (eventData.button != PointerEventData.InputButton.Left || eventData.clickCount < 2)
            return;

        ActionBarUI bar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);
        if (bar == null)
            return;

        if (ActionBarUI.IsGatheringSkillType(_ability.sourceSkill))
            bar.ShowGatheringBarForSkill(_ability.sourceSkill, GatheringBarDriveKind.SkillsMenuSelection);

        bar.TryAssignAbilityToFirstEmptySlot(_ability);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        RefreshDragState();
        if (!_canDrag || _ability == null)
            return;

        _rootCanvas ??= GetComponentInParent<Canvas>();
        if (_rootCanvas == null)
            _rootCanvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (_rootCanvas == null)
            return;

        AbilityDragState.BeginDrag(
            _ability.abilityId,
            SkillsAbilityPresentationResolver.ResolveAbilityIcon(_ability),
            SkillsAbilityPresentationResolver.ResolveAbilityDisplayName(_ability),
            ResolveDragDescription(_ability));
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

        _dragIconGo = new GameObject("TimelineAbilityDragIcon");
        _dragIconGo.transform.SetParent(_rootCanvas.transform, false);

        _dragIconRt = _dragIconGo.AddComponent<RectTransform>();
        _dragIconImage = _dragIconGo.AddComponent<Image>();
        _dragIconImage.raycastTarget = false;
        _dragIconImage.sprite = SkillsAbilityPresentationResolver.ResolveAbilityIcon(_ability);
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

    private static AbilityDefinition ResolveAbility(SkillTimelineNodeBinding binding)
    {
        if (binding == null)
            return null;

        if (binding.Choice?.ability != null)
            return binding.Choice.ability;

        if (binding.Unlock?.ability != null)
            return binding.Unlock.ability;

        return null;
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
