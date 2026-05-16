using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Toggles <see cref="quickMenuPanel"/> from a toolbar button (e.g. UIButton_QuickMenu on BottomGameBar).
/// </summary>
[RequireComponent(typeof(Button))]
public class QuickMenuPanelToggleUI : MonoBehaviour
{
    [SerializeField] private GameObject quickMenuPanel;
    [SerializeField] private string quickMenuPanelName = "QuickMenuPanel";

    private Button _button;

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
        GameObject panel = ResolvePanel();
        if (panel == null)
        {
            Debug.LogWarning(
                $"[{nameof(QuickMenuPanelToggleUI)}] Assign Quick Menu Panel or ensure a GameObject named '{quickMenuPanelName}' exists in the scene.",
                this);
            return;
        }

        panel.SetActive(!panel.activeSelf);
        if (panel.activeSelf)
            panel.transform.SetAsLastSibling();
    }

    private GameObject ResolvePanel()
    {
        if (quickMenuPanel != null)
            return quickMenuPanel;

        QuickMenuPanelToggleUI[] toggles = FindObjectsByType<QuickMenuPanelToggleUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < toggles.Length; i++)
        {
            QuickMenuPanelToggleUI t = toggles[i];
            if (t != null && t.quickMenuPanel != null)
            {
                quickMenuPanel = t.quickMenuPanel;
                return quickMenuPanel;
            }
        }

        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.hideFlags != HideFlags.None || !t.gameObject.scene.IsValid())
                continue;
            if (t.name == quickMenuPanelName)
            {
                quickMenuPanel = t.gameObject;
                return quickMenuPanel;
            }
        }

        return null;
    }
}
