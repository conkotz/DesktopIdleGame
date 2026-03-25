using UnityEngine;
using TMPro;

public class HUDToggle : MonoBehaviour
{
    [SerializeField] private GameObject hudRoot;
    [SerializeField] private TMP_Text arrowText;

    private bool _isVisible = true;

    private void Start()
    {
        UpdateVisual();
    }

    public void Toggle()
    {
        if (!hudRoot) return;

        _isVisible = !_isVisible;
        hudRoot.SetActive(_isVisible);

        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (!arrowText) return;

        arrowText.text = _isVisible ? "▼" : "▲";
    }
}