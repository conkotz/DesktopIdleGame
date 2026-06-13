using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Loot icon in the database enemy list; shows a full item tooltip on hover.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class DatabaseLootTableEntryUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IItemTooltipHoverSource
{
    private const string EliteDropNamePrefixRichText = "<color=#E85555><size=75%>ELITE DROP</size></color><br>";
    private const string GoldTooltipTitleRichText = "<size=130%>Gold</size>";

    private static readonly Color DefaultLootBackgroundColor = new Color(0.9547169f, 0.8938162f, 0.8268209f, 1f);
    private static readonly Color EliteLootBackgroundColor = new Color(0.92f, 0.55f, 0.55f, 1f);

    private static Sprite _cachedGoldIconSprite;

    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text dropChanceText;

    private ItemDefinition _item;
    private SharedTooltipUI _tooltip;
    private bool _isEliteDrop;
    private bool _isGoldEntry;
    private float _dropChance;
    private int _amountMin;
    private int _amountMax;
    private int _goldMin;
    private int _goldMax;

    private void ResolveDropChanceText()
    {
        if (dropChanceText)
            return;

        if (transform.parent != null)
            dropChanceText = transform.parent.Find("DropChanceText")?.GetComponent<TMP_Text>();

        if (!dropChanceText)
            dropChanceText = transform.Find("DropChanceText")?.GetComponent<TMP_Text>();
    }

    private void Awake()
    {
        if (!iconImage)
            iconImage = GetComponent<Image>();
        ResolveDropChanceText();
    }

    public void Bind(
        ItemDefinition item,
        SharedTooltipUI tooltip,
        float dropChance,
        bool isEliteDrop,
        int amountMin,
        int amountMax)
    {
        _item = item;
        _tooltip = tooltip;
        _isEliteDrop = isEliteDrop;
        _isGoldEntry = false;
        _dropChance = Mathf.Clamp01(dropChance);
        _amountMin = Mathf.Max(1, amountMin);
        _amountMax = Mathf.Max(_amountMin, amountMax);

        if (!iconImage)
            iconImage = GetComponent<Image>();
        ResolveDropChanceText();

        Sprite sprite = item != null ? item.icon : null;
        iconImage.sprite = sprite;
        iconImage.preserveAspect = true;
        iconImage.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.25f);
        iconImage.raycastTarget = item != null;

        ApplyLootBackgroundColor(isEliteDrop ? EliteLootBackgroundColor : DefaultLootBackgroundColor);

        if (dropChanceText)
        {
            bool show = item != null;
            dropChanceText.gameObject.SetActive(show);
            if (show)
                dropChanceText.text = MapCombatScaling.FormatSpecialDropChancePercent(dropChance);
        }
    }

    public void BindGold(Sprite goldIcon, SharedTooltipUI tooltip, int goldMin, int goldMax)
    {
        _item = null;
        _tooltip = tooltip;
        _isEliteDrop = false;
        _isGoldEntry = true;
        _goldMin = Mathf.Max(0, goldMin);
        _goldMax = Mathf.Max(_goldMin, goldMax);

        if (!iconImage)
            iconImage = GetComponent<Image>();
        ResolveDropChanceText();

        Sprite sprite = goldIcon != null ? goldIcon : ResolveGoldIconSprite();
        iconImage.sprite = sprite;
        iconImage.preserveAspect = true;
        iconImage.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.25f);
        iconImage.raycastTarget = true;

        ApplyLootBackgroundColor(DefaultLootBackgroundColor);

        if (dropChanceText)
            dropChanceText.gameObject.SetActive(false);
    }

    public static Sprite ResolveGoldIconSprite()
    {
        if (_cachedGoldIconSprite)
            return _cachedGoldIconSprite;

        Image[] images = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image.sprite == null)
                continue;
            if (!string.Equals(image.gameObject.name, "GoldIcon", System.StringComparison.Ordinal))
                continue;

            _cachedGoldIconSprite = image.sprite;
            return _cachedGoldIconSprite;
        }

        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null)
                continue;
            if (string.Equals(sprite.name, "Currency_12", System.StringComparison.Ordinal))
            {
                _cachedGoldIconSprite = sprite;
                return _cachedGoldIconSprite;
            }
        }

        return null;
    }

    private void ApplyLootBackgroundColor(Color color)
    {
        if (transform.parent == null)
            return;

        Transform fill = transform.parent.Find("IconBackgroundFill");
        if (fill != null && fill.TryGetComponent(out Image fillImage))
        {
            fillImage.color = color;
            return;
        }

        if (transform.parent.TryGetComponent(out Image backgroundImage))
            backgroundImage.color = color;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ItemTooltipHoverRegistry.SetHovered(this, true);
        ShowTooltipIfPossible();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ItemTooltipHoverRegistry.SetHovered(this, false);
        _tooltip?.Hide();
    }

    public void RefreshTooltipIfHovered()
    {
        ShowTooltipIfPossible();
    }

    private void ShowTooltipIfPossible()
    {
        if (_tooltip == null)
            return;

        if (_isGoldEntry)
        {
            _tooltip.ShowTextAt(
                transform,
                GoldTooltipTitleRichText,
                FormatGoldTooltipBody(_goldMin, _goldMax));
            return;
        }

        if (_item == null)
            return;

        _tooltip.ShowAt(
            transform,
            _item,
            1,
            compact: false,
            customValueOverride: FormatDropChanceAndAmountRange(_dropChance, _amountMin, _amountMax),
            namePrefixRichText: _isEliteDrop ? EliteDropNamePrefixRichText : null,
            maskUnrolledRandomStats: _item.HasRandomStatPool);
    }

    private static string FormatAmountRange(int amountMin, int amountMax)
    {
        if (amountMax <= amountMin)
            return amountMin.ToString();
        return $"{amountMin}-{amountMax}";
    }

    private static string FormatGoldTooltipBody(int goldMin, int goldMax)
    {
        return $"<size=130%>Quantity: {FormatAmountRange(goldMin, goldMax)}</size>";
    }

    private static string FormatDropChanceAndAmountRange(float dropChance, int amountMin, int amountMax)
    {
        string chance = MapCombatScaling.FormatSpecialDropChancePercent(dropChance);
        string quantity = FormatAmountRange(amountMin, amountMax);
        return $"Drop chance: {chance}\nQuantity: {quantity}";
    }
}
