using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    [SerializeField] private float stackOverlapThresholdPx = 64f;
    [SerializeField] private float stackVerticalSpacingPx = 56f;

    private RectTransform canvasRect;
    private readonly List<GameObject> spawnedDebuffIcons = new();

    private Vector2 _stackBaseAnchored;
    private float _stackYOffset;

    private static readonly List<UnitOverheadUI> s_instances = new();
    private static bool s_canvasCallbackSubscribed;
    private static int s_lastStackResolveFrame = -1;

    private PlayerCombatController _playerCombatCache;
    private Image _clickBackingImage;

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
        Camera cam)
    {
        Unsubscribe();

        characterStats = stats;
        enemy = enemyController;
        ailments = ailmentController;
        followTarget = target;
        parentCanvas = canvas;
        targetCamera = cam;
        canvasRect = canvas ? canvas.transform as RectTransform : null;

        Subscribe();
        RefreshAll();
        EnsureClickableBacking();
        ComputeBaseAnchoredAndVisibility();
        if (!enableOverlappingStack || enemy == null)
            ApplyDirectPosition();
        else
            ApplyStackedPosition();
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

        float threshold = Mathf.Max(1f, candidates[0].stackOverlapThresholdPx);
        float spacing = Mathf.Max(1f, candidates[0].stackVerticalSpacingPx);

        candidates.Sort((a, b) => a._stackBaseAnchored.x.CompareTo(b._stackBaseAnchored.x));

        int start = 0;
        while (start < candidates.Count)
        {
            int end = start;
            while (end + 1 < candidates.Count &&
                   candidates[end + 1]._stackBaseAnchored.x - candidates[end]._stackBaseAnchored.x <= threshold)
            {
                end++;
            }

            var cluster = new List<UnitOverheadUI>();
            for (int j = start; j <= end; j++)
                cluster.Add(candidates[j]);

            cluster.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));

            for (int k = 0; k < cluster.Count; k++)
                cluster[k]._stackYOffset = k * spacing;

            start = end + 1;
        }

        for (int i = 0; i < candidates.Count; i++)
            candidates[i].ApplyStackedPosition();
    }

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

    private void ComputeBaseAnchoredAndVisibility()
    {
        if (root == null || followTarget == null || parentCanvas == null || targetCamera == null)
            return;

        if (canvasRect == null)
            canvasRect = parentCanvas.transform as RectTransform;

        Vector3 worldPos = followTarget.position + worldOffset;
        Vector3 screenPos = targetCamera.WorldToScreenPoint(worldPos);

        bool visible = screenPos.z > 0f;
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);

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
        if (root == null || !gameObject.activeSelf)
            return;

        root.anchoredPosition = _stackBaseAnchored;
        root.localScale = Vector3.one;
    }

    private void ApplyStackedPosition()
    {
        if (root == null || !gameObject.activeSelf)
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
            baseName = enemy.DisplayName;
        else if (characterStats != null)
            baseName = characterStats.UnitDisplayName;

        if (nameText != null)
        {
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
            iconUI.SetData(sprite, stacks, true);
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

        _clickBackingImage.raycastTarget = true;

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