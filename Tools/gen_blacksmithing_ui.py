import pathlib

root = pathlib.Path(__file__).resolve().parents[1] / "Assets" / "6.Scripts"
src = (root / "Cooking" / "CookingUI.cs").read_text(encoding="utf-8")
reps = [
    ("CookingUI", "BlacksmithingUI"),
    ("CookingStation", "BlacksmithingStation"),
    ("CookingClick", "BlacksmithingClick"),
    ("CookingRecipe", "BlacksmithingRecipe"),
    ("CookingRecipes", "BlacksmithingRecipes"),
    ("CookingProficiencyBonuses", "BlacksmithingProficiencyBonuses"),
    ("ProcessingSkillType.Cooking", "ProcessingSkillType.Blacksmithing"),
    ("_fishPickerRoot", "_recipePickerRoot"),
    ("_fishPickerScrollContent", "_recipePickerScrollContent"),
    ("_fishIcon", "_materialsIcon"),
    ("_cookedIcon", "_outputIcon"),
    ("_fishAmountText", "_materialsText"),
    ("_cookedAmountText", "_outputAmountText"),
    ("_fishButton", "_materialsButton"),
    ("_cookedButton", "_outputButton"),
    ("_fishClearButton", "_recipeClearButton"),
    ("_cookingLevelButton", "_blacksmithingLevelButton"),
    ("_cookingLevelButtonText", "_blacksmithingLevelButtonText"),
    ("_cookingXpFill", "_blacksmithingXpFill"),
    ("_cookingXpFillRt", "_blacksmithingXpFillRt"),
    ("SidePickerMode.Fish", "SidePickerMode.Recipe"),
    ("CreateFishPickerRow", "CreateRecipePickerRow"),
    ("RebuildFishPicker", "RebuildRecipePicker"),
    ("IsCooking", "IsCrafting"),
    ("CookProgressSeconds", "CraftProgressSeconds"),
    ("TryStartCooking", "TryStartCrafting"),
    ("StopCooking", "StopCrafting"),
    ("TryCollectCooked", "TryCollectOutput"),
    ("ReadyCookedAmount", "ReadyOutputAmount"),
    ("ReadyCookedItemId", "ReadyOutputItemId"),
    ("StoredRawItemId", "SelectedRecipeOutputId"),
    ("StoredRawAmount", "SelectedRecipePlaceholderAmount"),
    ("Cooking", "Blacksmithing"),
    ("cooking", "blacksmithing"),
    ("Cook", "Forge"),
    ("cook", "craft"),
    ('"cooking_range"', '"blacksmithing_anvil"'),
]
for old, new in reps:
    src = src.replace(old, new)
(root / "Blacksmithing" / "BlacksmithingUI.cs").write_text(src, encoding="utf-8")
print("wrote BlacksmithingUI.cs", len(src))
