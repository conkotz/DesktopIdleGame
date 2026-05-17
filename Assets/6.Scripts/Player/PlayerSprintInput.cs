using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hold-to-sprint: adds flat move speed while the key is held (during movement), drains 20% max stamina/s
/// when actually moving, and blocks passive stamina regen during that drain.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-20)]
public class PlayerSprintInput : MonoBehaviour
{
    public const float SprintSpeedBonusFlat = 1.5f;
    public const float SprintDrainMaxEnergyFractionPerSecond = 0.20f;
    public const float MinMoveSpeedForSprint = 0.1f;

    public static bool IsSprinting { get; private set; }

    /// <summary>While true, <see cref="CharacterStats.TickRegen"/> skips energy (stamina) regeneration.</summary>
    public static bool BlocksStaminaRegen { get; private set; }

    private static PlayerSprintInput _instance;
    private static bool _sprintKeyHeld;

    [SerializeField] private CharacterStats characterStats;

    private Vector3 _previousPosition;
    private bool _hasPreviousPosition;
    private float _lastHorizontalSpeed;

    private void Awake()
    {
        _instance = this;
        if (!characterStats)
            characterStats = GetComponent<CharacterStats>();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        _sprintKeyHeld = false;

        if (!CanPollSprintInput())
        {
            SetSprintActive(false);
            return;
        }

        KeyCode sprintKey = HotkeyBindingManager.Instance != null
            ? HotkeyBindingManager.Instance.GetBinding(HotkeyBindId.Sprint)
            : HotkeyBindingManager.GetDefaultKey(HotkeyBindId.Sprint);

        _sprintKeyHeld = sprintKey != KeyCode.None && Input.GetKey(sprintKey);

        if (!_sprintKeyHeld)
            SetSprintActive(false);
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
    /// Uses key-held state (not LateUpdate speed) so the bonus applies the same frame movement starts.
    /// </summary>
    public static bool ShouldApplyMoveSpeedBonus()
    {
        if (!_sprintKeyHeld)
            return false;

        CharacterStats stats = _instance != null ? _instance.characterStats : null;
        return stats != null && stats.Energy > 0f;
    }

    private void SetSprintActive(bool active)
    {
        IsSprinting = active;
        BlocksStaminaRegen = active;
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
