using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Toggles <see cref="PlayerWeaponRangeIndicator"/> from the character equipment panel button.
/// Stays on while gameplay continues; clears on scene load or when toggled off.
/// </summary>
[DisallowMultipleComponent]
public sealed class EquipmentRangeIndicatorToggleUI : MonoBehaviour
{
    [SerializeField] private Button showRangeIndicatorButton;
    [SerializeField] private TMP_Text rangeIndicatorLabel;

    private static bool s_indicatorEnabled;

    private UnityEngine.Events.UnityAction _clickHandler;

    private void Awake()
    {
        if (showRangeIndicatorButton == null)
        {
            Transform buttonTransform = transform.Find("ShowRangeIndicatorButton");
            if (buttonTransform == null)
                buttonTransform = FindChildRecursive(transform, "ShowRangeIndicatorButton");

            if (buttonTransform != null)
                showRangeIndicatorButton = buttonTransform.GetComponent<Button>();
        }

        if (rangeIndicatorLabel == null && showRangeIndicatorButton != null)
        {
            Transform label = showRangeIndicatorButton.transform.Find("RangeIndicatorText");
            if (label != null)
                rangeIndicatorLabel = label.GetComponent<TMP_Text>();
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        WireButton();
        ApplyIndicatorState();
        RefreshLabel();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnwireButton();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        s_indicatorEnabled = false;
        ApplyIndicatorState();
        RefreshLabel();
    }

    private void WireButton()
    {
        if (showRangeIndicatorButton == null)
            return;

        if (_clickHandler == null)
            _clickHandler = ToggleIndicator;

        showRangeIndicatorButton.onClick.RemoveListener(_clickHandler);
        showRangeIndicatorButton.onClick.AddListener(_clickHandler);
    }

    private void UnwireButton()
    {
        if (showRangeIndicatorButton != null && _clickHandler != null)
            showRangeIndicatorButton.onClick.RemoveListener(_clickHandler);
    }

    private void ToggleIndicator()
    {
        s_indicatorEnabled = !s_indicatorEnabled;
        ApplyIndicatorState();
        RefreshLabel();
    }

    private void ApplyIndicatorState()
    {
        SetPlayerIndicatorVisible(s_indicatorEnabled);
    }

    private static void SetPlayerIndicatorVisible(bool visible)
    {
        PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player == null)
            return;

        PlayerWeaponRangeIndicator indicator = player.GetComponent<PlayerWeaponRangeIndicator>();
        if (indicator == null && visible)
            indicator = player.gameObject.AddComponent<PlayerWeaponRangeIndicator>();
        if (indicator == null)
            return;

        indicator.SetVisible(visible);
    }

    private void RefreshLabel()
    {
        if (rangeIndicatorLabel == null)
            return;

        rangeIndicatorLabel.text = s_indicatorEnabled ? "Hide Range" : "Show Range";
    }

    private static Transform FindChildRecursive(Transform root, string leafName)
    {
        if (root == null || string.IsNullOrEmpty(leafName))
            return null;

        if (root.name == leafName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), leafName);
            if (found != null)
                return found;
        }

        return null;
    }
}
