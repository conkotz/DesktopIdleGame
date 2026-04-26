using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIWindowCloseButton : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private GameObject targetWindow;

    [Header("Optional")]
    [SerializeField] private bool disableInsteadOfHide = false;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.RemoveListener(CloseWindow);
        _button.onClick.AddListener(CloseWindow);
    }

    public void Configure(GameObject target, bool disableInstead = false)
    {
        targetWindow = target;
        disableInsteadOfHide = disableInstead;
    }

    public void CloseWindow()
    {
        if (!targetWindow)
        {
            Debug.LogWarning($"[{nameof(UIWindowCloseButton)}] No targetWindow assigned on {name}");
            return;
        }

        // Main menu uses CanvasGroup hide on the same GameObject; SetActive(false) disables MainMenuWindowUI and
        // OpenPage could not reactivate the root (canvas-group path skipped SetActive). Prefer proper Close().
        MainMenuWindowUI menuUi = targetWindow.GetComponent<MainMenuWindowUI>();
        if (menuUi != null)
        {
            menuUi.Close();
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

        targetWindow.SetActive(false);
    }
}