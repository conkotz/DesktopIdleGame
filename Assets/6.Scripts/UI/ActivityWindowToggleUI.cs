using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ActivityWindowToggleUI : MonoBehaviour
{
    [SerializeField] private GameObject activityWindow;
    [SerializeField] private string activityWindowName = "GameActivityWindow";

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.RemoveListener(Toggle);
        _button.onClick.AddListener(Toggle);
    }

    public void Toggle()
    {
        GameObject window = ResolveWindow();
        if (window == null)
        {
            Debug.LogWarning($"[{nameof(ActivityWindowToggleUI)}] Could not find activity window '{activityWindowName}'.", this);
            return;
        }

        if (window.activeSelf && UIWindowCloseButton.BlocksClose(window))
            return;

        window.SetActive(!window.activeSelf);
        if (window.activeSelf)
        {
            window.transform.SetAsLastSibling();
            GameLogWindowUI logUi = window.GetComponent<GameLogWindowUI>() ??
                                    window.GetComponentInChildren<GameLogWindowUI>(true);
            logUi?.FlushNow();
        }
    }

    private GameObject ResolveWindow()
    {
        if (activityWindow != null)
            return activityWindow;

        ActivityWindowToggleUI[] toggles = FindObjectsByType<ActivityWindowToggleUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < toggles.Length; i++)
        {
            ActivityWindowToggleUI t = toggles[i];
            if (t != null && t.activityWindow != null)
            {
                activityWindow = t.activityWindow;
                return activityWindow;
            }
        }

        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.hideFlags != HideFlags.None || !t.gameObject.scene.IsValid())
                continue;
            if (t.name == activityWindowName)
            {
                activityWindow = t.gameObject;
                return activityWindow;
            }
        }

        return null;
    }
}
