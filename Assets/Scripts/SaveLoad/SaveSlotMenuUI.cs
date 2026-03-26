using System.IO;
using System.Globalization;
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

    [Header("Confirm New Game Popup")]
    [SerializeField] private GameObject confirmNewGamePopupRoot;
    [SerializeField] private TMP_Text confirmTitleText;
    [SerializeField] private TMP_Text confirmBodyText;
    [SerializeField] private TMP_Text confirmInfoLabelText;
    [SerializeField] private UnityEngine.UI.Button confirmCancelButton;
    [SerializeField] private UnityEngine.UI.Button confirmConfirmButton;

    private int _pendingNewGameSlotIndex = -1;
    private bool _confirmPopupBound;

    private void Awake()
    {
        AutoBindInfoLabelsIfNeeded();
        BindConfirmPopupOnce();
    }

    private void OnEnable()
    {
        RefreshSlotInfoUI();
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
        SaveSlotManager.SetActiveSlot(slotIndex);
        SaveSlotManager.SetPendingStartMode(SaveSlotManager.SlotStartMode.NewGame);

        if (CanLoadGameplayScene())
            SceneManager.LoadScene(gameplaySceneName);
    }

    private void AutoBindInfoLabelsIfNeeded()
    {
        if (slot0InfoText && slot1InfoText) return;

        // These names match the objects in your Bootstrap UI.
        slot0InfoText ??= FindInfoLabelUnder("Slot1Card");
        slot1InfoText ??= FindInfoLabelUnder("Slot2Card");
    }

    private TMP_Text FindInfoLabelUnder(string cardRootName)
    {
        var card = GameObject.Find(cardRootName);
        if (!card) return null;

        var info = card.transform.Find("InfoLabel");
        if (!info) return null;

        return info.GetComponent<TMP_Text>();
    }

    private void RefreshSlotInfoUI()
    {
        SetSlotInfoText(0, slot0InfoText);
        SetSlotInfoText(1, slot1InfoText);
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
            label.text =
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
        Debug.Log($"[SaveSlotMenuUI] Continue/Load selected. slot={slotIndex}");

        SaveSlotManager.SetActiveSlot(slotIndex);
        SaveSlotManager.SetPendingStartMode(SaveSlotManager.SlotStartMode.LoadGame);
        if (CanLoadGameplayScene())
            SceneManager.LoadScene(gameplaySceneName);
    }

    public void OnClickNewGame(int slotIndex)
    {
        Debug.Log($"[SaveSlotMenuUI] New Game clicked (show confirm). slot={slotIndex}");
        OpenConfirmNewGamePopup(slotIndex);
    }

    public void OnClickDeleteSlot(int slotIndex)
    {
        Debug.Log($"Delete slot {slotIndex}");

        SaveSlotManager.DeleteSlot(slotIndex);

        RefreshSlotInfoUI();
    }
}