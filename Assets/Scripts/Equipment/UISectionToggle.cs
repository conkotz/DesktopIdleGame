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
        AutoBindContentsIfNeeded();

        _group = GetComponentInParent<UISectionToggleGroup>(true);

        if (headerButton)
            _buttonImage = headerButton.GetComponent<Image>();

        SetExpandedInternal(startExpanded);

        if (headerButton)
            headerButton.onClick.AddListener(OnHeaderClicked);
    }

    private void OnValidate()
    {
        AutoBindContentsIfNeeded();
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

    private void AutoBindContentsIfNeeded()
    {
        if (contents == null)
            contents = new List<GameObject>();

        // Clean nulls first.
        for (int i = contents.Count - 1; i >= 0; i--)
        {
            if (!contents[i])
                contents.RemoveAt(i);
        }

        // If already configured with multiple blocks, do not override user wiring.
        if (contents.Count >= 2)
            return;

        TryAddNamedContent("Content");
        TryAddNamedContent("ContentLeft");
        TryAddNamedContent("ContentRight");
    }

    private void TryAddNamedContent(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        var children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            var t = children[i];
            if (!t || t == transform) continue;
            if (!string.Equals(t.name, name, System.StringComparison.Ordinal)) continue;

            GameObject go = t.gameObject;
            if (!contents.Contains(go))
                contents.Add(go);
            return;
        }
    }
}