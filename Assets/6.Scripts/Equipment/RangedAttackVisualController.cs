using UnityEngine;

public class RangedAttackVisualController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private EquipmentManager equipment;
    [SerializeField] private Inventory inventory;

    [Header("Normal Equipped Visual Objects")]
    [SerializeField] private GameObject weapon;   // Front arm / Weapon
    [SerializeField] private GameObject offHand;  // Back arm / Off hand

    [Header("Ranged Animation Prop Objects")]
    [SerializeField] private GameObject bow;      // Back arm / bow
    [SerializeField] private GameObject arrow;    // Front arm / arrow

    [Header("Renderers (optional auto-find)")]
    [SerializeField] private SpriteRenderer equippedOffHandRenderer;
    [SerializeField] private SpriteRenderer bowRenderer;
    [SerializeField] private SpriteRenderer arrowRenderer;

    [Header("Static Arrows Buff Outline")]
    [Tooltip("Yellow lightning tint on the equipped off-hand arrow (idle) and nocked arrow (during attacks).")]
    [SerializeField] private Color staticArrowsBuffOutlineColor = new Color(1f, 0.92f, 0.35f, 0.5f);
    [SerializeField, Min(1f)] private float staticArrowsBuffOutlineScale = 1.1f;

    [Header("Animation")]
    [SerializeField] private string rangedAttackState = "range_attack";

    private bool _usingAttackProps;
    private bool _buffArrowVisualActive;

    private EquippedArrowVisualEffects _equippedOffHandEffects;
    private EquippedArrowVisualEffects _attackArrowEffects;

    private Sprite _cachedArrowSprite;
    private bool _cachedArrowSpriteValid;

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        if (!animator)
            animator = GetComponentInChildren<Animator>();

        if (!equipment)
            equipment = GetComponentInParent<EquipmentManager>() ?? GetComponentInChildren<EquipmentManager>(true);

        if (!inventory)
            inventory = equipment ? equipment.Inventory : GetComponentInParent<Inventory>();

        if (!equippedOffHandRenderer && offHand)
            equippedOffHandRenderer = offHand.GetComponent<SpriteRenderer>();

        if (!bowRenderer && bow)
            bowRenderer = bow.GetComponent<SpriteRenderer>();

        if (!arrowRenderer && arrow)
            arrowRenderer = arrow.GetComponent<SpriteRenderer>();

        if (arrowRenderer)
        {
            _cachedArrowSprite = arrowRenderer.sprite;
            _cachedArrowSpriteValid = _cachedArrowSprite != null;
        }

        if (equippedOffHandRenderer)
        {
            _equippedOffHandEffects = equippedOffHandRenderer.GetComponent<EquippedArrowVisualEffects>()
                                      ?? equippedOffHandRenderer.gameObject.AddComponent<EquippedArrowVisualEffects>();
            ApplyStaticArrowsOutlineSettings(_equippedOffHandEffects);
        }

        if (arrow)
        {
            _attackArrowEffects = arrow.GetComponent<EquippedArrowVisualEffects>()
                                  ?? arrow.AddComponent<EquippedArrowVisualEffects>();
            ApplyStaticArrowsOutlineSettings(_attackArrowEffects);
        }

        ShowNormalVisuals();
    }

    public void SetBuffArrowVisualActive(bool active)
    {
        _buffArrowVisualActive = active;
        RefreshBuffArrowOutlines();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || !animator)
            return;

        bool shouldUseAttackProps = IsInOrTransitioningFromRangedAttack();

        if (shouldUseAttackProps && !_usingAttackProps)
            ShowAttackProps();
        else if (!shouldUseAttackProps && _usingAttackProps)
            ShowNormalVisuals();
    }

    private bool IsInOrTransitioningFromRangedAttack()
    {
        var current = animator.GetCurrentAnimatorStateInfo(0);

        if (current.IsName(rangedAttackState))
            return true;

        if (animator.IsInTransition(0))
        {
            var next = animator.GetNextAnimatorStateInfo(0);
            if (next.IsName(rangedAttackState))
                return true;
        }

        return false;
    }

    private void ShowAttackProps()
    {
        _usingAttackProps = true;

        ApplyBowVisualFromEquippedWeapon();
        ApplyArrowVisualFromEquippedOffHand();

        if (weapon) weapon.SetActive(false);
        if (offHand) offHand.SetActive(false);

        if (bow) bow.SetActive(true);
        if (arrow) arrow.SetActive(true);

        RefreshBuffArrowOutlines();
    }

    private void ShowNormalVisuals()
    {
        _usingAttackProps = false;

        if (weapon) weapon.SetActive(true);
        if (offHand) offHand.SetActive(true);

        if (bow) bow.SetActive(false);
        if (arrow) arrow.SetActive(false);

        if (bowRenderer)
            bowRenderer.sprite = null;

        if (arrowRenderer)
            arrowRenderer.sprite = _cachedArrowSpriteValid ? _cachedArrowSprite : null;

        RefreshBuffArrowOutlines();
    }

    private void RefreshBuffArrowOutlines()
    {
        bool active = _buffArrowVisualActive;
        bool onAttackArrow = active && _usingAttackProps;
        bool onEquippedOffHand = active && !onAttackArrow;

        _equippedOffHandEffects?.SetEffectActive(
            EquippedArrowVisualEffects.EffectId.LightningOutline,
            onEquippedOffHand);

        _attackArrowEffects?.SetEffectActive(
            EquippedArrowVisualEffects.EffectId.LightningOutline,
            onAttackArrow);
    }

    private void ApplyStaticArrowsOutlineSettings(EquippedArrowVisualEffects effects)
    {
        if (effects == null)
            return;

        effects.ApplySettings(
            staticArrowsBuffOutlineColor,
            staticArrowsBuffOutlineScale);
    }

    private void ApplyBowVisualFromEquippedWeapon()
    {
        if (!bowRenderer || !equipment || !inventory)
            return;

        string mainHandId = equipment.MainHandItemId;
        if (string.IsNullOrWhiteSpace(mainHandId))
        {
            bowRenderer.sprite = null;
            return;
        }

        var def = inventory.GetItemDef(mainHandId);
        if (!def)
        {
            bowRenderer.sprite = null;
            return;
        }

        bowRenderer.sprite = def.HeldSprite;
    }

    private void ApplyArrowVisualFromEquippedOffHand()
    {
        if (!arrowRenderer || !equipment || !inventory)
            return;

        string offHandId = equipment.OffHandItemId;
        if (string.IsNullOrWhiteSpace(offHandId))
        {
            if (_cachedArrowSpriteValid)
                arrowRenderer.sprite = _cachedArrowSprite;

            return;
        }

        var def = inventory.GetItemDef(offHandId);
        if (!def)
        {
            if (_cachedArrowSpriteValid)
                arrowRenderer.sprite = _cachedArrowSprite;

            return;
        }

        Sprite s = def.HeldSprite ? def.HeldSprite : def.icon;
        if (s)
            arrowRenderer.sprite = s;
        else if (_cachedArrowSpriteValid)
            arrowRenderer.sprite = _cachedArrowSprite;
    }
}
