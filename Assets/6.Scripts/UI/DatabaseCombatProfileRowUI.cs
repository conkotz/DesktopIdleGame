using TMPro;
using UnityEngine;

/// <summary>One combat profile entry in the database general information panel.</summary>
[DisallowMultipleComponent]
public sealed class DatabaseCombatProfileRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text profileNameText;
    [SerializeField] private TMP_Text profileDetailsText;

    private void Awake()
    {
        ResolveReferences();
    }

    public void Bind(string profileLabel, string description)
    {
        ResolveReferences();

        if (profileNameText)
        {
            profileNameText.text = profileLabel ?? string.Empty;
            profileNameText.color = CombatProfileClassifier.GetColorForLabel(profileLabel);
        }

        if (profileDetailsText)
            profileDetailsText.text = description ?? string.Empty;
    }

    private void ResolveReferences()
    {
        if (!profileNameText)
            profileNameText = transform.Find("CombatProfileName")?.GetComponent<TMP_Text>();
        if (!profileDetailsText)
            profileDetailsText = transform.Find("CombatProfileDetails")?.GetComponent<TMP_Text>();
    }
}
