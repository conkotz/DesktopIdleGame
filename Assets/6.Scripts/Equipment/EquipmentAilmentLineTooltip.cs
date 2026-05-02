using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Hover tooltips for equipment stats ailment lines. Add to the same GameObject as the text (or a child with a raycast Graphic).
/// Assign <see cref="line"/>, then let <see cref="EquipmentStatsPanelUI"/> call <see cref="Bind"/> or rely on auto-find for <see cref="CharacterStats"/>.
/// </summary>
[DisallowMultipleComponent]
public class EquipmentAilmentLineTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public enum LineId
    {
        BleedOverview,
        BleedChance,
        BleedMultiplier,
        BleedDuration,
        BleedMaxStacks,
        PoisonOverview,
        PoisonChance,
        PoisonMultiplier,
        PoisonDuration,
        PoisonMaxStacks,
        BurnOverview,
        BurnChance,
        BurnMultiplier,
        BurnDuration,
        BurnStacks,
        ShockOverview,
        ShockChance,
        ShockDamageAmount,
        ShockDuration,
        ChillOverview,
        ChillChance,
        ChillEffect,
        ChillDuration,
        ChillStacks,
    }

    [SerializeField] private LineId line = LineId.BleedOverview;
    [SerializeField] private SharedTooltipUI tooltipPanel;
    [SerializeField] private RectTransform tooltipMeasureRect;
    [SerializeField] private FlipInsideBounds.PreferredSide preferredSide = FlipInsideBounds.PreferredSide.Right;

    private CharacterStats _stats;

    /// <summary>Used when <see cref="EquipmentStatsPanelUI"/> auto-adds this component on stat lines.</summary>
    public void SetLineId(LineId id) => line = id;

    private void Awake()
    {
        if (!tooltipPanel)
            tooltipPanel = FindFirstObjectByType<SharedTooltipUI>(FindObjectsInactive.Include);
    }

    /// <summary>Wired from <see cref="EquipmentStatsPanelUI"/> so tooltips track the panel's player stats.</summary>
    public void Bind(CharacterStats characterStats, SharedTooltipUI sharedTooltip = null)
    {
        _stats = characterStats;
        if (sharedTooltip)
            tooltipPanel = sharedTooltip;
    }

    private CharacterStats ResolveStats()
    {
        if (_stats)
            return _stats;
        _stats = FindFirstObjectByType<CharacterStats>(FindObjectsInactive.Include);
        return _stats;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        CharacterStats s = ResolveStats();
        if (!tooltipPanel || !s)
            return;

        if (!TryBuildTooltip(out string title, out string body) || string.IsNullOrWhiteSpace(body))
            return;

        var flipper = tooltipPanel.GetComponent<FlipInsideBounds>();
        if (flipper)
        {
            flipper.SetPreferredSide(preferredSide);
            if (tooltipMeasureRect)
            {
                flipper.SetMeasureRect(tooltipMeasureRect);
                flipper.SetHeightRect(tooltipMeasureRect);
            }
        }

        tooltipPanel.SetAnchor(transform);
        tooltipPanel.ShowText(title, body, null, useStatsDisplayHeader: true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        tooltipPanel?.Hide();
    }

    private void OnDisable()
    {
        tooltipPanel?.Hide();
    }

    private bool TryBuildTooltip(out string title, out string body)
    {
        title = "";
        body = "";

        switch (line)
        {
            case LineId.BleedOverview:
                title = GameTooltipTexts.BleedTitle;
                body =
                    "Damage over time from physical hits. Base bleed deals 100% of the hit's physical damage over its duration. " +
                    "\n\n" +
                    "If a new bleed is applied from a harder hit, the remaining ticks become stronger.";
                return true;

            case LineId.BleedChance:
                title = "Bleed chance";
                body = "Chance to apply bleed from physical damage.";
                return true;

            case LineId.BleedMultiplier:
                title = "Bleed multiplier";
                body = "How much extra damage the bleed over time does.";
                return true;

            case LineId.BleedDuration:
                title = "Bleed duration";
                body = "How long each bleed stack lasts. Applying a new bleed refreshes its duration.";
                return true;

            case LineId.BleedMaxStacks:
                title = "Bleed max stacks";
                body = "Maximum bleed stacks that can be applied to an enemy.";
                return true;

            case LineId.PoisonOverview:
                title = GameTooltipTexts.PoisonTitle;
                body =
                    "Damage over time from corruption hits. Base poison from one hit stores 40% of that hit's corruption damage in total, " +
                    "spread across one damage tick per second for that stack (poison damage bonuses on your stats increase that total). " +
                    "Several stacks add their ticks together, so poison can scale higher over time.\n\n" +
                    "Each application adds its own stack with its own timer. Every second, damage from all stacks is added together. " +
                    "Stacks fall off one by one. At max stacks, the oldest stack is removed when a new one is added.";
                return true;

            case LineId.PoisonChance:
                title = "Poison chance";
                body = "Chance to apply poison from corruption damage.";
                return true;

            case LineId.PoisonMultiplier:
                title = "Poison multiplier";
                body = "How much extra damage the poison over time does.";
                return true;

            case LineId.PoisonDuration:
                title = "Poison duration";
                body = "How long each poison stack lasts before it falls off.";
                return true;

            case LineId.PoisonMaxStacks:
                title = "Poison max stacks";
                body = "Maximum poison stacks that can be applied to an enemy.";
                return true;

            case LineId.BurnOverview:
                title = GameTooltipTexts.BurnTitle;
                body =
                    "Damage over time from fire hits. Each burn tick uses 15% of your strongest recent fire hit's damage (before rounding to whole damage), " +
                    "then your burn damage multiplier is applied (burn damage bonuses on your stats increase that strength).\n\n" +
                    "Fire hits can add burn stacks. At max stacks, burn detonates as a heavy magic hit dealing 10 seconds worth of the strongest tick, then clears. " +
                    "Landing fire damage again refreshes how long burn keeps ticking.";
                return true;

            case LineId.BurnChance:
                title = "Burn chance";
                body = "Chance to add a burn stack when your fire damage hits.";
                return true;

            case LineId.BurnMultiplier:
                title = "Burn multiplier";
                body = "How much extra damage burn over time does.";
                return true;

            case LineId.BurnDuration:
                title = "Burn duration";
                body = "How long burn keeps ticking each time it is refreshed by fire damage.";
                return true;

            case LineId.BurnStacks:
                title = "Burn stacks";
                body = "Maximum burn stacks that can be applied to an enemy.";
                return true;

            case LineId.ShockOverview:
                title = GameTooltipTexts.ShockTitle;
                body =
                    "Shocked enemies take extra damage from all non-ailment damage. Applying shock again refreshes how long it lasts.";
                return true;

            case LineId.ShockChance:
                title = "Shock chance";
                body = "Chance to apply shock with lightning and related attacks.";
                return true;

            case LineId.ShockDamageAmount:
                title = "Shock damage amount";
                body = "How much extra damage the enemy takes from non-ailment damage while shocked.";
                return true;

            case LineId.ShockDuration:
                title = "Shock duration";
                body = "How long shock lasts on the target.";
                return true;

            case LineId.ChillOverview:
                title = GameTooltipTexts.ChillTitle;
                body =
                    "Ice chills slow enemies. Each stack adds more slow.\n\n" +
                    "When chill is applied again, every active stack gets its duration refreshed. At max stacks, the oldest stack is removed to make room.";
                return true;

            case LineId.ChillChance:
                title = "Chill chance";
                body = "Chance to apply chill when your ice damage hits.";
                return true;

            case LineId.ChillEffect:
                title = "Chill effect";
                body = "How much slow each chill stack contributes.";
                return true;

            case LineId.ChillDuration:
                title = "Chill duration";
                body = "How long each chill stack lasts. Applying chill again refreshes this on every stack.";
                return true;

            case LineId.ChillStacks:
                title = "Chill stacks";
                body = "Maximum chill stacks that can be applied to an enemy.";
                return true;

            default:
                return false;
        }
    }
}
