import pathlib
import re

root = pathlib.Path(__file__).resolve().parents[1] / "Assets" / "6.Scripts"
src = (root / "Furnace" / "FurnaceUI.cs").read_text(encoding="utf-8")

# Strip fuel/enhancement blocks from BuildUi via regex (first pass simplification)
src = re.sub(
    r"\n        var topSlotsRow = CreateUiObject.*?_enhancementClearButton\.gameObject\.SetActive\(false\);\n",
    "\n",
    src,
    count=1,
    flags=re.S,
)

src = re.sub(
    r"\n        _fuelSummaryText = CreateTmpText.*?_fuelSummaryText\.text = \"\";\n",
    "\n        _ingredientsSummaryText = CreateTmpText(\"IngredientsSummary\", _root, 11f, TextLight, TextAlignmentOptions.Center);\n        var ingredientsSummaryRt = _ingredientsSummaryText.rectTransform;\n        ingredientsSummaryRt.anchorMin = new Vector2(0.5f, 0.195f);\n        ingredientsSummaryRt.anchorMax = new Vector2(0.5f, 0.195f);\n        ingredientsSummaryRt.pivot = new Vector2(0.5f, 0.5f);\n        ingredientsSummaryRt.sizeDelta = new Vector2(320f, 36f);\n        _ingredientsSummaryText.textWrappingMode = TextWrappingModes.Normal;\n        _ingredientsSummaryText.text = \"\";\n",
    src,
    count=1,
    flags=re.S,
)

reps = [
    ("FurnaceUI", "BlacksmithingUI"),
    ("FurnaceSmelter", "BlacksmithingStation"),
    ("FurnaceClick", "BlacksmithingClick"),
    ("SmeltingProficiencyBonuses", "BlacksmithingProficiencyBonuses"),
    ("SmeltingRecipe", "BlacksmithingRecipe"),
    ("SmeltingRecipes", "BlacksmithingRecipes"),
    ("ProcessingSkillType.Smelting", "ProcessingSkillType.Blacksmithing"),
    ("_smelter", "_station"),
    ("_orePickerRoot", "_recipePickerRoot"),
    ("_orePickerScrollContent", "_recipePickerScrollContent"),
    ("_oresButton", "_recipeButton"),
    ("_barsButton", "_outputButton"),
    ("_oreIcon", "_recipeIcon"),
    ("_barIcon", "_outputIcon"),
    ("_oreAmountText", "_recipeAmountText"),
    ("_barAmountText", "_outputAmountText"),
    ("_oreClearButton", "_recipeClearButton"),
    ("_smeltingLevelButton", "_blacksmithingLevelButton"),
    ("_smeltingLevelButtonText", "_blacksmithingLevelButtonText"),
    ("_smeltingXpFill", "_blacksmithingXpFill"),
    ("_smeltingXpFillRt", "_blacksmithingXpFillRt"),
    ("_fuelSummaryText", "_ingredientsSummaryText"),
    ("IsSmelting", "IsCrafting"),
    ("SmeltProgressSeconds", "CraftProgressSeconds"),
    ("TryStartSmelting", "TryStartCrafting"),
    ("StopSmelting", "StopCrafting"),
    ("ReadyBarAmount", "ReadyOutputCount"),
    ("ReadyBarItemId", "ReadyOutputItemId"),
    ("StoredOreItemId", "SelectedRecipeOutputId"),
    ("StoredOreAmount", "SelectedRecipeCount"),
    ("TryCollectBars", "TryCollectOutput"),
    ("OnOresClicked", "OnRecipeClicked"),
    ("HideOrePicker", "HideRecipePicker"),
    ("RebuildOrePicker", "RebuildRecipePicker"),
    ("CreateOrePickerRow", "CreateRecipePickerRow"),
    ("OnSmeltingLevelClicked", "OnBlacksmithingLevelClicked"),
    ("RefreshSmeltingLevelButton", "RefreshBlacksmithingLevelButton"),
    ("RefreshSmeltingXpBar", "RefreshBlacksmithingXpBar"),
    ("BuildSmeltingXpBar", "BuildBlacksmithingXpBar"),
    ("LogFurnaceBlocked", "LogBlacksmithingBlocked"),
    ("FurnaceBlockedLogCooldownSeconds", "BlacksmithingBlockedLogCooldownSeconds"),
    ("Furnace", "Blacksmithing Anvil"),
    ("FurnacePanel", "BlacksmithingPanel"),
    ("Smelting", "Blacksmithing"),
    ("smelting", "blacksmithing"),
    ("SMELT", "FORGE"),
    ("Smelt", "Forge"),
    ("smelt", "forge"),
    ("Ores", "Recipe"),
    ("Bars", "Item"),
    ("ore", "recipe"),
    ("Ore", "Recipe"),
    ("bar", "output"),
    ("Bar", "Output"),
    ('"furnace"', '"blacksmithing_anvil"'),
    ("OrePicker", "RecipePicker"),
]
for old, new in reps:
    src = src.replace(old, new)

# Remove enhancement/fuel methods blocks (rough)
for method in [
    "RefreshFuelClearButton",
    "RefreshFuelSlotVisuals",
    "RefreshEnhancementClearButton",
    "RefreshEnhancementSlotVisuals",
    "OnFuelClicked",
    "OnFuelClearClicked",
    "OnEnhancementClicked",
    "OnEnhancementClearClicked",
    "ShowEnhancementSlotTooltip",
    "HideEnhancementSlotTooltip",
    "TryDepositEnhancementFromInventorySlot",
    "TryDepositFuelFromInventorySlot",
]:
    src = re.sub(
        rf"\n    (?:public |private ).*?{method}\(.*?\n    \}}\n",
        "\n",
        src,
        count=1,
        flags=re.S,
    )

out = root / "Blacksmithing" / "BlacksmithingUI.cs"
out.write_text(src, encoding="utf-8")
print("wrote", out, len(src))
