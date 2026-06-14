using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks a summon as a valid enemy retaliation target (HP bar + enemy melee attacks).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterStats))]
public class MinionCombatTarget : MonoBehaviour
{
    private static readonly List<MinionCombatTarget> s_active = new();

    [SerializeField] private CharacterStats stats;
    [SerializeField] private MinionUnit unit;

    private PlayerCombatController _ownerCombat;

    public static IReadOnlyList<MinionCombatTarget> ActiveTargets => s_active;

    public static bool IsMinionTransform(Transform t)
    {
        if (!t)
            return false;

        return t.GetComponent<MinionCombatTarget>() != null ||
               t.GetComponentInParent<MinionCombatTarget>() != null;
    }

    public CharacterStats Stats => stats;
    public bool IsAlive => stats && !stats.IsDead;
    public PlayerCombatController OwnerCombat => _ownerCombat;

    public void BindOwnerCombat(PlayerCombatController ownerCombat) => _ownerCombat = ownerCombat;

    private void Awake()
    {
        if (!stats)
            stats = GetComponent<CharacterStats>();
        if (!unit)
            unit = GetComponent<MinionUnit>();
    }

    private void OnEnable()
    {
        if (!s_active.Contains(this))
            s_active.Add(this);
    }

    private void OnDisable() => s_active.Remove(this);

    public int TakeDamageFromEnemy(
        SplitDamage hit,
        bool wasCrit,
        Transform attacker,
        EnemyBaseController sourceEnemy)
    {
        if (!IsAlive || hit.IsEmpty)
            return 0;

        int total = 0;

        if (hit.physical > 0f)
            total += ApplyTypedDamage(hit.physical, DamageType.Physical, wasCrit, attacker);
        if (hit.magic > 0f)
            total += ApplyTypedDamage(hit.magic, DamageType.Magic, wasCrit, attacker);
        if (hit.corruptionDamage > 0f)
            total += ApplyTypedDamage(hit.corruptionDamage, DamageType.Corruption, false, attacker);

        if (total > 0)
        {
            unit?.TriggerHurt();
            if (sourceEnemy != null)
            {
                sourceEnemy.NotifyRetaliationAgainstMinion(transform);
                NotifyMinionCombatControllerStruck(sourceEnemy);
            }
        }

        return total;
    }

    private void NotifyMinionCombatControllerStruck(EnemyBaseController sourceEnemy)
    {
        MinionCombatController combat = GetComponent<MinionCombatController>();
        if (!combat)
            combat = GetComponentInParent<MinionCombatController>();
        combat?.NotifyStruckByEnemy(sourceEnemy);
    }

    private int ApplyTypedDamage(float amount, DamageType type, bool wasCrit, Transform attacker)
    {
        float applied = stats.TakeDamage(amount, type, out bool blocked, out _);
        int finalDamage = Mathf.Max(0, Mathf.RoundToInt(applied));
        if (finalDamage <= 0)
            return 0;

        if (blocked)
            wasCrit = false;

        if (DamagePopupSystem.Instance != null &&
            ToggleSettingsStore.Get(ToggleSettingId.ShowIncomingDamageNumbers))
        {
            DamagePopupAnchor anchor = GetComponentInChildren<DamagePopupAnchor>(true);
            Vector3 anchorPos = anchor != null ? anchor.WorldPos : transform.position;
            Vector3 dealerPos = attacker != null ? attacker.position : transform.position;
            Vector3 dir = (anchorPos - dealerPos).sqrMagnitude > 1e-6f
                ? (anchorPos - dealerPos).normalized
                : Vector3.up;

            if (!blocked)
            {
                Vector3 pos = DamagePopupSystem.GetWorldPosBehindVictim(anchorPos, dealerPos);
                FloatingDamageTextUI.PopupDamageKind popupKind = type switch
                {
                    DamageType.Magic => FloatingDamageTextUI.PopupDamageKind.Magic,
                    DamageType.Corruption => FloatingDamageTextUI.PopupDamageKind.Corruption,
                    DamageType.Typless => FloatingDamageTextUI.PopupDamageKind.Typless,
                    _ => FloatingDamageTextUI.PopupDamageKind.Physical
                };

                DamagePopupSystem.Instance.Spawn(
                    pos,
                    finalDamage,
                    popupKind,
                    wasCrit,
                    false,
                    dir,
                    false);
            }
        }

        return finalDamage;
    }
}
