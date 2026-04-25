using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ActivityLogClearButtonUI : MonoBehaviour
{
    private static readonly string[] ButtonNameCandidates =
    {
        "ClearActivityButton",
        "ClearActivityLogButton",
        "ClearGameActivityButton"
    };

    private Button _button;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttachInLoadedScene()
    {
        SceneManager.sceneLoaded += (_, _) => AutoAttachToNamedButtons();
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
            if (!IsClearButtonName(t.name))
                continue;
            if (!t.GetComponent<Button>())
                continue;
            if (!t.GetComponent<ActivityLogClearButtonUI>())
                t.gameObject.AddComponent<ActivityLogClearButtonUI>();
        }
    }

    private static bool IsClearButtonName(string objectName)
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
    }

    private void OnEnable()
    {
        if (!_button)
            _button = GetComponent<Button>();

        _button.onClick.RemoveListener(ClearActivityLog);
        _button.onClick.AddListener(ClearActivityLog);
    }

    private void OnDisable()
    {
        if (_button)
            _button.onClick.RemoveListener(ClearActivityLog);
    }

    public void ClearActivityLog()
    {
        GameLog.Clear();
    }
}
