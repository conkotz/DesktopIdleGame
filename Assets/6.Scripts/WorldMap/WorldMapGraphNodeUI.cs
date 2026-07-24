using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>Circular world-map node; connector geometry matches <see cref="SkillTreeConnectorUI"/>.</summary>
public class WorldMapGraphNodeUI : MonoBehaviour, ITreeConnectorEndpoint
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image iconImage;
    [Tooltip("Small map-type badge (child named Icon). Hidden when no sprite is assigned for the node type.")]
    [FormerlySerializedAs("gatheringTypeIconImage")]
    [SerializeField] private Image mapTypeIconImage;
    [Tooltip("Boss badge overlay (child named BossIcon). Uses Boss Map Type Icon sprite when the node is a boss map.")]
    [SerializeField] private Image bossIconImage;
    [Header("Map type icons")]
    [Tooltip("Shown when nodeType is Town.")]
    [SerializeField] private Sprite townMapTypeIcon;
    [Tooltip("Shown when nodeType is Combat (and Boss).")]
    [SerializeField] private Sprite combatMapTypeIcon;
    [Tooltip("Shown when nodeType is Gathering and gatheringSubtype is Mining.")]
    [SerializeField] private Sprite miningMapTypeIcon;
    [Tooltip("Shown when nodeType is Gathering and gatheringSubtype is Woodcutting.")]
    [SerializeField] private Sprite woodcuttingMapTypeIcon;
    [Tooltip("Shown when nodeType is Gathering and gatheringSubtype is Fishing.")]
    [SerializeField] private Sprite fishingMapTypeIcon;
    [Tooltip("Shown when nodeType is Dungeon or EnduranceTrial.")]
    [SerializeField] private Sprite dungeonEnduranceMapTypeIcon;
    [Tooltip("Shown when MapNodeDefinition Is Boss Map is enabled (combat boss badge).")]
    [SerializeField] private Sprite bossMapTypeIcon;
    [SerializeField] private Button button;
    [SerializeField] private GameObject selectedBorder;
    [SerializeField] private GameObject lockedOverlay;
    [Tooltip("Shown when this node’s UI state is Cleared (non-repeatable map completed). Assign or place child named ClearedOverlay.")]
    [SerializeField] private GameObject clearedOverlay;
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text cpHintText;
    [Tooltip("Shown when MapNodeDefinition has combat map scaling enabled.")]
    [SerializeField] private TMP_Text mapScalingText;
    [Tooltip("Optional icon shown when player is currently in this map node.")]
    [SerializeField] private GameObject currentLocationIcon;
    [Range(0.1f, 1f)]
    [SerializeField] private float unavailableAlpha = 0.45f;

    private MapNodeDefinition _node;
    private Action<MapNodeDefinition> _onSelected;
    private bool _greyedOut;
    private bool _unavailable;
    private string _stateLabel = "";

    public MapNodeDefinition Node => _node;

    public RectTransform RectTransform => rectTransform != null ? rectTransform : (RectTransform)transform;

    private void Awake()
    {
        if (!rectTransform)
            rectTransform = (RectTransform)transform;
        if (!button)
            button = GetComponentInChildren<Button>(true);
        if (!rootCanvasGroup)
        {
            rootCanvasGroup = GetComponent<CanvasGroup>();
            if (!rootCanvasGroup)
                rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        if (!clearedOverlay)
        {
            Transform t = transform.Find("ClearedOverlay");
            if (t)
                clearedOverlay = t.gameObject;
        }
        if (!mapScalingText)
            mapScalingText = transform.Find("MapScalingText")?.GetComponent<TMP_Text>();
        if (!mapTypeIconImage)
            mapTypeIconImage = transform.Find("Icon")?.GetComponent<Image>();
        if (!bossIconImage)
            bossIconImage = transform.Find("BossIcon")?.GetComponent<Image>();
        if (button)
            button.onClick.AddListener(OnClick);

        EnsureRightClickRelay();
    }

    private void EnsureRightClickRelay()
    {
        if (!button)
            return;

        if (!button.TryGetComponent(out WorldMapGraphNodeRightClickUI relay))
            relay = button.gameObject.AddComponent<WorldMapGraphNodeRightClickUI>();

        relay.RightClicked -= HandleRightClicked;
        relay.RightClicked += HandleRightClicked;
    }

    private void HandleRightClicked(MapNodeDefinition node, Vector2 screenPosition)
    {
        WorldMapPageUI page = GetComponentInParent<WorldMapPageUI>(true);
        if (!page)
            page = FindFirstObjectByType<WorldMapPageUI>(FindObjectsInactive.Include);
        WorldMapGraphNodeContextMenu.Show(node, screenPosition, page);
    }

    private void OnDestroy()
    {
        if (button)
            button.onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        if (_node != null)
            _onSelected?.Invoke(_node);
    }

    public void Bind(
        MapNodeDefinition node,
        string stateLabel,
        bool selected,
        Action<MapNodeDefinition> onSelected,
        bool greyOutCompletedNonRepeatable,
        bool unavailable,
        WorldMapNodeButtonUI themePaletteSource,
        bool playerAtThisMap = false)
    {
        _node = node;
        _onSelected = onSelected;
        _greyedOut = greyOutCompletedNonRepeatable;
        _unavailable = unavailable;
        _stateLabel = stateLabel ?? "";

        Color theme = themePaletteSource
            ? themePaletteSource.GetThemeColorForNode(node)
            : ResolveFallbackTheme(node);

        if (fillImage)
            fillImage.color = theme;

        if (nameText)
        {
            nameText.text = node ? node.displayName : "—";
            nameText.gameObject.SetActive(true);
        }

        if (iconImage)
        {
            bool has = node && node.icon;
            iconImage.gameObject.SetActive(has);
            if (has)
                iconImage.sprite = node.icon;
        }

        if (cpHintText)
        {
            if (!node)
            {
                cpHintText.text = "";
                cpHintText.gameObject.SetActive(false);
            }
            else
            {
                int recCp = RecommendedCombatPower.GetRecommendedCombatPowerForDisplay(node);
                if (recCp <= 1)
                {
                    cpHintText.text = "";
                    cpHintText.gameObject.SetActive(false);
                }
                else
                {
                    cpHintText.gameObject.SetActive(true);
                    cpHintText.text = $"CP {recCp}";
                }
            }
        }

        RefreshMapScalingLabel(node);
        RefreshMapTypeIcon(node);
        RefreshBossIcon(node);

        if (currentLocationIcon)
            currentLocationIcon.SetActive(playerAtThisMap);

        if (lockedOverlay)
        {
            bool lockedVisual =
                string.Equals(_stateLabel.Trim(), "Map locked", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(_stateLabel.Trim(), "Skill locked", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(_stateLabel.Trim(), "Progress locked", StringComparison.OrdinalIgnoreCase);
            lockedOverlay.SetActive(lockedVisual);
        }

        if (clearedOverlay)
        {
            bool cleared =
                string.Equals(_stateLabel.Trim(), "Cleared", StringComparison.OrdinalIgnoreCase);
            clearedOverlay.SetActive(cleared);
        }

        if (button)
            button.interactable = true;

        RefreshVisuals(selected);
    }

    public void SetSelected(bool selected)
    {
        RefreshVisuals(selected);
    }

    private void RefreshMapScalingLabel(MapNodeDefinition node)
    {
        if (!mapScalingText)
            return;

        if (node == null || !node.IsMapCombatScalingEnabled())
        {
            mapScalingText.text = "";
            mapScalingText.gameObject.SetActive(false);
            return;
        }

        WorldMapProgressManager progress = WorldMapProgressManager.Instance ??
            FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        int selectedTier = progress != null
            ? progress.GetCombatMapScalingSelectedTier(node.nodeId)
            : MapCombatScaling.SliderMin;

        mapScalingText.gameObject.SetActive(true);
        string prefix = node.UsesBossAreaScaling() ? "Boss Scaling" : "Map Scaling";
        mapScalingText.text = $"{prefix}: {selectedTier}";
    }

    private void RefreshBossIcon(MapNodeDefinition node)
    {
        if (!bossIconImage)
            return;

        bool show = node != null && node.isBossMap && bossMapTypeIcon != null;
        bossIconImage.gameObject.SetActive(show);
        if (!show)
            return;

        bossIconImage.sprite = bossMapTypeIcon;
        bossIconImage.preserveAspect = true;
    }

    private void RefreshMapTypeIcon(MapNodeDefinition node)
    {
        if (!mapTypeIconImage)
            return;

        Sprite icon = ResolveMapTypeIcon(node);
        if (icon == null)
        {
            mapTypeIconImage.gameObject.SetActive(false);
            return;
        }

        mapTypeIconImage.sprite = icon;
        mapTypeIconImage.preserveAspect = true;
        mapTypeIconImage.gameObject.SetActive(true);
    }

    private Sprite ResolveMapTypeIcon(MapNodeDefinition node)
    {
        if (node == null)
            return null;

        switch (node.nodeType)
        {
            case MapNodeType.Town:
                return townMapTypeIcon;
            case MapNodeType.Combat:
            case MapNodeType.Boss:
                return combatMapTypeIcon;
            case MapNodeType.Gathering:
                return node.gatheringSubtype switch
                {
                    MapGatheringSubtype.Mining => miningMapTypeIcon,
                    MapGatheringSubtype.Woodcutting => woodcuttingMapTypeIcon,
                    MapGatheringSubtype.Fishing => fishingMapTypeIcon,
                    _ => null
                };
            case MapNodeType.Dungeon:
            case MapNodeType.EnduranceTrial:
                return dungeonEnduranceMapTypeIcon;
            default:
                return null;
        }
    }

    private void RefreshVisuals(bool selected)
    {
        if (selectedBorder)
            selectedBorder.SetActive(selected);

        if (!rootCanvasGroup)
            return;

        bool isLockState =
            string.Equals(_stateLabel.Trim(), "Map locked", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(_stateLabel.Trim(), "Skill locked", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(_stateLabel.Trim(), "Progress locked", StringComparison.OrdinalIgnoreCase);
        bool dimByUnavailable = _unavailable && (_greyedOut || isLockState);
        rootCanvasGroup.alpha = dimByUnavailable ? Mathf.Clamp01(unavailableAlpha) : 1f;
        rootCanvasGroup.interactable = true;
        rootCanvasGroup.blocksRaycasts = true;
    }

    public Vector2 GetVisualBoxSize()
    {
        if (fillImage)
            return fillImage.rectTransform.rect.size;
        return RectTransform.rect.size;
    }

    public float GetVisualHalfWidth() => GetVisualBoxSize().x * 0.5f;
    public float GetVisualHalfHeight() => GetVisualBoxSize().y * 0.5f;

    private static Color ResolveFallbackTheme(MapNodeDefinition node)
    {
        if (!node)
            return new Color32(74, 81, 95, 255);

        return node.nodeType switch
        {
            MapNodeType.Town => new Color32(196, 171, 128, 255),
            MapNodeType.Combat => new Color32(154, 82, 82, 255),
            MapNodeType.Gathering => new Color32(161, 190, 168, 255),
            MapNodeType.EnduranceTrial => new Color32(130, 88, 60, 255),
            MapNodeType.Special => new Color32(177, 143, 63, 255),
            MapNodeType.Dungeon => new Color32(88, 106, 122, 255),
            MapNodeType.Boss => new Color32(118, 60, 90, 255),
            _ => new Color32(74, 81, 95, 255)
        };
    }
}

/// <summary>Forwards right-clicks on world-map graph nodes to the context menu.</summary>
[DisallowMultipleComponent]
public sealed class WorldMapGraphNodeRightClickUI : MonoBehaviour, IPointerClickHandler
{
    public event Action<MapNodeDefinition, Vector2> RightClicked;

    private WorldMapGraphNodeUI _owner;

    private void Awake()
    {
        _owner = GetComponent<WorldMapGraphNodeUI>();
        if (!_owner)
            _owner = GetComponentInParent<WorldMapGraphNodeUI>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Right)
            return;

        if (_owner == null || _owner.Node == null)
            return;

        eventData.Use();
        RightClicked?.Invoke(_owner.Node, eventData.position);
    }
}

/// <summary>Right-click context menu for world-map graph nodes.</summary>
public static class WorldMapGraphNodeContextMenu
{
    public static void Show(MapNodeDefinition node, Vector2 screenPosition, WorldMapPageUI page)
    {
        if (node == null)
            return;

        WorldMapProgressManager progress = WorldMapProgressManager.Instance ??
            UnityEngine.Object.FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        SkillsManager skills = FindSkillsManager();
        bool canTeleport = node.CanEnterFromLevelMenu(progress, skills);
        string header = !string.IsNullOrWhiteSpace(node.displayName) ? node.displayName.Trim() : node.nodeId;

        var entries = new List<ContextMenuEntry>
        {
            new ContextMenuEntry(
                canTeleport ? "Enter area" : "Can't Teleport",
                canTeleport ? () => TryEnterMap(node, progress, skills) : static () => { },
                disabled: !canTeleport)
        };

        if (node.IsMapCombatScalingEnabled())
        {
            entries.Add(new ContextMenuEntry("Scaling", () =>
            {
                MapCombatScalingPopupUI.Show(node, progress, () => page?.NotifyGraphPresentationChanged());
            }));
        }

        ContextMenuUI.EnsureInstance().ShowAtScreen(entries, screenPosition, header);
    }

    private static void TryEnterMap(MapNodeDefinition node, WorldMapProgressManager progress, SkillsManager skills)
    {
        if (node == null || !node.CanEnterFromLevelMenu(progress, skills))
            return;

        MapTravelSession.TryBeginTravelAndLoadScene(
            node,
            MapTravelSession.EntryMethod.MapTeleport,
            "GamePlay");
    }

    private static SkillsManager FindSkillsManager()
    {
        if (SkillsManager.Instance != null)
            return SkillsManager.Instance;
        return UnityEngine.Object.FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);
    }
}
