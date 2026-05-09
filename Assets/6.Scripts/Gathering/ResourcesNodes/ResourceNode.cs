using System;
using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public enum NodeAction
{
    Mining,
    Woodcutting,
    Fishing
}

/// <summary>
/// Builds runtime sprite overlays in Awake before <see cref="SimpleHoverHighlight2D"/> caches renderers.
/// </summary>
[DefaultExecutionOrder(-100)]
public class ResourceNode : MonoBehaviour
{
    public const string RuntimeDepletionOverlayPrefix = "RuntimeDepletionOverlay";

    private const string DepletedNamePrefix = "DEPLETED";

    [Header("Definition")]
    [SerializeField] private NodeDefinition definition;
    public NodeDefinition Definition => definition;

    [Header("Interaction")]
    public Transform workSpot;
    public float interactRange = 0.1f;

    /// <summary>
    /// World X the player should face while gathering. Uses this node's position (visual center), not
    /// <see cref="workSpot"/> — the avatar stands at the work spot, so work-spot X matches the player and breaks flip logic.
    /// </summary>
    public float GatherFacingWorldX => transform.position.x;

    // Read-only views of the definition data
    public string DisplayName => definition ? definition.displayName : "Resource";
    public NodeAction ActionType => definition ? definition.actionType : NodeAction.Woodcutting;
    public int RequiredLevel => definition ? definition.requiredLevel : 1;
    public ItemDefinition YieldItem => definition ? definition.yieldItem : null;
    public string YieldItemId => definition ? definition.YieldItemId : string.Empty;

    public bool UseRandomInterval => definition && definition.useRandomInterval;
    public float RatePerSecond => definition ? definition.ratePerSecond : 0f;
    public float MinInterval => definition ? definition.minInterval : 0f;
    public float MaxInterval => definition ? definition.maxInterval : 0f;

    public float GetNextInterval() => definition ? definition.GetNextInterval() : float.MaxValue;
    public string GetActionText() => definition ? definition.GetActionText() : string.Empty;

    public bool RequiresTool => definition && definition.requiresTool && definition.requiredTool != ToolKey.None;
    public ToolKey RequiredTool => definition ? definition.requiredTool : ToolKey.None;
    public string MissingToolMessage => definition ? definition.missingToolMessage : "Put the required tool in your toolbelt.";
    public float EnergyCostPerSwing => definition ? Mathf.Max(0f, definition.energyCostPerSwing) : 0f;

    public bool UseLevelRequirement => definition && definition.useLevelRequirement;

    public bool IsDepleted => _isDepleted;
    public bool UsesDepletion => definition && definition.UsesDepletion;

    /// <summary>
    /// True for this gather tick when the node is already depleted, or this tick reaches the depletion cap
    /// (so yield is reduced on the same swing that exhausts the node).
    /// </summary>
    public bool ApplyDepletedYieldPenaltyThisTick => _applyDepletedYieldPenaltyThisTick;

    [Header("Name Label Visibility")]
    [Tooltip("Hide name label object(s) while player stands on this node.")]
    [SerializeField] private bool hideNameLabelsWhenPlayerOverlaps = true;
    [Tooltip("Optional explicit label roots; when empty, children containing 'NameLabel' in their name are auto-detected.")]
    [SerializeField] private List<GameObject> nameLabelRoots = new();

    [Header("Depletion timer UI")]
    [Tooltip("Roots for countdown (TMP/Text/Image fill). When empty, names containing 'DepletionTimer' are auto-detected.")]
    [SerializeField] private List<GameObject> depletionTimerRoots = new();

    [Header("Depletion visuals (runtime sprite overlay)")]
    [Tooltip("Dark tint on a duplicate of each world sprite — follows the tree silhouette, not a UI rectangle.")]
    [SerializeField] private Color depletionSpriteOverlayTint = new Color(0.06f, 0.06f, 0.1f, 0.75f);
    [Tooltip("Sorting order offset above the source sprite so the overlay draws on top.")]
    [SerializeField] private int depletionSpriteOverlaySortDelta = 1;
    [Tooltip("If empty, every SpriteRenderer under this object (outside any Canvas) gets an overlay. Otherwise only these.")]
    [SerializeField] private List<SpriteRenderer> depletionOverlaySpriteSources = new();
    [SerializeField] private Color depletedNamePrefixColor = new Color(0.92f, 0.22f, 0.22f, 1f);

    private PlayerController _player;
    private Collider2D _nodeCollider;
    private bool _nameLabelsHidden;

    private int _gathersSinceRegen;
    private bool _isDepleted;
    private float _regenAtTime = float.PositiveInfinity;
    private bool _applyDepletedYieldPenaltyThisTick;

    private readonly List<TMP_Text> _cachedTmpLabels = new();
    private readonly List<Text> _cachedUiTexts = new();
    private readonly List<string> _defaultLabelLines = new();
    private bool _labelsCached;

    private readonly List<TMP_Text> _timerTmp = new();
    private readonly List<Text> _timerUi = new();
    private readonly List<Image> _timerFillImages = new();

    private readonly List<GameObject> _runtimeDepletionOverlays = new();

    private void Awake()
    {
        _nodeCollider = GetComponent<Collider2D>() ?? GetComponentInChildren<Collider2D>();
        if (nameLabelRoots == null)
            nameLabelRoots = new List<GameObject>();
        if (nameLabelRoots.Count == 0)
            AutoCollectNameLabelRoots();
        if (depletionTimerRoots == null)
            depletionTimerRoots = new List<GameObject>();
        if (depletionTimerRoots.Count == 0)
            AutoCollectDepletionTimerRoots();
        if (depletionOverlaySpriteSources == null)
            depletionOverlaySpriteSources = new List<SpriteRenderer>();

        BuildRuntimeDepletionSpriteOverlays();

        CacheLabelComponents();
        CacheDepletionTimerVisuals();
        SetRuntimeDepletionOverlaysActive(false);
        RefreshDepletionTimerDisplay();
    }

    private void Update()
    {
        if (_isDepleted && definition != null && definition.depletionRegenSeconds > 0.0001f &&
            Time.time >= _regenAtTime)
            ClearDepletedState();

        RefreshDepletionTimerDisplay();

        if (!hideNameLabelsWhenPlayerOverlaps || nameLabelRoots == null || nameLabelRoots.Count == 0)
            return;

        if (_player == null)
            _player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (_player == null)
            return;

        bool overlap = IsPlayerOverlappingNode();
        if (overlap == _nameLabelsHidden)
            return;

        _nameLabelsHidden = overlap;
        for (int i = 0; i < nameLabelRoots.Count; i++)
        {
            GameObject go = nameLabelRoots[i];
            if (go != null)
                go.SetActive(!overlap);
        }
    }

    /// <summary>
    /// Call once at the start of a main-yield gather tick, before any yield bonuses are applied.
    /// </summary>
    public void NotifyGatherTickBeforeBonuses()
    {
        _applyDepletedYieldPenaltyThisTick = false;
        if (definition == null || !definition.UsesDepletion)
            return;

        if (_isDepleted)
        {
            _applyDepletedYieldPenaltyThisTick = true;
            return;
        }

        _gathersSinceRegen++;
        if (_gathersSinceRegen >= definition.depletionGatherCount)
            _applyDepletedYieldPenaltyThisTick = true;
    }

    /// <summary>
    /// Call at end of the gather tick after rewards; enters depleted state if the gather cap was reached this tick.
    /// </summary>
    public void NotifyGatherTickFinishedDepletionCheck()
    {
        if (definition == null || !definition.UsesDepletion || _isDepleted)
            return;
        if (_gathersSinceRegen >= definition.depletionGatherCount)
            EnterDepletedState();
    }

    private void EnterDepletedState()
    {
        if (_isDepleted)
            return;
        _isDepleted = true;
        if (definition != null && definition.depletionRegenSeconds > 0.0001f)
            _regenAtTime = Time.time + definition.depletionRegenSeconds;
        else
            _regenAtTime = float.PositiveInfinity;

        GameLog.Add("This resource is depleted.", GameLog.ResourceDepletedColor);
        if (definition != null)
        {
            int fewerPct = Mathf.RoundToInt((1f - Mathf.Clamp01(definition.depletedYieldMultiplier)) * 100f);
            GameLog.Add(
                $"Gathering yields {fewerPct}% fewer resources from this node until it replenishes.",
                GameLog.ResourceDepletedColor);
        }

        ApplyDepletedVisuals();
    }

    private void ClearDepletedState()
    {
        _isDepleted = false;
        _gathersSinceRegen = 0;
        _regenAtTime = float.PositiveInfinity;
        RestoreVisuals();
    }

    private void ApplyDepletedVisuals()
    {
        EnsureLabelsCached();
        string baseName = definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
            ? definition.displayName.Trim()
            : DisplayName;
        string prefixHex = ColorUtility.ToHtmlStringRGBA(depletedNamePrefixColor);
        string depletedTextRich = $"<color=#{prefixHex}>{DepletedNamePrefix}</color>\n{baseName}";

        for (int i = 0; i < _cachedTmpLabels.Count; i++)
        {
            TMP_Text t = _cachedTmpLabels[i];
            if (!t) continue;
            t.richText = true;
            t.text = depletedTextRich;
        }

        for (int i = 0; i < _cachedUiTexts.Count; i++)
        {
            Text t = _cachedUiTexts[i];
            if (!t) continue;
            t.supportRichText = true;
            t.text = depletedTextRich;
        }

        SetRuntimeDepletionOverlaysActive(true);
        RefreshDepletionTimerDisplay();
    }

    private void RestoreVisuals()
    {
        EnsureLabelsCached();
        for (int i = 0; i < _cachedTmpLabels.Count; i++)
        {
            TMP_Text t = _cachedTmpLabels[i];
            if (t && i < _defaultLabelLines.Count)
                t.text = _defaultLabelLines[i];
        }

        for (int i = 0; i < _cachedUiTexts.Count; i++)
        {
            Text t = _cachedUiTexts[i];
            int idx = _cachedTmpLabels.Count + i;
            if (t && idx < _defaultLabelLines.Count)
                t.text = _defaultLabelLines[idx];
        }

        SetRuntimeDepletionOverlaysActive(false);
        RefreshDepletionTimerDisplay();
    }

    private void BuildRuntimeDepletionSpriteOverlays()
    {
        ClearRuntimeDepletionSpriteOverlays();

        var sources = new List<SpriteRenderer>(8);
        CollectOverlaySourceRenderers(sources);

        for (int s = 0; s < sources.Count; s++)
        {
            SpriteRenderer src = sources[s];
            if (!src || src.sprite == null)
                continue;

            var go = new GameObject($"{RuntimeDepletionOverlayPrefix}_{src.gameObject.name}");
            go.transform.SetParent(src.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var ov = go.AddComponent<SpriteRenderer>();
            ov.sprite = src.sprite;
            ov.color = depletionSpriteOverlayTint;
            ov.flipX = src.flipX;
            ov.flipY = src.flipY;
            ov.drawMode = src.drawMode;
            ov.size = src.size;
            ov.tileMode = src.tileMode;
            ov.spriteSortPoint = src.spriteSortPoint;
            ov.sortingLayerID = src.sortingLayerID;
            ov.sortingOrder = src.sortingOrder + depletionSpriteOverlaySortDelta;
            ov.maskInteraction = src.maskInteraction;
            ov.sharedMaterial = src.sharedMaterial;

            _runtimeDepletionOverlays.Add(go);
        }
    }

    private void CollectOverlaySourceRenderers(List<SpriteRenderer> outList)
    {
        outList.Clear();

        if (depletionOverlaySpriteSources != null && depletionOverlaySpriteSources.Count > 0)
        {
            for (int i = 0; i < depletionOverlaySpriteSources.Count; i++)
            {
                SpriteRenderer sr = depletionOverlaySpriteSources[i];
                if (sr && sr.sprite != null)
                    outList.Add(sr);
            }
            return;
        }

        SpriteRenderer[] all = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < all.Length; i++)
        {
            SpriteRenderer sr = all[i];
            if (!sr || sr.sprite == null)
                continue;
            if (sr.gameObject.name.StartsWith(RuntimeDepletionOverlayPrefix, StringComparison.Ordinal))
                continue;
            if (sr.GetComponentInParent<Canvas>() != null)
                continue;
            outList.Add(sr);
        }
    }

    private void ClearRuntimeDepletionSpriteOverlays()
    {
        for (int i = 0; i < _runtimeDepletionOverlays.Count; i++)
        {
            GameObject go = _runtimeDepletionOverlays[i];
            if (go)
                Destroy(go);
        }

        _runtimeDepletionOverlays.Clear();
    }

    private void SetRuntimeDepletionOverlaysActive(bool on)
    {
        for (int i = 0; i < _runtimeDepletionOverlays.Count; i++)
        {
            GameObject go = _runtimeDepletionOverlays[i];
            if (go != null)
                go.SetActive(on);
        }
    }

    private void CacheDepletionTimerVisuals()
    {
        _timerTmp.Clear();
        _timerUi.Clear();
        _timerFillImages.Clear();
        for (int r = 0; r < depletionTimerRoots.Count; r++)
        {
            GameObject root = depletionTimerRoots[r];
            if (root == null) continue;

            TMP_Text[] tmps = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmps.Length; i++)
            {
                if (tmps[i] != null)
                    _timerTmp.Add(tmps[i]);
            }

            Text[] legacy = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < legacy.Length; i++)
            {
                if (legacy[i] != null)
                    _timerUi.Add(legacy[i]);
            }

            Image[] imgs = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < imgs.Length; i++)
            {
                if (imgs[i] != null)
                    _timerFillImages.Add(imgs[i]);
            }
        }
    }

    private void RefreshDepletionTimerDisplay()
    {
        bool finiteRegen = definition != null && definition.depletionRegenSeconds > 0.0001f;
        bool show = UsesDepletion && _isDepleted;
        float duration = finiteRegen ? definition.depletionRegenSeconds : 0f;
        float remain = finiteRegen && Time.time < _regenAtTime
            ? Mathf.Max(0f, _regenAtTime - Time.time)
            : 0f;
        float fill01 = finiteRegen && duration > 0.0001f
            ? Mathf.Clamp01(remain / duration)
            : 0f;

        string txt = !show
            ? string.Empty
            : finiteRegen
                ? $"{Mathf.CeilToInt(remain)}s"
                : "—";

        for (int i = 0; i < depletionTimerRoots.Count; i++)
        {
            GameObject go = depletionTimerRoots[i];
            if (go != null)
                go.SetActive(show);
        }

        for (int i = 0; i < _timerTmp.Count; i++)
        {
            TMP_Text t = _timerTmp[i];
            if (t) t.text = txt;
        }

        for (int i = 0; i < _timerUi.Count; i++)
        {
            Text t = _timerUi[i];
            if (t) t.text = txt;
        }

        for (int i = 0; i < _timerFillImages.Count; i++)
        {
            Image img = _timerFillImages[i];
            if (!img) continue;
            if (img.type == Image.Type.Filled)
                img.fillAmount = fill01;
        }
    }

    private void AutoCollectDepletionTimerRoots()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform t = children[i];
            if (t == null || t == transform)
                continue;
            if (t.gameObject == null)
                continue;
            string n = t.name;
            if (string.IsNullOrWhiteSpace(n))
                continue;
            if (n.IndexOf("DepletionTimer", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (!depletionTimerRoots.Contains(t.gameObject))
                depletionTimerRoots.Add(t.gameObject);
        }
    }

    private void EnsureLabelsCached()
    {
        if (_labelsCached)
            return;
        CacheLabelComponents();
    }

    private void CacheLabelComponents()
    {
        _cachedTmpLabels.Clear();
        _cachedUiTexts.Clear();
        _defaultLabelLines.Clear();

        for (int r = 0; r < nameLabelRoots.Count; r++)
        {
            GameObject root = nameLabelRoots[r];
            if (root == null) continue;

            TMP_Text[] tmps = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmps.Length; i++)
            {
                if (tmps[i] == null) continue;
                _cachedTmpLabels.Add(tmps[i]);
                _defaultLabelLines.Add(tmps[i].text);
            }

            Text[] legacy = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < legacy.Length; i++)
            {
                if (legacy[i] == null) continue;
                _cachedUiTexts.Add(legacy[i]);
                _defaultLabelLines.Add(legacy[i].text);
            }
        }

        _labelsCached = true;
    }

    private bool IsPlayerOverlappingNode()
    {
        if (_player == null)
            return false;

        if (_nodeCollider != null)
            return _nodeCollider.bounds.Contains(_player.transform.position);

        float dx = Mathf.Abs(_player.transform.position.x - transform.position.x);
        return dx <= interactRange;
    }

    private void AutoCollectNameLabelRoots()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform t = children[i];
            if (t == null || t == transform)
                continue;
            if (t.gameObject == null)
                continue;
            string n = t.name;
            if (string.IsNullOrWhiteSpace(n))
                continue;
            if (n.IndexOf("NameLabel", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (!nameLabelRoots.Contains(t.gameObject))
                nameLabelRoots.Add(t.gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!workSpot) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(workSpot.position, interactRange);
    }
}
