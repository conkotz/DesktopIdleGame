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
    [SerializeField] private string gameplaySceneName = "Resource_Map_01";

    private const string FallbackGameplaySceneName = "Resource_Map_01";

    [Header("Slot Info Labels (optional)")]
    [Tooltip("If not assigned, we'll auto-find Slot1Card/InfoLabel and Slot2Card/InfoLabel.")]
    [SerializeField] private TMP_Text slot0InfoText;
    [SerializeField] private TMP_Text slot1InfoText;
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

    private void Awake()
    {
        AutoBindInfoLabelsIfNeeded();
        AutoBindSlotButtonsIfNeeded();
        AutoBindNameSelectIfNeeded();
        BindConfirmPopupOnce();
        BindNameSelectOnce();
    }

    private void OnEnable()
    {
        AutoBindSlotButtonsIfNeeded();
        RefreshSlotInfoUI();
        RefreshSlotButtonsState();
    }

    private void BindConfirmPopupOnce()
    {
        if (_confirmPopupBound) return;
        _confirmPopupBound = true;

        if (confirmCancelButton)
        {
            confirmCancelButton.onClick.RemoveListener(CloseConfirmNewGamePopup);
            confirmCancelButton.onClick.AddListener(CloseConfirmNewGamePopup);
        }

        if (confirmConfirmButton)
        {
            confirmConfirmButton.onClick.RemoveListener(ConfirmNewGameNow);
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

        Debug.Log($"[SaveSlotMenuUI] Confirmed New Game. slot={slotIndex}");

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
            var slot1Card = GameObject.Find("Slot1Card");
            if (slot1Card)
                slot0ResumeButton = FindResumeLikeButtonUnder(slot1Card.transform);
        }

        if (!slot1ResumeButton)
        {
            var slot2Card = GameObject.Find("Slot2Card");
            if (slot2Card)
                slot1ResumeButton = FindResumeLikeButtonUnder(slot2Card.transform);
        }

        if (slot0ResumeButton != null && slot0ResumeButton == slot1ResumeButton)
            slot1ResumeButton = null;
    }

    private void AutoBindNameSelectIfNeeded()
    {
        if (!playerNameSelectRoot)
        {
            var go = GameObject.Find("PlayerNameSelect");
            if (go) playerNameSelectRoot = go;
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
        var card = GameObject.Find(cardRootName);
        if (!card) return null;

        var info = card.transform.Find("InfoLabel");
        if (!info) return null;

        return info.GetComponent<TMP_Text>();
    }

    private Button FindButtonUnder(string cardRootName, string relativePath)
    {
        var card = GameObject.Find(cardRootName);
        if (!card) return null;
        var node = card.transform.Find(relativePath);
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

    private void BindNameSelectOnce()
    {
        if (_nameUiBound) return;
        _nameUiBound = true;

        if (playerNameSelectRoot)
            playerNameSelectRoot.SetActive(false);

        if (playerNameInputField)
        {
            playerNameInputField.characterLimit = Mathf.Clamp(playerNameMaxLength, 3, 8);
            playerNameInputField.onValueChanged.AddListener(_ => RefreshNameStartButtonState());
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
        SetSlotInfoText(0, slot0InfoText);
        SetSlotInfoText(1, slot1InfoText);
    }

    private void RefreshSlotButtonsState()
    {
        if (slot0ResumeButton)
            slot0ResumeButton.gameObject.SetActive(SaveSlotManager.HasSave(0));

        if (slot1ResumeButton)
            slot1ResumeButton.gameObject.SetActive(SaveSlotManager.HasSave(1));
    }

    private void SetSlotInfoText(int slotIndex, TMP_Text label)
    {
        if (!label) return;

        // Prefer meta header if present.
        if (SaveSlotManager.TryReadHeader(slotIndex, out SaveGameHeader header) && header != null && header.hasSave)
        {
            string scene = string.IsNullOrWhiteSpace(header.sceneName) ? "Unknown" : header.sceneName;
            string last = FormatSavedTimeForDisplay(header.lastSavedUtc);
            string cpText = header.combatPower > 0 ? $"CP: {header.combatPower}" : "CP: --";
            string playerName = string.IsNullOrWhiteSpace(header.characterName) ? "Adventurer" : header.characterName;
            label.text =
                $"{playerName}\n" +
                $"{cpText}  •  Gold {Mathf.Max(0, header.gold)}\n" +
                $"{scene}\n" +
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
            return utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
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

        Debug.Log($"[SaveSlotMenuUI] Continue/Load selected. slot={slotIndex}");

        SaveSlotManager.SetActiveSlot(slotIndex);
        SaveSlotManager.SetPendingStartMode(SaveSlotManager.SlotStartMode.LoadGame);
        if (CanLoadGameplayScene())
            SceneManager.LoadScene(gameplaySceneName);
    }

    public void OnClickNewGame(int slotIndex)
    {
        bool hasSave = SaveSlotManager.HasSave(slotIndex);
        Debug.Log($"[SaveSlotMenuUI] New Game clicked. slot={slotIndex} hasSave={hasSave}");

        if (hasSave)
        {
            OpenConfirmNewGamePopup(slotIndex);
            return;
        }

        OpenPlayerNameSelect(slotIndex);
    }

    public void OnClickDeleteSlot(int slotIndex)
    {
        Debug.Log($"Delete slot {slotIndex}");

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
        SaveSlotManager.SetPendingStartMode(SaveSlotManager.SlotStartMode.NewGame);
        SaveSlotManager.SetPendingNewGamePlayerName(sanitizedName);

        ClosePlayerNameSelect();

        if (CanLoadGameplayScene())
            SceneManager.LoadScene(gameplaySceneName);
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

        return sb.ToString().Trim();
    }
}