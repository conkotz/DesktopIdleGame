using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SaleUndoRowUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private Button undoButton;
    [SerializeField] private Image iconImage;

    private int _id;
    private Action<int> _onUndo;

    public void Bind(int id, string text, Sprite icon, Action<int> onUndo)
    {
        _id = id;
        _onUndo = onUndo;

        if (label) label.text = text;

        if (iconImage)
        {
            bool has = icon != null;
            iconImage.enabled = has;
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
        }

        if (undoButton)
        {
            undoButton.gameObject.SetActive(true);
            undoButton.onClick.RemoveAllListeners();
            undoButton.onClick.AddListener(() => _onUndo?.Invoke(_id));
        }
    }

    // For the "+N more" row
    public void BindSummary(string text)
    {
        _id = -1;
        _onUndo = null;

        if (label) label.text = text;

        if (iconImage)
            iconImage.enabled = false;

        if (undoButton)
        {
            undoButton.onClick.RemoveAllListeners();
            undoButton.gameObject.SetActive(false);
        }
    }
}