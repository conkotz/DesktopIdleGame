using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RetaliationButton : MonoBehaviour
{
    [SerializeField] private PlayerCombatController combat;
    [SerializeField] private TMP_Text label;

    [Tooltip("Image (or any Graphic) behind the name row — tinted green/red. Leave empty to tint the Label text instead.")]
    [SerializeField] private Graphic nameLabelBackground;

    [Header("Colours")]
    [SerializeField] private Color onColor = new Color(0.2f, 0.75f, 0.25f, 1f);
    [SerializeField] private Color offColor = new Color(0.85f, 0.25f, 0.22f, 1f);

    private void Awake()
    {
        if (!combat)
            combat = FindFirstObjectByType<PlayerCombatController>(FindObjectsInactive.Include);

        if (!label)
            label = GetComponentInChildren<TMP_Text>();

        if (!nameLabelBackground && label && label.transform.parent != null)
            nameLabelBackground = label.transform.parent.GetComponent<Graphic>();
    }

    private void OnEnable()
    {
        if (combat != null)
            combat.OnRetaliationChanged += HandleRetaliationChanged;
    }

    private void OnDisable()
    {
        if (combat != null)
            combat.OnRetaliationChanged -= HandleRetaliationChanged;
    }

    public void ToggleRetaliation()
    {
        if (!combat)
            combat = FindFirstObjectByType<PlayerCombatController>(FindObjectsInactive.Include);

        if (combat == null) return;
        combat.ToggleRetaliation();
    }

    private void Start()
    {
        if (combat != null)
            HandleRetaliationChanged(combat.RetaliationEnabled);
    }

    private void HandleRetaliationChanged(bool enabled)
    {
        if (label)
            label.text = enabled ? "Retaliation: ON" : "Retaliation: OFF";

        Color c = enabled ? onColor : offColor;

        if (nameLabelBackground)
            nameLabelBackground.color = c;
        else if (label)
            label.color = c;
    }
}
