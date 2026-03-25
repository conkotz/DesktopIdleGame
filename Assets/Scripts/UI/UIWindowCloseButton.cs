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

    public void CloseWindow()
    {
        if (!targetWindow)
        {
            Debug.LogWarning($"[{nameof(UIWindowCloseButton)}] No targetWindow assigned on {name}");
            return;
        }

        if (disableInsteadOfHide)
            targetWindow.SetActive(false);
        else
            targetWindow.SetActive(false);
    }
}