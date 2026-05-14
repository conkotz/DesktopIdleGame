using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Temporary dev-only panel (delete for release). Wires RowGroup action buttons and a collapsible header.
/// Mirrors hotkey behaviour from <see cref="DebugGiveItems"/> where applicable.
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
    [SerializeField] private Button devWeaponButton;
    [SerializeField] private Button skipTutorialButton;

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

    [Header("Amounts")]
    [SerializeField] private int grantResourcePackStoneFishLogs = 100;
    [SerializeField] private int grantResourcePackLinenLeather = 50;
    [SerializeField] private int grantGoldAmount = 50000;

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
    private const string IdDevMace = "dev_destroyer_mace";

    private void Awake()
    {
        TryResolveRefsByName();
        TryResolveFeedbackRefs();
        TryResolveItemDefsFromDebugGiveItems();

        Bind(showHideButton, ToggleRowGroupClicked);
        Bind(reportBugButton, OnReportBugClicked);
        Bind(maxLevelButton, OnMaxLevelClicked);
        Bind(minLevelButton, OnMinLevelClicked);
        Bind(plusLevelButton, OnPlusLevelClicked);
        Bind(minusLevelButton, OnMinusLevelClicked);
        Bind(addResourcesButton, OnAddResourcesClicked);
        Bind(addGoldButton, OnAddGoldClicked);
        Bind(devWeaponButton, OnDevWeaponClicked);
        Bind(skipTutorialButton, OnSkipTutorialClicked);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyRowGroupVisible(false, forceLabel: true);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene _, LoadSceneMode __) =>
        ApplyRowGroupVisible(false, forceLabel: true);

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

    private void OnMaxLevelClicked()
    {
        SkillsManager sm = ResolveSkillsManager();
        if (!sm)
        {
            Debug.LogWarning("[DevTestingPanel] No SkillsManager.");
            return;
        }

        sm.DebugSetAllTrackedSkillsLevel(50);
        ShowPopup("DEBUG ALL SKILLS → 50", new Color(0.4f, 1f, 0.55f));
    }

    private void OnMinLevelClicked()
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

    private void OnPlusLevelClicked()
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

    private void OnMinusLevelClicked()
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

    private void OnAddResourcesClicked()
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
        Debug.Log($"[DevTestingPanel] +{n} fish/logs/stone, +{m} linen/leather.");
    }

    private void OnAddGoldClicked()
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

    private void OnDevWeaponClicked()
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
        devWeaponButton ??= FindButtonUnderRow("DevWeapon");
        skipTutorialButton ??= FindButtonUnderRow("SkipTutorial");
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
