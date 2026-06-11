using System.Collections;

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

        public float NextPounceTime;

        public bool PlayerWasInPounceRange;

        public bool EnrageApplied;

    }



    private static readonly Color PounceGlowColor = new Color(1f, 0.28f, 0.28f, 1f);
    private static readonly Color ToxicFangsGlowColor = new Color(0.35f, 0.95f, 0.38f, 1f);

    private Coroutine _toxicFangsGlowRoutine;



    private EnemyBaseController _enemy;

    private Transform _player;

    private PlayerController _playerController;

    private Rigidbody2D _rb;

    private readonly List<RuntimeState> _states = new();

    private Coroutine _movementRoutine;

    private SpriteRenderer[] _glowRenderers;

    private Color[] _glowOriginalColors;

    private int _activeGlowCount;



    public bool IsMovementLocked { get; private set; }



    public void Bind(EnemyDefinition definition, EnemyBaseController enemy)

    {

        _enemy = enemy;

        _states.Clear();

        ReleaseRedGlow();



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

            state.PlayerWasInPounceRange = false;

            state.NextPounceTime = 0f;



            if (state.Definition.kind == EnemyAbilityKind.ShadowDashBehindPlayer)

                state.NextShadowDashTime = now + state.Definition.RollShadowDashFirstDelaySeconds();

        }

    }



    public void NotifyCombatEnded()

    {

        StopMovementRoutine();

        ReleaseRedGlow();

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

        float playerDistanceX = _enemy.GetPlayerMeleeThreatDistanceX();



        for (int i = 0; i < _states.Count; i++)

        {

            RuntimeState state = _states[i];

            if (!state.CombatActive || state.Definition == null)

                continue;



            if (state.Definition.kind == EnemyAbilityKind.LowHealthEnrage)

                TickEnrage(state);

        }



        if (IsMovementLocked)

            return;



        for (int i = 0; i < _states.Count; i++)

        {

            RuntimeState state = _states[i];

            if (!state.CombatActive || state.Definition == null)

                continue;



            EnemyAbilityDefinition def = state.Definition;

            switch (def.kind)

            {

                case EnemyAbilityKind.ShadowDashBehindPlayer:

                    TickShadowDash(state, now);

                    break;

                case EnemyAbilityKind.PounceOnRangeBreak:

                    TickPounce(state, now, playerDistanceX);

                    break;

            }

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



    private void OnDisable()

    {

        StopMovementRoutine();

        ReleaseRedGlow();

        StopToxicFangsGlow();

    }



    private void TickShadowDash(RuntimeState state, float now)

    {

        EnemyAbilityDefinition def = state.Definition;

        if (now < state.NextShadowDashTime)

            return;



        float interval = Mathf.Max(0.1f, def.shadowDashRepeatIntervalSeconds);

        state.NextShadowDashTime = now + interval;

        TryExecuteShadowDash(def);

    }



    private void TickPounce(RuntimeState state, float now, float playerDistanceX)

    {

        EnemyAbilityDefinition def = state.Definition;

        float triggerRange = Mathf.Max(0.1f, def.pounceTriggerRange);



        if (playerDistanceX <= triggerRange)

        {

            state.PlayerWasInPounceRange = true;

            return;

        }



        if (!state.PlayerWasInPounceRange || now < state.NextPounceTime)

            return;



        state.NextPounceTime = now + Mathf.Max(0f, def.pounceCooldownSeconds);

        TryExecutePounce(def);

    }



    private void TickEnrage(RuntimeState state)

    {

        if (state.EnrageApplied || _enemy == null || _enemy.MaxHP <= 0)

            return;



        EnemyAbilityDefinition def = state.Definition;

        float threshold = Mathf.Clamp01(def.enrageHealthThreshold);

        float healthFraction = _enemy.HP / (float)_enemy.MaxHP;

        if (healthFraction > threshold)

            return;



        state.EnrageApplied = true;

        _enemy.ApplyAbilityEnrage(

            def.enrageScaleMultiplier,

            def.enrageAttackSpeedMultiplier,

            def.enrageMoveSpeedMultiplier);

    }

    public void TryRollToxicFangsOnAttack(ref SplitDamage hit, out bool forcePoison)
    {
        forcePoison = false;
        if (_enemy == null || _enemy.IsDead)
            return;

        if (_states.Count == 0 && _enemy.Definition != null)
            Bind(_enemy.Definition, _enemy);

        if (_states.Count == 0)
            return;

        for (int i = 0; i < _states.Count; i++)
        {
            EnemyAbilityDefinition def = _states[i].Definition;
            if (def == null || def.kind != EnemyAbilityKind.ToxicFangsAttackProc)
                continue;

            if (Random.value > Mathf.Clamp01(def.toxicFangsChance))
                continue;

            float bonusCorruption = Mathf.Max(0f, def.toxicFangsBonusCorruptionDamage);
            if (bonusCorruption > 0f)
                hit.corruptionDamage += bonusCorruption;

            forcePoison = true;
            BeginToxicFangsGlow(Mathf.Max(0.05f, def.toxicFangsOverlaySeconds));
            return;
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



        ApplyPosition(arrive);

        SpawnAbilityTeleportVfx(depart, arrive, def);

        _enemy.FaceTargetWorldX(_player.position.x);

    }



    private void TryExecuteDisengage(EnemyAbilityDefinition def)

    {

        if (!_player || _enemy == null || def == null)

            return;



        StopMovementRoutine();

        _movementRoutine = StartCoroutine(CoDisengage(def));

    }



    private void TryExecutePounce(EnemyAbilityDefinition def)

    {

        if (!_player || _enemy == null || def == null)

            return;



        StopMovementRoutine();

        Vector3 target = _player.position;

        target.x = ClampWorldX(target.x);

        _movementRoutine = StartCoroutine(CoPounce(def, target));

    }



    private IEnumerator CoDisengage(EnemyAbilityDefinition def)

    {

        IsMovementLocked = true;

        if (_rb)

            _rb.linearVelocity = Vector2.zero;



        Vector3 depart = transform.position;

        float awaySign = Mathf.Approximately(depart.x - _player.position.x, 0f)

            ? 1f

            : Mathf.Sign(depart.x - _player.position.x);

        float targetX = ClampWorldX(depart.x + awaySign * Mathf.Max(0f, def.disengageDistance));

        Vector3 arrive = depart;

        arrive.x = targetX;



        SpawnDisengageDepartVfx(depart, def);

        yield return CoFlightPathMove(depart, arrive, def.disengageMoveSpeed, def.disengageAirHeight);

        SpawnDisengageLandVfx(arrive, def);



        if (_player && _enemy != null && !_enemy.IsDead)

            _enemy.FaceTargetWorldX(_player.position.x);



        IsMovementLocked = false;

        _movementRoutine = null;

    }



    private IEnumerator CoPounce(EnemyAbilityDefinition def, Vector3 targetWorld)

    {

        IsMovementLocked = true;

        if (_rb)

            _rb.linearVelocity = Vector2.zero;



        Vector3 depart = transform.position;

        Vector3 arrive = new Vector3(targetWorld.x, depart.y, depart.z);

        float telegraphSeconds = Mathf.Max(0.05f, def.pounceTelegraphSeconds);

        float shockwaveRadius = Mathf.Max(0.1f, def.pounceShockwaveRadius);
        float directHitRadius = Mathf.Max(0.1f, def.pounceDirectHitRadius);
        GameObject mark = SpawnPounceTargetMark(arrive, shockwaveRadius, directHitRadius);



        AcquireRedGlow();

        try

        {

            float telegraphElapsed = 0f;

            while (telegraphElapsed < telegraphSeconds)

            {

                if (_enemy == null || _enemy.IsDead || _enemy.IsStunned)

                    yield break;



                if (_player)

                    _enemy.FaceTargetWorldX(_player.position.x);



                telegraphElapsed += Time.deltaTime;

                yield return null;

            }



            if (mark)

                Destroy(mark);



            SpawnDisengageDepartVfx(depart, def);

            yield return CoFlightPathMove(depart, arrive, def.pounceMoveSpeed, def.pounceAirHeight);



            SpawnPounceShockwaveVfx(arrive, shockwaveRadius);

            _enemy.TryApplyAbilityShockwaveDamageToPlayer(
                arrive,
                shockwaveRadius,
                directHitRadius,
                def.pounceDirectHitDamageMultiplier);

            SpawnDisengageLandVfx(arrive, def);



            if (_player && _enemy != null && !_enemy.IsDead)

                _enemy.FaceTargetWorldX(_player.position.x);

        }

        finally

        {

            if (mark)

                Destroy(mark);



            ReleaseRedGlow();

            IsMovementLocked = false;

            _movementRoutine = null;

        }

    }



    private IEnumerator CoFlightPathMove(Vector3 depart, Vector3 arrive, float moveSpeed, float airHeight)

    {

        float totalDistance = EnemyAbilityFlightPath.HorizontalDistance(depart, arrive);

        if (totalDistance <= 0.02f)

        {

            ApplyPosition(arrive);

            yield break;

        }



        float traveled = 0f;

        float speed = Mathf.Max(0.1f, moveSpeed);

        while (traveled < totalDistance - 0.02f)

        {

            if (_enemy == null || _enemy.IsDead || _enemy.IsStunned)

                yield break;



            traveled += speed * Time.deltaTime;

            float progress = Mathf.Clamp01(traveled / totalDistance);

            Vector3 pos = EnemyAbilityFlightPath.Evaluate(depart, arrive, progress, airHeight);

            ApplyPosition(pos);



            if (_player)

                _enemy.FaceTargetWorldX(_player.position.x);



            yield return null;

        }



        ApplyPosition(arrive);

    }



    private void StopMovementRoutine()

    {

        if (_movementRoutine != null)

        {

            StopCoroutine(_movementRoutine);

            _movementRoutine = null;

        }



        IsMovementLocked = false;

    }



    private void ApplyPosition(Vector3 worldPosition)

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



    private void EnsureGlowRenderersCached()

    {

        if (_glowRenderers != null)

            return;



        Transform tintRoot = _enemy != null ? _enemy.GetVisualsRootTransform() : null;

        if (!tintRoot)

            tintRoot = transform;



        _glowRenderers = tintRoot.GetComponentsInChildren<SpriteRenderer>(true);

        _glowOriginalColors = new Color[_glowRenderers.Length];

        for (int i = 0; i < _glowRenderers.Length; i++)

            _glowOriginalColors[i] = _glowRenderers[i] != null ? _glowRenderers[i].color : Color.white;

    }



    private void AcquireRedGlow()

    {

        EnsureGlowRenderersCached();

        _activeGlowCount++;

        ApplyRedGlowTint(true);

    }



    private void ReleaseRedGlow()

    {

        if (_activeGlowCount <= 0)

            return;



        _activeGlowCount = Mathf.Max(0, _activeGlowCount - 1);

        if (_activeGlowCount == 0)

            ApplyRedGlowTint(false);

    }



    private void ApplyRedGlowTint(bool enabled) => ApplySpriteGlowTint(enabled ? PounceGlowColor : (Color?)null);

    private void BeginToxicFangsGlow(float durationSeconds)
    {
        EnsureGlowRenderersCached();
        if (_toxicFangsGlowRoutine != null)
            StopCoroutine(_toxicFangsGlowRoutine);

        _toxicFangsGlowRoutine = StartCoroutine(CoToxicFangsGlow(durationSeconds));
    }

    private void StopToxicFangsGlow()
    {
        if (_toxicFangsGlowRoutine != null)
        {
            StopCoroutine(_toxicFangsGlowRoutine);
            _toxicFangsGlowRoutine = null;
        }

        if (_activeGlowCount <= 0)
            ApplySpriteGlowTint(null);
    }

    private IEnumerator CoToxicFangsGlow(float durationSeconds)
    {
        ApplySpriteGlowTint(ToxicFangsGlowColor);
        yield return new WaitForSeconds(durationSeconds);
        _toxicFangsGlowRoutine = null;
        if (_activeGlowCount <= 0)
            ApplySpriteGlowTint(null);
    }

    private void ApplySpriteGlowTint(Color? glowColor)
    {
        EnsureGlowRenderersCached();
        if (_glowRenderers == null || _glowOriginalColors == null)
            return;

        for (int i = 0; i < _glowRenderers.Length; i++)
        {
            SpriteRenderer sr = _glowRenderers[i];
            if (!sr)
                continue;

            if (glowColor.HasValue)
            {
                Color orig = _glowOriginalColors[i];
                Color c = Color.Lerp(orig, glowColor.Value, 0.72f);
                c.r = Mathf.Clamp01(c.r * glowColor.Value.r);
                c.g = Mathf.Clamp01(c.g * glowColor.Value.g);
                c.b = Mathf.Clamp01(c.b * glowColor.Value.b);
                c.a = orig.a;
                sr.color = c;
            }
            else
            {
                sr.color = _glowOriginalColors[i];
            }
        }
    }



    private void SpawnDisengageDepartVfx(Vector3 departPosition, EnemyAbilityDefinition def)

    {

        PlayerAbilityVfxController vfx = ResolvePlayerAbilityVfx();

        if (!vfx)

            return;



        if (def.useCustomAbilitySmokeColor)

            vfx.SpawnShadowStrikeDepartSmoke(departPosition, def.abilitySmokeCoreColor, def.abilitySmokeEdgeColor);

        else

            vfx.SpawnShadowStrikeDepartSmoke(departPosition);

    }



    private void SpawnDisengageLandVfx(Vector3 landPosition, EnemyAbilityDefinition def)

    {

        PlayerAbilityVfxController vfx = ResolvePlayerAbilityVfx();

        if (!vfx)

            return;



        if (def.useCustomAbilitySmokeColor)

            vfx.SpawnShadowStrikeBurst(landPosition, def.abilitySmokeCoreColor);

        else

            vfx.SpawnShadowStrikeBurst(landPosition);

    }



    private GameObject SpawnPounceTargetMark(Vector3 worldPosition, float shockwaveRadius, float directHitRadius)

    {

        PlayerAbilityVfxController vfx = ResolvePlayerAbilityVfx();

        return vfx != null
            ? vfx.SpawnEnemyPounceTelegraph(worldPosition, shockwaveRadius, directHitRadius)
            : null;

    }



    private void SpawnPounceShockwaveVfx(Vector3 landPosition, float radius)

    {

        PlayerAbilityVfxController vfx = ResolvePlayerAbilityVfx();

        vfx?.SpawnEnemyAbilityImpactShockwave(landPosition, radius);

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


