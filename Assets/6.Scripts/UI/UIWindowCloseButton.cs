using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
[DefaultExecutionOrder(0)]
public class UIWindowCloseButton : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private GameObject targetWindow;

    [Header("Optional")]
    [SerializeField] private bool disableInsteadOfHide = false;

    [Header("Optional window lock")]
    [Tooltip("When locked, this close button and ESC / toggle-close paths cannot dismiss the target window.")]
    [SerializeField] private Button lockButton;
    [Tooltip("Save-slot key for lock state. Empty = target window name (e.g. GameActivityWindow, MainMenuWindow).")]
    [SerializeField] private string persistenceWindowId;
    [SerializeField] private GameObject iconUnlocked;
    [SerializeField] private GameObject iconLocked;
    [SerializeField, Range(0.05f, 1f)] private float closeButtonLockedAlpha = 0.35f;

    private readonly Dictionary<Graphic, float> _closeGraphicFullAlphas = new Dictionary<Graphic, float>();
    private Button _closeButton;

    public bool IsLocked { get; private set; }

    /// <summary>Key used in <see cref="SaveData.uiWindowLockKeys"/> for this window.</summary>
    public string PersistenceWindowId => ResolvePersistenceWindowId();

    /// <summary>True when a <see cref="UIWindowCloseButton"/> for this window has lock engaged.</summary>
    public static bool BlocksClose(GameObject windowRoot)
    {
        if (!windowRoot)
            return false;

        UIWindowCloseButton[] closers = windowRoot.GetComponentsInChildren<UIWindowCloseButton>(true);
        for (int i = 0; i < closers.Length; i++)
        {
            UIWindowCloseButton closer = closers[i];
            if (closer != null && closer.IsLocked && closer.OwnsWindow(windowRoot))
                return true;
        }

        return false;
    }

    private void Awake()
    {
        _closeButton = GetComponent<Button>();
        _closeButton.onClick.RemoveListener(CloseWindow);
        _closeButton.onClick.AddListener(CloseWindow);

        ResolveLockButtonIfNeeded();
        ResolveLockIconsIfNeeded();
        CacheCloseGraphicAlphas();
    }

    private void Start()
    {
        EnsureLockButtonWired();
        ApplyLockVisuals();
        UIWindowLockStore.ApplyToCloseButton(this);
    }

    private void OnEnable()
    {
        EnsureLockButtonWired();
        ApplyLockVisuals();
        UIWindowLockStore.ApplyToCloseButton(this);
    }

    private void OnDestroy()
    {
        if (!lockButton)
            return;

        UIWindowLockButton lockUi = lockButton.GetComponent<UIWindowLockButton>();
        if (lockUi == null)
            lockButton.onClick.RemoveListener(ToggleLock);
    }

    public void Configure(GameObject target, bool disableInstead = false)
    {
        targetWindow = target;
        disableInsteadOfHide = disableInstead;
    }

    public void ToggleLock() => SetLocked(!IsLocked, persist: true);

    public void SetLocked(bool locked) => SetLocked(locked, persist: false);

    /// <summary>Apply saved lock state without writing back to the save file.</summary>
    public void SetLockedFromPersistence(bool locked) => SetLocked(locked, persist: false);

    private void SetLocked(bool locked, bool persist)
    {
        IsLocked = locked;
        ApplyLockVisuals();

        if (persist)
            UIWindowLockStore.Record(ResolvePersistenceWindowId(), locked);
    }

    private string ResolvePersistenceWindowId()
    {
        if (!string.IsNullOrWhiteSpace(persistenceWindowId))
            return persistenceWindowId.Trim();

        if (targetWindow != null)
            return targetWindow.name;

        return name;
    }

    public void CloseWindow()
    {
        if (!targetWindow)
        {
            Debug.LogWarning($"[{nameof(UIWindowCloseButton)}] No targetWindow assigned on {name}");
            return;
        }

        if (IsLocked)
            return;

        MainMenuWindowUI menuUi = targetWindow.GetComponent<MainMenuWindowUI>();
        if (menuUi != null)
        {
            menuUi.Close();
            return;
        }

        ShopUI shopUi = targetWindow.GetComponent<ShopUI>() ?? targetWindow.GetComponentInParent<ShopUI>(true);
        if (shopUi != null)
        {
            MerchantClick.ForceCloseMerchantMode();
            return;
        }

        if (disableInsteadOfHide)
        {
            CanvasGroup cg = targetWindow.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
                return;
            }

            Canvas c = targetWindow.GetComponent<Canvas>();
            if (c != null)
            {
                c.enabled = false;
                return;
            }
        }

        QuestTrackerWindowUI questTracker = targetWindow.GetComponent<QuestTrackerWindowUI>();
        if (questTracker != null)
            questTracker.RememberWindowClosedByUser();

        targetWindow.SetActive(false);
    }

    private bool OwnsWindow(GameObject windowRoot) =>
        targetWindow != null && targetWindow == windowRoot;

    private void EnsureLockButtonWired()
    {
        ResolveLockButtonIfNeeded();
        if (!lockButton)
            return;

        UIWindowLockButton lockUi = lockButton.GetComponent<UIWindowLockButton>();
        if (lockUi == null)
            lockUi = lockButton.gameObject.AddComponent<UIWindowLockButton>();

        lockUi.Bind(this);
        lockUi.EnsureWired();
    }

    private void ResolveLockButtonIfNeeded()
    {
        if (lockButton)
            return;

        Transform search = transform.parent;
        for (int depth = 0; depth < 4 && search != null; depth++)
        {
            Transform lockT = search.Find("LockButton");
            if (lockT && lockT.TryGetComponent(out lockButton))
                return;

            search = search.parent;
        }
    }

    private void ResolveLockIconsIfNeeded()
    {
        if (!lockButton)
            return;

        Transform lockRoot = lockButton.transform;
        if (!iconUnlocked)
        {
            Transform t = lockRoot.Find("IconUnlocked");
            if (t)
                iconUnlocked = t.gameObject;
        }

        if (!iconLocked)
        {
            Transform t = lockRoot.Find("Iconlocked") ?? lockRoot.Find("IconLocked");
            if (t)
                iconLocked = t.gameObject;
        }
    }

    private void CacheCloseGraphicAlphas()
    {
        if (_closeGraphicFullAlphas.Count > 0 || !_closeButton)
            return;

        Graphic[] graphics = _closeButton.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic g = graphics[i];
            if (g == null || _closeGraphicFullAlphas.ContainsKey(g))
                continue;

            _closeGraphicFullAlphas[g] = g.color.a;
        }
    }

    private void ApplyLockVisuals()
    {
        ApplyLockIconState();
        CacheCloseGraphicAlphas();
        ApplyCloseButtonLockVisual();
    }

    private void ApplyLockIconState()
    {
        if (iconUnlocked)
            iconUnlocked.SetActive(false);
        if (iconLocked)
            iconLocked.SetActive(false);

        if (IsLocked)
        {
            if (iconLocked)
                iconLocked.SetActive(true);
        }
        else if (iconUnlocked)
        {
            iconUnlocked.SetActive(true);
        }
    }

    private void ApplyCloseButtonLockVisual()
    {
        if (!_closeButton)
            return;

        _closeButton.interactable = !IsLocked;

        Graphic[] graphics = _closeButton.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic g = graphics[i];
            if (g == null)
                continue;

            if (!_closeGraphicFullAlphas.TryGetValue(g, out float fullAlpha))
            {
                fullAlpha = g.color.a;
                _closeGraphicFullAlphas[g] = fullAlpha;
            }

            Color c = g.color;
            c.a = IsLocked ? closeButtonLockedAlpha : fullAlpha;
            g.color = c;
        }

        if (!IsLocked)
        {
            ColorBlock colors = _closeButton.colors;
            _closeButton.colors = colors;
            if (_closeButton.targetGraphic != null)
            {
                _closeButton.targetGraphic.CrossFadeColor(
                    colors.normalColor,
                    colors.fadeDuration,
                    true,
                    true);
            }
        }
    }
}
