using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

/// <summary>
/// Handles ability cooldowns + executing ability effects.
/// Minimal implementation for "Power Slash" style abilities.
/// </summary>
[DisallowMultipleComponent]
public class PlayerAbilityController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerController player;
    [SerializeField] private CharacterStats stats;
    [SerializeField] private PlayerCombatController combat;

    [Header("Global Cooldown")]
    [SerializeField, Min(0f)] private float globalCooldownSeconds = 0.15f;
    private float _globalCooldownEndsAt;

    [Header("Power Slash VFX")]
    [SerializeField] private Transform powerSlashTrailAnchor;
    [SerializeField] private string[] powerSlashAnchorNameCandidates = { "Weapon", "MainHandItem", "MainHand" };
    [SerializeField] private Color powerSlashTrailColor = new Color(1f, 0.88f, 0.22f, 0.95f);
    [SerializeField, Min(0.01f)] private float powerSlashTrailTime = 0.18f;
    [SerializeField, Min(0.01f)] private float powerSlashSwingDuration = 0.14f;
    [SerializeField, Min(0.01f)] private float powerSlashTrailWidth = 0.4f;
    [SerializeField] private Vector2 powerSlashAngleRange = new Vector2(155f, -30f);
    [SerializeField] private Vector3 powerSlashLocalOffset = new Vector3(0.04f, 0.02f, 0f);
    [SerializeField] private Vector2 powerSlashTipLocalOffset = new Vector2(0.52f, 0.06f);
    [SerializeField, Min(0f)] private float powerSlashEdgeFollowSmoothing = 0.06f;
    [SerializeField] private bool powerSlashUseDoubleSwipe = true;
    [SerializeField, Min(0f)] private float powerSlashSecondSwipeDelay = 0.035f;
    [SerializeField] private float powerSlashSecondSwipeAngleOffset = 18f;

    private readonly Dictionary<string, float> _cooldownEndsById = new(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        if (!player) player = GetComponent<PlayerController>();
        if (!stats) stats = GetComponent<CharacterStats>();
        if (!combat) combat = GetComponent<PlayerCombatController>();
    }

    public bool IsOnCooldown(string abilityId, out float remainingSeconds)
    {
        remainingSeconds = 0f;
        if (string.IsNullOrWhiteSpace(abilityId))
            return false;

        if (!_cooldownEndsById.TryGetValue(abilityId, out float end))
            return false;

        remainingSeconds = Mathf.Max(0f, end - Time.time);
        return remainingSeconds > 0f;
    }

    public float GetCooldownNormalized(string abilityId)
    {
        var def = AbilityLibrary.Get(abilityId);
        if (!def || def.cooldown <= 0f)
            return 0f;

        if (!IsOnCooldown(abilityId, out float remaining))
            return 0f;

        return Mathf.Clamp01(remaining / Mathf.Max(0.01f, def.cooldown));
    }

    public bool IsOnGlobalCooldown(out float remainingSeconds)
    {
        remainingSeconds = Mathf.Max(0f, _globalCooldownEndsAt - Time.time);
        return remainingSeconds > 0f;
    }

    public float GetGlobalCooldownNormalized()
    {
        if (globalCooldownSeconds <= 0f)
            return 0f;

        if (!IsOnGlobalCooldown(out float remaining))
            return 0f;

        return Mathf.Clamp01(remaining / Mathf.Max(0.01f, globalCooldownSeconds));
    }

    public bool TryUseAbility(string abilityId)
    {
        AbilityDefinition def = AbilityLibrary.Get(abilityId);
        if (!def)
            return false;

        if (globalCooldownSeconds > 0f && Time.time < _globalCooldownEndsAt)
            return false;

        if (IsOnCooldown(def.abilityId, out _))
            return false;

        if (!player || !stats)
            return false;

        if (def.energyCost > 0f && !player.SpendEnergy(def.energyCost))
        {
            player.ShowPopup("Not enough energy.");
            return false;
        }

        EnemyBaseController target = combat != null ? combat.CurrentTarget : null;
        if (target == null || target.IsDead)
        {
            player.ShowPopup("No target.");
            return false;
        }

        // Instant-cast damage model:
        // - base physical = average weapon physical hit (already includes buffs/gear via Min/Max split damage)
        // - apply physical multiplier to that base hit
        // - add ability power scaling on top
        float basePhysical =
            (Mathf.Max(0f, stats.MinSplitDamage.physical) + Mathf.Max(0f, stats.MaxSplitDamage.physical)) * 0.5f;

        float scaledPhysical = basePhysical * Mathf.Max(0f, def.physicalDamageMultiplier);
        float physicalBonus = Mathf.Max(0f, scaledPhysical - basePhysical);
        float apBonus = Mathf.Max(0f, stats.AbilityPower * Mathf.Max(0f, def.abilityPowerMultiplier));
        float raw = Mathf.Max(0f, scaledPhysical + apBonus);

        bool wasCrit = false;
        if (raw > 0f && UnityEngine.Random.value < Mathf.Clamp01(stats.CritChance))
        {
            wasCrit = true;
            raw *= Mathf.Max(1f, stats.CritMultiplier);
        }

        int final = Mathf.Max(0, Mathf.RoundToInt(raw));
        int dealt = 0;
        if (final > 0)
            dealt = target.TakeDamage(final, DamageType.Physical, wasCrit, transform);

        if (string.Equals(def.abilityId, "power_slash", StringComparison.OrdinalIgnoreCase))
        {
            SpawnPowerSlashTrail();
            player.ShowPopup("power slash used");
            float totalBonus = Mathf.Max(0f, physicalBonus + apBonus);
            Debug.Log(
                $"[Ability] Power Slash instant cast. " +
                $"BaseHit={basePhysical:0.##}, " +
                $"PhysicalBonus={physicalBonus:0.##}, " +
                $"ApBonus={apBonus:0.##}, " +
                $"TotalAbilityBonus={totalBonus:0.##}, " +
                $"FinalHitPreMitigation={raw:0.##}, " +
                $"Dealt={dealt}, crit={wasCrit}");
        }

        // Fire the attack anim as feedback, but do not modify basic attack cooldown timing.
        player.TriggerAttackAnim();
        StartCooldown(def);
        if (globalCooldownSeconds > 0f)
            _globalCooldownEndsAt = Time.time + globalCooldownSeconds;
        return true;
    }

    private void StartCooldown(AbilityDefinition def)
    {
        if (!def) return;
        float cd = Mathf.Max(0f, def.cooldown);
        if (cd <= 0f) return;
        _cooldownEndsById[def.abilityId] = Time.time + cd;
    }

    private void SpawnPowerSlashTrail()
    {
        Transform anchor = ResolvePowerSlashAnchor();
        if (anchor == null)
            return;

        SpawnSinglePowerSlashTrail(anchor, 0f, 0f);
        if (powerSlashUseDoubleSwipe)
            SpawnSinglePowerSlashTrail(anchor, powerSlashSecondSwipeAngleOffset, powerSlashSecondSwipeDelay);
    }

    private void SpawnSinglePowerSlashTrail(Transform anchor, float angleOffset, float delay)
    {
        GameObject slashGO = new GameObject("PowerSlashTrail");
        slashGO.transform.SetParent(anchor, false);
        slashGO.transform.localPosition = Vector3.zero;

        TrailRenderer trail = slashGO.AddComponent<TrailRenderer>();
        trail.time = Mathf.Max(0.01f, powerSlashTrailTime);
        trail.minVertexDistance = 0.004f;
        trail.widthMultiplier = Mathf.Max(0.01f, powerSlashTrailWidth);
        trail.numCornerVertices = 4;
        trail.numCapVertices = 4;
        trail.alignment = LineAlignment.TransformZ;
        trail.textureMode = LineTextureMode.Stretch;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.emitting = false;

        AnimationCurve widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.95f),
            new Keyframe(0.35f, 1f),
            new Keyframe(1f, 0f)
        );
        trail.widthCurve = widthCurve;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(powerSlashTrailColor, 0f),
                new GradientColorKey(Color.Lerp(powerSlashTrailColor, Color.white, 0.25f), 0.45f),
                new GradientColorKey(powerSlashTrailColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(powerSlashTrailColor.a, 0f),
                new GradientAlphaKey(Mathf.Clamp01(powerSlashTrailColor.a * 0.75f), 0.35f),
                new GradientAlphaKey(Mathf.Clamp01(powerSlashTrailColor.a * 0.4f), 0.65f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trail.colorGradient = gradient;

        StartCoroutine(AnimatePowerSlashTrail(
            slashGO.transform,
            anchor,
            powerSlashSwingDuration,
            trail.time + 0.08f,
            angleOffset,
            Mathf.Max(0f, delay)));
    }

    private IEnumerator AnimatePowerSlashTrail(
        Transform slashTransform,
        Transform anchor,
        float swingDuration,
        float lingerAfter,
        float angleOffset,
        float startDelay)
    {
        if (slashTransform == null || anchor == null)
            yield break;

        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        float duration = Mathf.Max(0.01f, swingDuration);
        float elapsed = 0f;

        float facing = 1f;
        EnemyBaseController target = combat != null ? combat.CurrentTarget : null;
        if (target != null)
            facing = target.transform.position.x >= transform.position.x ? 1f : -1f;
        else if (player != null)
            facing = player.transform.localScale.x >= 0f ? 1f : -1f;

        float startAngle = powerSlashAngleRange.x;
        float endAngle = powerSlashAngleRange.y;
        Vector3 smoothWorldPos = slashTransform.position;
        slashTransform.localPosition = powerSlashLocalOffset;
        TrailRenderer trail = slashTransform.GetComponent<TrailRenderer>();
        if (trail != null)
            trail.Clear();

        while (elapsed < duration && slashTransform != null && anchor != null)
        {
            float t = elapsed / duration;
            float angle = Mathf.Lerp(startAngle, endAngle, t) + angleOffset;
            float signedAngle = angle * facing;

            Vector2 dir = new Vector2(Mathf.Cos(signedAngle * Mathf.Deg2Rad), Mathf.Sin(signedAngle * Mathf.Deg2Rad));
            Vector3 tipLocal = powerSlashLocalOffset + new Vector3(
                dir.x * powerSlashTipLocalOffset.x * facing,
                dir.y * powerSlashTipLocalOffset.y,
                0f);

            Vector3 desiredWorld = anchor.TransformPoint(tipLocal);
            float smooth = Mathf.Clamp01(powerSlashEdgeFollowSmoothing <= 0.0001f ? 1f : (Time.deltaTime / powerSlashEdgeFollowSmoothing));
            smoothWorldPos = Vector3.Lerp(smoothWorldPos, desiredWorld, smooth);
            slashTransform.position = smoothWorldPos;

            if (trail != null && !trail.emitting && t >= 0.05f)
                trail.emitting = true;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (slashTransform != null)
            Destroy(slashTransform.gameObject, Mathf.Max(0.05f, lingerAfter));
    }

    private Transform ResolvePowerSlashAnchor()
    {
        if (powerSlashTrailAnchor != null)
            return powerSlashTrailAnchor;

        MainHandEquipper mainHand = GetComponentInChildren<MainHandEquipper>(true);
        if (mainHand != null)
        {
            Transform root = mainHand.transform.root;
            Transform found = FindChildByCandidateName(root, powerSlashAnchorNameCandidates);
            if (found != null)
            {
                powerSlashTrailAnchor = found;
                return powerSlashTrailAnchor;
            }
        }

        powerSlashTrailAnchor = FindChildByCandidateName(transform.root, powerSlashAnchorNameCandidates);
        if (powerSlashTrailAnchor != null)
            return powerSlashTrailAnchor;

        return transform;
    }

    private static Transform FindChildByCandidateName(Transform root, string[] candidates)
    {
        if (root == null || candidates == null || candidates.Length == 0)
            return null;

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null)
                continue;

            for (int c = 0; c < candidates.Length; c++)
            {
                string candidate = candidates[c];
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                if (string.Equals(t.name, candidate, StringComparison.OrdinalIgnoreCase))
                    return t;
            }
        }

        return null;
    }
}

