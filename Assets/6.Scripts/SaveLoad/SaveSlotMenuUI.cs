using System;
using System.IO;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SaveSlotMenuUI : MonoBehaviour
{
    [Tooltip("Gameplay scene to enter after selecting/creating a slot.")]
    [SerializeField] private string gameplaySceneName = "GamePlay";

    private const string FallbackGameplaySceneName = "GamePlay";

    [Header("Slot Info Labels (optional)")]
    [Tooltip("If not assigned, we'll auto-find Slot1Card/InfoLabel and Slot2Card/InfoLabel.")]
    [SerializeField] private TMP_Text slot0InfoText;
    [SerializeField] private TMP_Text slot1InfoText;
    [Tooltip("Optional title labels (Save Slot 1/2). If empty, auto-finds Slot1Card/SlotLabel and Slot2Card/SlotLabel.")]
    [SerializeField] private TMP_Text slot0TitleText;
    [SerializeField] private TMP_Text slot1TitleText;
    [Tooltip("Per-slot Resume / LoadGameButton. Leave empty to auto-find Slot1Card/Slot2Card → ButtonsRow/LoadGameButton.")]
    [SerializeField] private Button slot0ResumeButton;
    [SerializeField] private Button slot1ResumeButton;

    [Header("Confirm New Game Popup")]
    [SerializeField] private GameObject confirmNewGamePopupRoot;
    [SerializeField] private TMP_Text confirmTitleText;
    [SerializeField] private TMP_Text confirmBodyText;
    [SerializeField] private TMP_Text confirmInfoLabelText;
    [SerializeField] private UnityEngine.UI.Button confirmCancelButton;
    [SerializeField] private UnityEngine.UI.Button confirmConfirmButton;

    [Header("Player Name Select (New Game)")]
    [SerializeField] private GameObject playerNameSelectRoot;
    [SerializeField] private TMP_InputField playerNameInputField;
    [SerializeField] private Button playerNameStartButton;
    [SerializeField] private Button playerNameCancelButton;
    [SerializeField] private TMP_Text playerNameErrorText;
    [SerializeField, Min(3)] private int playerNameMaxLength = 8;

    private int _pendingNewGameSlotIndex = -1;
    private int _pendingNameSlotIndex = -1;
    private bool _confirmPopupBound;
    private bool _nameUiBound;

    private void Update()
    {
        if (playerNameSelectRoot == null || !playerNameSelectRoot.activeSelf)
            return;
        if (playerNameStartButton == null || !playerNameStartButton.interactable)
            return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            OnClickConfirmPlayerNameStartGame();
    }

    private void Awake()
    {
        AutoBindInfoLabelsIfNeeded();
        AutoBindTitleLabelsIfNeeded();
        AutoBindSlotButtonsIfNeeded();
        EnsureDoubleClickResumeOnSlotCards();
        AutoBindNameSelectIfNeeded();
        BindConfirmPopupOnce();
        BindNameSelectOnce();
        RewireResumeSlotButtons();
        RewireNewGameSlotButtons();
    }

    private void OnEnable()
    {
        AutoBindTitleLabelsIfNeeded();
        RefreshSlotInfoUI();

        if (ShouldRebindSlotResumeButtonsForVisibleBootstrap())
            RebindSlotResumeButtonsStrict();
        else
            AutoBindSlotButtonsIfNeeded();

        RewireResumeSlotButtons();
        RewireNewGameSlotButtons();
        RefreshSlotButtonsState();
    }

    private bool ShouldRebindSlotResumeButtonsForVisibleBootstrap()
    {
        if (!TryGetLoadedBootstrapScene(out Scene bs))
            return false;

        if (!slot0ResumeButton || !slot1ResumeButton)
            return true;

        return slot0ResumeButton.gameObject.scene != bs || slot1ResumeButton.gameObject.scene != bs;
    }

    /// <summary>
    /// Re-paints slot rows from disk. Call when the Bootstrap scene is shown again: this component often lives on
    /// DontDestroyOnLoad with <see cref="SaveManager"/>, so <see cref="OnEnable"/> may not run on return from gameplay.
    /// </summary>
    public void RefreshSlotsFromDisk()
    {
        // Slot rows live in the Bootstrap *scene*; this menu lives on DDOL BootstrapMain. After a gameplay round-trip,
        // serialized refs can still point at destroyed objects or the wrong SlotNCard — always rebind from the loaded Bootstrap scene.
        DiscardRuntimeSlotUiRefs();
        InvalidateBootstrapSceneUiRefsIfStale();

        AutoBindInfoLabelsIfNeeded();
        RebindSlotResumeButtonsStrict();
        AutoBindTitleLabelsIfNeeded();
        EnsureDoubleClickResumeOnSlotCards();
        AutoBindNameSelectIfNeeded();
        BindConfirmPopupOnce();
        BindNameSelectOnce();

        RefreshSlotInfoUI();
        RewireResumeSlotButtons();
        RewireNewGameSlotButtons();
        RefreshSlotButtonsState();
    }

    private void DiscardRuntimeSlotUiRefs()
    {
        slot0ResumeButton = null;
        slot1ResumeButton = null;
        slot0InfoText = null;
        slot1InfoText = null;
        slot0TitleText = null;
        slot1TitleText = null;
    }

    private void InvalidateBootstrapSceneUiRefsIfStale()
    {
        if (!TryGetLoadedBootstrapScene(out Scene bs))
            return;

        bool Wrong(Component c) => c && c.gameObject.scene != bs;

        if (Wrong(confirmTitleText)) confirmTitleText = null;
        if (Wrong(confirmBodyText)) confirmBodyText = null;
        if (Wrong(confirmInfoLabelText)) confirmInfoLabelText = null;
        if (Wrong(confirmCancelButton)) confirmCancelButton = null;
        if (Wrong(confirmConfirmButton)) confirmConfirmButton = null;

        if (confirmNewGamePopupRoot && confirmNewGamePopupRoot.scene != bs)
            confirmNewGamePopupRoot = null;

        if (Wrong(playerNameInputField)) playerNameInputField = null;
        if (Wrong(playerNameStartButton)) playerNameStartButton = null;
        if (Wrong(playerNameCancelButton)) playerNameCancelButton = null;
        if (Wrong(playerNameErrorText)) playerNameErrorText = null;

        if (playerNameSelectRoot && playerNameSelectRoot.scene != bs)
            playerNameSelectRoot = null;

        _confirmPopupBound = false;
        _nameUiBound = false;
    }

    public static bool TryGetLoadedBootstrapScene(out Scene scene)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s.isLoaded && s.name.Equals("Bootstrap", StringComparison.OrdinalIgnoreCase))
            {
                scene = s;
                return true;
            }
        }

        scene = default;
        return false;
    }

    private static Transform FindNamedTransformInBootstrapScene(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;
        if (!TryGetLoadedBootstrapScene(out Scene bs))
            return null;

        GameObject[] roots = bs.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            Transform[] trs = roots[r].GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < trs.Length; i++)
            {
                if (trs[i] && trs[i].name == objectName)
                    return trs[i];
            }
        }

        return null;
    }

    /// <summary>Prefer slot cards under the active Bootstrap scene (not DDOL / other canvases).</summary>
    private static Transform FindCardTransformForSaveMenu(string cardName)
    {
        Transform t = FindNamedTransformInBootstrapScene(cardName);
        return t ? t : FindCardTransformIncludingInactive(cardName);
    }

    /// <summary>
    /// Slot buttons live in the Bootstrap scene; this menu often persists on DDOL. Scene reload replaces buttons but
    /// leaves their <see cref="Button.onClick"/> pointing at destroyed targets — rebind to this instance every refresh.
    /// </summary>
    private void RewireResumeSlotButtons()
    {
        if (slot0ResumeButton)
        {
            // Clear inspector + stale targets (second Bootstrap creates a duplicate menu that is destroyed;
            // buttons still pointed at that destroyed instance).
            slot0ResumeButton.onClick.RemoveAllListeners();
            slot0ResumeButton.onClick.AddListener(HandleResumeSlot0Clicked);
        }

        if (slot1ResumeButton)
        {
            slot1ResumeButton.onClick.RemoveAllListeners();
            slot1ResumeButton.onClick.AddListener(HandleResumeSlot1Clicked);
        }
    }

    private void HandleResumeSlot0Clicked() => OnClickLoadSlot(0);

    private void HandleResumeSlot1Clicked() => OnClickLoadSlot(1);

    private void RewireNewGameSlotButtons()
    {
        Button b0 = FindButtonUnder("Slot1Card", "ButtonsRow/NewGameButton");
        Button b1 = FindButtonUnder("Slot2Card", "ButtonsRow/NewGameButton");

        if (b0)
        {
            b0.onClick.RemoveAllListeners();
            b0.onClick.AddListener(HandleNewGameSlot0Clicked);
        }

        if (b1)
        {
            b1.onClick.RemoveAllListeners();
            b1.onClick.AddListener(HandleNewGameSlot1Clicked);
        }
    }

    private void HandleNewGameSlot0Clicked() => OnClickNewGame(0);

    private void HandleNewGameSlot1Clicked() => OnClickNewGame(1);

    private void BindConfirmPopupOnce()
    {
        if (_confirmPopupBound) return;
        _confirmPopupBound = true;

        if (!confirmNewGamePopupRoot)
        {
            Transform t = FindNamedTransformInBootstrapScene("ConfirmNewGamePopup");
            if (t)
                confirmNewGamePopupRoot = t.gameObject;
        }

        AutoBindConfirmPopupChildrenIfNeeded();

        if (confirmCancelButton)
        {
            confirmCancelButton.onClick.RemoveAllListeners();
            confirmCancelButton.onClick.AddListener(CloseConfirmNewGamePopup);
        }

        if (confirmConfirmButton)
        {
            confirmConfirmButton.onClick.RemoveAllListeners();
            confirmConfirmButton.onClick.AddListener(ConfirmNewGameNow);
        }

        // Ensure default state is closed.
        if (confirmNewGamePopupRoot)
            confirmNewGamePopupRoot.SetActive(false);
    }

    private void OpenConfirmNewGamePopup(int slotIndex)
    {
        BindConfirmPopupOnce();

        _pendingNewGameSlotIndex = slotIndex;

        bool hasSave = File.Exists(SaveSlotManager.GetSavePath(slotIndex));

        if (confirmTitleText)
            confirmTitleText.text = "Start New Game?";

        if (confirmBodyText)
            confirmBodyText.text = "Are you sure you want to start a new game? This will delete the current save file.";

        if (confirmInfoLabelText)
            confirmInfoLabelText.text = hasSave
                ? $"Slot: {slotIndex + 1}  •  Existing save: YES"
                : $"Slot: {slotIndex + 1}  •  Existing save: NO";

        if (confirmNewGamePopupRoot)
            confirmNewGamePopupRoot.SetActive(true);
    }

    private void CloseConfirmNewGamePopup()
    {
        _pendingNewGameSlotIndex = -1;
        if (confirmNewGamePopupRoot)
            confirmNewGamePopupRoot.SetActive(false);
    }

    private void ConfirmNewGameNow()
    {
        int slotIndex = _pendingNewGameSlotIndex;
        CloseConfirmNewGamePopup();

        if (slotIndex < 0)
            return;

        SaveSlotManager.DeleteSlot(slotIndex);
        RefreshSlotInfoUI();
        RefreshSlotButtonsState();
        OpenPlayerNameSelect(slotIndex);
    }

    private void AutoBindInfoLabelsIfNeeded()
    {
        if (slot0InfoText && slot1InfoText) return;

        // These names match the objects in your Bootstrap UI.
        slot0InfoText ??= FindInfoLabelUnder("Slot1Card");
        slot1InfoText ??= FindInfoLabelUnder("Slot2Card");
    }

    private void AutoBindSlotButtonsIfNeeded()
    {
        slot0ResumeButton ??= FindButtonUnder("Slot1Card", "ButtonsRow/ResumeGame");
        slot1ResumeButton ??= FindButtonUnder("Slot2Card", "ButtonsRow/ResumeGame");
        slot0ResumeButton ??= FindButtonUnder("Slot1Card", "ButtonsRow/LoadGameButton");
        slot1ResumeButton ??= FindButtonUnder("Slot2Card", "ButtonsRow/LoadGameButton");
        if (!slot0ResumeButton)
        {
            Transform c = FindCardTransformForSaveMenu("Slot1Card");
            if (c)
                slot0ResumeButton = FindResumeLikeButtonUnder(c);
        }

        if (!slot1ResumeButton)
        {
            Transform c = FindCardTransformForSaveMenu("Slot2Card");
            if (c)
                slot1ResumeButton = FindResumeLikeButtonUnder(c);
        }

        if (slot0ResumeButton != null && slot0ResumeButton == slot1ResumeButton)
            slot1ResumeButton = null;

    }

    /// <summary>
    /// Always binds resume/load buttons from the named slot cards (no ??=). Avoids picking the wrong Button when
    /// <see cref="FindResumeLikeButtonUnder"/> iteration order differs from layout order.
    /// </summary>
    private void RebindSlotResumeButtonsStrict()
    {
        Transform c0 = FindCardTransformForSaveMenu("Slot1Card");
        Transform c1 = FindCardTransformForSaveMenu("Slot2Card");
        slot0ResumeButton = FindResumeOrLoadGameButtonOnCard(c0);
        slot1ResumeButton = FindResumeOrLoadGameButtonOnCard(c1);
        if (slot0ResumeButton != null && slot0ResumeButton == slot1ResumeButton)
            slot1ResumeButton = null;
    }

    private static Button FindResumeOrLoadGameButtonOnCard(Transform cardRoot)
    {
        if (!cardRoot)
            return null;

        Transform row = cardRoot.Find("ButtonsRow");
        if (!row)
            return null;

        Transform resume = row.Find("ResumeGame");
        if (resume)
        {
            Button b = resume.GetComponent<Button>();
            if (b)
                return b;
        }

        Transform load = row.Find("LoadGameButton");
        return load ? load.GetComponent<Button>() : null;
    }

    private void AutoBindTitleLabelsIfNeeded()
    {
        slot0TitleText ??= FindTitleLabelUnder("Slot1Card");
        slot1TitleText ??= FindTitleLabelUnder("Slot2Card");
    }

    /// <summary>
    /// <see cref="GameObject.Find"/> skips inactive objects; slot cards are often disabled in the scene asset.
    /// </summary>
    private void EnsureDoubleClickResumeOnSlotCards()
    {
        EnsureDoubleClickOnCard("Slot1Card", 0);
        EnsureDoubleClickOnCard("Slot2Card", 1);
    }

    private void EnsureDoubleClickOnCard(string cardName, int slotIndex)
    {
        Transform t = FindCardTransformForSaveMenu(cardName);
        if (!t)
            return;

        var dc = t.GetComponent<SaveSlotCardDoubleClickUI>();
        if (!dc)
            dc = t.gameObject.AddComponent<SaveSlotCardDoubleClickUI>();
        dc.menu = this;
        dc.slotIndex = slotIndex;
    }

    private static Transform FindCardTransformIncludingInactive(string cardName)
    {
        GameObject active = GameObject.Find(cardName);
        if (active)
            return active.transform;

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int c = 0; c < canvases.Length; c++)
        {
            if (!canvases[c])
                continue;
            Transform[] all = canvases[c].transform.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == cardName)
                    return all[i];
            }
        }

        return null;
    }

    private void AutoBindNameSelectIfNeeded()
    {
        if (!playerNameSelectRoot)
        {
            Transform t = FindNamedTransformInBootstrapScene("PlayerNameSelect");
            if (t)
                playerNameSelectRoot = t.gameObject;
            else
            {
                var go = GameObject.Find("PlayerNameSelect");
                if (go) playerNameSelectRoot = go;
            }
        }

        if (!playerNameSelectRoot)
            return;

        if (!playerNameInputField)
        {
            var t = playerNameSelectRoot.transform.Find("Panel/NameChooseField");
            if (t) playerNameInputField = t.GetComponent<TMP_InputField>();
        }

        if (!playerNameStartButton)
        {
            var t = playerNameSelectRoot.transform.Find("Panel/ButtonsRow/StartGame");
            if (t) playerNameStartButton = t.GetComponent<Button>();
        }

        if (!playerNameCancelButton)
        {
            var t = playerNameSelectRoot.transform.Find("Panel/ButtonsRow/ConfirmButton");
            if (t) playerNameCancelButton = t.GetComponent<Button>();
        }
    }

    private TMP_Text FindInfoLabelUnder(string cardRootName)
    {
        Transform card = FindCardTransformForSaveMenu(cardRootName);
        if (!card) return null;

        Transform info = card.Find("InfoLabel");
        return info ? info.GetComponent<TMP_Text>() : null;
    }

    private TMP_Text FindTitleLabelUnder(string cardRootName)
    {
        Transform card = FindCardTransformForSaveMenu(cardRootName);
        if (!card) return null;

        Transform t = card.Find("SlotLabel");
        if (t) return t.GetComponent<TMP_Text>();

        // Fallback: legacy naming
        t = card.Find("Title");
        return t ? t.GetComponent<TMP_Text>() : null;
    }

    private Button FindButtonUnder(string cardRootName, string relativePath)
    {
        Transform card = FindCardTransformForSaveMenu(cardRootName);
        if (!card) return null;
        Transform node = card.Find(relativePath);
        return node ? node.GetComponent<Button>() : null;
    }

    private Button FindResumeLikeButtonUnder(Transform root)
    {
        if (root == null)
            return null;

        var buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            var b = buttons[i];
            if (!b) continue;

            string n = b.name.ToLowerInvariant();
            if (n.Contains("resume") || n.Contains("loadgame"))
                return b;

            var tmp = b.GetComponentInChildren<TMP_Text>(true);
            if (tmp == null) continue;

            string label = (tmp.text ?? "").Trim().ToLowerInvariant();
            if (label == "resume game" || label == "load game")
                return b;
        }

        return null;
    }

    private void AutoBindConfirmPopupChildrenIfNeeded()
    {
        if (!confirmNewGamePopupRoot)
            return;

        Transform root = confirmNewGamePopupRoot.transform;
        confirmCancelButton ??= FindDeepChildButtonByGameObjectName(root, "CancelButton");
        confirmConfirmButton ??= FindDeepChildButtonByGameObjectName(root, "ConfirmButton");
        confirmTitleText ??= FindDeepChildTmpByGameObjectName(root, "TitleText");
        confirmBodyText ??= FindDeepChildTmpByGameObjectName(root, "BodyText");
        confirmInfoLabelText ??= FindDeepChildTmpByGameObjectName(root, "InfoLabel");
        confirmInfoLabelText ??= FindDeepChildTmpByGameObjectName(root, "Info Label");
    }

    private static Button FindDeepChildButtonByGameObjectName(Transform root, string goName)
    {
        if (!root || string.IsNullOrEmpty(goName))
            return null;
        Transform[] trs = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
        {
            if (trs[i] && trs[i].name == goName)
                return trs[i].GetComponent<Button>();
        }

        return null;
    }

    private static TMP_Text FindDeepChildTmpByGameObjectName(Transform root, string goName)
    {
        if (!root || string.IsNullOrEmpty(goName))
            return null;
        Transform[] trs = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
        {
            if (trs[i] && trs[i].name == goName)
                return trs[i].GetComponent<TMP_Text>();
        }

        return null;
    }

    private void BindNameSelectOnce()
    {
        if (_nameUiBound) return;
        _nameUiBound = true;

        if (playerNameSelectRoot)
            playerNameSelectRoot.SetActive(false);

        if (playerNameInputField)
        {
            playerNameInputField.characterLimit = Mathf.Clamp(playerNameMaxLength, 3, 8);
            playerNameInputField.onValueChanged.RemoveAllListeners();
            playerNameInputField.onValueChanged.AddListener(_ =>
            {
                EnsureFirstLetterOfNameDraftIsCapital();
                RefreshNameStartButtonState();
            });
        }

        if (playerNameStartButton)
        {
            playerNameStartButton.onClick.RemoveAllListeners();
            playerNameStartButton.onClick.AddListener(OnClickConfirmPlayerNameStartGame);
        }

        if (playerNameCancelButton)
        {
            playerNameCancelButton.onClick.RemoveAllListeners();
            playerNameCancelButton.onClick.AddListener(ClosePlayerNameSelect);
        }
    }

    private void RefreshSlotInfoUI()
    {
        RefreshSlotTitleUI();
        SetSlotInfoText(0, slot0InfoText);
        SetSlotInfoText(1, slot1InfoText);
    }

    private void RefreshSlotTitleUI()
    {
        int lastPlayed = SaveSlotManager.GetLastPlayedSlotIndex();
        bool anySlotHasData = false;
        for (int i = 0; i < SaveSlotManager.MaxSlots; i++)
        {
            if (SaveSlotManager.HasSave(i))
            {
                anySlotHasData = true;
                break;
            }
        }

        SetSlotTitleText(0, slot0TitleText, anySlotHasData && lastPlayed == 0);
        SetSlotTitleText(1, slot1TitleText, anySlotHasData && lastPlayed == 1);
    }

    private static void SetSlotTitleText(int slotIndex, TMP_Text label, bool isLastPlayed)
    {
        if (!label) return;

        string baseText = $"Save Slot {slotIndex + 1}";
        if (isLastPlayed)
            label.text = $"{baseText}  <size=72%><color=#C8BD91>Last Played</color></size>";
        else
            label.text = baseText;
    }

    private void RefreshSlotButtonsState()
    {
        // Hide Resume when that slot has no save file (per-slot Load buttons).
        if (slot0ResumeButton)
        {
            bool has = SaveSlotManager.HasSave(0);
            slot0ResumeButton.gameObject.SetActive(has);
            if (has)
                slot0ResumeButton.interactable = true;
        }

        if (slot1ResumeButton)
        {
            bool has = SaveSlotManager.HasSave(1);
            slot1ResumeButton.gameObject.SetActive(has);
            if (has)
                slot1ResumeButton.interactable = true;
        }
    }

    private void SetSlotInfoText(int slotIndex, TMP_Text label)
    {
        if (!label) return;

        // Prefer meta header if present.
        if (SaveSlotManager.TryReadHeader(slotIndex, out SaveGameHeader header) && header != null && header.hasSave)
        {
            string location = !string.IsNullOrWhiteSpace(header.activeMapDisplayName)
                ? header.activeMapDisplayName.Trim()
                : (string.IsNullOrWhiteSpace(header.sceneName) ? "Unknown" : header.sceneName);
            string last = FormatSavedTimeForDisplay(header.lastSavedUtc);
            string cpText = header.combatPower > 0 ? $"CP: {header.combatPower}" : "CP: --";
            string playerName = string.IsNullOrWhiteSpace(header.characterName) ? "Adventurer" : header.characterName;
            label.text =
                $"{playerName}\n" +
                $"{cpText}  •  Gold {Mathf.Max(0, header.gold)}\n" +
                $"{location}\n" +
                (string.IsNullOrWhiteSpace(last) ? "" : $"Last saved {last}");
            return;
        }

        // Fallback: if save exists but no meta, show basic status.
        bool hasSave = File.Exists(SaveSlotManager.GetSavePath(slotIndex));
        label.text = hasSave
            ? "Save found\n(loading details...)"
            : "Empty\nStart a New Game";
    }

    private static string FormatSavedTimeForDisplay(string rawUtc)
    {
        if (string.IsNullOrWhiteSpace(rawUtc))
            return "";

        string trimmed = rawUtc.Trim();

        if (System.DateTime.TryParse(
                trimmed,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var utc))
        {
            string format = ToggleSettingsStore.Get(ToggleSettingId.UseTwentyFourHourTime)
                ? "yyyy-MM-dd HH:mm:ss"
                : "yyyy-MM-dd h:mm:ss tt";
            return utc.ToLocalTime().ToString(format, CultureInfo.InvariantCulture);
        }

        // Fallback if parse fails for older/custom strings.
        return trimmed;
    }

    private bool CanLoadGameplayScene()
    {
        if (string.IsNullOrWhiteSpace(gameplaySceneName))
        {
            Debug.LogError("[SaveSlotMenuUI] gameplaySceneName is empty.", this);
            return false;
        }

        // Avoid "freeze" reports when the scene name is wrong / not in Build Settings.
        if (!Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            // Common migration issue: older scenes serialized a placeholder like "GameScene".
            // Fall back safely so BootMenu still works even if inspector values are stale.
            if (!gameplaySceneName.Equals(FallbackGameplaySceneName))
            {
                if (Application.CanStreamedLevelBeLoaded(FallbackGameplaySceneName))
                {
                    Debug.LogWarning($"[SaveSlotMenuUI] Can't load scene '{gameplaySceneName}'. Falling back to '{FallbackGameplaySceneName}'. " +
                                     "Update the BootMenu inspector field when convenient.", this);
                    gameplaySceneName = FallbackGameplaySceneName;
                    return true;
                }
            }

            Debug.LogError($"[SaveSlotMenuUI] Can't load scene '{gameplaySceneName}'. Add it to Build Settings or fix the name.", this);
            return false;
        }

        return true;
    }

    public void OnClickLoadSlot(int slotIndex)
    {
        if (!SaveSlotManager.HasSave(slotIndex))
            return;

        SaveSlotManager.SetActiveSlot(slotIndex);
        SaveSlotManager.MarkSlotAsLastPlayed(slotIndex);
        SaveSlotManager.SetPendingStartMode(SaveSlotManager.SlotStartMode.LoadGame);
        if (CanLoadGameplayScene())
            SceneManager.LoadScene(gameplaySceneName);
    }

    public void OnClickNewGame(int slotIndex)
    {
        bool hasSave = SaveSlotManager.HasSave(slotIndex);

        if (hasSave)
        {
            OpenConfirmNewGamePopup(slotIndex);
            return;
        }

        OpenPlayerNameSelect(slotIndex);
    }

    public void OnClickDeleteSlot(int slotIndex)
    {
        SaveSlotManager.DeleteSlot(slotIndex);

        RefreshSlotInfoUI();
        RefreshSlotButtonsState();
    }

    private void OpenPlayerNameSelect(int slotIndex)
    {
        BindNameSelectOnce();
        _pendingNameSlotIndex = slotIndex;

        if (playerNameErrorText)
            playerNameErrorText.text = "";

        if (playerNameInputField)
        {
            playerNameInputField.text = "";
            playerNameInputField.ActivateInputField();
        }

        RefreshNameStartButtonState();

        if (playerNameSelectRoot)
            playerNameSelectRoot.SetActive(true);
    }

    private void ClosePlayerNameSelect()
    {
        _pendingNameSlotIndex = -1;
        if (playerNameSelectRoot)
            playerNameSelectRoot.SetActive(false);
    }

    private void RefreshNameStartButtonState()
    {
        if (!playerNameStartButton)
            return;

        string value = playerNameInputField ? playerNameInputField.text : "";
        string error;
        playerNameStartButton.interactable = TryValidatePlayerName(value, out _, out error);

        if (playerNameErrorText)
            playerNameErrorText.text = string.IsNullOrWhiteSpace(error) ? "" : error;
    }

    public void OnClickConfirmPlayerNameStartGame()
    {
        int slotIndex = _pendingNameSlotIndex;
        if (slotIndex < 0)
            return;

        string chosenName = playerNameInputField ? playerNameInputField.text : "";
        if (!TryValidatePlayerName(chosenName, out string sanitizedName, out string error))
        {
            if (playerNameErrorText)
                playerNameErrorText.text = error;
            RefreshNameStartButtonState();
            return;
        }

        SaveSlotManager.DeleteSlot(slotIndex);
        SaveSlotManager.SetActiveSlot(slotIndex);
        SaveSlotManager.MarkSlotAsLastPlayed(slotIndex);
        SaveSlotManager.SetPendingStartMode(SaveSlotManager.SlotStartMode.NewGame);
        SaveSlotManager.SetPendingNewGamePlayerName(sanitizedName);

        ClosePlayerNameSelect();

        if (CanLoadGameplayScene())
            SceneManager.LoadScene(gameplaySceneName);
    }

    public void OnClickSwapSlots()
    {
        if (!SaveSlotManager.SwapSlots(0, 1))
            return;

        RefreshSlotInfoUI();
        RefreshSlotButtonsState();
    }

    private bool TryValidatePlayerName(string rawInput, out string sanitizedName, out string error)
    {
        sanitizedName = SanitizePlayerName(rawInput);

        if (string.IsNullOrWhiteSpace(sanitizedName))
        {
            error = "Name required. Use letters/numbers/spaces only.";
            return false;
        }

        int maxLen = Mathf.Clamp(playerNameMaxLength, 3, 8);
        if (sanitizedName.Length > maxLen)
            sanitizedName = sanitizedName.Substring(0, maxLen);

        if (sanitizedName.Length < 3)
        {
            error = "Name must be at least 3 characters.";
            return false;
        }

        error = "";
        return true;
    }

    private static string SanitizePlayerName(string rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
            return "";

        StringBuilder sb = new StringBuilder(rawInput.Length);
        bool previousWasSpace = false;

        for (int i = 0; i < rawInput.Length; i++)
        {
            char c = rawInput[i];
            bool isAllowed = char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_';
            if (!isAllowed)
                continue;

            if (char.IsWhiteSpace(c))
            {
                if (previousWasSpace)
                    continue;

                sb.Append(' ');
                previousWasSpace = true;
            }
            else
            {
                sb.Append(c);
                previousWasSpace = false;
            }
        }

        return CapitalizeFirstLetterOfName(sb.ToString().Trim());
    }

    private static string CapitalizeFirstLetterOfName(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;

        char c = s[0];
        return char.IsLetter(c) && char.IsLower(c) ? $"{char.ToUpperInvariant(c)}{s.Substring(1)}" : s;
    }

    private void EnsureFirstLetterOfNameDraftIsCapital()
    {
        if (!playerNameInputField)
            return;

        string value = playerNameInputField.text;
        if (string.IsNullOrEmpty(value))
            return;

        string next = CapitalizeFirstLetterOfName(value);
        if (next == value)
            return;

        int caretBefore = Mathf.Clamp(playerNameInputField.caretPosition, 0, value.Length);
        playerNameInputField.SetTextWithoutNotify(next);
        playerNameInputField.caretPosition =
            Mathf.Clamp(caretBefore + (next.Length - value.Length), 0, next.Length);
    }
}