using System.Collections.Generic;
using UnityEngine;

public sealed class TornadoCastGroup
{
    public readonly List<TornadoInstance> Instances = new();
}

[DisallowMultipleComponent]
public sealed class TornadoInstance : MonoBehaviour
{
    [SerializeField] private Collider2D hitCollider;

    private PlayerAbilityController _owner;
    private TornadoCastGroup _group;
    private EnemyBaseController _target;
    private float _endsAt;
    private float _nextDamageTickAt;
    private float _lightningInfusionBonus;
    private bool _canAbsorbLightning;
    private bool _useBowPhysicalBonus;
    private float _bowPhysicalMinBonus;
    private float _bowPhysicalMaxBonus;
    private float _colliderBottomBelowRoot;
    private float _baseWorldScale;
    private bool _lightningInfusionScaleApplied;

    public bool IsAlive => _owner != null && Time.time < _endsAt;
    public bool CanAbsorbLightning => _canAbsorbLightning && IsAlive;
    public EnemyBaseController CurrentTarget => _target;

    public void RetargetToClosestEnemy(bool preferDifferentTarget)
    {
        EnemyBaseController exclude = preferDifferentTarget ? _target : null;
        EnemyBaseController next = FindBestTarget(exclude);
        if (next == null && exclude != null)
            next = FindBestTarget(null);

        _target = next;
    }

    public void Initialize(
        PlayerAbilityController owner,
        TornadoCastGroup group,
        EnemyBaseController initialTarget,
        float durationSeconds,
        float worldScale,
        bool canAbsorbLightning,
        bool useBowPhysicalBonus,
        float bowPhysicalMinBonus,
        float bowPhysicalMaxBonus)
    {
        _owner = owner;
        _group = group;
        _target = initialTarget;
        _endsAt = Time.time + Mathf.Max(0.1f, durationSeconds);
        _nextDamageTickAt = Time.time + AbilityCombatPower.TornadoDamageTickIntervalSeconds;
        _canAbsorbLightning = canAbsorbLightning;
        _useBowPhysicalBonus = useBowPhysicalBonus;
        _bowPhysicalMinBonus = Mathf.Max(0f, bowPhysicalMinBonus);
        _bowPhysicalMaxBonus = Mathf.Max(_bowPhysicalMinBonus, bowPhysicalMaxBonus);
        _baseWorldScale = Mathf.Max(0.1f, worldScale);
        _lightningInfusionScaleApplied = false;

        transform.localScale = Vector3.one * _baseWorldScale;
        ApplySpriteOpacity();
        CacheColliderBottomOffset();
        TornadoCombatRegistry.Register(this);
        if (_group != null)
            _group.Instances.Add(this);
    }

    private void OnDestroy()
    {
        TornadoCombatRegistry.Unregister(this);
        if (_group != null)
            _group.Instances.Remove(this);
    }

    private void Update()
    {
        if (!IsAlive)
        {
            Destroy(gameObject);
            return;
        }

        RefreshTarget();
        TickMovement();
        TickDamage();
    }

    public Vector3 GetLightningArcAnchor()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
            return sr.bounds.center;

        if (hitCollider != null)
            return hitCollider.bounds.center;

        return transform.position + Vector3.up * 1.5f;
    }

    public void AbsorbLightningArc(float arcLightningDamage)
    {
        if (!CanAbsorbLightning || arcLightningDamage <= 0f)
            return;

        float infusionBonus = arcLightningDamage * AbilityCombatPower.TornadoLightningInfusionPerArcFraction;
        _lightningInfusionBonus = Mathf.Max(_lightningInfusionBonus, infusionBonus);
        ApplyLightningInfusionScaleIfNeeded();
    }

    private void ApplyLightningInfusionScaleIfNeeded()
    {
        if (!_canAbsorbLightning || _lightningInfusionScaleApplied || _lightningInfusionBonus <= 0.001f)
            return;

        _lightningInfusionScaleApplied = true;
        float infusedScale = _baseWorldScale * (1f + AbilityCombatPower.TornadoLightningInfusionScaleBonus);
        transform.localScale = Vector3.one * infusedScale;
        CacheColliderBottomOffset();
    }

    private void ApplySpriteOpacity()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null)
            return;

        Color color = sr.color;
        color.a = AbilityCombatPower.TornadoSpriteOpacity;
        sr.color = color;
    }

    public void SnapBottomToFloorAtX(float worldX)
    {
        Vector3 point = LaneGroundEffectPlacement.SnapWorldPointToLaneFloor(new Vector3(worldX, 0f, 0f), 0.02f);
        transform.position = new Vector3(point.x, point.y + _colliderBottomBelowRoot, point.z);
    }

    private void CacheColliderBottomOffset()
    {
        if (hitCollider == null)
            hitCollider = GetComponent<Collider2D>();

        if (hitCollider != null)
        {
            Physics2D.SyncTransforms();
            _colliderBottomBelowRoot = transform.position.y - hitCollider.bounds.min.y;
            return;
        }

        _colliderBottomBelowRoot = 0f;
    }

    private void RefreshTarget()
    {
        if (_target != null && !_target.IsDead && _target.gameObject.activeInHierarchy)
            return;

        _target = FindBestTarget(null);
    }

    private EnemyBaseController FindBestTarget(EnemyBaseController exclude)
    {
        if (_owner == null)
            return null;

        IReadOnlyList<EnemyBaseController> enemies = CombatEnemyRegistry.GetLiveEnemies();
        EnemyBaseController best = null;
        float bestDistSq = float.MaxValue;
        Vector3 origin = transform.position;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyBaseController enemy = enemies[i];
            if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
                continue;
            if (exclude != null && enemy == exclude)
                continue;
            float distSq = (enemy.transform.position - origin).sqrMagnitude;
            if (distSq >= bestDistSq)
                continue;

            bestDistSq = distSq;
            best = enemy;
        }

        return best;
    }

    private void TickMovement()
    {
        if (_target == null || _target.IsDead)
            return;

        Vector3 targetPos = _target.transform.position;
        float attachDistance = AbilityCombatPower.TornadoAttachDistance;
        float dist = Vector2.Distance(transform.position, targetPos);

        if (dist <= attachDistance)
        {
            SnapBottomToFloorAtX(targetPos.x);
            return;
        }

        Vector3 next = Vector3.MoveTowards(
            transform.position,
            new Vector3(targetPos.x, transform.position.y, 0f),
            AbilityCombatPower.TornadoMoveSpeed * Time.deltaTime);
        SnapBottomToFloorAtX(next.x);
    }

    private void TickDamage()
    {
        if (Time.time < _nextDamageTickAt)
            return;

        _nextDamageTickAt = Time.time + AbilityCombatPower.TornadoDamageTickIntervalSeconds;

        if (hitCollider == null)
            hitCollider = GetComponent<Collider2D>();
        if (hitCollider == null || _owner == null)
            return;

        ContactFilter2D filter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = true,
            layerMask = Physics2D.AllLayers
        };

        var hits = new List<Collider2D>(8);
        hitCollider.Overlap(filter, hits);

        for (int i = 0; i < hits.Count; i++)
        {
            Collider2D col = hits[i];
            if (col == null)
                continue;

            EnemyBaseController enemy = col.GetComponentInParent<EnemyBaseController>();
            if (enemy == null || enemy.IsDead)
                continue;

            ApplyDamageTickToEnemy(enemy);
        }
    }

    private void ApplyDamageTickToEnemy(EnemyBaseController enemy)
    {
        if (enemy == null || enemy.IsDead || _owner == null)
            return;

        float tickMin = AbilityCombatPower.TornadoBaseMinDamagePerSecond;
        float tickMax = AbilityCombatPower.TornadoBaseMaxDamagePerSecond;

        if (_useBowPhysicalBonus)
        {
            tickMin += _bowPhysicalMinBonus;
            tickMax += _bowPhysicalMaxBonus;
        }

        bool infused = _lightningInfusionBonus > 0.001f;
        if (infused)
        {
            tickMin += _lightningInfusionBonus;
            tickMax += _lightningInfusionBonus;
            float magicDamage = Random.Range(tickMin, tickMax);
            SplitDamage hit = new SplitDamage(0f, Mathf.Max(0f, magicDamage), 0f);
            _owner.ApplyTornadoSplitDamage(enemy, hit, applyGlobalPhysical: false);
            return;
        }

        float physicalDamage = Random.Range(tickMin, tickMax);
        SplitDamage physicalHit = new SplitDamage(Mathf.Max(0f, physicalDamage), 0f, 0f);
        _owner.ApplyTornadoSplitDamage(enemy, physicalHit, applyGlobalPhysical: true);
    }
}
