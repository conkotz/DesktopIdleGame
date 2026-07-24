using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class TrackerWindowToggleUI : MonoBehaviour
{
    private static readonly string[] ButtonNameCandidates =
    {
        "ToggleTrackerButton",
        "ToggleSessionTrackerButton",
        "ToggleXpLootTrackerButton"
    };

    private static readonly string[] WindowNameCandidates =
    {
        "TrackerWindow",
        "SessionTrackerWindow",
        "XpLootTrackerWindow"
    };

    [SerializeField] private GameObject trackerWindow;
    [SerializeField] private string trackerWindowName = "TrackerWindow";

    private Button _button;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedAutoAttach;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterAutoAttach()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedAutoAttach;
        SceneManager.sceneLoaded += OnSceneLoadedAutoAttach;
        AutoAttachToNamedButtons();
    }

    private static void OnSceneLoadedAutoAttach(Scene scene, LoadSceneMode mode)
    {
        AutoAttachToNamedButtons();
    }

    private static void AutoAttachToNamedButtons()
    {
        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.hideFlags != HideFlags.None || !t.gameObject.scene.IsValid())
                continue;

            if (!IsToggleButtonName(t.name))
                continue;

            if (!t.GetComponent<Button>())
                continue;

            if (!t.GetComponent<TrackerWindowToggleUI>())
                t.gameObject.AddComponent<TrackerWindowToggleUI>();
        }
    }

    private static bool IsToggleButtonName(string objectName)
    {
        for (int i = 0; i < ButtonNameCandidates.Length; i++)
        {
            if (objectName == ButtonNameCandidates[i])
                return true;
        }

        return false;
    }

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.RemoveListener(Toggle);
        _button.onClick.AddListener(Toggle);
    }

    private void OnEnable()
    {
        if (!_button)
            _button = GetComponent<Button>();

        _button.onClick.RemoveListener(Toggle);
        _button.onClick.AddListener(Toggle);
    }

    private void OnDisable()
    {
        if (_button)
            _button.onClick.RemoveListener(Toggle);
    }

    public void Toggle()
    {
        GameObject window = ResolveWindow();
        if (window == null)
        {
            Debug.LogWarning($"[{nameof(TrackerWindowToggleUI)}] Could not find tracker window '{trackerWindowName}'.", this);
            return;
        }

        bool newState = !window.activeSelf;
        window.SetActive(newState);
        if (newState)
            window.transform.SetAsLastSibling();
    }

    private GameObject ResolveWindow()
    {
        if (trackerWindow != null)
            return trackerWindow;

        TrackerWindowToggleUI[] toggles = FindObjectsByType<TrackerWindowToggleUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < toggles.Length; i++)
        {
            TrackerWindowToggleUI t = toggles[i];
            if (t != null && t.trackerWindow != null)
            {
                trackerWindow = t.trackerWindow;
                return trackerWindow;
            }
        }

        trackerWindow = FindSceneObjectByName(trackerWindowName);
        if (trackerWindow != null)
            return trackerWindow;

        for (int i = 0; i < WindowNameCandidates.Length; i++)
        {
            trackerWindow = FindSceneObjectByName(WindowNameCandidates[i]);
            if (trackerWindow != null)
                return trackerWindow;
        }

        return null;
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.hideFlags != HideFlags.None || !t.gameObject.scene.IsValid())
                continue;

            if (t.name == objectName)
                return t.gameObject;
        }

        return null;
    }
}
