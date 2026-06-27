import pathlib
import re

root = pathlib.Path(__file__).resolve().parents[1] / "Assets" / "6.Scripts" / "Cooking"

fixes = [
    ("recipe.OreItemId", "recipe.RawItemId"),
    ("recipe.BarItemId", "recipe.CookedItemId"),
    ("activeRecipe.BarItemId", "activeRecipe.CookedItemId"),
    ("TryDepositAllOreFromInventory", "TryDepositAllRawFromInventory"),
    ("DoubleBarChancePercent", "DoublePortionChancePercent"),
    ("[Furnace]", "[Cooking]"),
    ("while smelting (3s cooldown)", "while cooking (3s cooldown)"),
    ("furnace UI is open", "cooking UI is open"),
    ("Deposit ore from", "Deposit fish from"),
    ("FurnaceOreSlotInteractions", "CookingFishSlotInteractions"),
    ('GetItemDisplayName(oreId, "Ore")', 'GetItemDisplayName(oreId, "Fish")'),
    ('GetItemDisplayName(barId, "Bar")', 'GetItemDisplayName(barId, "Cooked")'),
    ('GetItemDisplayName(recipe.OreItemId, "Ore")', 'GetItemDisplayName(recipe.RawItemId, "Fish")'),
    ('reason != "No ore stored."', 'reason != "No fish stored."'),
    ("ResolveBarSlotItemId", "ResolveCookedSlotItemId"),
    ("ResolveBarItemId", "ResolveCookedItemId"),
    ("RemoveAllOre", "RemoveAllFish"),
    ("AddAllOre", "AddAllFish"),
    ("DepositOreFromPicker", "DepositFishFromPicker"),
    ("DepositAllOreFromPicker", "DepositAllFishFromPicker"),
    ("OnOresClicked", "OnFishClicked"),
    ("OnOresRightClicked", "OnFishRightClicked"),
    ("OnBarsRightClicked", "OnCookedRightClicked"),
    ('CreateUiObject("OreRow"', 'CreateUiObject("FishRow"'),
    ("bool smelting =", "bool cooking ="),
    ("smelting &&", "cooking &&"),
    ("if (!smelting)", "if (!cooking)"),
    ('string barWord = barsRemaining == 1 ? "bar" : "bars"', 'string portionWord = portionsRemaining == 1 ? "portion" : "portions"'),
    ("barsRemaining", "portionsRemaining"),
    ("barWord", "portionWord"),
    ("int oreAmt", "int fishAmt"),
    ("int ore =", "int fish ="),
    ("oreId", "fishId"),
    ("oreAmt", "fishAmt"),
    ("barAmt", "cookedAmt"),
    ("barId", "cookedId"),
    ("bool hasOre", "bool hasFish"),
    ("_nextPendingBarsLogTime", "_nextPendingCookedLogTime"),
]

for path in root.glob("*.cs"):
    text = path.read_text(encoding="utf-8")
    original = text
    for old, new in fixes:
        text = text.replace(old, new)
    if text != original:
        path.write_text(text, encoding="utf-8")
        print("fixed", path.name)

station = root / "CookingStation.cs"
text = station.read_text(encoding="utf-8")
text = text.replace("TryDepositAllRawFromInventory(string oreItemId", "TryDepositAllRawFromInventory(string rawItemId")
text = text.replace("oreItemId, out failureReason", "rawItemId, out failureReason")
station.write_text(text, encoding="utf-8")

runtime = root / "CookingRuntime.cs"
text = runtime.read_text(encoding="utf-8")
text = text.replace("TryDepositAllRawFromInventory(string oreItemId", "TryDepositAllRawFromInventory(string rawItemId")
text = text.replace("TryDepositRaw(oreItemId, available", "TryDepositRaw(rawItemId, available")
runtime.write_text(text, encoding="utf-8")
