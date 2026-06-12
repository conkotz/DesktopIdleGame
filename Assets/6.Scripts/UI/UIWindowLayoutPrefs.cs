using System;
using UnityEngine;

/// <summary>
/// Persists movable HUD window layout (anchors, pivot, position, size, scale) in <see cref="PlayerPrefs"/>.
/// </summary>
public static class UIWindowLayoutPrefs
{
    private const string PrefsPrefix = "UI.WindowLayout.";

    [Serializable]
    public struct Snapshot
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector3 localScale;
    }

    public static Snapshot Capture(RectTransform rect)
    {
        if (!rect)
            return default;

        return new Snapshot
        {
            anchorMin = rect.anchorMin,
            anchorMax = rect.anchorMax,
            pivot = rect.pivot,
            anchoredPosition = rect.anchoredPosition,
            sizeDelta = rect.sizeDelta,
            localScale = rect.localScale
        };
    }

    public static void Apply(RectTransform rect, in Snapshot snapshot)
    {
        if (!rect)
            return;

        rect.anchorMin = snapshot.anchorMin;
        rect.anchorMax = snapshot.anchorMax;
        rect.pivot = snapshot.pivot;
        rect.anchoredPosition = snapshot.anchoredPosition;
        rect.sizeDelta = snapshot.sizeDelta;
        rect.localScale = snapshot.localScale;
    }

    public static bool HasSaved(string memoryKey)
    {
        if (string.IsNullOrWhiteSpace(memoryKey))
            return false;

        return PlayerPrefs.HasKey(GetPrefsKey(memoryKey));
    }

    public static bool TryLoad(string memoryKey, out Snapshot snapshot)
    {
        snapshot = default;
        if (string.IsNullOrWhiteSpace(memoryKey))
            return false;

        string key = GetPrefsKey(memoryKey);
        if (!PlayerPrefs.HasKey(key))
            return false;

        string json = PlayerPrefs.GetString(key, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
            return false;

        SerializableSnapshot data = JsonUtility.FromJson<SerializableSnapshot>(json);
        if (data == null || !data.hasData)
            return false;

        snapshot = data.ToSnapshot();
        return true;
    }

    public static void Save(string memoryKey, in Snapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(memoryKey))
            return;

        SerializableSnapshot data = SerializableSnapshot.FromSnapshot(snapshot);
        data.hasData = true;
        PlayerPrefs.SetString(GetPrefsKey(memoryKey), JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    public static void Save(RectTransform rect, string memoryKey)
    {
        if (!rect || string.IsNullOrWhiteSpace(memoryKey))
            return;

        Save(memoryKey, Capture(rect));
    }

    public static void TryLoadAndApply(RectTransform rect, string memoryKey)
    {
        if (!rect || !TryLoad(memoryKey, out Snapshot snapshot))
            return;

        Apply(rect, snapshot);
    }

    public static void Delete(string memoryKey)
    {
        if (string.IsNullOrWhiteSpace(memoryKey))
            return;

        PlayerPrefs.DeleteKey(GetPrefsKey(memoryKey));
    }

    public static void ClearAll()
    {
        string[] keys = {
            "MainMenuWindow",
            "GameActivityWindow",
            "TrackerWindow",
            "FullDPSWindow",
            "ActionBarWindow",
            "QuestTrackerWindow",
            "ShopWindow"
        };

        for (int i = 0; i < keys.Length; i++)
            Delete(keys[i]);

        PlayerPrefs.Save();
    }

    private static string GetPrefsKey(string memoryKey) => PrefsPrefix + memoryKey.Trim();

    [Serializable]
    private sealed class SerializableSnapshot
    {
        public bool hasData;
        public float anchorMinX;
        public float anchorMinY;
        public float anchorMaxX;
        public float anchorMaxY;
        public float pivotX;
        public float pivotY;
        public float posX;
        public float posY;
        public float sizeX;
        public float sizeY;
        public float scaleX = 1f;
        public float scaleY = 1f;
        public float scaleZ = 1f;

        public Snapshot ToSnapshot() => new Snapshot
        {
            anchorMin = new Vector2(anchorMinX, anchorMinY),
            anchorMax = new Vector2(anchorMaxX, anchorMaxY),
            pivot = new Vector2(pivotX, pivotY),
            anchoredPosition = new Vector2(posX, posY),
            sizeDelta = new Vector2(sizeX, sizeY),
            localScale = new Vector3(scaleX, scaleY, scaleZ)
        };

        public static SerializableSnapshot FromSnapshot(in Snapshot snapshot)
        {
            return new SerializableSnapshot
            {
                anchorMinX = snapshot.anchorMin.x,
                anchorMinY = snapshot.anchorMin.y,
                anchorMaxX = snapshot.anchorMax.x,
                anchorMaxY = snapshot.anchorMax.y,
                pivotX = snapshot.pivot.x,
                pivotY = snapshot.pivot.y,
                posX = snapshot.anchoredPosition.x,
                posY = snapshot.anchoredPosition.y,
                sizeX = snapshot.sizeDelta.x,
                sizeY = snapshot.sizeDelta.y,
                scaleX = snapshot.localScale.x,
                scaleY = snapshot.localScale.y,
                scaleZ = snapshot.localScale.z
            };
        }
    }
}
