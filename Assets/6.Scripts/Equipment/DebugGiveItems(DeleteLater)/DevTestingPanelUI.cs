using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Temporary dev-only panel (delete for release). Wires RowGroup action buttons and a collapsible header.
/// Dev toolbar actions. Keyboard shortcuts (F1–F7) are handled on <see cref="BugAndSuggestionReportUI"/>.
/// </summary>
public class DevTestingPanelUI : MonoBehaviour
{
    private const string GameplaySceneName = "GamePlay";
    private const string DuskwoodNodeId = "duskwood";

    [Header("Layout")]
    [SerializeField] private GameObject rowGroup;
    [SerializeField] private Button showHideButton;
    [SerializeField] private TMP_Text showHideButtonLabel;

    [Header("Buttons (optional — resolved by child name if unset)")]
    [SerializeField] private Button reportBugButton;
    [SerializeField] private Button maxLevelButton;
    [SerializeField] private Button minLevelButton;
    [SerializeField] private Button plusLevelButton;
    [SerializeField] private Button minusLevelButton;
    [SerializeField] private Button addResourcesButton;
    [SerializeField] private Button addGoldButton;
    [SerializeField] private Button addCombatItemsButton;
    [SerializeField] private Button devWeaponButton;
    [SerializeField] private Button skipTutorialButton;
    [SerializeField] private Button resetCooldownsButton;
    [SerializeField] private Button refreshEnergyButton;
    [Tooltip("Optional — child TMP showing On/Off. Auto-resolved from ResetCooldownsButton if unset.")]
    [SerializeField] private TMP_Text resetCooldownsStatusText;
    [Tooltip("Optional — child TMP showing On/Off. Auto-resolved from RefreshEnergyButton if unset.")]
    [SerializeField] private TMP_Text refreshEnergyStatusText;

    [Header("Reset cooldowns toggle")]
    [SerializeField, Min(0.1f)] private float autoResetCooldownsIntervalSeconds = 2f;
    [SerializeField] private Color resetCooldownsActiveColor = new Color(0.45f, 0.95f, 0.55f, 1f);

    [Header("Refresh energy toggle")]
    [SerializeField, Min(0.1f)] private float autoRefreshEnergyIntervalSeconds = 2f;
    [SerializeField] private Color refreshEnergyActiveColor = new Color(0.45f, 0.95f, 0.55f, 1f);

    [Header("Optional feedback (same as DebugGiveItems)")]
    [SerializeField] private LevelUpEffect levelUpEffect;
    [SerializeField] private GoldPopupSpawner popupSpawner;
    [SerializeField] private Transform popupAnchor;
    [SerializeField] private Vector3 popupWorldOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Item defs (optional — IDs used when null)")]
    [SerializeField] private ItemDefinition fishDef;
    [SerializeField] private ItemDefinition logsDef;
    [SerializeField] private ItemDefinition stoneChunkDef;
    [SerializeField] private ItemDefinition devDestroyerMaceDef;
    [SerializeField] private ItemDefinition splitwoodArrowDef;
    [SerializeField] private ItemDefinition hardwoodArrowDef;

    [Header("Amounts")]
    [SerializeField] private int grantResourcePackStoneFishLogs = 100;
    [SerializeField] private int grantResourcePackLinenLeather = 50;
    [SerializeField] private int grantGoldAmount = 50000;
    [SerializeField] private int grantCombatArrowAmount = 500;

    private static readonly string[] TutorialSkipQuestOrder =
    {
        "tutorial_learning_ropes_1",
        "tutorial_learning_ropes_2",
        "tutorial_basic_combat",
        "tutorial_basic_combat_2",
        "tutorial_aid_merlin",
        "tutorial_defeat_ivan",
    };

    private const string IdStoneChunk = "stone_chunk";
    private const string IdRawFish = "raw_fish";
    private const string IdSplitwoodLog = "splitwood_log";
    private const string IdLinen = "linen";
    private const string IdLeather = "leather";
    private const string IdVialPoison = "vial_poison";
    private const string IdGemSapphire = "gem_sapphire";
    private const string IdFeather = "feather";
    private const string IdDevMace = "dev_destroyer_mace";
    private const string IdSplitwoodArrow = "splitwood_arrow";
    private const string IdHardwoodArrow = "hardwood_arrow";

    private bool _autoResetCooldownsEnabled;
    private float _nextAutoResetCooldownsTime;
    private ColorBlock _resetCooldownsButtonDefaultColors;
    private bool _resetCooldownsButtonColorsCaptured;
    private bool _resetCooldownsCombinedStatusInOneLabel;
    private bool _autoRefreshEnergyEnabled;
    private float _nextAutoRefreshEnergyTime;
    private ColorBlock _refreshEnergyButtonDefaultColors;
    private bool _refreshEnergyButtonColorsCaptured;
    private bool _refreshEnergyCombinedStatusInOneLabel;

    private void Awake()
    {
        ShowDevPanelSettingsInstaller.EnsureSettingsRowExists();
        TryResolveRefsByName();
        TryResolveFeedbackRefs();
        TryResolveItemDefsFromDebugGiveItems();

        Bind(showHideButton, ToggleRowGroupClicked);
        Bind(reportBugButton, OnReportBugClicked);
        Bind(maxLevelButton, DevTesting_ApplyMaxLevelAllSkills);
        Bind(minLevelButton, DevTesting_ApplyMinLevelAllSkills);
        Bind(plusLevelButton, DevTesting_ApplyPlusOneAllSkills);
        Bind(minusLevelButton, DevTesting_ApplyMinusOneAllSkills);
        Bind(addResourcesButton, DevTesting_ApplyAddResourcePack);
        Bind(addGoldButton, DevTesting_ApplyAddGold);
        Bind(addCombatItemsButton, DevTesting_ApplyAddCombatItems);
        Bind(devWeaponButton, DevTesting_ApplyDevWeapon);
        Bind(skipTutorialButton, OnSkipTutorialClicked);
        Bind(resetCooldownsButton, OnResetCooldownsToggleClicked);
        Bind(refreshEnergyButton, OnRefreshEnergyToggleClicked);
        TryResolveResetCooldownsStatusText();
        TryResolveRefreshEnergyStatusText();
        CaptureResetCooldownsButtonColors();
        CaptureRefreshEnergyButtonColors();
        ApplyResetCooldownsButtonHighlight();
        ApplyRefreshEnergyButtonHighlight();
    }

    private void OnEnable()
    {
        ToggleSettingsStore.Changed += OnToggleSettingChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyVisibilityFromSettings();
        if (gameObject.activeSelf)
            ApplyRowGroupVisible(false, forceLabel: true);
    }

    private void OnDisable()
    {
        ToggleSettingsStore.Changed -= OnToggleSettingChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _autoResetCooldownsEnabled = false;
        _autoRefreshEnergyEnabled = false;
        ApplyResetCooldownsButtonHighlight();
        ApplyRefreshEnergyButtonHighlight();
    }

    private void Update()
    {
        if (_autoResetCooldownsEnabled && Time.unscaledTime >= _nextAutoResetCooldownsTime)
        {
            _nextAutoResetCooldownsTime = Time.unscaledTime + Mathf.Max(0.1f, autoResetCooldownsIntervalSeconds);
            DevTesting_ClearAllCooldownsNow();
        }

        if (_autoRefreshEnergyEnabled && Time.unscaledTime >= _nextAutoRefreshEnergyTime)
        {
            _nextAutoRefreshEnergyTime = Time.unscaledTime + Mathf.Max(0.1f, autoRefreshEnergyIntervalSeconds);
            DevTesting_RefreshEnergyNow();
        }
    }

    private void OnSceneLoaded(Scene _, LoadSceneMode __)
    {
        ApplyVisibilityFromSettings();
        if (gameObject.activeSelf)
            ApplyRowGroupVisible(false, forceLabel: true);
    }

    private void OnToggleSettingChanged(ToggleSettingId id, bool _)
    {
        if (id == ToggleSettingId.ShowDevPanel)
            ApplyVisibilityFromSettings();
    }

    public static void RefreshAllFromSettings()
    {
        DevTestingPanelUI[] list = FindObjectsByType<DevTestingPanelUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < list.Length; i++)
        {
            if (list[i])
                list[i].ApplyVisibilityFromSettings();
        }
    }

    private void ApplyVisibilityFromSettings()
    {
        bool show = ToggleSettingsStore.Get(ToggleSettingId.ShowDevPanel);
        if (gameObject.activeSelf == show)
            return;

        gameObject.SetActive(show);
        if (show)
            ApplyRowGroupVisible(false, forceLabel: true);
    }

    private void OnReportBugClicked()
    {
        BugAndSuggestionReportUI reportUi = GetComponent<BugAndSuggestionReportUI>() ??
            GetComponentInParent<BugAndSuggestionReportUI>();
        if (!reportUi)
            reportUi = FindFirstObjectByType<BugAndSuggestionReportUI>(FindObjectsInactive.Include);
        if (reportUi != null)
            reportUi.ToggleBugReportPanel();
        else
            Debug.LogWarning("[DevTestingPanel] Add BugAndSuggestionReportUI (same GameObject as this panel or in scene) to use Report Bug.");
    }

    private void ToggleRowGroupClicked()
    {
        if (!rowGroup)
            return;
        bool next = !rowGroup.activeSelf;
        ApplyRowGroupVisible(next, forceLabel: false);
    }

    private void ApplyRowGroupVisible(bool visible, bool forceLabel)
    {
        if (rowGroup)
            rowGroup.SetActive(visible);

        if (showHideButtonLabel)
            showHideButtonLabel.text = visible ? "-" : "+";
        else if (showHideButton && forceLabel)
        {
            TMP_Text t = showHideButton.GetComponentInChildren<TMP_Text>(true);
            if (t)
                t.text = visible ? "-" : "+";
        }
    }

    /// <summary>Same as MaxLevelButton (all tracked skills → 50).</summary>
    public void DevTesting_ApplyMaxLevelAllSkills()
    {
        SkillsManager sm = ResolveSkillsManager();
        if (!sm)
        {
            Debug.LogWarning("[DevTestingPanel] No SkillsManager.");
            return;
        }

        sm.DebugSetAllTrackedSkillsLevel(50);

        ProcessingProficiencyRuntime processing = ProcessingProficiencyRuntime.EnsureInstance();
        processing.DebugSetAllProcessingLevels(50);

        ShowPopup("DEBUG ALL SKILLS → 50", new Color(0.4f, 1f, 0.55f));
    }

    /// <summary>Same as MinLevelButton (all tracked skills → 1).</summary>
    public void DevTesting_ApplyMinLevelAllSkills()
    {
        SkillsManager sm = ResolveSkillsManager();
        if (!sm)
        {
            Debug.LogWarning("[DevTestingPanel] No SkillsManager.");
            return;
        }

        sm.DebugSetAllTrackedSkillsLevel(1);
        ShowPopup("DEBUG ALL SKILLS → 1", new Color(0.85f, 0.55f, 0.35f));
    }

    /// <summary>Same as PlusLevelButton (+1 all skills).</summary>
    public void DevTesting_ApplyPlusOneAllSkills()
    {
        SkillsManager sm = ResolveSkillsManager();
        if (!sm)
        {
            Debug.LogWarning("[DevTestingPanel] No SkillsManager.");
            return;
        }

        sm.DebugIncreaseAllSkillsOneLevel();
        if (levelUpEffect)
            levelUpEffect.PlayLevelUp();
        ShowPopup("DEBUG +1 ALL SKILLS", Color.yellow);
    }

    /// <summary>Same as MinusLevelButton (−1 all skills).</summary>
    public void DevTesting_ApplyMinusOneAllSkills()
    {
        SkillsManager sm = ResolveSkillsManager();
        if (!sm)
        {
            Debug.LogWarning("[DevTestingPanel] No SkillsManager.");
            return;
        }

        sm.DebugDecreaseAllSkillsOneLevel();
        ShowPopup("DEBUG -1 ALL SKILLS", new Color(0.85f, 0.55f, 0.35f));
    }

    /// <summary>Same as AddResourcesButton.</summary>
    public void DevTesting_ApplyAddResourcePack()
    {
        Inventory inv = ResolveInventory();
        if (!inv)
        {
            Debug.LogWarning("[DevTestingPanel] No Inventory.");
            return;
        }

        int n = Mathf.Max(1, grantResourcePackStoneFishLogs);
        int m = Mathf.Max(1, grantResourcePackLinenLeather);
        AddToInventory(inv, fishDef, IdRawFish, n);
        AddToInventory(inv, logsDef, IdSplitwoodLog, n);
        AddToInventory(inv, stoneChunkDef, IdStoneChunk, n);
        inv.Add(IdLinen, m);
        inv.Add(IdLeather, m);
        inv.Add(IdVialPoison, m);
        inv.Add(IdGemSapphire, m);
        inv.Add(IdFeather, 100);
        Debug.Log($"[DevTestingPanel] +{n} fish/logs/stone, +{m} linen/leather/vial poison/sapphire, +100 feathers.");
    }

    /// <summary>Same as AddGoldButton.</summary>
    public void DevTesting_ApplyAddGold()
    {
        CurrencyWallet w = FindFirstObjectByType<CurrencyWallet>(FindObjectsInactive.Include);
        if (!w)
        {
            Debug.LogWarning("[DevTestingPanel] No CurrencyWallet.");
            return;
        }

        int g = Mathf.Max(1, grantGoldAmount);
        w.AddGold(g);
        if (popupSpawner)
            popupSpawner.ShowGoldGained(g);
        Debug.Log($"[DevTestingPanel] +{g} gold.");
    }

    /// <summary>Same as AddCombatItems — splitwood + hardwood arrows for ranged testing.</summary>
    public void DevTesting_ApplyAddCombatItems()
    {
        Inventory inv = ResolveInventory();
        if (!inv)
        {
            Debug.LogWarning("[DevTestingPanel] No Inventory.");
            return;
        }

        int qty = Mathf.Max(1, grantCombatArrowAmount);
        AddToInventory(inv, splitwoodArrowDef, IdSplitwoodArrow, qty);
        AddToInventory(inv, hardwoodArrowDef, IdHardwoodArrow, qty);
        Debug.Log($"[DevTestingPanel] +{qty} splitwood arrows, +{qty} hardwood arrows.");
    }

    /// <summary>Same as DevWeapon / + dev mace.</summary>
    public void DevTesting_ApplyDevWeapon()
    {
        Inventory inv = ResolveInventory();
        if (!inv)
        {
            Debug.LogWarning("[DevTestingPanel] No Inventory.");
            return;
        }

        string id = devDestroyerMaceDef && !string.IsNullOrWhiteSpace(devDestroyerMaceDef.itemId)
            ? devDestroyerMaceDef.itemId
            : IdDevMace;
        inv.Add(id, 1);
        Debug.Log($"[DevTestingPanel] +1 {id}.");
    }

    private void OnResetCooldownsToggleClicked()
    {
        _autoResetCooldownsEnabled = !_autoResetCooldownsEnabled;
        ApplyResetCooldownsButtonHighlight();

        if (_autoResetCooldownsEnabled)
        {
            _nextAutoResetCooldownsTime = Time.unscaledTime;
            DevTesting_ClearAllCooldownsNow();
        }
    }

    private void DevTesting_ClearAllCooldownsNow()
    {
        PlayerAbilityController abilities = ResolvePlayerAbilityController();
        if (!abilities)
        {
            Debug.LogWarning("[DevTestingPanel] No PlayerAbilityController — cannot reset cooldowns.");
            return;
        }

        abilities.DevTesting_ClearAllAbilityCooldowns();
    }

    private void OnRefreshEnergyToggleClicked()
    {
        _autoRefreshEnergyEnabled = !_autoRefreshEnergyEnabled;
        ApplyRefreshEnergyButtonHighlight();

        if (_autoRefreshEnergyEnabled)
        {
            _nextAutoRefreshEnergyTime = Time.unscaledTime;
            DevTesting_RefreshEnergyNow();
        }
    }

    private void DevTesting_RefreshEnergyNow()
    {
        CharacterStats stats = ResolveCharacterStats();
        if (!stats)
        {
            Debug.LogWarning("[DevTestingPanel] No CharacterStats — cannot refresh mana/energy.");
            return;
        }

        stats.AddEnergy(Mathf.Max(0f, stats.MaxEnergy));
        stats.AddMana(Mathf.Max(0f, stats.MaxMana));
    }

    private void CaptureResetCooldownsButtonColors()
    {
        if (!resetCooldownsButton || _resetCooldownsButtonColorsCaptured)
            return;

        _resetCooldownsButtonDefaultColors = resetCooldownsButton.colors;
        _resetCooldownsButtonColorsCaptured = true;
    }

    private void ApplyResetCooldownsButtonHighlight()
    {
        if (!resetCooldownsButton)
            return;

        CaptureResetCooldownsButtonColors();

        ColorBlock colors = _resetCooldownsButtonDefaultColors;
        if (_autoResetCooldownsEnabled)
        {
            Color on = resetCooldownsActiveColor;
            colors.normalColor = on;
            colors.highlightedColor = Color.Lerp(on, Color.white, 0.2f);
            colors.selectedColor = on;
            colors.pressedColor = Color.Lerp(on, Color.black, 0.15f);
        }

        resetCooldownsButton.colors = colors;
        ApplyResetCooldownsStatusLabel();
    }

    private void TryResolveResetCooldownsStatusText()
    {
        if (resetCooldownsStatusText || !resetCooldownsButton)
            return;

        resetCooldownsStatusText = ResolveToggleStatusText(
            resetCooldownsButton,
            out _resetCooldownsCombinedStatusInOneLabel);
    }

    private void ApplyResetCooldownsStatusLabel()
    {
        if (!resetCooldownsStatusText)
            return;

        if (_resetCooldownsCombinedStatusInOneLabel)
        {
            resetCooldownsStatusText.text = _autoResetCooldownsEnabled
                ? "Reset Cooldowns\nOn"
                : "Reset Cooldowns\nOff";
            return;
        }

        resetCooldownsStatusText.text = _autoResetCooldownsEnabled ? "On" : "Off";
    }

    private void CaptureRefreshEnergyButtonColors()
    {
        if (!refreshEnergyButton || _refreshEnergyButtonColorsCaptured)
            return;

        _refreshEnergyButtonDefaultColors = refreshEnergyButton.colors;
        _refreshEnergyButtonColorsCaptured = true;
    }

    private void ApplyRefreshEnergyButtonHighlight()
    {
        if (!refreshEnergyButton)
            return;

        CaptureRefreshEnergyButtonColors();

        ColorBlock colors = _refreshEnergyButtonDefaultColors;
        if (_autoRefreshEnergyEnabled)
        {
            Color on = refreshEnergyActiveColor;
            colors.normalColor = on;
            colors.highlightedColor = Color.Lerp(on, Color.white, 0.2f);
            colors.selectedColor = on;
            colors.pressedColor = Color.Lerp(on, Color.black, 0.15f);
        }

        refreshEnergyButton.colors = colors;
        ApplyRefreshEnergyStatusLabel();
    }

    private void TryResolveRefreshEnergyStatusText()
    {
        if (refreshEnergyStatusText || !refreshEnergyButton)
            return;

        refreshEnergyStatusText = ResolveToggleStatusText(
            refreshEnergyButton,
            out _refreshEnergyCombinedStatusInOneLabel);
    }

    private void ApplyRefreshEnergyStatusLabel()
    {
        if (!refreshEnergyStatusText)
            return;

        if (_refreshEnergyCombinedStatusInOneLabel)
        {
            refreshEnergyStatusText.text = _autoRefreshEnergyEnabled
                ? "Refresh Energy\nOn"
                : "Refresh Energy\nOff";
            return;
        }

        refreshEnergyStatusText.text = _autoRefreshEnergyEnabled ? "On" : "Off";
    }

    private void OnSkipTutorialClicked()
    {
        QuestProgressManager qm = QuestProgressManager.Instance ??
            FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);
        if (!qm)
        {
            Debug.LogWarning("[DevTestingPanel] No QuestProgressManager.");
            return;
        }

        for (int i = 0; i < TutorialSkipQuestOrder.Length; i++)
        {
            QuestDefinition q = qm.GetQuestDefinition(TutorialSkipQuestOrder[i]);
            if (!q)
            {
                Debug.LogWarning($"[DevTestingPanel] Missing quest def '{TutorialSkipQuestOrder[i]}'.");
                continue;
            }

            bool bulk = i < TutorialSkipQuestOrder.Length - 1;
            qm.DevTestingForceCompleteQuestReward(q, runTeleportAfterClaim: false, suppressQuestCompleteLog: bulk, skipSave: bulk);
        }

        WorldMapProgressManager wmp = WorldMapProgressManager.Instance ??
            FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        if (wmp != null)
        {
            wmp.SetNodeCompleted("tutorial_1", true);
            wmp.SetNodeCompleted("tutorial_2", true);
            wmp.UnlockNode(DuskwoodNodeId);
        }

        TeleportToDuskwoodGameplay();

        if (SaveManager.Instance != null)
            SaveManager.Instance.Save();

        Debug.Log("[DevTestingPanel] Skip tutorial: quests completed, tutorial nodes marked, duskwood unlocked, travelling to Duskwood.");
    }

    private void TeleportToDuskwoodGameplay()
    {
        WorldMapProgressManager wmp = WorldMapProgressManager.Instance ??
            FindFirstObjectByType<WorldMapProgressManager>(FindObjectsInactive.Include);
        WorldMapDefinition map = wmp && wmp.WorldMap
            ? wmp.WorldMap
            : Resources.Load<WorldMapDefinition>("Databases/WorldMap_Main");
        if (!map)
        {
            Debug.LogError("[DevTestingPanel] No WorldMapDefinition.");
            return;
        }

        MapNodeDefinition dusk = map.FindNodeById(DuskwoodNodeId);
        if (!dusk)
        {
            Debug.LogError($"[DevTestingPanel] Map node '{DuskwoodNodeId}' not found.");
            return;
        }

        ActiveLevelContext.SetPendingLevel(dusk, logToConsole: false);
        PlayerLevelTransition.LoadSceneWithEffectOrImmediate(GameplaySceneName);
    }

    private static void Bind(Button b, Action action)
    {
        if (!b || action == null)
            return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() => action());
    }

    private void ShowPopup(string msg, Color c)
    {
        if (popupSpawner && popupAnchor)
            popupSpawner.ShowMessageAtWorld(popupAnchor.position + popupWorldOffset, msg, c);
    }

    private void TryResolveRefsByName()
    {
        if (!rowGroup)
        {
            Transform t = transform.Find("RowGroup");
            if (t) rowGroup = t.gameObject;
        }

        if (!showHideButton)
        {
            Transform t = transform.Find("ShowHideButton");
            if (t) showHideButton = t.GetComponent<Button>();
        }

        if (!showHideButtonLabel && showHideButton)
            showHideButtonLabel = showHideButton.GetComponentInChildren<TMP_Text>(true);

        reportBugButton ??= FindButtonUnderRow("ReportBugButton");
        maxLevelButton ??= FindButtonUnderRow("MaxLevelButton");
        minLevelButton ??= FindButtonUnderRow("MinLevelButton");
        plusLevelButton ??= FindButtonUnderRow("PlusLevelButton");
        minusLevelButton ??= FindButtonUnderRow("MinusLevelButton");
        addResourcesButton ??= FindButtonUnderRow("AddResourcesButton");
        addGoldButton ??= FindButtonUnderRow("AddGoldButton");
        addCombatItemsButton ??= FindButtonUnderRow("AddCombatItems");
        devWeaponButton ??= FindButtonUnderRow("DevWeapon");
        skipTutorialButton ??= FindButtonUnderRow("SkipTutorial");
        resetCooldownsButton ??= FindButtonUnderRow("ResetCooldownsButton");
        refreshEnergyButton ??= FindButtonUnderRow("RefreshEnergyButton");
        if (resetCooldownsButton)
            TryResolveResetCooldownsStatusText();
        if (refreshEnergyButton)
            TryResolveRefreshEnergyStatusText();
    }

    private static PlayerAbilityController ResolvePlayerAbilityController() =>
        FindFirstObjectByType<PlayerAbilityController>(FindObjectsInactive.Include);

    private static CharacterStats ResolveCharacterStats()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player)
        {
            CharacterStats playerStats = player.GetComponent<CharacterStats>();
            if (playerStats)
                return playerStats;
        }

        return FindFirstObjectByType<CharacterStats>(FindObjectsInactive.Include);
    }

    private Button FindButtonUnderRow(string childName)
    {
        if (!rowGroup || string.IsNullOrWhiteSpace(childName))
            return null;
        Transform rg = rowGroup.transform;
        Transform t = rg.Find(childName);
        if (t)
            return t.GetComponent<Button>();
        for (int i = 0; i < rg.childCount; i++)
        {
            Transform c = rg.GetChild(i);
            if (c && string.Equals(c.name, childName, StringComparison.OrdinalIgnoreCase))
                return c.GetComponent<Button>();
        }

        return null;
    }

    private static TMP_Text ResolveToggleStatusText(Button button, out bool combinedStatusInOneLabel)
    {
        combinedStatusInOneLabel = false;
        if (!button)
            return null;

        TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text preferredMainLabel = null;
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text t = texts[i];
            if (!t)
                continue;

            string trimmed = t.text != null ? t.text.Trim() : string.Empty;
            string objectName = t.gameObject.name ?? string.Empty;
            if (trimmed.Equals("Off", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("On", StringComparison.OrdinalIgnoreCase))
            {
                return t;
            }

            if (objectName.IndexOf("Status", StringComparison.OrdinalIgnoreCase) >= 0)
                return t;

            if (objectName.IndexOf("Hotkey", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            if (preferredMainLabel == null ||
                objectName.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                preferredMainLabel = t;
            }
        }

        if (preferredMainLabel != null)
        {
            combinedStatusInOneLabel = true;
            return preferredMainLabel;
        }

        if (texts.Length > 0)
            return texts[0];

        return null;
    }

    private void TryResolveFeedbackRefs()
    {
        if (!popupSpawner)
            popupSpawner = FindFirstObjectByType<GoldPopupSpawner>(FindObjectsInactive.Include);
        if (!popupAnchor)
        {
            PlayerController pc = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            popupAnchor = pc ? pc.transform : transform;
        }

        if (!levelUpEffect)
            levelUpEffect = FindFirstObjectByType<LevelUpEffect>(FindObjectsInactive.Include);
    }

    private void TryResolveItemDefsFromDebugGiveItems()
    {
        DebugGiveItems dg = FindFirstObjectByType<DebugGiveItems>(FindObjectsInactive.Include);
        if (!dg)
            return;

        dg.ExportDevItemRefsForTestingPanel(
            out ItemDefinition fish,
            out ItemDefinition logs,
            out ItemDefinition stone,
            out ItemDefinition mace);
        if (!fishDef) fishDef = fish;
        if (!logsDef) logsDef = logs;
        if (!stoneChunkDef) stoneChunkDef = stone;
        if (!devDestroyerMaceDef) devDestroyerMaceDef = mace;
    }

    private static Inventory ResolveInventory() =>
        FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);

    private static SkillsManager ResolveSkillsManager()
    {
        if (SkillsManager.Instance != null)
            return SkillsManager.Instance;
        return FindFirstObjectByType<SkillsManager>(FindObjectsInactive.Include);
    }

    private static void AddToInventory(Inventory inv, ItemDefinition def, string fallbackItemId, int qty)
    {
        if (!inv || qty <= 0)
            return;
        string id = def && !string.IsNullOrWhiteSpace(def.itemId) ? def.itemId : fallbackItemId;
        if (string.IsNullOrWhiteSpace(id))
            return;
        inv.Add(id, qty);
    }
}
