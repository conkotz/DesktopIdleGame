using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Dev bug / suggestion flow: opens a text entry panel, appends reports to a file on the desktop, then shows a thanks panel.
/// Add alongside <see cref="DevTestingPanelUI"/> on the same root (e.g. DevTestingPanel) that contains <c>BugSuggestionReportPanel</c> and <c>ThanksPanel</c> children.
/// Dev toolbar shortcuts F1–F7 (same order as RowGroup buttons with Hotkey labels) call into <see cref="DevTestingPanelUI"/>; suppressed while the bug report text field is focused.
/// </summary>
public class BugAndSuggestionReportUI : MonoBehaviour
{
    public const string ReportFileName = "BugAndSuggestionsReport.txt";

    [Header("Dev toolbar hotkeys (F1–F7)")]
    [Tooltip("Defaults to a DevTestingPanelUI on this object, parent, or in the scene.")]
    [SerializeField] private DevTestingPanelUI devTestingPanel;

    [Header("Panels (optional — resolved as children by name if unset)")]
    [SerializeField] private GameObject bugSuggestionReportPanel;
    [SerializeField] private GameObject thanksPanel;

    [Header("Bug panel (optional — resolved under bug panel if unset)")]
    [SerializeField] private TMP_InputField reportInputField;
    [SerializeField] private Button saveReportButton;

    [Header("Thanks panel (optional)")]
    [SerializeField] private Button thanksCloseButton;
    [SerializeField] private TMP_Text thanksDetailsText;

    private string _thanksDetailsTemplate;
    private static bool _warnedMissingThanksCloseButton;

    private void Awake()
    {
        TryResolveHierarchy();
        CacheDevTestingPanel();
        CacheThanksTemplate();

        if (bugSuggestionReportPanel)
            bugSuggestionReportPanel.SetActive(false);
        if (thanksPanel)
            thanksPanel.SetActive(false);

        Bind(saveReportButton, OnSaveReportClicked);
        WireThanksCloseButton();
    }

    private void Start()
    {
        TryResolveHierarchy();
        WireThanksCloseButton();
    }

    private void OnEnable()
    {
        TryResolveHierarchy();
        CacheDevTestingPanel();
        WireThanksCloseButton();
    }

    private void Update()
    {
        CacheDevTestingPanel();
        if (!devTestingPanel)
            return;

        if (ShouldSuppressDevHotkeys())
            return;

        // F1–F7 match RowGroup hotkey labels left-to-right after Report Bug: +1, −1, Min, Max, Resources, Gold, Dev Mace.

        if (Input.GetKeyDown(KeyCode.F1))
            devTestingPanel.DevTesting_ApplyPlusOneAllSkills();
        else if (Input.GetKeyDown(KeyCode.F2))
            devTestingPanel.DevTesting_ApplyMinusOneAllSkills();
        else if (Input.GetKeyDown(KeyCode.F3))
            devTestingPanel.DevTesting_ApplyMinLevelAllSkills();
        else if (Input.GetKeyDown(KeyCode.F4))
            devTestingPanel.DevTesting_ApplyMaxLevelAllSkills();
        else if (Input.GetKeyDown(KeyCode.F5))
            devTestingPanel.DevTesting_ApplyAddResourcePack();
        else if (Input.GetKeyDown(KeyCode.F6))
            devTestingPanel.DevTesting_ApplyAddGold();
        else if (Input.GetKeyDown(KeyCode.F7))
            devTestingPanel.DevTesting_ApplyDevWeapon();
    }

    private void CacheDevTestingPanel()
    {
        if (devTestingPanel)
            return;
        devTestingPanel = GetComponent<DevTestingPanelUI>();
        if (!devTestingPanel)
            devTestingPanel = GetComponentInParent<DevTestingPanelUI>();
        if (!devTestingPanel)
            devTestingPanel = FindFirstObjectByType<DevTestingPanelUI>(FindObjectsInactive.Include);
    }

    private bool ShouldSuppressDevHotkeys()
    {
        TryResolveHierarchy();
        return reportInputField != null && reportInputField.isFocused;
    }

    /// <summary>Called from <see cref="DevTestingPanelUI"/> Report Bug button: open panel or hide it while keeping draft text.</summary>
    public void ToggleBugReportPanel()
    {
        TryResolveHierarchy();
        WireThanksCloseButton();
        if (!bugSuggestionReportPanel)
            return;

        bool opening = !bugSuggestionReportPanel.activeSelf;
        if (opening)
        {
            if (thanksPanel)
                thanksPanel.SetActive(false);
            bugSuggestionReportPanel.SetActive(true);
            if (reportInputField)
            {
                reportInputField.ActivateInputField();
                reportInputField.Select();
            }
        }
        else
        {
            bugSuggestionReportPanel.SetActive(false);
            // Intentionally do not clear reportInputField.text — draft is kept for next open.
        }
    }

    /// <summary>Force-open the bug panel (hides thanks). Prefer <see cref="ToggleBugReportPanel"/> from the toolbar.</summary>
    public void OpenReportPanel() => ToggleBugReportPanelWhenClosed();

    private void ToggleBugReportPanelWhenClosed()
    {
        TryResolveHierarchy();
        if (bugSuggestionReportPanel && !bugSuggestionReportPanel.activeSelf)
            ToggleBugReportPanel();
        else if (bugSuggestionReportPanel && bugSuggestionReportPanel.activeSelf)
        {
            // Already open — no-op so we do not close on legacy OpenReportPanel callers.
            if (reportInputField)
            {
                reportInputField.ActivateInputField();
                reportInputField.Select();
            }
        }
    }

    private void OnSaveReportClicked()
    {
        TryResolveHierarchy();
        if (!reportInputField)
        {
            Debug.LogWarning("[BugAndSuggestionReportUI] No TMP_InputField assigned or found.");
            return;
        }

        string message = reportInputField.text != null ? reportInputField.text.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(message))
        {
            Debug.LogWarning("[BugAndSuggestionReportUI] Report text is empty — nothing saved.");
            return;
        }

        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string fullPath = Path.Combine(desktop, ReportFileName);
        string location = BuildLocationSummary();
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        try
        {
            var block = new StringBuilder();
            block.AppendLine($"--- {timestamp} ---");
            block.AppendLine($"Location: {location}");
            block.AppendLine("Message:");
            block.AppendLine(message);
            block.AppendLine();
            File.AppendAllText(fullPath, block.ToString(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BugAndSuggestionReportUI] Failed to write report file: {ex.Message}");
            return;
        }

        if (bugSuggestionReportPanel)
            bugSuggestionReportPanel.SetActive(false);
        reportInputField.text = string.Empty;

        ApplyThanksPanelPlaceholders(fullPath);
        if (thanksPanel)
        {
            thanksPanel.SetActive(true);
            WireThanksCloseButton();
            if (thanksCloseButton)
                thanksCloseButton.transform.SetAsLastSibling();
        }
    }

    private void OnThanksCloseClicked()
    {
        if (thanksPanel)
            thanksPanel.SetActive(false);
        RestoreThanksDetailsToTemplate();
    }

    private void RestoreThanksDetailsToTemplate()
    {
        if (thanksDetailsText && !string.IsNullOrEmpty(_thanksDetailsTemplate))
            thanksDetailsText.text = _thanksDetailsTemplate;
    }

    private void ApplyThanksPanelPlaceholders(string fullPathToReportFile)
    {
        if (!thanksDetailsText)
            return;

        string template = string.IsNullOrEmpty(_thanksDetailsTemplate)
            ? thanksDetailsText.text
            : _thanksDetailsTemplate;

        string fileName = Path.GetFileName(fullPathToReportFile);

        string body = template
            .Replace("{userssavepath}", fullPathToReportFile, StringComparison.OrdinalIgnoreCase)
            .Replace("{usersavepath}", fullPathToReportFile, StringComparison.OrdinalIgnoreCase)
            .Replace("{filename}", fileName, StringComparison.OrdinalIgnoreCase);

        thanksDetailsText.text = body;
    }

    private void CacheThanksTemplate()
    {
        if (thanksDetailsText && string.IsNullOrEmpty(_thanksDetailsTemplate))
            _thanksDetailsTemplate = thanksDetailsText.text ?? string.Empty;
    }

    private static string BuildLocationSummary()
    {
        var sb = new StringBuilder();
        sb.Append("Scene=").Append(SceneManager.GetActiveScene().name);

        MapNodeDefinition def = null;
        if (GameplayLevelBootstrapper.Instance != null)
            def = GameplayLevelBootstrapper.Instance.ActiveDefinition;
        if (def != null)
        {
            sb.Append(" | Map=");
            sb.Append(string.IsNullOrWhiteSpace(def.displayName) ? def.nodeId : def.displayName);
            sb.Append(" (nodeId=").Append(def.nodeId).Append(')');
        }
        else if (ActiveLevelContext.Current != null)
        {
            sb.Append(" | PendingMapNodeId=").Append(ActiveLevelContext.Current.nodeId);
        }

        PlayerController pc = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (pc != null)
        {
            Vector3 p = pc.transform.position;
            sb.Append(" | WorldPos=(")
                .Append(p.x.ToString("F1")).Append(',')
                .Append(p.y.ToString("F1")).Append(',')
                .Append(p.z.ToString("F1")).Append(')');
        }

        return sb.ToString();
    }

    private void TryResolveHierarchy()
    {
        if (!bugSuggestionReportPanel &&
            string.Equals(gameObject.name, "BugSuggestionReportPanel", StringComparison.OrdinalIgnoreCase))
            bugSuggestionReportPanel = gameObject;

        if (!thanksPanel && string.Equals(gameObject.name, "ThanksPanel", StringComparison.OrdinalIgnoreCase))
            thanksPanel = gameObject;

        if (!bugSuggestionReportPanel)
        {
            Transform t = FindNamedObjectInUiSubtree("BugSuggestionReportPanel");
            if (t) bugSuggestionReportPanel = t.gameObject;
        }

        if (!thanksPanel)
        {
            Transform t = FindNamedObjectInUiSubtree("ThanksPanel");
            if (t) thanksPanel = t.gameObject;
        }

        if (bugSuggestionReportPanel)
        {
            if (!reportInputField)
                reportInputField = bugSuggestionReportPanel.GetComponentInChildren<TMP_InputField>(true);
            if (!saveReportButton)
                saveReportButton = FindComponentInChildrenByName<Button>(bugSuggestionReportPanel.transform, "SaveButton");
        }

        if (thanksPanel)
        {
            if (!thanksCloseButton)
                ResolveThanksCloseButton();
            if (!thanksDetailsText)
            {
                Transform row = FindDescendantTransformByName(thanksPanel.transform, "TextRow");
                if (row)
                {
                    Transform dt = FindDescendantTransformByName(row, "DetailsText");
                    if (dt)
                        thanksDetailsText = dt.GetComponent<TMP_Text>();
                }

                if (!thanksDetailsText)
                    thanksDetailsText = FindComponentInChildrenByName<TMP_Text>(thanksPanel.transform, "DetailsText");
            }
        }

        ApplyReportInputMultiLineSettings(reportInputField);
    }

    private static void ApplyReportInputMultiLineSettings(TMP_InputField field)
    {
        if (!field)
            return;
        field.lineType = TMP_InputField.LineType.MultiLineNewline;
    }

    private void ResolveThanksCloseButton()
    {
        if (thanksCloseButton || thanksPanel == null)
            return;

        Transform closeRoot = FindDescendantTransformByName(thanksPanel.transform, "CloseButton");
        if (closeRoot != null)
        {
            thanksCloseButton = closeRoot.GetComponent<Button>();
            if (!thanksCloseButton)
                thanksCloseButton = closeRoot.GetComponentInChildren<Button>(true);
        }

        if (!thanksCloseButton)
        {
            Button[] buttons = thanksPanel.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button b = buttons[i];
                if (b == null)
                    continue;
                if (b.gameObject.name.IndexOf("close", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    thanksCloseButton = b;
                    break;
                }
            }
        }
    }

    private void WireThanksCloseButton()
    {
        ResolveThanksCloseButton();
        if (thanksCloseButton)
        {
            Graphic g = thanksCloseButton.targetGraphic;
            if (!g)
                g = thanksCloseButton.GetComponent<Graphic>();
            if (g)
                g.raycastTarget = true;
            Bind(thanksCloseButton, OnThanksCloseClicked);
        }
        else if (thanksPanel && !_warnedMissingThanksCloseButton)
        {
            _warnedMissingThanksCloseButton = true;
            Debug.LogWarning(
                "[BugAndSuggestionReportUI] No close Button found under ThanksPanel. Assign 'Thanks Close Button' on BugAndSuggestionReportUI, or add a Button named CloseButton (under any depth). " +
                "Empty On Click () in the Inspector is normal when wiring is done from code.");
        }
    }

    private static void Bind(Button b, UnityAction handler)
    {
        if (!b || handler == null)
            return;
        b.onClick.RemoveListener(handler);
        b.onClick.AddListener(handler);
    }

    /// <summary>Walks up from this component so siblings (e.g. ThanksPanel next to BugSuggestionReportPanel) are discoverable.</summary>
    private Transform FindNamedObjectInUiSubtree(string wantedName)
    {
        if (string.IsNullOrWhiteSpace(wantedName))
            return null;
        Transform probe = transform;
        for (int i = 0; i < 16 && probe != null; i++)
        {
            Transform hit = FindDescendantTransformByName(probe, wantedName);
            if (hit)
                return hit;
            probe = probe.parent;
        }

        return FindDescendantTransformByName(transform.root, wantedName);
    }

    private static Transform FindDescendantTransformByName(Transform root, string wanted)
    {
        if (!root || string.IsNullOrWhiteSpace(wanted))
            return null;
        var queue = new Queue<Transform>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            Transform t = queue.Dequeue();
            if (t && string.Equals(t.name, wanted, StringComparison.OrdinalIgnoreCase))
                return t;
            for (int c = 0; c < t.childCount; c++)
            {
                Transform child = t.GetChild(c);
                if (child)
                    queue.Enqueue(child);
            }
        }

        return null;
    }

    private static T FindComponentInChildrenByName<T>(Transform root, string wantedName) where T : Component
    {
        if (!root || string.IsNullOrWhiteSpace(wantedName))
            return null;
        T[] all = root.GetComponentsInChildren<T>(true);
        for (int i = 0; i < all.Length; i++)
        {
            T c = all[i];
            if (c != null && string.Equals(c.gameObject.name, wantedName, StringComparison.OrdinalIgnoreCase))
                return c;
        }

        return null;
    }
}
