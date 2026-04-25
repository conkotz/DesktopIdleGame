using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIWindowManager : MonoBehaviour
{
    public static UIWindowManager Instance;

    private readonly List<GameObject> _openWindows = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        KeyCode closeKey = HotkeyBindingManager.Instance != null
            ? HotkeyBindingManager.Instance.GetBinding(HotkeyBindId.CloseAllWindows)
            : HotkeyBindingManager.GetDefaultKey(HotkeyBindId.CloseAllWindows);

        if (closeKey != KeyCode.None &&
            !HotkeySettingsRowUI.IsRebinding &&
            !IsTypingIntoInputField() &&
            Input.GetKeyDown(closeKey))
        {
            CloseAllWindows();
        }
    }

    public void Register(GameObject window)
    {
        if (!_openWindows.Contains(window))
            _openWindows.Add(window);
    }

    public void Unregister(GameObject window)
    {
        _openWindows.Remove(window);
    }

    public void CloseAllWindows()
    {
        MainMenuWindowUI.Resolve()?.Close();

        for (int i = _openWindows.Count - 1; i >= 0; i--)
        {
            if (!_openWindows[i])
                continue;

            if (_openWindows[i].GetComponentInParent<MainMenuWindowUI>(true) != null ||
                _openWindows[i].GetComponentInChildren<MainMenuWindowUI>(true) != null)
                continue;

            if (_openWindows[i])
                _openWindows[i].SetActive(false);
        }

        _openWindows.Clear();
    }

    private static bool IsTypingIntoInputField()
    {
        if (EventSystem.current == null)
            return false;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null)
            return false;

        return selected.GetComponent<TMP_InputField>() != null ||
               selected.GetComponent<InputField>() != null;
    }
}