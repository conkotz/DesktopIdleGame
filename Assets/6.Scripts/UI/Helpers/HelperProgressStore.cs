using System.Collections.Generic;
using UnityEngine;

/// <summary>/// Tracks which helpers were dismissed for the current save (<see cref="SaveData.dismissedHelperIds"/>).
/// New Game creates an empty save list so tutorials show again on that slot.
/// </summary>
public static class HelperProgressStore
{
    private const string LegacyKeyPrefix = "Helper.Shown.";

    private static readonly HashSet<string> RegisteredIds = new();

    private static readonly HashSet<string> DismissedThisSave = new();

    /// <summary>True once <see cref="ApplyFromSaveData"/> rebuilt state from SaveManager.</summary>
    public static bool IsHydratedFromSave { get; private set; }

    public static void RegisterDefinition(HelperPopupDefinition def)
    {
        if (!def || string.IsNullOrWhiteSpace(def.helperId))
            return;
        RegisteredIds.Add(def.helperId.Trim());
    }

    public static bool WasDismissed(string helperId)
    {
        if (string.IsNullOrWhiteSpace(helperId))
            return false;
        return DismissedThisSave.Contains(helperId.Trim());
    }

    public static void MarkDismissed(string helperId)
    {
        if (string.IsNullOrWhiteSpace(helperId))
            return;

        string id = helperId.Trim();
        if (!DismissedThisSave.Add(id))
            return;

        PlayerPrefs.DeleteKey(LegacyKeyPrefix + id);

        SaveManager.Instance?.Save();
        PlayerPrefs.Save();
    }

    /// <summary>Next <see cref="ApplyFromSaveData"/> will set <see cref="IsHydratedFromSave"/>.</summary>
    internal static void ResetHydrationForNewSession()
    {
        IsHydratedFromSave = false;
    }

    internal static void ApplyFromSaveData(SaveData data)
    {
        DismissedThisSave.Clear();

        List<string> list = data?.dismissedHelperIds;
        if (list != null)
        {
            for (int i = 0; i < list.Count; i++)
            {
                string row = list[i];
                if (string.IsNullOrWhiteSpace(row))
                    continue;
                DismissedThisSave.Add(row.Trim());
            }
        }

        IsHydratedFromSave = true;
    }

    internal static void WriteDismissedInto(SaveData data)
    {
        if (data == null)
            return;

        data.dismissedHelperIds ??= new List<string>();
        data.dismissedHelperIds.Clear();

        if (DismissedThisSave.Count == 0)
            return;

        List<string> sorted = new(DismissedThisSave);
        sorted.Sort(string.CompareOrdinal);

        foreach (string id in sorted)
            data.dismissedHelperIds.Add(id);
    }

    /// <summary>Yields until SaveManager hydrated helper progress (normally same frame).</summary>
    internal static System.Collections.IEnumerator WaitUntilHydratedFromSave()
    {
        float timeoutAt = Time.unscaledTime + 5f;

        while (!IsHydratedFromSave)
        {
            if (Time.unscaledTime > timeoutAt)
            {
                Debug.LogWarning("[HelperProgressStore] Timed out waiting for save hydration — treating helpers as not dismissed.");
                IsHydratedFromSave = true;
                yield break;
            }

            yield return null;
        }
    }

    /// <summary>Deletes legacy global PlayerPrefs keys for registered helper ids (optional cleanup).</summary>
    public static void ClearRegisteredLegacyPlayerPrefs()
    {
        foreach (string id in RegisteredIds)
            PlayerPrefs.DeleteKey(LegacyKeyPrefix + id);

        PlayerPrefs.Save();
    }
}
