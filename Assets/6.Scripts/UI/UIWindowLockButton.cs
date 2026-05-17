using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Title-bar lock toggle only — never closes the window. Wired by <see cref="UIWindowCloseButton"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class UIWindowLockButton : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private UIWindowCloseButton windowClose;

    private Button _button;

    public void Bind(UIWindowCloseButton closeController)
    {
        windowClose = closeController;
    }

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (!windowClose)
            windowClose = ResolveWindowCloseController();

        StripMistakenCloseComponent();
    }

    private void OnEnable()
    {
        StripMistakenCloseComponent();
        WireToggleOnly();
    }

    private void OnDisable()
    {
        if (_button)
            _button.onClick.RemoveListener(OnLockClicked);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        eventData.Use();
    }

    private UIWindowCloseButton ResolveWindowCloseController()
    {
        Transform search = transform.parent;
        for (int depth = 0; depth < 4 && search != null; depth++)
        {
            Transform closeT = search.Find("CloseButton");
            if (closeT && closeT.TryGetComponent(out UIWindowCloseButton closer))
                return closer;

            search = search.parent;
        }

        return GetComponentInParent<UIWindowCloseButton>(true);
    }

    private void StripMistakenCloseComponent()
    {
        UIWindowCloseButton mistakenClose = GetComponent<UIWindowCloseButton>();
        if (mistakenClose != null)
            Destroy(mistakenClose);
    }

    private void WireToggleOnly()
    {
        if (!_button)
            _button = GetComponent<Button>();
        if (!_button)
            return;

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(OnLockClicked);
    }

    private void OnLockClicked()
    {
        if (windowClose != null)
            windowClose.ToggleLock();
    }
}
