using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bottom-strip Auto Battle toggle. When <see cref="QuestProgressManager.IsIdleCombatUnlocked"/> is false,
/// shows a light red tint and logs a blocked message instead of toggling idle combat.
/// </summary>
public class IdleCombatButton : MonoBehaviour
{
    [SerializeField] private PlayerCombatController combat;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image buttonImage;

    [Tooltip("Drawn above the button graphic while locked; disable Raycast Target on this image so clicks reach the button.")]
    [SerializeField] private Image lockTintOverlay;

    [Header("Colours")]
    [SerializeField] private Color onColor = new Color(0.2f, 0.8f, 0.2f); // green
    [SerializeField] private Color offColor = new Color(0.85f, 0.85f, 0.85f); // light grey

    [Tooltip(
        "Fallback activity log line when Auto Battle is locked but no quest grants idle unlock in the database. " +
        "Normally the message is built from quests with Grant Idle Combat Unlock On Reward Claim.")]
    [SerializeField] private string lockedActivityLogMessage = "";

    [SerializeField] private Color lockTintColor = new Color(1f, 0.45f, 0.45f, 0.45f);

    private void Awake()
    {
        if (!combat)
            combat = FindFirstObjectByType<PlayerCombatController>(FindObjectsInactive.Include);

        if (!label)
            label = GetComponentInChildren<TMP_Text>();

        if (!buttonImage)
            buttonImage = GetComponent<Image>();

        if (!lockTintOverlay)
        {
            Transform t = transform.Find("LockTint");
            if (t)
                lockTintOverlay = t.GetComponent<Image>();
        }

        if (lockTintOverlay)
        {
            lockTintOverlay.raycastTarget = false;
            lockTintOverlay.color = lockTintColor;
        }
    }

    private void OnEnable()
    {
        if (combat != null)
            combat.OnIdleCombatChanged += HandleIdleChanged;

        QuestProgressManager qpm = QuestProgressManager.Instance ??
            FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);
        if (qpm != null)
            qpm.ProgressChanged += HandleQuestProgressChanged;

        RefreshLockVisual();
        if (combat != null)
            HandleIdleChanged(combat.IdleCombatEnabled);
    }

    private void OnDisable()
    {
        if (combat != null)
            combat.OnIdleCombatChanged -= HandleIdleChanged;

        QuestProgressManager qpm = QuestProgressManager.Instance ??
            FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);
        if (qpm != null)
            qpm.ProgressChanged -= HandleQuestProgressChanged;
    }

    private void HandleQuestProgressChanged() => RefreshLockVisual();

    public void ToggleIdleCombat()
    {
        if (IsAutoBattleLocked())
        {
            QuestProgressManager qpm = QuestProgressManager.Instance ??
                FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);
            string dynamicMsg = qpm != null ? qpm.BuildIdleCombatUnlockBlockedMessage() : "";
            string msg = !string.IsNullOrWhiteSpace(dynamicMsg)
                ? dynamicMsg
                : !string.IsNullOrWhiteSpace(lockedActivityLogMessage)
                    ? lockedActivityLogMessage.Trim()
                    : "Auto Battle is locked.";
            GameLog.Add(msg, GameLog.CannotMessageColor);
            return;
        }

        if (!combat)
            combat = FindFirstObjectByType<PlayerCombatController>(FindObjectsInactive.Include);

        if (combat == null)
            return;

        combat.ToggleIdleCombat();
    }

    private bool IsAutoBattleLocked()
    {
        QuestProgressManager qpm = QuestProgressManager.Instance ??
            FindFirstObjectByType<QuestProgressManager>(FindObjectsInactive.Include);
        return qpm != null && !qpm.IsIdleCombatUnlocked;
    }

    private void RefreshLockVisual()
    {
        if (!lockTintOverlay)
            return;

        lockTintOverlay.gameObject.SetActive(IsAutoBattleLocked());
    }

    private void Start()
    {
        RefreshLockVisual();
        if (combat != null)
            HandleIdleChanged(combat.IdleCombatEnabled);
    }

    private void HandleIdleChanged(bool enabled)
    {
        if (label)
            label.text = enabled ? "Auto Battle: ON" : "Auto Battle: OFF";

        if (buttonImage)
            buttonImage.color = enabled ? onColor : offColor;
    }
}
