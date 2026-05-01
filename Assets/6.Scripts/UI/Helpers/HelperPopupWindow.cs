using UnityEngine;

/// <summary>
/// Host object for the runtime-built helper popup. Marked <see cref="DontDestroyOnLoad"/> so layout survives level loads;
/// reparented under the current scene's <c>FullWindowCanvas/WindowsArea</c> when <see cref="EnsureUnderWindowsArea"/> runs.
/// </summary>
[DisallowMultipleComponent]
public sealed class HelperPopupWindow : MonoBehaviour
{
    private bool _markedPersistent;

    public static RectTransform ResolveWindowsArea()
    {
        GameObject full = GameObject.FindGameObjectWithTag("FullWindowCanvas");
        if (!full)
            return null;

        Transform windowsArea = full.transform.Find("WindowsArea");
        return windowsArea as RectTransform;
    }

    public static HelperPopupWindow FindExisting() =>
        FindFirstObjectByType<HelperPopupWindow>(FindObjectsInactive.Include);

    /// <summary>Reparents into <paramref name="windowsArea"/> and stretches to fill it (same as original overlay root).</summary>
    public void EnsureUnderWindowsArea(RectTransform windowsArea)
    {
        if (!windowsArea)
            return;

        RectTransform rt = transform as RectTransform;
        rt.SetParent(windowsArea, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    /// <summary>
    /// Keeps this object alive across scene loads. Must be a root before calling (Unity requirement).
    /// </summary>
    public void MarkPersistentRoot()
    {
        if (_markedPersistent)
            return;

        _markedPersistent = true;
        transform.SetParent(null, false);
        DontDestroyOnLoad(gameObject);
    }
}

/// <summary>PlayerPrefs-backed layout for <see cref="HelperPopupWindow"/> / <c>HelperPanel</c>.</summary>
public static class HelperPopupLayoutPrefs
{
    private const string HasLayout = "UI.HelperPopup.layoutV1";

    public const string PosX = "UI.HelperPopup.posX";
    public const string PosY = "UI.HelperPopup.posY";

    public const string LastExpW = "UI.HelperPopup.lastExpW";
    public const string LastExpH = "UI.HelperPopup.lastExpH";

    private const string HeaderOnly = "UI.HelperPopup.headerOnly";

    public static bool HasSavedLayout() => PlayerPrefs.GetInt(HasLayout, 0) != 0;

    public static void Save(Vector2 anchoredPosition, Vector2 lastExpandedSizeDelta, bool headerOnlyLayout)
    {
        PlayerPrefs.SetInt(HasLayout, 1);
        PlayerPrefs.SetFloat(PosX, anchoredPosition.x);
        PlayerPrefs.SetFloat(PosY, anchoredPosition.y);
        PlayerPrefs.SetFloat(LastExpW, lastExpandedSizeDelta.x);
        PlayerPrefs.SetFloat(LastExpH, lastExpandedSizeDelta.y);
        PlayerPrefs.SetInt(HeaderOnly, headerOnlyLayout ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static bool TryLoad(out Vector2 anchoredPosition, out Vector2 lastExpandedSizeDelta, out bool headerOnlyLayout)
    {
        anchoredPosition = default;
        lastExpandedSizeDelta = default;
        headerOnlyLayout = false;

        if (!HasSavedLayout())
            return false;

        anchoredPosition = new Vector2(PlayerPrefs.GetFloat(PosX), PlayerPrefs.GetFloat(PosY));
        lastExpandedSizeDelta = new Vector2(PlayerPrefs.GetFloat(LastExpW), PlayerPrefs.GetFloat(LastExpH));
        headerOnlyLayout = PlayerPrefs.GetInt(HeaderOnly, 0) != 0;
        return true;
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(HasLayout);
        PlayerPrefs.DeleteKey(PosX);
        PlayerPrefs.DeleteKey(PosY);
        PlayerPrefs.DeleteKey(LastExpW);
        PlayerPrefs.DeleteKey(LastExpH);
        PlayerPrefs.DeleteKey(HeaderOnly);
        PlayerPrefs.Save();
    }
}
