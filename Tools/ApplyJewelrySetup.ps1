# Applies tier-1 gem jewelry renames, updates, and creates new assets.
$ErrorActionPreference = "Stop"
$JewelryFolder = "D:\UNITY\Desktop Idle RPG Game\Assets\3.ScriptableObjects\ItemsDefinitions\Jewelery"
$SpriteGuid = "ac973c93083da51478234ecaea5660ca"
$ScriptGuid = "b6afcc71e74e9fa4a9bc9e73453263b6"
$DbPath = "D:\UNITY\Desktop Idle RPG Game\Assets\Resources\Databases\ItemDatabase.asset"

$SpriteIds = @{
    "JewelerySpriteSheet_0RUBY_Ring" = 8339452560537083239
    "JewelerySpriteSheet_1EMERALD_Ring" = 3141935959341602472
    "JewelerySpriteSheet_2SAPPHIRE_Ring" = 647588772610573750
    "JewelerySpriteSheet_3" = 3483010696081942063
    "JewelerySpriteSheet_4CITRINE_Ring" = -8049167607937462275
    "JewelerySpriteSheet_6" = 3799228797227806774
    "JewelerySpriteSheet_7" = 2990212776707475007
    "JewelerySpriteSheet_11" = 6092755267475268119
    "JewelerySpriteSheet_12" = 381312801842361883
    "JewelerySpriteSheet_14RUBY_Pendant" = 3726802059033397765
    "JewelerySpriteSheet_15Emerald_Pendant" = -4369353832453534255
    "JewelerySpriteSheet_16" = 7238642868589546159
    "JewelerySpriteSheet_17Amethyst_Pendant" = 6223703686444782735
    "JewelerySpriteSheet_19Diamond_Pendant" = 9214255559410618789
}

function Get-PoolEntryYaml($stat, $min, $max, $kind) {
@"

  - stat: $stat
    weight: 1
    minValue: $min
    maxValue: $max
    valueKind: $kind
    rollSecondaryValue: 0
    secondaryMinValue: 0
    secondaryMaxValue: 0
"@
}

function Get-GemPoolYaml([int]$gemType) {
    $health = Get-PoolEntryYaml 0 5 10 0
    $gem = switch ($gemType) {
        1 { # Ruby
            (Get-PoolEntryYaml 15 1 3 3) +
            (Get-PoolEntryYaml 36 2 4 3) +
            (Get-PoolEntryYaml 37 3 6 3)
        }
        2 { # Emerald
            (Get-PoolEntryYaml 17 1 3 3) +
            (Get-PoolEntryYaml 27 2 5 3) +
            (Get-PoolEntryYaml 26 3 7 3) +
            (Get-PoolEntryYaml 22 2 5 3) +
            (Get-PoolEntryYaml 92 2 4 3) +
            (Get-PoolEntryYaml 45 3 6 3)
        }
        3 { # Sapphire
            (Get-PoolEntryYaml 23 2 5 3) +
            (Get-PoolEntryYaml 19 2 5 3) +
            (Get-PoolEntryYaml 21 2 5 3) +
            (Get-PoolEntryYaml 44 3 6 3) +
            (Get-PoolEntryYaml 89 2 4 3)
        }
        4 { # Citrine
            (Get-PoolEntryYaml 29 2 5 3) +
            (Get-PoolEntryYaml 30 1 3 3) +
            (Get-PoolEntryYaml 31 1 3 3) +
            (Get-PoolEntryYaml 32 2 5 3)
        }
        5 { # Quartz
            (Get-PoolEntryYaml 12 1 3 3) +
            (Get-PoolEntryYaml 16 1 3 3) +
            (Get-PoolEntryYaml 19 1 3 3) +
            (Get-PoolEntryYaml 17 1 3 3)
        }
        6 { # Diamond
            (Get-PoolEntryYaml 33 2 5 3) +
            (Get-PoolEntryYaml 34 3 6 3) +
            (Get-PoolEntryYaml 27 2 5 3)
        }
        7 { # Amethyst
            (Get-PoolEntryYaml 24 2 5 3) +
            (Get-PoolEntryYaml 38 2 4 3) +
            (Get-PoolEntryYaml 39 3 6 3) +
            (Get-PoolEntryYaml 27 2 5 3)
        }
        8 { # Topaz
            (Get-PoolEntryYaml 15 1 3 3) +
            (Get-PoolEntryYaml 19 1 3 3) +
            (Get-PoolEntryYaml 20 2 5 3) +
            (Get-PoolEntryYaml 42 2 4 3) +
            (Get-PoolEntryYaml 43 3 6 3)
        }
        default { "" }
    }
    return $health + $gem
}

function New-JewelryAssetYaml(
    [string]$fileName,
    [string]$itemId,
    [string]$displayName,
    [string]$description,
    [int]$equipSlot,
    [int]$gemType,
    [string]$spriteName,
    [int]$uniquelyEquipped,
    [float]$respawnReduction = 0
) {
    $spriteId = $SpriteIds[$spriteName]
    $pool = Get-GemPoolYaml $gemType
    $respawnYaml = if ($respawnReduction -gt 0) { $respawnReduction } else { 0 }

@"

%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: $ScriptGuid, type: 3}
  m_Name: $fileName
  m_EditorClassIdentifier: Assembly-CSharp::ItemDefinition
  itemKind: 4
  maxStack: 1
  itemId: $itemId
  displayName: $displayName
  displayNameSingular: 
  icon: {fileID: $spriteId, guid: $SpriteGuid, type: 3}
  heldSprite: {fileID: 0}
  equippedSprite: {fileID: 0}
  useCustomEquippedPose: 0
  equippedLocalOffset: {x: 0, y: 0}
  equippedLocalRotationZ: 0
  equippedFlipX: 0
  equippedFlipY: 0
  description: $description
  rarity: 1
  value: 800
  equipSlot: $equipSlot
  uniquelyEquipped: $uniquelyEquipped
  handVisualKey: 0
  usedUpgradeSlots: 0
  successfulEnhancements: 0
  enhancementScrollHistory: []
  weaponStats:
    minPhysicalDamage: 0
    maxPhysicalDamage: 0
    minFireDamage: 0
    maxFireDamage: 0
    minIceDamage: 0
    maxIceDamage: 0
    minLightningDamage: 0
    maxLightningDamage: 0
    legacyMinMagicDamage: 0
    legacyMaxMagicDamage: 0
    minCorruptionDamage: 0
    maxCorruptionDamage: 0
    attacksPerSecond: 0
    critChance: 0
    critMultiplier: 0
    handedness: 0
    attackRange: 0
    attackSkill: 0
    mainHandArchetype: 0
    rangedBowType: 0
    magicAttackType: 0
    manaCostPerAttack: 0
    magicAilmentApplyChance: 0
    unusedLegacyWeaponBurnChance: 0
    canEquipInOffHand: 0
    requiresOffhandSupport: 0
    requiredSupportType: 0
    equipmentTier: 0
    weaponWeight: 0
  combatSupportStats:
    supportType: 0
    requiredMainHandArchetype: 0
    bonusPhysicalDamage: 0
    bonusMagicDamage: 0
    bonusCorruptionDamage: 0
    critChanceBonus: 0
    critMultiplierBonus: 0
    attackSpeedPercent: 0
    globalPhysicalDamagePercent: 0
    rangedPhysicalDamagePercent: 0
    magicDamagePercent: 0
    fireDamagePercent: 0
    iceDamagePercent: 0
    coldDamagePercent: 0
    corruptionDamagePercent: 0
    lightningDamagePercent: 0
    spellDamagePercent: 0
    minFireDamage: 0
    maxFireDamage: 0
    minIceDamage: 0
    maxIceDamage: 0
    minLightningDamage: 0
    maxLightningDamage: 0
    chargedRuneElement: 0
    consumableOnAttack: 0
    consumeAmountPerAttack: 0
    consumableOnSpell: 0
  toolStats:
    toolType: 0
    equipmentTier: 0
    gatherSpeedMultiplier: 0
    gatheringGrit: 0
    bonusResourceFindChance: 0
    staminaEfficiency: 0
  armourStats:
    equipmentTier: 0
    armourType: 0
    armour: 0
    magicResist: 0
    corruptionResist: 0
    physBlockChance: 0
    physBlockMitigation: 0
    bonusHealth: 0
    bonusEnergy: 0
    energyEfficiency: 0
    flatGuard: 0
    maxGuardPercent: 0
    armor: 0
    staminaEfficiency: 0
    armorType: 0
  bonusStats:
    bonusHealth: 5
    bonusEnergy: 0
    bonusMana: 0
    maxHealthPercent: 0
    armour: 0
    magicResist: 0
    corruptionResist: 0
    physBlockChance: 0
    physBlockMitigation: 0
    lifeRegen: 0
    energyRegen: 0
    manaRegen: 0
    energyEfficiency: 0
    lifeSteal: 0
    moveSpeedPercent: 0
    physicalDamage: 0
    meleePhysicalDamagePercent: 0
    globalPhysicalDamagePercent: 0
    rangedPhysicalDamagePercent: 0
    magicDamage: 0
    magicDamagePercent: 0
    fireSkillDamagePercent: 0
    iceSkillDamagePercent: 0
    lightningSkillDamagePercent: 0
    spellDamagePercent: 0
    corruptionDamagePercent: 0
    corruptionDamage: 0
    abilityPower: 0
    attackSpeedPercent: 0
    abilityCooldownReductionFraction: 0
    minionDamagePercent: 0
    minionAttackSpeedPercent: 0
    minionCritChance: 0
    minionMaxLifePercent: 0
    critChanceBonus: 0
    critMultiplierBonus: 0
    attackRangeBonus: 0
    bleedChance: 0
    bleedMultiplier: 0
    poisonChance: 0
    poisonMultiplier: 0
    poisonDurationBonus: 0
    poisonMaxStacksBonus: 0
    burnExplosionMultiplierBonus: 0
    burnChance: 0
    chillChance: 0
    shockChance: 0
    chillSlowPerStackBonus: 0
    shockDamageTakenMultiplierBonus: 0
    allElementalAilmentChance: 0
    parryChance: 0
    stunChance: 0
    minThornsDamage: 0
    maxThornsDamage: 0
    thornsDamagePercent: 0
    evadeChance: 0
  randomStatPool:$pool
  useDefaultRandomStatPoolPackage: 1
  extraRandomStatPoolPackages: 0
  jewelryEquipmentTier: 0
  jewelryGemType: $gemType
  miscEffects:
    enemyRespawnTimeReductionSeconds: $respawnYaml
  randomStatsPendingIdentification: 0
  consumableStats:
    consumableType: 0
    healAmount: 0
    energyAmount: 0
    cooldownSeconds: 0
    consumeOnUse: 0
    grantedEffect:
      effectType: 0
      magnitude: 0
      duration: 0
      effectId: 
      hideFromBuffPanel: 0
    baitTier: 0
    fishingSpeedPercentBonus: 0
    mapEnhancementTier: 0
    mapEnhancementModRolls: []
    openRequiredAmount: 0
    openableLoot: []
    foodEffectDurationSeconds: 0
    foodEnableRegen: 0
    foodRegenTotalHeal: 0
    foodEnableSwiftness: 0
    foodSwiftnessPercentBonus: 0
    foodEnableOverheal: 0
    foodOverhealMaxAboveMaxHp: 0
    foodOverhealInstantHeal: 0
    foodEnableFocused: 0
    foodFocusedDamageBonusFraction: 0
  enhancementOptionId: 
  enhancementScrollStats:
    successChance: 0
    targetStat: 0
    modifierKind: 0
    modifierValue: 0
    consumeSlotOnFailure: 0
    failureOutcome: 0
    destroyChanceOnFailure: 0
    cursed: 0
    allowedGearTypes: 0
  mapEnhancementStats:
    tier: 0
    modRolls: []
  cookableStats:
    isCookable: 0
    cookedResultItemId: 
    cookedResultAmount: 0
    requiredCookingLevel: 0
    cookingXp: 0
"@
}

$renames = @(
    @{ Old = "ability_power_pendant"; New = "amethyst_pendant" },
    @{ Old = "bone_ring"; New = "citrine_ring" },
    @{ Old = "crit_ring"; New = "diamond_ring" },
    @{ Old = "physical_damage_pendant"; New = "ruby_pendant" },
    @{ Old = "vamp_ring"; New = "quartz_ring" }
)

foreach ($r in $renames) {
    $oldPath = Join-Path $JewelryFolder "$($r.Old).asset"
    $newPath = Join-Path $JewelryFolder "$($r.New).asset"
    $oldMeta = "$oldPath.meta"
    $newMeta = "$newPath.meta"
    if (Test-Path $oldPath) {
        if (Test-Path $newPath) { Remove-Item $newPath -Force }
        Move-Item $oldPath $newPath
        if (Test-Path $oldMeta) {
            if (Test-Path $newMeta) { Remove-Item $newMeta -Force }
            Move-Item $oldMeta $newMeta
        }
        Write-Host "Renamed $($r.Old) -> $($r.New)"
    }
}

$existingSpecs = @(
    @{ File = "amethyst_pendant"; Id = "amethyst_pendant"; Name = "Amethyst Pendant"; Desc = "Tier 1 amethyst pendant."; Slot = 7; Gem = 7; Sprite = "JewelerySpriteSheet_17Amethyst_Pendant"; Unique = 0 },
    @{ File = "citrine_ring"; Id = "citrine_ring"; Name = "Citrine Ring"; Desc = "Tier 1 citrine ring."; Slot = 8; Gem = 4; Sprite = "JewelerySpriteSheet_4CITRINE_Ring"; Unique = 1 },
    @{ File = "diamond_ring"; Id = "diamond_ring"; Name = "Diamond Ring"; Desc = "Tier 1 diamond ring."; Slot = 8; Gem = 6; Sprite = "JewelerySpriteSheet_7"; Unique = 1 },
    @{ File = "ruby_pendant"; Id = "ruby_pendant"; Name = "Ruby Pendant"; Desc = "Tier 1 ruby pendant."; Slot = 7; Gem = 1; Sprite = "JewelerySpriteSheet_14RUBY_Pendant"; Unique = 0 },
    @{ File = "quartz_ring"; Id = "quartz_ring"; Name = "Quartz Ring"; Desc = "Tier 1 quartz ring."; Slot = 8; Gem = 5; Sprite = "JewelerySpriteSheet_6"; Unique = 1 },
    @{ File = "sapphire_trinket"; Id = "sapphire_trinket"; Name = "Trinket of respawning"; Desc = "Reduces the time enemies in the current combat map take to respawn."; Slot = 6; Gem = 3; Sprite = "JewelerySpriteSheet_16"; Unique = 0; Respawn = 1 }
)

$newSpecs = @(
    @{ File = "ruby_ring"; Id = "ruby_ring"; Name = "Ruby Ring"; Desc = "Tier 1 ruby ring."; Slot = 8; Gem = 1; Sprite = "JewelerySpriteSheet_0RUBY_Ring"; Unique = 1 },
    @{ File = "emerald_ring"; Id = "emerald_ring"; Name = "Emerald Ring"; Desc = "Tier 1 emerald ring."; Slot = 8; Gem = 2; Sprite = "JewelerySpriteSheet_1EMERALD_Ring"; Unique = 1 },
    @{ File = "emerald_pendant"; Id = "emerald_pendant"; Name = "Emerald Pendant"; Desc = "Tier 1 emerald pendant."; Slot = 7; Gem = 2; Sprite = "JewelerySpriteSheet_15Emerald_Pendant"; Unique = 0 },
    @{ File = "sapphire_ring"; Id = "sapphire_ring"; Name = "Sapphire Ring"; Desc = "Tier 1 sapphire ring."; Slot = 8; Gem = 3; Sprite = "JewelerySpriteSheet_2SAPPHIRE_Ring"; Unique = 1 },
    @{ File = "sapphire_pendant"; Id = "sapphire_pendant"; Name = "Sapphire Pendant"; Desc = "Tier 1 sapphire pendant."; Slot = 7; Gem = 3; Sprite = "JewelerySpriteSheet_16"; Unique = 0 },
    @{ File = "citrine_pendant"; Id = "citrine_pendant"; Name = "Citrine Pendant"; Desc = "Tier 1 citrine pendant."; Slot = 7; Gem = 4; Sprite = "JewelerySpriteSheet_3"; Unique = 0 },
    @{ File = "quartz_pendant"; Id = "quartz_pendant"; Name = "Quartz Pendant"; Desc = "Tier 1 quartz pendant."; Slot = 7; Gem = 5; Sprite = "JewelerySpriteSheet_11"; Unique = 0 },
    @{ File = "diamond_pendant"; Id = "diamond_pendant"; Name = "Diamond Pendant"; Desc = "Tier 1 diamond pendant."; Slot = 7; Gem = 6; Sprite = "JewelerySpriteSheet_19Diamond_Pendant"; Unique = 0 },
    @{ File = "amethyst_ring"; Id = "amethyst_ring"; Name = "Amethyst Ring"; Desc = "Tier 1 amethyst ring."; Slot = 8; Gem = 7; Sprite = "JewelerySpriteSheet_12"; Unique = 1 }
)

$newGuids = @{}

function Write-JewelrySpec($spec, [bool]$isNew) {
    $respawn = if ($spec.Respawn) { $spec.Respawn } else { 0 }
    $yaml = New-JewelryAssetYaml $spec.File $spec.Id $spec.Name $spec.Desc $spec.Slot $spec.Gem $spec.Sprite $spec.Unique $respawn
    $path = Join-Path $JewelryFolder "$($spec.File).asset"
    Set-Content -Path $path -Value $yaml.TrimStart() -NoNewline -Encoding utf8
    if ($isNew) {
        $guid = [guid]::NewGuid().ToString("N")
        $newGuids[$spec.File] = $guid
        $meta = @"
fileFormatVersion: 2
guid: $guid
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 

"@
        Set-Content -Path "$path.meta" -Value $meta -NoNewline -Encoding utf8
        Write-Host "Created $($spec.File)"
    } else {
        Write-Host "Updated $($spec.File)"
    }
}

foreach ($spec in $existingSpecs) { Write-JewelrySpec $spec $false }
foreach ($spec in $newSpecs) { Write-JewelrySpec $spec $true }

# Register new items in ItemDatabase
$db = Get-Content $DbPath -Raw
foreach ($entry in $newGuids.GetEnumerator()) {
    $guid = $entry.Value
    $ref = "  - {fileID: 11400000, guid: $guid, type: 2}"
    if ($db -notmatch $guid) {
        $db = $db -replace "(items:\r?\n)", "`$1$ref`r`n"
        Write-Host "Registered $($entry.Key) in ItemDatabase"
    }
}
Set-Content -Path $DbPath -Value $db -NoNewline -Encoding utf8

Write-Host "Done."
