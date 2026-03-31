using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorldMapNodeButtonUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text typeText;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private GameObject selectedHighlight;

    private MapNodeDefinition _node;
    private Action<MapNodeDefinition> _onSelected;

    private void Awake()
    {
        if (!button)
            button = GetComponent<Button>();

        if (button)
            button.onClick.AddListener(OnClick);
    }

    private void OnDestroy()
    {
        if (button)
            button.onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        if (_node != null)
            _onSelected?.Invoke(_node);
    }

    public void Bind(
        MapNodeDefinition node,
        string stateLabel,
        bool selected,
        Action<MapNodeDefinition> onSelected)
    {
        _node = node;
        _onSelected = onSelected;

        if (nameText)
            nameText.text = node ? node.displayName : "—";

        if (typeText)
            typeText.text = node ? node.nodeType.ToString() : "";

        if (stateText)
            stateText.text = stateLabel ?? "";

        SetSelected(selected);
    }

    public void SetSelected(bool selected)
    {
        if (selectedHighlight)
            selectedHighlight.SetActive(selected);
    }
}
