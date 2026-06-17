using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Sprint key: tap or hold start performs a short dash (cooldown), hold while moving adds 50% move speed,
/// costs 20% max stamina on dash start, then drains 15% max stamina/s while sprinting, and blocks passive
/// stamina regen during sprint drain. Depleting stamina enters exhaustion until energy recovers to
/// <see cref="SprintExhaustionRecoveryMaxEnergyFraction"/> of max (sprint key can stay held).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class PlayerSprintInput : MonoBehaviour
{
    public const string SprintHudBuffId = "player_sprint";

    public const float SprintSpeedBonusPercent = 0.50f;
    public const float SprintDashInitialCostMaxEnergyFraction = 0.20f;
    public const float SprintDrainMaxEnergyFractionPerSecond = 0.15f;
    public const float MinMoveSpeedForSprint = 0.1f;
    public const float SprintDashDistance = 1.5f;
    public const float SprintDashCooldownSeconds = 5f;

    /// <summary>
    /// Extra max-stamina above <see cref="SprintDashInitialCostMaxEnergyFraction"/> required before sprint is allowed
    /// again after exhaustion (small cushion so hold-sprint does not instantly re-empty after the first dash).
    /// </summary>
    public const float SprintExhaustionRecoveryBufferFraction = 0.08f;

    /// <summary>
    /// After depletion, sprint stays blocked until energy reaches at least dash cost + buffer (28% of max by default).
    /// Must be ≥ dash cost or the player clears exhaustion but still cannot <see cref="TryStartDash"/>.
    /// </summary>
    public const float SprintExhaustionRecoveryMaxEnergyFraction =
        SprintDashInitialCostMaxEnergyFraction + SprintExhaustionRecoveryBufferFraction;

    public static bool IsSprinting { get; private set; }
    public static bool IsSprintExhausted { get; private set; }
    public static bool IsSprintDashing { get; private set; }
    public static event Action<bool> SprintStateChanged;

    /// <summary>While true, <see cref="CharacterStats.TickRegen"/> skips energy (stamina) regeneration.</summary>
    public static bool BlocksStaminaRegen { get; private set; }

    private static PlayerSprintInput _instance;
    private static bool _sprintKeyHeld;
    private static bool _sprintKeyDownThisFrame;

    [SerializeField] private CharacterStats characterStats;
    [SerializeField] private PlayerBuffController buffController;
    [SerializeField] private PlayerController playerController;

    [Tooltip("Keeps the sprint HUD icon visible briefly after sprint stops (e.g. direction change zeroes measured speed for a frame).")]
    [SerializeField, Min(0f)] private float sprintHudIconHoldSeconds = 0.15f;

    [Tooltip("Minimum time between sprint key-down attempts (stops dash / buff flicker when spam-clicking).")]
    [SerializeField, Min(0f)] private float sprintKeyDownDebounceSeconds = 0.22f;

    [Tooltip("After an accepted sprint press, move speed + HUD stay stable briefly even if the key is released early.")]
    [SerializeField, Min(0f)] private float sprintTapStabilitySeconds = 0.18f;

    [Tooltip("How long the sprint dash takes to travel its full distance.")]
    [SerializeField, Min(0.01f)] private float sprintDashDurationSeconds = 0.12f;

    private Vector3 _previousPosition;
    private bool _hasPreviousPosition;
    private float _lastHorizontalSpeed;
    private bool _sprintHudBuffRegistered;
    private float _sprintHudShowUntil;
    private float _sprintStabilityUntil;
    private float _nextSprintKeyDownAcceptedAt;
    private bool _armedDashWhenCooldownReady;
    private bool _sprintGameplayActiveLastFrame;
    private float _dashCooldownEndsAt;
    private bool _isDashing;
    private float _dashStartX;
    private float _dashTargetX;
    private float _dashEndTime;
    private float _dashLaneReferenceX;
    private float _dashFaceDirectionSign;
    private bool _sprintExhausted;

    private void Awake()
    {
        _instance = this;
        if (!characterStats)
            characterStats = GetComponent<CharacterStats>();
        if (!buffController)
            buffController = GetComponent<PlayerBuffController>();
        if (!playerController)
            playerController = GetComponent<PlayerController>();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
            IsSprintDashing = false;
            IsSprintExhausted = false;
        }
    }

    /// <summary>
    /// Call from <see cref="PlayerController"/> at the start of <c>Update</c> so sprint key state is ready before movement.
    /// </summary>
    public static void PollSprintKey()
    {
        _sprintKeyHeld = false;
        _sprintKeyDownThisFrame = false;

        if (!CanPollSprintInput())
            return;

        KeyCode sprintKey = HotkeyBindingManager.Instance != null
            ? HotkeyBindingManager.Instance.GetBinding(HotkeyBindId.Sprint)
            : HotkeyBindingManager.GetDefaultKey(HotkeyBindId.Sprint);

        if (sprintKey == KeyCode.None)
            return;

        _sprintKeyDownThisFrame = Input.GetKeyDown(sprintKey);
        _sprintKeyHeld = Input.GetKey(sprintKey);
    }

    /// <summary>
    /// Call from <see cref="PlayerController"/> immediately after <see cref="PollSprintKey"/> and before movement.
    /// </summary>
    public static void TickSprintDash()
    {
        if (_instance == null)
            return;

        _instance.TickSprintDashInternal();
    }

    private void TickSprintDashInternal()
    {
        if (_isDashing)
        {
            TickActiveDash();
            return;
        }

        if (!_sprintKeyHeld)
        {
            _armedDashWhenCooldownReady = true;
            return;
        }

        if (IsSprintBlockedByExhaustionOrEmptyStamina())
            return;

        if (TryConsumeDebouncedSprintKeyDown())
            TryStartDash();
        else if (_armedDashWhenCooldownReady && Time.time >= _dashCooldownEndsAt)
        {
            _armedDashWhenCooldownReady = false;
            TryStartDash();
        }
    }

    /// <summary>Honors sprint key-down at most once per debounce window; extends tap stability for HUD / move speed.</summary>
    private bool TryConsumeDebouncedSprintKeyDown()
    {
        if (!_sprintKeyDownThisFrame)
            return false;

        if (sprintKeyDownDebounceSeconds > 0f && Time.time < _nextSprintKeyDownAcceptedAt)
            return false;

        if (sprintKeyDownDebounceSeconds > 0f)
            _nextSprintKeyDownAcceptedAt = Time.time + sprintKeyDownDebounceSeconds;

        ExtendSprintTapStability();
        return true;
    }

    private void ExtendSprintTapStability()
    {
        if (sprintTapStabilitySeconds <= 0f)
            return;

        float until = Time.time + sprintTapStabilitySeconds;
        if (until > _sprintStabilityUntil)
            _sprintStabilityUntil = until;
    }

    private bool IsWithinSprintTapStability() =>
        _sprintStabilityUntil > 0f && Time.time < _sprintStabilityUntil;

    private void TickActiveDash()
    {
        if (!playerController)
            playerController = GetComponent<PlayerController>();

        float duration = Mathf.Max(0.01f, sprintDashDurationSeconds);
        float elapsed = duration - (_dashEndTime - Time.time);
        float t = Mathf.Clamp01(elapsed / duration);
        float eased = 1f - (1f - t) * (1f - t);
        float x = Mathf.Lerp(_dashStartX, _dashTargetX, eased);

        if (playerController != null)
            playerController.SetHorizontalPositionForScriptedMove(x, _dashFaceDirectionSign, _dashLaneReferenceX);
        else
            transform.position = new Vector3(x, transform.position.y, transform.position.z);

        if (t >= 1f)
            EndDash();
    }

    private void TryStartDash()
    {
        if (Time.time < _dashCooldownEndsAt)
            return;

        if (IsSprintBlockedByExhaustionOrEmptyStamina())
            return;

        if (!playerController)
            playerController = GetComponent<PlayerController>();

        if (playerController == null || playerController.IsDead)
            return;

        if (!characterStats)
            characterStats = GetComponent<CharacterStats>();

        float maxEnergy = characterStats != null ? Mathf.Max(0f, characterStats.MaxEnergy) : 0f;
        if (maxEnergy <= 0f)
            return;

        float dashCost = maxEnergy * SprintDashInitialCostMaxEnergyFraction;
        if (dashCost <= 0f || characterStats.Energy < dashCost)
            return;

        float directionSign = playerController.ResolveSprintDashDirectionSign();
        if (Mathf.Abs(directionSign) < 0.01f)
            directionSign = 1f;

        float startX = transform.position.x;
        if (!TryResolveDashTargetX(startX, directionSign, out float targetX, out float usedDirectionSign))
            return;

        directionSign = usedDirectionSign;

        if (!characterStats.SpendEnergy(dashCost))
            return;

        if (characterStats.Energy <= 0f)
            SetSprintExhausted(true);

        ExtendSprintTapStability();
        playerController.InterruptForSprintDash();

        _dashStartX = startX;
        _dashTargetX = targetX;
        _dashLaneReferenceX = startX;
        _dashFaceDirectionSign = directionSign;
        _dashEndTime = Time.time + Mathf.Max(0.01f, sprintDashDurationSeconds);
        _dashCooldownEndsAt = Time.time + SprintDashCooldownSeconds;
        _isDashing = true;
        IsSprintDashing = true;
    }

    private void EndDash()
    {
        _isDashing = false;
        IsSprintDashing = false;
    }

    private void LateUpdate()
    {
        TickSprintDashInternal();

        _lastHorizontalSpeed = GetCurrentHorizontalSpeed();
        CapturePositionForNextFrame();

        bool sprintActive = EvaluateSprintGameplayActive();
        SetSprintActive(sprintActive);
        RefreshSprintHudBuffGrace(sprintActive);
    }

    /// <summary>
    /// Picks a dash destination inside world bounds; tries the facing direction first, then the opposite.
    /// </summary>
    private bool TryResolveDashTargetX(
        float startX,
        float directionSign,
        out float targetX,
        out float usedDirectionSign)
    {
        usedDirectionSign = directionSign >= 0f ? 1f : -1f;

        if (TryClampDashTarget(startX, usedDirectionSign, out targetX))
            return true;

        usedDirectionSign = -usedDirectionSign;
        return TryClampDashTarget(startX, usedDirectionSign, out targetX);
    }

    private bool TryClampDashTarget(float startX, float directionSign, out float targetX)
    {
        targetX = playerController.ClampWorldXForLaneAt(startX, startX + directionSign * SprintDashDistance);
        return Mathf.Abs(targetX - startX) >= 0.001f;
    }

    private bool EvaluateSprintGameplayActive()
    {
        if (!_sprintKeyHeld)
            return false;

        if (characterStats == null)
            return false;

        TickSprintExhaustionRecovery();

        if (IsSprintBlockedByExhaustionOrEmptyStamina())
            return false;

        if (_lastHorizontalSpeed <= MinMoveSpeedForSprint)
            return false;

        float maxEnergy = Mathf.Max(0f, characterStats.MaxEnergy);
        if (maxEnergy <= 0f)
            return false;

        float drain = maxEnergy * SprintDrainMaxEnergyFractionPerSecond * Time.deltaTime;
        if (drain <= 0f)
            return true;

        drain = Mathf.Min(drain, characterStats.Energy);
        if (drain <= 0f || !characterStats.SpendEnergy(drain))
        {
            if (characterStats.Energy <= 0f)
                SetSprintExhausted(true);
            return false;
        }

        if (characterStats.Energy <= 0f)
            SetSprintExhausted(true);

        return true;
    }

    /// <summary>
    /// Called from <see cref="PlayerController.GetMoveSpeed"/> while the player is moving.
    /// </summary>
    public static bool ShouldApplyMoveSpeedBonus()
    {
        if (IsSprintExhausted)
            return false;

        CharacterStats stats = ResolveCharacterStats();
        if (stats == null || stats.Energy <= 0f)
            return false;

        if (IsSprinting)
            return true;

        if (_sprintKeyHeld)
        {
            // Brief hold after the last sprint frame while still holding the key.
            if (_instance != null && Time.time < _instance._sprintHudShowUntil)
                return true;
            return false;
        }

        if (_instance == null)
            return false;

        if (Time.time < _instance._sprintHudShowUntil)
            return true;

        return _instance.IsWithinSprintTapStability();
    }

    public static float ApplySprintBonus(float baseMoveSpeed)
    {
        if (!ShouldApplyMoveSpeedBonus())
            return baseMoveSpeed;
        return baseMoveSpeed * (1f + SprintSpeedBonusPercent);
    }

    /// <summary>Percent increase from sprint relative to <paramref name="baseMoveSpeed"/>.</summary>
    public static float GetSprintBonusPercentOfBase(float baseMoveSpeed)
    {
        if (!ShouldApplyMoveSpeedBonus())
            return 0f;
        return SprintSpeedBonusPercent * 100f;
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
        SprintStateChanged?.Invoke(active);
    }

    private bool IsSprintBlockedByExhaustionOrEmptyStamina()
    {
        if (characterStats == null)
            return true;

        TickSprintExhaustionRecovery();

        if (_sprintExhausted)
            return true;

        return characterStats.Energy <= 0f;
    }

    private void TickSprintExhaustionRecovery()
    {
        if (!_sprintExhausted || characterStats == null)
            return;

        float maxEnergy = Mathf.Max(0f, characterStats.MaxEnergy);
        if (maxEnergy <= 0f)
        {
            SetSprintExhausted(false);
            return;
        }

        float threshold = maxEnergy * SprintExhaustionRecoveryMaxEnergyFraction;
        if (characterStats.Energy >= threshold)
            SetSprintExhausted(false);
    }

    private void SetSprintExhausted(bool exhausted)
    {
        if (_sprintExhausted == exhausted)
            return;

        _sprintExhausted = exhausted;
        IsSprintExhausted = exhausted;
    }

    private void RefreshSprintHudBuffGrace(bool sprintGameplayActive)
    {
        if (!sprintGameplayActive && _sprintGameplayActiveLastFrame)
        {
            float hold = Mathf.Max(sprintHudIconHoldSeconds, sprintTapStabilitySeconds);
            if (hold > 0f)
                _sprintHudShowUntil = Time.time + hold;
        }

        _sprintGameplayActiveLastFrame = sprintGameplayActive;

        bool showHud = sprintGameplayActive
            || Time.time < _sprintHudShowUntil
            || IsWithinSprintTapStability();
        SyncSprintHudBuff(showHud);
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
