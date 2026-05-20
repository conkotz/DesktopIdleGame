using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Title-bar lock toggle only — never closes the window. Wired by <see cref="UIWindowCloseButton"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
[DefaultExecutionOrder(-50)]
public sealed class UIWindowLockButton : MonoBehaviour
{
    [SerializeField] private UIWindowCloseButton windowClose;

    private Button _button;

    public void Bind(UIWindowCloseButton closeController)
    {
        windowClose = closeController;
    }

    /// <summary>Called by <see cref="UIWindowCloseButton"/> after bind — owns the lock button click.</summary>
    public void EnsureWired()
    {
        StripMistakenCloseComponent();
        WireToggleOnly();
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
        EnsureWired();
    }

    private void OnDisable()
    {
        if (_button)
            _button.onClick.RemoveListener(OnLockClicked);
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
        if (mistakenClose == null)
            return;

        mistakenClose.enabled = false;
        Destroy(mistakenClose);
    }

    private void WireToggleOnly()
    {
        if (!_button)
            _button = GetComponent<Button>();
        if (!_button)
            return;

        _button.onClick.RemoveListener(OnLockClicked);
        _button.onClick.AddListener(OnLockClicked);
    }

    private void OnLockClicked()
    {
        if (windowClose == null)
            windowClose = ResolveWindowCloseController();

        if (windowClose != null)
            windowClose.ToggleLock();
    }
}
