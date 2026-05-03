using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks which helpers were dismissed for the current save (<see cref="SaveData.dismissedHelperIds"/>)
/// and which helper ids should never show the flashing &quot;! new&quot; badge again (<see cref="SaveData.helperNewBadgeSuppressedHelperIds"/>).
/// New Game creates empty lists so tutorials show again on that slot.
/// </summary>
public static class HelperProgressStore
{
    private const string LegacyKeyPrefix = "Helper.Shown.";

    private static readonly HashSet<string> RegisteredIds = new();

    private static readonly HashSet<string> DismissedThisSave = new();

    private static readonly HashSet<string> NewBadgeSuppressedThisSave = new();

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

    /// <summary>
    /// Clears dismissed-helper state and persists. Used when the player turns &quot;Show help popups&quot; back on
    /// so previously dismissed tips can appear again.
    /// </summary>
    public static void ClearDismissedForHelpToggleOn()
    {
        DismissedThisSave.Clear();
        NewBadgeSuppressedThisSave.Clear();
        foreach (string id in RegisteredIds)
            PlayerPrefs.DeleteKey(LegacyKeyPrefix + id);

        SaveManager.Instance?.Save();
        PlayerPrefs.Save();
    }

    /// <summary>Next <see cref="ApplyFromSaveData"/> will set <see cref="IsHydratedFromSave"/>.</summary>
    internal static void ResetHydrationForNewSession()
    {
        IsHydratedFromSave = false;
    }

    public static bool WasNewBadgeSuppressed(string helperId)
    {
        if (string.IsNullOrWhiteSpace(helperId))
            return false;
        return NewBadgeSuppressedThisSave.Contains(helperId.Trim());
    }

    public static void MarkNewBadgeSuppressed(string helperId)
    {
        if (string.IsNullOrWhiteSpace(helperId))
            return;

        string id = helperId.Trim();
        if (!NewBadgeSuppressedThisSave.Add(id))
            return;

        SaveManager.Instance?.Save();
        PlayerPrefs.Save();
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

        NewBadgeSuppressedThisSave.Clear();
        List<string> badgeList = data?.helperNewBadgeSuppressedHelperIds;
        if (badgeList != null)
        {
            for (int i = 0; i < badgeList.Count; i++)
            {
                string row = badgeList[i];
                if (string.IsNullOrWhiteSpace(row))
                    continue;
                NewBadgeSuppressedThisSave.Add(row.Trim());
            }
        }

        // Older saves only had dismissedHelperIds: treat those as "already saw ! new" so respawn/loads stay quiet.
        foreach (string id in DismissedThisSave)
            NewBadgeSuppressedThisSave.Add(id);

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

    internal static void WriteNewBadgeSuppressedInto(SaveData data)
    {
        if (data == null)
            return;

        data.helperNewBadgeSuppressedHelperIds ??= new List<string>();
        data.helperNewBadgeSuppressedHelperIds.Clear();

        if (NewBadgeSuppressedThisSave.Count == 0)
            return;

        List<string> sorted = new(NewBadgeSuppressedThisSave);
        sorted.Sort(string.CompareOrdinal);

        foreach (string id in sorted)
            data.helperNewBadgeSuppressedHelperIds.Add(id);
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
