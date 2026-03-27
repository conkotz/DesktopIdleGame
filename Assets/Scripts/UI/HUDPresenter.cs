using UnityEngine;

public class HUDPresenter : MonoBehaviour
{
    [SerializeField] private HUDView hud;
    [SerializeField] private PlayerController player;
    [SerializeField] private CharacterStats stats;
    [SerializeField] private EquipmentManager equipment;
    [SerializeField] private PlayerCombatController combat;
    [SerializeField] private AilmentController ailments;
    [SerializeField] private PlayerBuffController buffs;

    private float _nextBuffRefreshTime;
    [SerializeField] private float buffRefreshInterval = 0.2f;

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

        if (!ailments && player)
            ailments = player.GetComponent<AilmentController>();

        if (!buffs && player)
            buffs = player.GetComponent<PlayerBuffController>();

        if (!stats)
            stats = FindFirstObjectByType<CharacterStats>(FindObjectsInactive.Include);

        if (!equipment)
            equipment = FindFirstObjectByType<EquipmentManager>(FindObjectsInactive.Include);

        if (!combat)
            combat = FindFirstObjectByType<PlayerCombatController>(FindObjectsInactive.Include);

        if (!ailments)
            ailments = FindFirstObjectByType<AilmentController>(FindObjectsInactive.Include);

        if (!buffs)
            buffs = FindFirstObjectByType<PlayerBuffController>(FindObjectsInactive.Include);
    }

    private void OnEnable()
    {
        if (!hud || !player) return;

        player.OnNameChanged += HandleNameChanged;
        player.OnHPChanged += hud.SetHP;
        player.OnEnergyChanged += hud.SetEnergy;
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

        if (ailments != null)
            ailments.OnAilmentsChanged += HandleAilmentsChanged;

        if (buffs != null)
            buffs.OnBuffsChanged += HandleBuffsChanged;

        RefreshAll();
    }

    private void OnDisable()
    {
        if (!hud || !player) return;

        player.OnNameChanged -= HandleNameChanged;
        player.OnHPChanged -= hud.SetHP;
        player.OnEnergyChanged -= hud.SetEnergy;
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

        if (ailments != null)
            ailments.OnAilmentsChanged -= HandleAilmentsChanged;

        if (buffs != null)
            buffs.OnBuffsChanged -= HandleBuffsChanged;
    }

    private void Update()
    {
        if (hud == null)
            return;

        if (buffs != null && Time.time >= _nextBuffRefreshTime)
        {
            _nextBuffRefreshTime = Time.time + Mathf.Max(0.05f, buffRefreshInterval);
            hud.UpdateBuffTimers(buffs);
        }

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

    private void HandleAilmentsChanged()
    {
        if (hud != null)
            hud.RefreshDebuffs(ailments);
    }

    private void HandleBuffsChanged()
    {
        if (hud != null)
            hud.RefreshBuffs(buffs);
    }

    private void RefreshAll()
    {
        RefreshNameAndCombatPower();
        hud.SetHP(player.HP, player.MaxHP);
        hud.SetEnergy(player.Energy, player.MaxEnergy);
        float aps = stats ? stats.AttacksPerSecond : 0f;
        float cycle = combat != null ? combat.GetAttackCycleNormalized() : 0f;
        hud.SetAttackDelay(cycle, aps);
        hud.SetDps(combat != null ? combat.GetCurrentDps() : 0f);
        HandleActionChanged(player.CurrentAction);
        hud.SetGatherDebuff(false, 1f);
        hud.RefreshDebuffs(ailments);
        hud.RefreshBuffs(buffs);
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

        return a switch
        {
            PlayerController.PlayerAction.Idle => "Idle",
            PlayerController.PlayerAction.Walking when !string.IsNullOrWhiteSpace(enemyName) => $"Engaging {enemyName}",
            PlayerController.PlayerAction.Walking when !string.IsNullOrWhiteSpace(resourceName) => $"Walking to {resourceName}",
            PlayerController.PlayerAction.Walking => "Walking",
            PlayerController.PlayerAction.Mining when !string.IsNullOrWhiteSpace(resourceName) => $"Mining {resourceName}",
            PlayerController.PlayerAction.Woodcutting when !string.IsNullOrWhiteSpace(resourceName) => $"Woodcutting {resourceName}",
            PlayerController.PlayerAction.Fishing when !string.IsNullOrWhiteSpace(resourceName) => $"Fishing {resourceName}",
            PlayerController.PlayerAction.Mining => "Mining",
            PlayerController.PlayerAction.Woodcutting => "Woodcutting",
            PlayerController.PlayerAction.Fishing => "Fishing",
            PlayerController.PlayerAction.Fighting when !string.IsNullOrWhiteSpace(enemyName) => $"Fighting {enemyName}",
            PlayerController.PlayerAction.Fighting => "Fighting",
            PlayerController.PlayerAction.Fatigued => "Fatigued",
            _ => a.ToString()
        };
    }

    private void HandleGatherDebuff(bool active, float multiplier)
    {
        hud.SetGatherDebuff(active, multiplier);
    }
}