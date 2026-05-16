using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Title-bar or bottom-bar tab control. Forwards clicks to <see cref="MainMenuWindowTabsUI"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class MainMenuTabButtonUI : MonoBehaviour
{
    [SerializeField] private MainMenuTabId tabId = MainMenuTabId.Character;
    [SerializeField] private MainMenuWindowTabsUI tabs;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (!tabs)
            tabs = GetComponentInParent<MainMenuWindowTabsUI>(true);
        if (!tabs)
            tabs = FindFirstObjectByType<MainMenuWindowTabsUI>(FindObjectsInactive.Include);

        _button.onClick.RemoveListener(OnClicked);
        _button.onClick.AddListener(OnClicked);
    }

    private void OnDestroy()
    {
        if (_button)
            _button.onClick.RemoveListener(OnClicked);
    }

    public MainMenuTabId TabId => tabId;

    public void SetTabId(MainMenuTabId id) => tabId = id;

    public void OnClicked()
    {
        if (!tabs)
            tabs = FindFirstObjectByType<MainMenuWindowTabsUI>(FindObjectsInactive.Include);
        if (tabs)
            tabs.SelectTab(tabId);
    }
}
