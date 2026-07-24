using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class FullDpsWindowToggleUI : MonoBehaviour
{
    private static readonly string[] ButtonNameCandidates =
    {
        "ToggleFullDPSButton",
        "ToggleFullDpsButton",
        "ToggleFullDamageButton",
        "ToggleFullDamageBreakdownButton"
    };

    private static readonly string[] WindowNameCandidates =
    {
        "FullDPSWindow",
        "FullDpsWindow",
        "FullDamageBreakdownWindow"
    };

    [SerializeField] private GameObject fullDpsWindow;
    [SerializeField] private string fullDpsWindowName = "FullDPSWindow";

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

            if (!t.GetComponent<FullDpsWindowToggleUI>())
                t.gameObject.AddComponent<FullDpsWindowToggleUI>();
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
            Debug.LogWarning($"[{nameof(FullDpsWindowToggleUI)}] Could not find DPS window '{fullDpsWindowName}'.", this);
            return;
        }

        bool newState = !window.activeSelf;
        window.SetActive(newState);
        if (newState)
            window.transform.SetAsLastSibling();
    }

    private GameObject ResolveWindow()
    {
        if (fullDpsWindow != null)
            return fullDpsWindow;

        FullDpsWindowToggleUI[] toggles = FindObjectsByType<FullDpsWindowToggleUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < toggles.Length; i++)
        {
            FullDpsWindowToggleUI t = toggles[i];
            if (t != null && t.fullDpsWindow != null)
            {
                fullDpsWindow = t.fullDpsWindow;
                return fullDpsWindow;
            }
        }

        fullDpsWindow = FindSceneObjectByName(fullDpsWindowName);
        if (fullDpsWindow != null)
            return fullDpsWindow;

        for (int i = 0; i < WindowNameCandidates.Length; i++)
        {
            fullDpsWindow = FindSceneObjectByName(WindowNameCandidates[i]);
            if (fullDpsWindow != null)
                return fullDpsWindow;
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
