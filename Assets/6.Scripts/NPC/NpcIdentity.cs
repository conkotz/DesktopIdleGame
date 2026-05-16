using TMPro;
using UnityEngine;

/// <summary>
/// World NPC identity (name + optional role), parallel to <see cref="Merchant"/>'s identity block.
/// Drives the optional <c>NameLabel</c> child TMP and supplies display names for map / UI previews.
/// </summary>
[AddComponentMenu("Desktop Idle Game/NPC/NPC Identity")]
public class NpcIdentity : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField, InspectorName("Name")] private string characterName = "";
    [Tooltip("Shown under the name (smaller), e.g. job or archetype.")]
    [SerializeField] private string role = "";

    [Header("Refs (optional)")]
    [SerializeField] private TMP_Text nameLabel;

    /// <summary>Inspector "Name" — primary label for lists and map summaries.</summary>
    public string CharacterDisplayName =>
        string.IsNullOrWhiteSpace(characterName) ? "" : characterName.Trim();

    /// <summary>Subtitle (e.g. job); used when no character name is set, or as the second line on the world label.</summary>
    public string RoleDisplayLabel =>
        string.IsNullOrWhiteSpace(role) ? "" : role.Trim();

    private void Awake()
    {
        ApplyIdentityToLabel();
    }

    private void OnEnable()
    {
        ApplyIdentityToLabel();
    }

    private void OnValidate()
    {
        ApplyIdentityToLabel();
    }

    public void RegisterNameLabel(TMP_Text label)
    {
        if (label)
            nameLabel = label;
    }

    public void RefreshNameLabel() => ApplyIdentityToLabel();

    private void ApplyIdentityToLabel()
    {
        if (!nameLabel)
            nameLabel = FindNameLabel();

        if (!nameLabel)
            return;

        NpcNameLabelFormatting.Apply(nameLabel, CharacterDisplayName, RoleDisplayLabel);
    }

    private TMP_Text FindNameLabel()
    {
        TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            if (label && label.name == "NameLabel")
                return label;
        }

        return null;
    }
}
