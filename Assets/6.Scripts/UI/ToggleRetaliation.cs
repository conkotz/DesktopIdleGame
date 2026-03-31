using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RetaliationButton : MonoBehaviour
{
    [SerializeField] private PlayerCombatController combat;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image buttonImage;

    [Header("Colours")]
    [SerializeField] private Color onColor = new Color(0.2f, 0.8f, 0.2f);
    [SerializeField] private Color offColor = new Color(0.85f, 0.85f, 0.85f);

    private void Awake()
    {
        if (!combat)
            combat = FindFirstObjectByType<PlayerCombatController>(FindObjectsInactive.Include);

        if (!label)
            label = GetComponentInChildren<TMP_Text>();

        if (!buttonImage)
            buttonImage = GetComponent<Image>();
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

        if (buttonImage)
            buttonImage.color = enabled ? onColor : offColor;
    }
}
