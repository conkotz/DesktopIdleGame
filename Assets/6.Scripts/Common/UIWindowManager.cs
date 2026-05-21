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
            return;
        }

        if (HotkeySettingsRowUI.IsRebinding || IsTypingIntoInputField())
            return;

        if (Time.frameCount == HotkeySettingsRowUI.SuppressActionBarHotkeyPollFrame)
            return;

        MainMenuWindowUI menu = MainMenuWindowUI.Resolve();
        if (menu == null)
            return;

        if (WasHotkeyPressedThisFrame(HotkeyBindId.OpenCharacterPage))
            menu.SelectTab(MainMenuTabId.Character);
        else if (WasHotkeyPressedThisFrame(HotkeyBindId.OpenSkillsAbilities))
            menu.SelectTab(MainMenuTabId.Skills);
        else if (WasHotkeyPressedThisFrame(HotkeyBindId.OpenLevelSelect))
            menu.SelectTab(MainMenuTabId.LevelSelect);
        else if (WasHotkeyPressedThisFrame(HotkeyBindId.OpenQuestPage))
            menu.SelectTab(MainMenuTabId.Quest);
        else if (WasHotkeyPressedThisFrame(HotkeyBindId.ReturnToTown))
            PlayerController.TryReturnToTownViaHotkey();
    }

    private static bool WasHotkeyPressedThisFrame(HotkeyBindId id)
    {
        KeyCode k = HotkeyBindingManager.Instance != null
            ? HotkeyBindingManager.Instance.GetBinding(id)
            : HotkeyBindingManager.GetDefaultKey(id);
        return k != KeyCode.None && Input.GetKeyDown(k);
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
        NPCDialogueBoxUI.DismissAllActive();
        QuickMenuPanelToggleUI.HideIfOpen();
        MainMenuWindowUI.Resolve()?.Close();

        // Snapshot first — closing the menu (or any window) runs UIWindow.OnDisable → Unregister,
        // which must not shrink _openWindows while we index into it.
        GameObject[] snapshot = _openWindows.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
        {
            GameObject w = snapshot[i];
            if (!w)
            {
                _openWindows.Remove(w);
                continue;
            }

            if (!_openWindows.Contains(w))
                continue;

            if (w.GetComponentInParent<MainMenuWindowUI>(true) != null ||
                w.GetComponentInChildren<MainMenuWindowUI>(true) != null)
                continue;

            if (UIWindowCloseButton.BlocksClose(w))
                continue;

            w.SetActive(false);
            _openWindows.Remove(w);
        }
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