using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hold-to-sprint: adds flat move speed while the key is held (during movement), drains 20% max stamina/s
/// when actually moving, and blocks passive stamina regen during that drain.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public class PlayerSprintInput : MonoBehaviour
{
    public const string SprintHudBuffId = "player_sprint";

    public const float SprintSpeedBonusFlat = 1.5f;
    public const float SprintDrainMaxEnergyFractionPerSecond = 0.20f;
    public const float MinMoveSpeedForSprint = 0.1f;

    public static bool IsSprinting { get; private set; }
    public static event Action<bool> SprintStateChanged;

    /// <summary>While true, <see cref="CharacterStats.TickRegen"/> skips energy (stamina) regeneration.</summary>
    public static bool BlocksStaminaRegen { get; private set; }

    private static PlayerSprintInput _instance;
    private static bool _sprintKeyHeld;

    [SerializeField] private CharacterStats characterStats;
    [SerializeField] private PlayerBuffController buffController;

    private Vector3 _previousPosition;
    private bool _hasPreviousPosition;
    private float _lastHorizontalSpeed;
    private bool _sprintHudBuffRegistered;

    private void Awake()
    {
        _instance = this;
        if (!characterStats)
            characterStats = GetComponent<CharacterStats>();
        if (!buffController)
            buffController = GetComponent<PlayerBuffController>();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    /// <summary>
    /// Call from <see cref="PlayerController"/> at the start of <c>Update</c> so sprint key state is ready before movement.
    /// </summary>
    public static void PollSprintKey()
    {
        _sprintKeyHeld = false;

        if (!CanPollSprintInput())
            return;

        KeyCode sprintKey = HotkeyBindingManager.Instance != null
            ? HotkeyBindingManager.Instance.GetBinding(HotkeyBindId.Sprint)
            : HotkeyBindingManager.GetDefaultKey(HotkeyBindId.Sprint);

        _sprintKeyHeld = sprintKey != KeyCode.None && Input.GetKey(sprintKey);
    }

    private void LateUpdate()
    {
        _lastHorizontalSpeed = GetCurrentHorizontalSpeed();
        CapturePositionForNextFrame();

        if (!_sprintKeyHeld)
        {
            SetSprintActive(false);
            return;
        }

        if (characterStats == null || characterStats.Energy <= 0f)
        {
            SetSprintActive(false);
            return;
        }

        if (_lastHorizontalSpeed <= MinMoveSpeedForSprint)
        {
            SetSprintActive(false);
            return;
        }

        SetSprintActive(true);

        float maxEnergy = Mathf.Max(0f, characterStats.MaxEnergy);
        if (maxEnergy <= 0f)
        {
            SetSprintActive(false);
            return;
        }

        float drain = maxEnergy * SprintDrainMaxEnergyFractionPerSecond * Time.deltaTime;
        if (drain <= 0f)
            return;

        drain = Mathf.Min(drain, characterStats.Energy);
        if (drain <= 0f || !characterStats.SpendEnergy(drain))
            SetSprintActive(false);
    }

    /// <summary>
    /// Called from <see cref="PlayerController.GetMoveSpeed"/> while the player is moving.
    /// </summary>
    public static bool ShouldApplyMoveSpeedBonus()
    {
        if (!_sprintKeyHeld)
            return false;

        CharacterStats stats = ResolveCharacterStats();
        return stats != null && stats.Energy > 0f;
    }

    public static float ApplySprintBonus(float baseMoveSpeed)
    {
        if (!ShouldApplyMoveSpeedBonus())
            return baseMoveSpeed;
        return baseMoveSpeed + SprintSpeedBonusFlat;
    }

    /// <summary>Percent increase from the flat sprint bonus relative to <paramref name="baseMoveSpeed"/>.</summary>
    public static float GetSprintBonusPercentOfBase(float baseMoveSpeed)
    {
        if (!ShouldApplyMoveSpeedBonus() || baseMoveSpeed <= 0.01f)
            return 0f;
        return (SprintSpeedBonusFlat / baseMoveSpeed) * 100f;
    }

    private static CharacterStats ResolveCharacterStats()
    {
        if (_instance != null && _instance.characterStats != null)
            return _instance.characterStats;

        PlayerSprintInput found = FindFirstObjectByType<PlayerSprintInput>(FindObjectsInactive.Exclude);
        return found != null ? found.characterStats : null;
    }

    private void SetSprintActive(bool active)
    {
        if (IsSprinting == active)
            return;

        IsSprinting = active;
        BlocksStaminaRegen = active;
        SyncSprintHudBuff(active);
        SprintStateChanged?.Invoke(active);
        characterStats?.NotifyStatsChanged();
    }

    private void SyncSprintHudBuff(bool active)
    {
        if (!buffController)
            buffController = GetComponent<PlayerBuffController>();
        if (!buffController)
            return;

        if (active)
        {
            if (_sprintHudBuffRegistered)
                return;

            buffController.SetHudAbilityBuff(SprintHudBuffId, 1, 0f, 0f);
            _sprintHudBuffRegistered = true;
        }
        else
        {
            if (!_sprintHudBuffRegistered)
                return;

            buffController.ClearHudAbilityBuff(SprintHudBuffId);
            _sprintHudBuffRegistered = false;
        }
    }

    private float GetCurrentHorizontalSpeed()
    {
        if (!_hasPreviousPosition)
            return 0f;

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return 0f;

        return Mathf.Abs((transform.position.x - _previousPosition.x) / dt);
    }

    private void CapturePositionForNextFrame()
    {
        _previousPosition = transform.position;
        _hasPreviousPosition = true;
    }

    private static bool CanPollSprintInput()
    {
        if (HotkeySettingsRowUI.IsRebinding)
            return false;
        if (HelperGameplayController.BlocksStripGameplay)
            return false;
        if (IsTypingIntoInputField())
            return false;
        return true;
    }

    private static bool IsTypingIntoInputField()
    {
        if (EventSystem.current == null)
            return false;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null)
            return false;

        return selected.GetComponent<TMP_InputField>() != null ||
               selected.GetComponent<InputField>() != null;
    }
}
