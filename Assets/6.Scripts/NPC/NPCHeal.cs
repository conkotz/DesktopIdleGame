using UnityEngine;

public interface INPCInteractionEffect
{
    void OnNpcInteract(NPCInteractionSettings source);
}

[DisallowMultipleComponent]
public class NPCHeal : MonoBehaviour, INPCInteractionEffect
{
    [Header("Restore")]
    [SerializeField] private bool restoreHP = true;
    [SerializeField] private bool restoreEnergy = true;
    [SerializeField] private bool restoreMana = true;
    [SerializeField] private bool restoreGuard = true;

    [Header("Feedback")]
    [SerializeField] private bool showPlayerPopup = true;
    [SerializeField] private string restoredMessage = "Fully restored.";

    public void OnNpcInteract(NPCInteractionSettings source)
    {
        PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (!player)
            return;

        CharacterStats stats = player.GetComponent<CharacterStats>();
        if (!stats || stats.IsDead)
            return;

        stats.RefreshVitalsFromStats(fillIfEmpty: false);

        if (restoreHP)
            player.Heal(player.MaxHP);

        if (restoreEnergy)
            player.AddEnergy(player.MaxEnergy);

        if (restoreMana)
            player.AddMana(player.MaxMana);

        if (restoreGuard)
            RestoreGuardToNaturalCap(stats);

        if (showPlayerPopup && !string.IsNullOrWhiteSpace(restoredMessage))
            player.ShowPopup(restoredMessage);
    }

    private static void RestoreGuardToNaturalCap(CharacterStats stats)
    {
        float cap = stats.NaturalGuardCap;
        if (cap <= 0f)
            return;

        float missingGuard = cap - stats.Guard;
        if (missingGuard > 0.0001f)
            stats.AddBonusGuard(missingGuard);
    }
}
