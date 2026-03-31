using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IdleCombatButton : MonoBehaviour
{
    [SerializeField] private PlayerCombatController combat;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image buttonImage;

    [Header("Colours")]
    [SerializeField] private Color onColor = new Color(0.2f, 0.8f, 0.2f);      // green
    [SerializeField] private Color offColor = new Color(0.85f, 0.85f, 0.85f);  // light grey

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
            combat.OnIdleCombatChanged += HandleIdleChanged;
    }

    private void OnDisable()
    {
        if (combat != null)
            combat.OnIdleCombatChanged -= HandleIdleChanged;
    }

    public void ToggleIdleCombat()
    {
        if (!combat)
            combat = FindFirstObjectByType<PlayerCombatController>(FindObjectsInactive.Include);

        if (combat == null) return;

        combat.ToggleIdleCombat();
    }

    private void Start()
    {
        if (combat != null)
            HandleIdleChanged(combat.IdleCombatEnabled);
    }

    private void HandleIdleChanged(bool enabled)
    {
        if (label)
            label.text = enabled ? "Auto Battle: ON" : "Auto Battle: OFF";

        if (buttonImage)
            buttonImage.color = enabled ? onColor : offColor;
    }
}