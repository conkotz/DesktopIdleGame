using System.Collections;
using UnityEngine;

/// <summary>
/// When <see cref="ConditionalAutoBattleSettingsStore.Enabled"/> is on, swaps gear + combat loadout
/// to set 1 or set 2 when the configured conditions are met.
/// </summary>
[DisallowMultipleComponent]
public sealed class ConditionalAutoBattleSetController : MonoBehaviour
{
    [SerializeField] private EquipmentManager equipment;
    [SerializeField] private ActionBarUI actionBar;
    [Tooltip("Optional. Drag the Action Bar window object here; ActionBarUI is resolved on a child.")]
    [SerializeField] private GameObject actionBarWindow;
    [SerializeField] private CharacterStats characterStats;
    [SerializeField] private PlayerCombatController combatController;
    [SerializeField, Min(0.05f)] private float evaluationIntervalSeconds = 0.15f;

    private bool _swapInProgress;
    private float _nextEvaluationTime;
    private int _lastDesiredSetIndex = -1;

    private void Awake() => BindRefs();

    private void OnEnable()
    {
        ConditionalAutoBattleSettingsStore.Changed += OnSettingsChanged;
        _nextEvaluationTime = 0f;
        _lastDesiredSetIndex = -1;
    }

    private void OnDisable()
    {
        ConditionalAutoBattleSettingsStore.Changed -= OnSettingsChanged;
    }

    private void Update()
    {
        if (_swapInProgress || !ConditionalAutoBattleSettingsStore.Enabled)
            return;

        if (!ConditionalAutoBattleSettingsStore.HasActiveConditions())
            return;

        BindRefs();
        if (combatController == null || !combatController.IdleCombatEnabled)
            return;

        if (Time.unscaledTime < _nextEvaluationTime)
            return;

        _nextEvaluationTime = Time.unscaledTime + evaluationIntervalSeconds;
        TryApplyConditionalSet();
    }

    private void OnSettingsChanged()
    {
        _lastDesiredSetIndex = -1;
        _nextEvaluationTime = 0f;
    }

    private void TryApplyConditionalSet()
    {
        BindRefs();
        if (equipment == null || characterStats == null)
            return;

        EvaluateCombatSignals(
            out bool targetInMeleeRange,
            out bool noMeleeTarget,
            out float healthFraction01);

        int desiredSet = ConditionalAutoBattleSettingsStore.EvaluateDesiredSetIndex(
            targetInMeleeRange,
            noMeleeTarget,
            healthFraction01);

        if (desiredSet < 0)
        {
            _lastDesiredSetIndex = -1;
            return;
        }

        GetActiveSets(out int gearSet, out int loadoutSet);
        bool needsGearSwap = gearSet != desiredSet;
        bool needsLoadoutSwap = actionBar != null && loadoutSet != desiredSet;

        if (!needsGearSwap && !needsLoadoutSwap)
        {
            _lastDesiredSetIndex = desiredSet;
            return;
        }

        if (_lastDesiredSetIndex == desiredSet && !needsLoadoutSwap)
            return;

        _lastDesiredSetIndex = desiredSet;
        StartCoroutine(CoApplyFullLoadoutSwap(desiredSet, needsGearSwap, needsLoadoutSwap));
    }

    private IEnumerator CoApplyFullLoadoutSwap(int setIndex, bool swapGear, bool swapLoadout)
    {
        _swapInProgress = true;
        BindRefs();

        actionBar?.ExitGatheringBarToCombat();

        bool gearSwapped = false;
        if (swapGear)
            gearSwapped = equipment != null && equipment.TrySetActiveWeaponSet(setIndex);

        if (gearSwapped)
            characterStats?.NotifyWeaponSetSwapped();

        yield return null;

        BindRefs();
        if (swapLoadout && actionBar != null)
        {
            actionBar.SetCombatLoadoutSet(setIndex);
            SkillsManager.Instance?.TryApplyLinkedPresetForWeaponSet(setIndex, actionBar);
        }

        if (gearSwapped || swapLoadout)
            characterStats?.NotifyStatsChanged();

        _swapInProgress = false;
    }

    private void GetActiveSets(out int gearSet, out int loadoutSet)
    {
        gearSet = equipment != null && equipment.ActiveWeaponSetIndex == 1 ? 1 : 0;
        loadoutSet = actionBar != null ? actionBar.ActiveCombatLoadoutSetIndex : gearSet;
    }

    private void EvaluateCombatSignals(
        out bool targetInMeleeRange,
        out bool noMeleeTarget,
        out float healthFraction01)
    {
        targetInMeleeRange = false;
        noMeleeTarget = true;

        if (combatController == null)
            combatController = FindFirstObjectByType<PlayerCombatController>();

        float meleeRange = ConditionalAutoBattleSettingsStore.MeleeRangeWorldUnits;
        EnemyBaseController target = combatController != null ? combatController.CurrentTarget : null;

        if (target != null && !target.IsDead)
        {
            targetInMeleeRange = combatController != null &&
                                 combatController.IsEnemyWithinApproachRange(target, meleeRange);
            noMeleeTarget = !targetInMeleeRange;
        }
        else if (combatController != null)
        {
            noMeleeTarget = combatController.FindClosestEnemyWithinApproachRange(meleeRange) == null;
        }

        float maxHp = Mathf.Max(1f, characterStats.MaxHP);
        healthFraction01 = characterStats.HP / maxHp;
    }

    private void BindRefs()
    {
        if (!equipment)
            equipment = FindFirstObjectByType<EquipmentManager>();

        if (!actionBar && actionBarWindow)
            actionBar = actionBarWindow.GetComponentInChildren<ActionBarUI>(true);

        if (!actionBar)
            actionBar = FindFirstObjectByType<ActionBarUI>(FindObjectsInactive.Include);

        if (!characterStats)
            characterStats = FindFirstObjectByType<CharacterStats>();

        if (!combatController)
            combatController = FindFirstObjectByType<PlayerCombatController>();
    }
}
