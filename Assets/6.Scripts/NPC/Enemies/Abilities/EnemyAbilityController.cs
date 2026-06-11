using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs <see cref="EnemyAbilityDefinition"/> entries assigned on the enemy's <see cref="EnemyDefinition"/>.
/// </summary>
[DisallowMultipleComponent]
public class EnemyAbilityController : MonoBehaviour
{
    private sealed class RuntimeState
    {
        public EnemyAbilityDefinition Definition;
        public bool CombatActive;
        public float NextShadowDashTime;
        public bool DisengageRollResolved;
        public bool DisengageUsed;
    }

    private EnemyBaseController _enemy;
    private Transform _player;
    private PlayerController _playerController;
    private Rigidbody2D _rb;
    private readonly List<RuntimeState> _states = new();

    public void Bind(EnemyDefinition definition, EnemyBaseController enemy)
    {
        _enemy = enemy;
        _states.Clear();

        if (definition?.abilities == null || definition.abilities.Count == 0)
            return;

        for (int i = 0; i < definition.abilities.Count; i++)
        {
            EnemyAbilityDefinition ability = definition.abilities[i];
            if (!ability)
                continue;

            _states.Add(new RuntimeState { Definition = ability });
        }
    }

    public void NotifyCombatActive(Transform player, PlayerController playerController, Rigidbody2D enemyRigidbody)
    {
        _player = player;
        _playerController = playerController;
        _rb = enemyRigidbody;

        float now = Time.time;
        for (int i = 0; i < _states.Count; i++)
        {
            RuntimeState state = _states[i];
            if (state.CombatActive)
                continue;

            state.CombatActive = true;
            state.DisengageRollResolved = false;
            state.DisengageUsed = false;

            if (state.Definition.kind == EnemyAbilityKind.ShadowDashBehindPlayer)
            {
                float delay = Mathf.Max(0f, state.Definition.shadowDashFirstDelaySeconds);
                state.NextShadowDashTime = now + delay;
            }
        }
    }

    public void NotifyCombatEnded()
    {
        for (int i = 0; i < _states.Count; i++)
            _states[i].CombatActive = false;
    }

    public void Tick()
    {
        if (_states.Count == 0 || _enemy == null || _enemy.IsDead || _enemy.IsStunned)
            return;

        if (!_player || !_playerController || _playerController.IsDead)
            return;

        float now = Time.time;
        for (int i = 0; i < _states.Count; i++)
        {
            RuntimeState state = _states[i];
            if (!state.CombatActive || state.Definition == null)
                continue;

            if (state.Definition.kind != EnemyAbilityKind.ShadowDashBehindPlayer)
                continue;

            if (now < state.NextShadowDashTime)
                continue;

            float interval = Mathf.Max(0.1f, state.Definition.shadowDashRepeatIntervalSeconds);
            state.NextShadowDashTime = now + interval;
            TryExecuteShadowDash(state.Definition);
        }
    }

    public void NotifyDamaged(AttackSkill? attackSkill, float distanceToPlayerX)
    {
        if (_states.Count == 0 || _enemy == null || _enemy.IsDead)
            return;

        if (attackSkill != AttackSkill.Melee)
            return;

        for (int i = 0; i < _states.Count; i++)
        {
            RuntimeState state = _states[i];
            EnemyAbilityDefinition def = state.Definition;
            if (!state.CombatActive || def == null || def.kind != EnemyAbilityKind.MeleeDisengage)
                continue;

            if (state.DisengageRollResolved || state.DisengageUsed)
                continue;

            float meleeRange = Mathf.Max(0.1f, def.disengageMeleeRange);
            if (distanceToPlayerX > meleeRange)
                continue;

            state.DisengageRollResolved = true;

            if (Random.value > Mathf.Clamp01(def.disengageFirstHitChance))
                continue;

            state.DisengageUsed = true;
            TryExecuteDisengage(def);
        }
    }

    private void TryExecuteShadowDash(EnemyAbilityDefinition def)
    {
        if (!_player || _enemy == null)
            return;

        Vector3 depart = transform.position;
        float playerX = _player.position.x;
        float enemyX = depart.x;
        float approachSign = Mathf.Sign(playerX - enemyX);
        if (Mathf.Approximately(approachSign, 0f))
            approachSign = _player.position.x >= enemyX ? 1f : -1f;

        float behindDistance = Mathf.Max(0f, def.shadowDashBehindDistance);
        float landX = playerX + approachSign * behindDistance;
        landX = ClampWorldX(landX);

        Vector3 arrive = depart;
        arrive.x = landX;

        ApplyTeleport(arrive);
        SpawnAbilityTeleportVfx(depart, arrive, def);
        _enemy.FaceTargetWorldX(_player.position.x);
    }

    private void TryExecuteDisengage(EnemyAbilityDefinition def)
    {
        if (!_player || _enemy == null)
            return;

        Vector3 depart = transform.position;
        float dx = depart.x - _player.position.x;
        float awaySign = Mathf.Approximately(dx, 0f) ? 1f : Mathf.Sign(dx);
        float distance = Mathf.Max(0f, def.disengageDistance);

        Vector3 arrive = depart;
        arrive.x = ClampWorldX(depart.x + awaySign * distance);

        ApplyTeleport(arrive);
        SpawnAbilityTeleportVfx(depart, arrive, def);
        _enemy.FaceTargetWorldX(_player.position.x);
    }

    private void ApplyTeleport(Vector3 worldPosition)
    {
        transform.position = worldPosition;
        if (_rb)
            _rb.position = worldPosition;
    }

    private float ClampWorldX(float x)
    {
        if (!WorldBounds.Instance)
            return x;

        float minX = WorldBounds.Instance.Left;
        float maxX = WorldBounds.Instance.Right;
        if (minX > maxX)
            maxX = minX;

        return Mathf.Clamp(x, minX, maxX);
    }

    private void SpawnAbilityTeleportVfx(Vector3 departPosition, Vector3 arrivePosition, EnemyAbilityDefinition def)
    {
        PlayerAbilityVfxController vfx = ResolvePlayerAbilityVfx();
        if (!vfx)
            return;

        if (def != null && def.useCustomAbilitySmokeColor)
        {
            vfx.SpawnShadowStrikeDepartSmoke(departPosition, def.abilitySmokeCoreColor, def.abilitySmokeEdgeColor);
            vfx.SpawnShadowStrikeBurst(arrivePosition, def.abilitySmokeCoreColor);
            return;
        }

        vfx.SpawnShadowStrikeDepartSmoke(departPosition);
        vfx.SpawnShadowStrikeBurst(arrivePosition);
    }

    private PlayerAbilityVfxController ResolvePlayerAbilityVfx()
    {
        if (!_playerController)
            return null;

        PlayerAbilityVfxController vfx = _playerController.GetComponent<PlayerAbilityVfxController>();
        if (!vfx)
            vfx = _playerController.GetComponentInChildren<PlayerAbilityVfxController>(true);
        return vfx;
    }
}
