using UnityEngine;

public class HUDPresenter : MonoBehaviour
{
    [SerializeField] private HUDView hud;
    [SerializeField] private PlayerController player;
    [SerializeField] private CharacterStats stats;
    [SerializeField] private EquipmentManager equipment;
    [SerializeField] private PlayerCombatController combat;

    private void Awake()
    {
        if (!hud)
            hud = GetComponent<HUDView>();

        if (!player)
            player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

        if (!stats && player)
            stats = player.GetComponent<CharacterStats>();

        if (!equipment && player)
            equipment = player.GetComponent<EquipmentManager>();

        if (!combat && player)
            combat = player.GetComponent<PlayerCombatController>();

        if (!stats)
            stats = FindFirstObjectByType<CharacterStats>(FindObjectsInactive.Include);

        if (!equipment)
            equipment = FindFirstObjectByType<EquipmentManager>(FindObjectsInactive.Include);

        if (!combat)
            combat = FindFirstObjectByType<PlayerCombatController>(FindObjectsInactive.Include);
    }

    private void OnEnable()
    {
        if (!hud || !player) return;

        player.OnNameChanged += HandleNameChanged;
        player.OnHPChanged += hud.SetHP;
        player.OnEnergyChanged += hud.SetEnergy;
        player.OnManaChanged += hud.SetMana;
        player.OnActionChanged += HandleActionChanged;
        player.OnGatherDebuffChanged += HandleGatherDebuff;

        if (equipment != null)
        {
            equipment.OnMainHandChanged += HandleEquipmentChanged;
            equipment.OnOffHandChanged += HandleEquipmentChanged;
            equipment.OnUISlotChanged += HandleUISlotChanged;
        }

        if (combat != null)
            combat.OnTargetChanged += HandleCombatTargetChanged;

        if (stats != null)
        {
            stats.OnStatsChanged += HandleStatsChangedForCombatPower;
            stats.OnGuardChanged += HandleGuardChanged;
        }

        RefreshAll();
    }

    private void OnDisable()
    {
        if (!hud || !player) return;

        player.OnNameChanged -= HandleNameChanged;
        player.OnHPChanged -= hud.SetHP;
        player.OnEnergyChanged -= hud.SetEnergy;
        player.OnManaChanged -= hud.SetMana;
        player.OnActionChanged -= HandleActionChanged;
        player.OnGatherDebuffChanged -= HandleGatherDebuff;

        if (equipment != null)
        {
            equipment.OnMainHandChanged -= HandleEquipmentChanged;
            equipment.OnOffHandChanged -= HandleEquipmentChanged;
            equipment.OnUISlotChanged -= HandleUISlotChanged;
        }

        if (combat != null)
            combat.OnTargetChanged -= HandleCombatTargetChanged;

        if (stats != null)
        {
            stats.OnStatsChanged -= HandleStatsChangedForCombatPower;
            stats.OnGuardChanged -= HandleGuardChanged;
        }
    }

    private void HandleGuardChanged(float current, float naturalCap)
    {
        if (hud == null || stats == null)
            return;
        hud.SetGuard(current, naturalCap, stats.MaxHP);
    }

    private void Update()
    {
        if (hud == null)
            return;

        float cycle = 0f;
        float aps = stats ? stats.AttacksPerSecond : 0f;
        if (combat != null)
            cycle = combat.GetAttackCycleNormalized();

        hud.SetAttackDelay(cycle, aps);
        hud.SetDps(combat != null ? combat.GetCurrentDps() : 0f);
    }

    private void HandleNameChanged(string _)
    {
        RefreshNameAndCombatPower();
    }

    /// <summary>
    /// Combat power is derived (abilities, buffs, gear). The HUD must refresh when stats notify —
    /// e.g. action bar assignment changes do not touch equipment events.
    /// </summary>
    private void HandleStatsChangedForCombatPower()
    {
        if (stats != null && !stats.LastStatsChangeAffectsCombatPower)
            return;

        RefreshNameAndCombatPower();
    }

    private void HandleEquipmentChanged(string _)
    {
        RefreshNameAndCombatPower();
    }

    private void HandleUISlotChanged(EquipmentUISlotType _, string __)
    {
        RefreshNameAndCombatPower();
    }

    private void HandleCombatTargetChanged()
    {
        HandleActionChanged(player.CurrentAction);
    }

    public void RefreshAll()
    {
        RefreshNameAndCombatPower();
        hud.SetHP(player.HP, player.MaxHP);
        if (stats)
            hud.SetGuard(stats.Guard, stats.NaturalGuardCap, stats.MaxHP);
        hud.SetEnergy(player.Energy, player.MaxEnergy);
        hud.SetMana(player.Mana, player.MaxMana);
        float aps = stats ? stats.AttacksPerSecond : 0f;
        float cycle = combat != null ? combat.GetAttackCycleNormalized() : 0f;
        hud.SetAttackDelay(cycle, aps);
        hud.SetDps(combat != null ? combat.GetCurrentDps() : 0f);
        HandleActionChanged(player.CurrentAction);
        hud.SetGatherDebuff(false, 1f);
    }

    private void RefreshNameAndCombatPower()
    {
        float cp = stats ? stats.CombatPower : 0f;
        hud.SetNameAndCombatPower(player.displayName, cp);
    }

    private void HandleActionChanged(PlayerController.PlayerAction action)
    {
        hud.SetAction(ActionToText(action));
    }

    private string ActionToText(PlayerController.PlayerAction a)
    {
        string enemyName = null;
        string resourceName = null;

        if (combat != null && combat.CurrentTarget != null && !combat.CurrentTarget.IsDead)
            enemyName = combat.CurrentTarget.DisplayName;

        if (player != null && player.CurrentTarget != null)
        {
            if (!string.IsNullOrWhiteSpace(player.CurrentTarget.DisplayName))
                resourceName = player.CurrentTarget.DisplayName;
            else
                resourceName = player.CurrentTarget.name;
        }

        if (a == PlayerController.PlayerAction.Fighting)
            return string.IsNullOrWhiteSpace(enemyName) ? "Fighting" : $"Fighting {enemyName}";

        if (!string.IsNullOrWhiteSpace(enemyName) &&
            a != PlayerController.PlayerAction.Mining &&
            a != PlayerController.PlayerAction.Woodcutting &&
            a != PlayerController.PlayerAction.Fishing &&
            a != PlayerController.PlayerAction.Fatigued)
            return $"Engaging {enemyName}";

        return a switch
        {
            PlayerController.PlayerAction.Idle => "Idle",
            PlayerController.PlayerAction.Walking when !string.IsNullOrWhiteSpace(resourceName) => $"Walking to {resourceName}",
            PlayerController.PlayerAction.Walking => "Walking",
            PlayerController.PlayerAction.Mining when !string.IsNullOrWhiteSpace(resourceName) => $"Mining {resourceName}",
            PlayerController.PlayerAction.Woodcutting when !string.IsNullOrWhiteSpace(resourceName) => $"Woodcutting {resourceName}",
            PlayerController.PlayerAction.Fishing when !string.IsNullOrWhiteSpace(resourceName) => $"Fishing {resourceName}",
            PlayerController.PlayerAction.Mining => "Mining",
            PlayerController.PlayerAction.Woodcutting => "Woodcutting",
            PlayerController.PlayerAction.Fishing => "Fishing",
            PlayerController.PlayerAction.Fatigued => "Fatigued",
            _ => a.ToString()
        };
    }

    private void HandleGatherDebuff(bool active, float multiplier)
    {
        hud.SetGatherDebuff(active, multiplier);
    }
}
