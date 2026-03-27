using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UISectionToggle : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private Button headerButton;

    // 🔥 changed: now supports multiple content sections
    [SerializeField] private List<GameObject> contents = new List<GameObject>();

    [SerializeField] private RectTransform arrow; // optional

    [Header("Colours")]
    [SerializeField] private Color activeColor = new Color(0.8f, 0.8f, 0.8f);
    [SerializeField] private Color inactiveColor = Color.white;

    [Header("State")]
    [SerializeField] private bool startExpanded = false;

    private bool _expanded;
    private UISectionToggleGroup _group;
    private Image _buttonImage;

    public bool IsExpanded => _expanded;

    private void Awake()
    {
        if (!headerButton) headerButton = GetComponentInChildren<Button>(true);

        _group = GetComponentInParent<UISectionToggleGroup>(true);

        if (headerButton)
            _buttonImage = headerButton.GetComponent<Image>();

        SetExpandedInternal(startExpanded);

        if (headerButton)
            headerButton.onClick.AddListener(OnHeaderClicked);
    }

    private void OnHeaderClicked()
    {
        if (_group != null)
            _group.OpenOnly(this);
        else
            Toggle();
    }

    public void Toggle()
    {
        if (_expanded && _group != null && _group.MustKeepOneOpen && _group.OpenCount <= 1)
            return;

        SetExpanded(!_expanded);
    }

    public void SetExpanded(bool expanded)
    {
        SetExpandedInternal(expanded);
    }

    internal void SetExpandedInternal(bool expanded)
    {
        _expanded = expanded;

        // 🔥 apply to ALL contents
        for (int i = 0; i < contents.Count; i++)
        {
            if (contents[i])
                contents[i].SetActive(_expanded);
        }

        if (arrow)
            arrow.localRotation = Quaternion.Euler(0f, 0f, _expanded ? 0f : -90f);

        if (_buttonImage)
            _buttonImage.color = _expanded ? activeColor : inactiveColor;
    }
}