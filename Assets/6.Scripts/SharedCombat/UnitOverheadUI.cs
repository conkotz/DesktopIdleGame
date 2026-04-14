using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UnitOverheadUI : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private RectTransform root;
    [SerializeField] private TMP_Text nameText;
    [Tooltip("Optional. Shows combat profile label (e.g. Glass Cannon, Deadly); color is set from the profile. Assign in the Inspector.")]
    [SerializeField] private TMP_Text combatProfileText;
    [SerializeField] private Image hpFill;
    [SerializeField] private TMP_Text hpValueText;
    [SerializeField] private Transform debuffContainer;
    [SerializeField] private GameObject debuffIconPrefab;

    [Header("Debuff Sprites")]
    [SerializeField] private Sprite bleedIcon;
    [SerializeField] private Sprite poisonIcon;
    [SerializeField] private Sprite burnIcon;
    [SerializeField] private Sprite chillIcon;
    [SerializeField] private Sprite shockIcon;

    [Header("Auto Bind")]
    [SerializeField] private CharacterStats characterStats;
    [SerializeField] private EnemyBaseController enemy;
    [SerializeField] private AilmentController ailments;

    [Header("Follow")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private Vector3 worldOffset = Vector3.zero;
    [SerializeField] private Canvas parentCanvas;
    [SerializeField] private Camera targetCamera;

    [Header("Overlap stack (enemy overhead only)")]
    [Tooltip("When multiple enemy overheads project to nearby X positions on the strip canvas, stack them vertically.")]
    [SerializeField] private bool enableOverlappingStack = true;
    [Tooltip(
        "Small fudge (canvas px) added when testing horizontal overlap. Keep low so stacks only form when UI actually overlaps.")]
    [FormerlySerializedAs("stackOverlapThresholdPx")]
    [SerializeField] private float stackHorizontalOverlapPaddingPx = 8f;
    [SerializeField] private float stackVerticalSpacingPx = 56f;
    [Tooltip(
        "Optional minimum half-width (canvas px) for overlap tests. 0 = use measured rect + TMP bounds only. " +
        "Increase slightly if very narrow layouts fail to stack when enemies stand on the same spot.")]
    [SerializeField] private float stackMinClusteringHalfWidthPx = 0f;

    private RectTransform canvasRect;
    private readonly List<GameObject> spawnedDebuffIcons = new();

    private Vector2 _stackBaseAnchored;
    private float _stackYOffset;

    private static readonly List<UnitOverheadUI> s_instances = new();
    private static bool s_canvasCallbackSubscribed;
    private static int s_lastStackResolveFrame = -1;

    private PlayerCombatController _playerCombatCache;
    private Image _clickBackingImage;

    private bool _hpBarOnlyLayout;

    /// <summary>True when the follow target is in the strip camera band this frame; used for overlap stacking (same idea as when the whole object was deactivated off-screen).</summary>
    private bool _worldBandVisible;

    private void Awake()
    {
        if (!root) root = transform as RectTransform;

        // Old behaviour still works if this UI is placed under the enemy directly.
        if (!characterStats) characterStats = GetComponentInParent<CharacterStats>();
        if (!enemy) enemy = GetComponentInParent<EnemyBaseController>();
        if (!ailments) ailments = GetComponentInParent<AilmentController>();

        EnsureClickableBacking();
    }

    private void OnEnable()
    {
        if (!s_instances.Contains(this))
            s_instances.Add(this);
        EnsureCanvasStackCallback();
        Subscribe();
        RefreshAll();
    }

    private void OnDisable()
    {
        s_instances.Remove(this);
        Unsubscribe();
    }

    private void LateUpdate()
    {
        ComputeBaseAnchoredAndVisibility();

        if (!enableOverlappingStack || enemy == null)
            ApplyDirectPosition();
        else
            ApplyStackedPosition();
    }

    public void Bind(
        CharacterStats stats,
        EnemyBaseController enemyController,
        AilmentController ailmentController,
        Transform target,
        Canvas canvas,
        Camera cam,
        bool hpBarOnly = false)
    {
        Unsubscribe();

        characterStats = stats;
        enemy = enemyController;
        ailments = ailmentController;
        followTarget = target;
        parentCanvas = canvas;
        targetCamera = cam;
        canvasRect = canvas ? canvas.transform as RectTransform : null;
        _hpBarOnlyLayout = hpBarOnly;

        ApplyHpBarOnlyVisuals();
        Subscribe();
        RefreshAll();
        EnsureClickableBacking();
        ComputeBaseAnchoredAndVisibility();
        if (!enableOverlappingStack || enemy == null)
            ApplyDirectPosition();
        else
            ApplyStackedPosition();
    }

    private void ApplyHpBarOnlyVisuals()
    {
        bool showExtras = !_hpBarOnlyLayout;
        if (nameText)
            nameText.gameObject.SetActive(showExtras);
        if (combatProfileText)
            combatProfileText.gameObject.SetActive(showExtras);
        if (debuffContainer)
            debuffContainer.gameObject.SetActive(showExtras);
    }

    private static void EnsureCanvasStackCallback()
    {
        if (s_canvasCallbackSubscribed)
            return;
        s_canvasCallbackSubscribed = true;
        Canvas.willRenderCanvases += OnCanvasWillRenderResolveStack;
    }

    private static void OnCanvasWillRenderResolveStack()
    {
        int fc = Time.frameCount;
        if (fc == s_lastStackResolveFrame)
            return;
        s_lastStackResolveFrame = fc;
        ResolveEnemyOverheadStacking();
    }

    private static void ResolveEnemyOverheadStacking()
    {
        var byCanvas = new Dictionary<int, List<UnitOverheadUI>>();

        for (int i = 0; i < s_instances.Count; i++)
        {
            UnitOverheadUI ui = s_instances[i];
            if (ui == null || !ui.isActiveAndEnabled || !ui.gameObject.activeInHierarchy)
                continue;
            if (!ui._worldBandVisible)
                continue;
            if (!ui.enableOverlappingStack || ui.enemy == null)
                continue;

            int canvasKey = ui.parentCanvas != null ? ui.parentCanvas.GetInstanceID() : 0;
            if (!byCanvas.TryGetValue(canvasKey, out List<UnitOverheadUI> list))
            {
                list = new List<UnitOverheadUI>();
                byCanvas[canvasKey] = list;
            }

            list.Add(ui);
        }

        foreach (KeyValuePair<int, List<UnitOverheadUI>> kv in byCanvas)
            ResolveStackingForCanvasGroup(kv.Value);
    }

    private static void ResolveStackingForCanvasGroup(List<UnitOverheadUI> candidates)
    {
        for (int i = 0; i < candidates.Count; i++)
            candidates[i]._stackYOffset = 0f;

        if (candidates.Count <= 1)
        {
            for (int i = 0; i < candidates.Count; i++)
                candidates[i].ApplyStackedPosition();
            return;
        }

        Canvas.ForceUpdateCanvases();

        float padding = Mathf.Max(0f, candidates[0].stackHorizontalOverlapPaddingPx);
        float spacing = Mathf.Max(1f, candidates[0].stackVerticalSpacingPx);

        float minHalfW = Mathf.Max(0f, candidates[0].stackMinClusteringHalfWidthPx);

        var spans = new List<(float minX, float maxX, UnitOverheadUI ui)>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            candidates[i].GetHorizontalSpanInCanvas(out float minX, out float maxX);
            WidenSpanForClusterMerge(candidates[i]._stackBaseAnchored.x, minHalfW, ref minX, ref maxX);
            spans.Add((minX, maxX, candidates[i]));
        }

        spans.Sort((a, b) => a.minX.CompareTo(b.minX));

        var cluster = new List<UnitOverheadUI>();
        float clusterMaxX = float.NegativeInfinity;

        for (int i = 0; i < spans.Count; i++)
        {
            (float minX, float maxX, UnitOverheadUI ui) = spans[i];

            if (cluster.Count == 0)
            {
                cluster.Add(ui);
                clusterMaxX = maxX;
                continue;
            }

            bool overlapsCluster = minX <= clusterMaxX + padding;
            if (overlapsCluster)
            {
                cluster.Add(ui);
                if (maxX > clusterMaxX)
                    clusterMaxX = maxX;
            }
            else
            {
                ApplyVerticalOffsetsToCluster(cluster, spacing);
                cluster.Clear();
                cluster.Add(ui);
                clusterMaxX = maxX;
            }
        }

        ApplyVerticalOffsetsToCluster(cluster, spacing);

        for (int i = 0; i < candidates.Count; i++)
            candidates[i].ApplyStackedPosition();
    }

    private static void WidenSpanForClusterMerge(float anchorCanvasX, float minHalfWidth, ref float minX, ref float maxX)
    {
        float w = maxX - minX;
        float center = w > 0.001f ? (minX + maxX) * 0.5f : anchorCanvasX;
        float half = Mathf.Max(w * 0.5f, minHalfWidth);
        minX = center - half;
        maxX = center + half;
    }

    private static void ApplyVerticalOffsetsToCluster(List<UnitOverheadUI> cluster, float spacing)
    {
        if (cluster == null || cluster.Count == 0)
            return;

        cluster.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));

        for (int k = 0; k < cluster.Count; k++)
            cluster[k]._stackYOffset = k * spacing;
    }

    /// <summary>
    /// Left/right in canvas-local space. Uses root rect <b>and</b> TMP mesh bounds — long names often draw wider than the root/bar rect.
    /// </summary>
    private void GetHorizontalSpanInCanvas(out float minX, out float maxX)
    {
        minX = maxX = _stackBaseAnchored.x;

        if (root == null)
            return;

        if (canvasRect == null && parentCanvas != null)
            canvasRect = parentCanvas.transform as RectTransform;

        if (canvasRect == null)
            return;

        minX = float.MaxValue;
        maxX = float.MinValue;

        root.GetWorldCorners(UnitOverheadUIWorkCorners);
        for (int c = 0; c < 4; c++)
        {
            Vector3 local = canvasRect.InverseTransformPoint(UnitOverheadUIWorkCorners[c]);
            if (local.x < minX) minX = local.x;
            if (local.x > maxX) maxX = local.x;
        }

        ExpandHorizontalSpanWithTmpMeshBounds(canvasRect, ref minX, ref maxX);
    }

    /// <summary>
    /// Root rect can be bar-sized while TMP draws past it; <see cref="TMP_Text.textBounds"/> matches rendered glyphs.
    /// </summary>
    private void ExpandHorizontalSpanWithTmpMeshBounds(RectTransform canvasRt, ref float minX, ref float maxX)
    {
        TMP_Text[] tmps = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < tmps.Length; i++)
        {
            TMP_Text tmp = tmps[i];
            if (!tmp || !tmp.gameObject.activeInHierarchy)
                continue;

            tmp.ForceMeshUpdate();
            Bounds b = tmp.textBounds;
            Vector3 c = b.center;
            Vector3 e = b.extents;
            if (e.x < 1e-6f && e.y < 1e-6f && e.z < 1e-6f)
                continue;

            for (int ix = -1; ix <= 1; ix += 2)
            for (int iy = -1; iy <= 1; iy += 2)
            for (int iz = -1; iz <= 1; iz += 2)
            {
                Vector3 localCorner = c + new Vector3(ix * e.x, iy * e.y, iz * e.z);
                Vector3 world = tmp.transform.TransformPoint(localCorner);
                Vector3 canvasLocal = canvasRt.InverseTransformPoint(world);
                if (canvasLocal.x < minX) minX = canvasLocal.x;
                if (canvasLocal.x > maxX) maxX = canvasLocal.x;
            }
        }
    }

    private static readonly Vector3[] UnitOverheadUIWorkCorners = new Vector3[4];

    private void Subscribe()
    {
        if (characterStats != null)
        {
            characterStats.OnNameChanged += HandleNameChanged;
            characterStats.OnHPChanged += HandleCharacterHpChanged;
            characterStats.OnStatsChanged += HandleStatsChanged;
        }

        if (enemy != null)
        {
            enemy.OnNameChanged += HandleNameChanged;
            enemy.OnHealthChanged += HandleEnemyHpChanged;
        }

        if (ailments != null)
        {
            ailments.OnAilmentsChanged += RefreshDebuffIcons;
        }
    }

    private void Unsubscribe()
    {
        if (characterStats != null)
        {
            characterStats.OnNameChanged -= HandleNameChanged;
            characterStats.OnHPChanged -= HandleCharacterHpChanged;
            characterStats.OnStatsChanged -= HandleStatsChanged;
        }

        if (enemy != null)
        {
            enemy.OnNameChanged -= HandleNameChanged;
            enemy.OnHealthChanged -= HandleEnemyHpChanged;
        }

        if (ailments != null)
        {
            ailments.OnAilmentsChanged -= RefreshDebuffIcons;
        }
    }

    private void RefreshAll()
    {
        HandleNameChanged(string.Empty);

        if (characterStats != null)
            HandleCharacterHpChanged(characterStats.HP, characterStats.MaxHP);

        if (enemy != null)
            HandleEnemyHpChanged(enemy.HP, enemy.MaxHP);

        RefreshDebuffIcons();
    }

    private static bool IsOverheadWorldPointVisible(Camera cam, Vector3 worldPos, Vector3 screenPos)
    {
        if (cam == null)
            return screenPos.z > 0f;

        if (cam.orthographic)
        {
            Vector3 vp = cam.WorldToViewportPoint(worldPos);
            if (vp.z < 0f)
                return false;
            const float margin = 0.2f;
            return vp.x >= -margin && vp.x <= 1f + margin && vp.y >= -margin && vp.y <= 1f + margin;
        }

        return screenPos.z > 0f;
    }

    private void ComputeBaseAnchoredAndVisibility()
    {
        if (root == null || followTarget == null || parentCanvas == null || targetCamera == null)
        {
            _worldBandVisible = false;
            return;
        }

        if (canvasRect == null)
            canvasRect = parentCanvas.transform as RectTransform;

        Vector3 worldPos = followTarget.position + worldOffset;
        Vector3 screenPos = targetCamera.WorldToScreenPoint(worldPos);

        // WorldToScreenPoint z <= 0 often means "behind" the camera, but orthographic 2D setups can edge-case;
        // viewport test keeps nameplates on-screen when the point is in front of the camera frustum.
        bool visible = IsOverheadWorldPointVisible(targetCamera, worldPos, screenPos);
        _worldBandVisible = visible;

        // Toggle only the UI root, not this behaviour's GameObject — otherwise LateUpdate stops
        // and we never recover when the follow target later moves on-screen (player scene teleport).
        if (root != null && root.gameObject.activeSelf != visible)
            root.gameObject.SetActive(visible);

        if (!visible)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCamera,
            out _stackBaseAnchored
        );
    }

    private void ApplyDirectPosition()
    {
        if (root == null || !root.gameObject.activeSelf)
            return;

        root.anchoredPosition = _stackBaseAnchored;
        root.localScale = Vector3.one;
    }

    private void ApplyStackedPosition()
    {
        if (root == null || !root.gameObject.activeSelf)
            return;

        root.anchoredPosition = _stackBaseAnchored + new Vector2(0f, _stackYOffset);
        root.localScale = Vector3.one;
    }

    private void HandleStatsChanged()
    {
        HandleNameChanged(string.Empty);
    }

    private void HandleNameChanged(string _)
    {
        RefreshNameCombatPowerAndProfile();
    }

    private void RefreshNameCombatPowerAndProfile()
    {
        string baseName = "Unit";

        if (enemy != null)
            baseName = enemy.GetRichTextDisplayNameForOverhead();
        else if (characterStats != null)
            baseName = characterStats.UnitDisplayName;

        if (nameText != null)
        {
            nameText.richText = true;
            if (characterStats == null)
                nameText.text = baseName;
            else
            {
                int cp = Mathf.RoundToInt(characterStats.GetCombatPowerBreakdown().TotalCombatPower);
                nameText.text = $"{baseName} <size=75%><color=#AAAAAA>CP {cp}</color></size>";
            }
        }

        if (combatProfileText != null)
        {
            if (characterStats == null)
            {
                combatProfileText.text = string.Empty;
                combatProfileText.color = Color.white;
            }
            else
            {
                CombatPowerBreakdown breakdown = characterStats.GetCombatPowerBreakdown();
                CombatProfileDefenseHints defenseHints = characterStats.GetCombatProfileDefenseHints();
                string profileLabel = CombatProfileClassifier.Classify(breakdown, defenseHints);
                combatProfileText.text = profileLabel;
                combatProfileText.color = CombatProfileClassifier.GetColorForLabel(profileLabel);
            }
        }
    }

    private void HandleCharacterHpChanged(float current, float max)
    {
        float fill = max > 0f ? current / max : 0f;

        if (hpFill != null)
            hpFill.fillAmount = Mathf.Clamp01(fill);

        if (hpValueText != null)
            hpValueText.text = $"{Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}";
    }

    private void HandleEnemyHpChanged(int current, int max)
    {
        float fill = max > 0 ? (float)current / max : 0f;

        if (hpFill != null)
            hpFill.fillAmount = Mathf.Clamp01(fill);

        if (hpValueText != null)
            hpValueText.text = $"{current}/{max}";
    }

    public void RefreshDebuffIcons()
    {
        ClearDebuffIcons();

        if (_hpBarOnlyLayout)
            return;

        if (ailments == null || debuffContainer == null || debuffIconPrefab == null)
            return;

        if (ailments.HasBleed)
            SpawnDebuffIcon(bleedIcon, "Bleed", 1);

        if (ailments.HasPoison)
            SpawnDebuffIcon(poisonIcon, "Poison", ailments.PoisonStacks);

        if (ailments.HasBurn)
            SpawnDebuffIcon(burnIcon, "Burn", ailments.BurnStacks);

        if (ailments.HasChill)
            SpawnDebuffIcon(chillIcon, "Chill", ailments.ChillStacks);

        if (ailments.HasShock)
            SpawnDebuffIcon(shockIcon, "Shock", 1);
    }

    private void SpawnDebuffIcon(Sprite sprite, string iconName, int stacks)
    {
        if (sprite == null) return;

        GameObject icon = Instantiate(debuffIconPrefab, debuffContainer);
        icon.name = $"Debuff_{iconName}";

        DebuffIconUI iconUI = icon.GetComponent<DebuffIconUI>();
        if (iconUI != null)
        {
            iconUI.SetData(sprite, stacks);
        }
        else
        {
            Image image = icon.GetComponent<Image>();
            if (image == null)
                image = icon.GetComponentInChildren<Image>();

            if (image != null)
                image.sprite = sprite;
        }

        spawnedDebuffIcons.Add(icon);

        foreach (Graphic g in icon.GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = false;
    }

    private void EnsureClickableBacking()
    {
        if (root == null)
            return;

        foreach (Graphic g in root.GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = false;

        _clickBackingImage = root.GetComponent<Image>();
        if (_clickBackingImage == null)
        {
            _clickBackingImage = root.gameObject.AddComponent<Image>();
            _clickBackingImage.color = new Color(1f, 1f, 1f, 0f);
        }

        _clickBackingImage.raycastTarget = !_hpBarOnlyLayout;

        OverheadClickRelay relay = root.GetComponent<OverheadClickRelay>();
        if (relay == null)
            relay = root.gameObject.AddComponent<OverheadClickRelay>();
        relay.Initialize(this);
    }

    internal void NotifyOverheadClicked(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        if (enemy == null || enemy.IsDead)
            return;

        if (_playerCombatCache == null)
            _playerCombatCache = FindFirstObjectByType<PlayerCombatController>(FindObjectsInactive.Exclude);

        if (_playerCombatCache == null)
            return;

        _playerCombatCache.SetTarget(enemy);
    }

    private void ClearDebuffIcons()
    {
        for (int i = 0; i < spawnedDebuffIcons.Count; i++)
        {
            if (spawnedDebuffIcons[i] != null)
                Destroy(spawnedDebuffIcons[i]);
        }

        spawnedDebuffIcons.Clear();
    }

    private sealed class OverheadClickRelay : MonoBehaviour, IPointerClickHandler
    {
        private UnitOverheadUI _owner;

        public void Initialize(UnitOverheadUI owner)
        {
            _owner = owner;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _owner?.NotifyOverheadClicked(eventData);
        }
    }
}