# Icon Texture Audit (read-only)

Sprites referenced as **item**, **equipment**, **resource**, **consumable**, **skill/ability**, or **buff/debuff HUD** icons.
No import settings were changed.

## Estimation method

| Target setting | Value |
|----------------|-------|
| Max Size | 256 |
| Compression | Normal Quality (GPU BC3/DXT5 ~1 byte/texel) |
| Read/Write | Off |
| Mip Maps | Off |

Memory figures are **estimates** for unique texture assets (deduplicated by GUID).
Category totals overlap when one sheet is used for multiple purposes (e.g. skill + ability).
Runtime memory may differ with sprite atlasing, duplicate loads, and CPU readback.

## Deduplicated summary

| Metric | Value |
|--------|-------|
| Unique textures | 105 |
| Unresolved GUID refs | 1 |
| Estimated current (GPU) | **247.06 MB** |
| Estimated at 256 / Normal | **6.56 MB** |
| Estimated savings | **~240.5 MB** |

### By category (textures may appear in multiple columns)

| Category | Textures | Est. current | Est. at 256 | Est. save |
|----------|----------|--------------|-------------|-----------|
| item all | 46 | 80.06 MB | 2.88 MB | 77.19 MB |
| equipment | 23 | 42.06 MB | 1.44 MB | 40.62 MB |
| resource | 14 | 25.00 MB | 0.88 MB | 24.12 MB |
| consumable | 7 | 12.00 MB | 0.44 MB | 11.56 MB |
| skill | 46 | 123.00 MB | 2.88 MB | 120.12 MB |
| ability | 28 | 94.00 MB | 1.75 MB | 92.25 MB |
| buff debuff | 17 | 51.00 MB | 1.06 MB | 49.94 MB |

## By folder (deduplicated)

### `Assets/5.Art/Sprites/AttacksAndAbilities`

**45 textures** · current **139.00 MB** → proposed **2.81 MB** · save **~136.19 MB**

| File | Source | Max | R/W | Mip | Est. now | Est. 256 | Save | Used as |
|------|--------|-----|-----|-----|----------|----------|------|---------|
| Ablity Power Slash.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | ability, skill |
| Avatar of the Forest.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | ability, skill |
| BladeDancer.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | buff_debuff |
| Bladestorm.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | ability, skill |
| BleedPlus.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | skill |
| BleedSpread.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | skill |
| Bloodbath.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | buff_debuff |
| BrutalCut.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | skill |
| CalmWatersBuff.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | buff_debuff |
| Cleaving Strikes red.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | ability |
| CleavingChop.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | ability, skill |
| ContagionBurst.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | skill |
| Crescent slash.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | ability |
| CrusaderStrike.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | ability, skill |
| Duality.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | buff_debuff |
| EnergyInfusion.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | ability, skill |
| Envenom.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | ability, skill |
| ExecutionersDescent.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | ability, skill |
| FinalSeverance.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | ability, skill |
| FishingFrenzy.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | ability, skill |
| FlameCharge.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | ability, skill |
| FlowStateBuff.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | buff_debuff |
| ForgeWeapon2.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | ability |
| GuardiansHammer.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | ability, skill |
| HammerTempest.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | ability, skill |
| HolySeal.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | ability |
| LumberFrenzy.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | ability, skill |
| MagicAutoAttack.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | ability |
| MeleeAutoAttack.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | ability |
| Overload.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | buff_debuff |
| Phoenix.png | 1024×1536 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | buff_debuff |
| PotentVenom.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | skill |
| RangedAutoAttack.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | ability |
| RelentlessFlow.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | skill |
| ShadowHunter.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | buff_debuff |
| ShadowStrike.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | ability, skill |
| SoulforgedWarrior.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | ability, skill |
| SpectralAxe.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | ability, skill |
| Sprint(Transparant).png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | buff_debuff |
| Sprint.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | buff_debuff |
| WarBannerIcon.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | ability, skill |
| WayOfTheBerserker.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | buff_debuff |
| WayOfTheBerserkerLeechEffect.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | buff_debuff |
| WhirlingBlade.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | ability |
| rending strike.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | ability |

### `Assets/5.Art/Sprites/Gear`

**20 textures** · current **39.00 MB** → proposed **1.25 MB** · save **~37.75 MB**

| File | Source | Max | R/W | Mip | Est. now | Est. 256 | Save | Used as |
|------|--------|-----|-----|-----|----------|----------|------|---------|
| Basic Tools.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | equipment, item_all, skill |
| Basic stone items tileset.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | equipment, item_all, skill |
| FishingRod.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | equipment, item_all, skill |
| JewelerySpriteSheet.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | equipment, item_all |
| Leather boots.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | equipment, item_all |
| LeatherHat.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | equipment, item_all |
| LeatherTunic.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | equipment, item_all |
| PoisonStoneDagger.png | 1024×1536 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | equipment, item_all |
| Ring of criticality.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | equipment, item_all |
| Splitwood Longbow.png | 1024×1536 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | equipment, item_all |
| Stone Helmet.png | 1024×1536 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | equipment, item_all |
| StoneAxe.png | 1024×1536 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | equipment, item_all, skill |
| StoneDagger.png | 1024×1536 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | equipment, item_all |
| StoneSword.png | 1024×1536 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | equipment, item_all, skill |
| Vamp ring.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | equipment, item_all |
| basic focus.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | item_all |
| basic trinkets.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | equipment, item_all |
| miniature-army1024x768.png | 1024×768 | 1024 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | equipment, item_all, skill |
| stone body.png | 1024×1536 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | equipment, item_all |
| wood bow.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | skill |

### `Assets/5.Art/Sprites/Resources`

**13 textures** · current **22.00 MB** → proposed **0.81 MB** · save **~21.19 MB**

| File | Source | Max | R/W | Mip | Est. now | Est. 256 | Save | Used as |
|------|--------|-----|-----|-----|----------|----------|------|---------|
| BirdsNestSprite.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | consumable, item_all |
| ChaosScroll2.png | 1024×1536 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | equipment, item_all |
| EnhancementScroll2.png | 1024×1536 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | equipment, item_all |
| EnhancementScrollAdvanced.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | equipment, item_all |
| EnhancementScrollIntermediate.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | equipment, item_all |
| FeatherSprite2.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | item_all, resource |
| GemSpriteSheet.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | item_all, resource |
| Leather.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | item_all, resource |
| LinenSprite.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | item_all, resource |
| PotionTileSet.png | 1024×1536 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | consumable, item_all |
| SpiderSilkSprite.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | item_all, resource |
| Trial relics.png | 1024×1536 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | item_all, resource |
| basic damage potion.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | consumable, item_all |

### `Assets/5.Art/Sprites/UI`

**6 textures** · current **14.00 MB** → proposed **0.38 MB** · save **~13.62 MB**

| File | Source | Max | R/W | Mip | Est. now | Est. 256 | Save | Used as |
|------|--------|-----|-----|-----|----------|----------|------|---------|
| BasicBuffSpriteSheet.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | buff_debuff |
| CapstonePassive.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | skill |
| EnhancementIcon.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | skill |
| Major Passive Icon.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | skill |
| MeleeIcons.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | skill |
| MinorPassiveNode.png | 2048×2048 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | skill |

### `Assets/5.Art/Sprites/Resources/Woodcutting`

**5 textures** · current **8.00 MB** → proposed **0.31 MB** · save **~7.69 MB**

| File | Source | Max | R/W | Mip | Est. now | Est. 256 | Save | Used as |
|------|--------|-----|-----|-----|----------|----------|------|---------|
| BasicTreeSprite.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | skill |
| HardwoodLogSprite.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | item_all, resource, skill |
| SplitwoodLogSprite.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | item_all, resource, skill |
| TwigsSprite.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | consumable, item_all |
| WildwoodLogSprite.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | item_all, resource, skill |

### `Assets/5.Art/Sprites/Resources/Fishing`

**4 textures** · current **7.00 MB** → proposed **0.25 MB** · save **~6.75 MB**

| File | Source | Max | R/W | Mip | Est. now | Est. 256 | Save | Used as |
|------|--------|-----|-----|-----|----------|----------|------|---------|
| BasicPondSprite.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | skill |
| FishingBaitSprite.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | consumable, item_all |
| MinnowRawCookedSprite.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | consumable, item_all, resource, skill |
| PondFishSpriteSheet.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | consumable, item_all, resource, skill |

### `Assets/5.Art/Sprites/AilmentsAndDebuffs`

**4 textures** · current **6.00 MB** → proposed **0.25 MB** · save **~5.75 MB**

| File | Source | Max | R/W | Mip | Est. now | Est. 256 | Save | Used as |
|------|--------|-----|-----|-----|----------|----------|------|---------|
| Ailments1.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | buff_debuff, item_all, resource, skill |
| Ailments2.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | buff_debuff |
| ShadowStrikeDebuff1.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | buff_debuff |
| ShadowStrikeDebuff2.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | buff_debuff |

### `Assets/5.Art/Sprites/Misc`

**4 textures** · current **5.00 MB** → proposed **0.25 MB** · save **~4.75 MB**

| File | Source | Max | R/W | Mip | Est. now | Est. 256 | Save | Used as |
|------|--------|-----|-----|-----|----------|----------|------|---------|
| AncientBarkSprite.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | item_all, resource |
| GoldenFishSprite.png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | item_all |
| MapScaleItemSprite.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | item_all |
| MapScaleItemSpriteT2.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | item_all |

### `Assets/5.Art/Sprites/AttacksAndAbilities/Ranged`

**1 textures** · current **4.00 MB** → proposed **0.06 MB** · save **~3.94 MB**

| File | Source | Max | R/W | Mip | Est. now | Est. 256 | Save | Used as |
|------|--------|-----|-----|-----|----------|----------|------|---------|
| TrippleShot.png | 1254×1254 | 2048 | Off | Off | 4096.0 KB | 64.0 KB | 4032.0 KB | ability, skill |

### `Assets/5.Art/Sprites/Resources/Mining`

**2 textures** · current **3.00 MB** → proposed **0.12 MB** · save **~2.88 MB**

| File | Source | Max | R/W | Mip | Est. now | Est. 256 | Save | Used as |
|------|--------|-----|-----|-----|----------|----------|------|---------|
| BasicOre Sprites (2).png | 1536×1024 | 2048 | Off | Off | 2048.0 KB | 64.0 KB | 1984.0 KB | item_all, resource |
| BasicRockSprite.png | 1024×1024 | 2048 | Off | Off | 1024.0 KB | 64.0 KB | 960.0 KB | skill |

### `Assets/5.Art/Sprites/Basic Icons`

**1 textures** · current **0.06 MB** → proposed **0.06 MB** · save **~0.00 MB**

| File | Source | Max | R/W | Mip | Est. now | Est. 256 | Save | Used as |
|------|--------|-----|-----|-----|----------|----------|------|---------|
| scroll256x256.png | 256×256 | 2048 | Off | Off | 64.0 KB | 64.0 KB | 0.0 KB | equipment, item_all |

## Unresolved references

- GUID `2e15a983e647ec142bfdadc1698f81bc` — categories: skill — no texture `.meta` found

---
*Generated by static scan of ScriptableObjects + HUD prefabs. Review before changing imports.*