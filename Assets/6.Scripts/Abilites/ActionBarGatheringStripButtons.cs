using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Optional helper: wires W/M/F if you do not assign <see cref="ActionBarUI"/>’s gathering strip buttons in the Inspector.
/// Do not wire the same buttons here and on <see cref="ActionBarUI"/> or clicks will run twice.
/// </summary>
[DisallowMultipleComponent]
public class ActionBarGatheringStripButtons : MonoBehaviour
{
    [SerializeField] private ActionBarUI actionBar;
    [SerializeField] private Button woodcuttingButton;
    [SerializeField] private Button miningButton;
    [SerializeField] private Button fishingButton;

    private void Awake()
    {
        if (!actionBar)
            actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);

        if (!woodcuttingButton)
            woodcuttingButton = FindButtonByNameInChildren("WoodcuttingSetButton");
        if (!miningButton)
            miningButton = FindButtonByNameInChildren("MiningSetButton");
        if (!fishingButton)
            fishingButton = FindButtonByNameInChildren("FishingSetButton");
    }

    private Button FindButtonByNameInChildren(string objectName)
    {
        var buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (b != null && string.Equals(b.gameObject.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                return b;
        }

        return null;
    }

    private void OnEnable()
    {
        Wire(woodcuttingButton, OnWoodcuttingClicked);
        Wire(miningButton, OnMiningClicked);
        Wire(fishingButton, OnFishingClicked);
    }

    private void OnDisable()
    {
        Unwire(woodcuttingButton, OnWoodcuttingClicked);
        Unwire(miningButton, OnMiningClicked);
        Unwire(fishingButton, OnFishingClicked);
    }

    private static void Wire(Button b, UnityEngine.Events.UnityAction handler)
    {
        if (b == null || handler == null)
            return;
        b.onClick.RemoveListener(handler);
        b.onClick.AddListener(handler);
    }

    private static void Unwire(Button b, UnityEngine.Events.UnityAction handler)
    {
        if (b == null || handler == null)
            return;
        b.onClick.RemoveListener(handler);
    }

    private void OnWoodcuttingClicked() =>
        actionBar?.ShowGatheringBarForSkill(SkillType.Woodcutting, GatheringBarDriveKind.ManualStripButton);

    private void OnMiningClicked() =>
        actionBar?.ShowGatheringBarForSkill(SkillType.Mining, GatheringBarDriveKind.ManualStripButton);

    private void OnFishingClicked() =>
        actionBar?.ShowGatheringBarForSkill(SkillType.Fishing, GatheringBarDriveKind.ManualStripButton);
}
