using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorldMapRegionRowUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private GameObject selectedHighlight;

    public Button Button => button;
    public TMP_Text NameText => nameText;

    public void SetSelected(bool selected)
    {
        if (selectedHighlight != null)
            selectedHighlight.SetActive(selected);
    }
}
