# First-pass mining tier resources: items, node defs, prefab variants.
$ErrorActionPreference = "Stop"
$Root = "D:\UNITY\Desktop Idle RPG Game"
$MiningItems = Join-Path $Root "Assets\3.ScriptableObjects\ItemsDefinitions\Resources\Mining"
$MiningNodes = Join-Path $Root "Assets\3.ScriptableObjects\ResourceNodeDefinitions\MiningSpots"
$MiningPrefabs = Join-Path $Root "Assets\2.Prefabs\ResourcesNodes\Mining"
$ItemDbPath = Join-Path $Root "Assets\Resources\Databases\ItemDatabase.asset"
$StoneItemTemplate = Join-Path $MiningItems "stone_chunk.asset"
$RunestonePrefabTemplate = Join-Path $MiningPrefabs "RunestoneDeposit.prefab"
$StoneDepositGuid = "133ab72ffa5091c479ee7c613cb8f309"
$NodeScriptGuid = "225d4c1aca51d1641b49131edf107d33"
$OreSheetGuid = "5ad45366897be304fa783e82de378f70"
$NodeSheetGuid = "0d6b2f4bf40a49f449d77dbbbc76145e"

function New-Guid32 { return [guid]::NewGuid().ToString("N").Substring(0, 32) }

function Write-Utf8NoBom($path, $content) {
    $normalized = $content.TrimStart("`r", "`n")
    if (-not $normalized.EndsWith("`n")) { $normalized += "`n" }
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($path, $normalized, $utf8NoBom)
}

function Write-Meta($path, $guid) {
    Write-Utf8NoBom $path @"
fileFormatVersion: 2
guid: $guid
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@
}

function Write-PrefabMeta($path, $guid) {
    Write-Utf8NoBom $path @"
fileFormatVersion: 2
guid: $guid
PrefabImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@
}

function New-ItemAsset($assetPath, $itemId, $displayName, $description, $iconFileId, $rarity, $value) {
    $content = Get-Content $StoneItemTemplate -Raw
    $content = $content -replace 'm_Name: stone_chunk', "m_Name: $itemId"
    $content = $content -replace 'itemId: stone_chunk', "itemId: $itemId"
    $content = $content -replace 'displayName: Stone Chunk', "displayName: $displayName"
    $content = $content -replace 'description: A stone chunk', "description: $description"
    $content = $content -replace 'icon: \{fileID: \d+, guid: [a-f0-9]+, type: 3\}', "icon: {fileID: $iconFileId, guid: $OreSheetGuid, type: 3}"
    $content = $content -replace 'rarity: 0', "rarity: $rarity"
    $content = $content -replace 'value: 18', "value: $value"
    Write-Utf8NoBom $assetPath $content.TrimEnd()
}

function ItemRef($guid) { "  - item: {fileID: 11400000, guid: $guid, type: 2}`n    chance: " }

function Build-BonusLines($drops) {
    $lines = New-Object System.Collections.Generic.List[string]
    foreach ($d in $drops) {
        $lines.Add("  - item: {fileID: 11400000, guid: $($d.guid), type: 2}")
        $lines.Add("    chance: $($d.chance)")
        $lines.Add("    amountMin: 1")
        $lines.Add("    amountMax: 1")
    }
    return ($lines -join "`n")
}

function New-NodeAsset($path, $name, $displayName, $level, $xp, $oreGuid, $minInt, $maxInt, $energyPct, $bonusYaml) {
    Write-Utf8NoBom $path @"
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
  m_Script: {fileID: 11500000, guid: $NodeScriptGuid, type: 3}
  m_Name: $name
  m_EditorClassIdentifier: Assembly-CSharp::NodeDefinition
  displayName: $displayName
  actionType: 0
  gatherPlayerAction: 2
  useLevelRequirement: 1
  requiredLevel: $level
  xpPerTick: $xp
  mainYieldEntries:
  - item: {fileID: 11400000, guid: $oreGuid, type: 2}
    itemRequiredLevel: 1
    chancePercent: 100
    xpPerTick: 0
  minInterval: $minInt
  maxInterval: $maxInt
  bonusDrops:
$bonusYaml
  requiresTool: 1
  requiredTool: 2
  missingToolMessage: No pickaxe found in toolbelt
  energyCostPercentOfMaxPerSwing: $energyPct
  depletionGatherCount: 15
  depletionRegenSeconds: 60
  depletedYieldMultiplier: 0.3
"@
}

function New-PrefabVariant($path, $prefabName, $labelText, $spriteFileId, $nodeDefGuid) {
    $content = Get-Content $RunestonePrefabTemplate -Raw
    $content = $content -replace 'value: RunestoneDeposit', "value: $prefabName"
    $content = $content -replace "(?s)value: 'Runestone\s+Deposit'", "value: '$labelText'"
    $content = $content -replace '\{fileID: -3267488870146170370, guid: ca56f4a66fb9cd145a055cda7e049823, type: 3\}', "{fileID: $spriteFileId, guid: $NodeSheetGuid, type: 3}"
    $content = $content -replace 'guid: f9e2b3c4d5e6789012345bcde6789012', "guid: $nodeDefGuid"
    Write-Utf8NoBom $path $content.TrimEnd()
}

# Gem GUIDs
$G = @{
    sapphire = "36038d5dfd5228b4682f03cfcf3151df"
    ruby     = "212768177bfc68f44aec2e779670e552"
    emerald  = "78978cfc548432a4ea84da872ae16b2f"
    quartz   = "0679f085e3db4718b1e26f9a5f3d7027"
    amethyst = "1018484d9d9649bb858c783bc64917ad"
    citrine  = "f16afa89917c4dc69b26df39e9b7d941"
    topaz    = "ded6deb4923b40578382181cd24dad80"
    crystal  = "1fe8129001de4ab98aad722013c08832"
    diamond  = "a8dba90ddcaa45819ac3ef0c50c2762a"
    obsidian = "5e73624f54494e4db98f7bb887119357"
}

$stoneBonus = @(@{ guid = $G.sapphire; chance = "0.1" })
$ironBonus = $stoneBonus
$mythriteBonus = @(
    @{ guid = $G.quartz; chance = "0.08" },
    @{ guid = $G.amethyst; chance = "0.08" },
    @{ guid = $G.citrine; chance = "0.08" },
    @{ guid = $G.topaz; chance = "0.08" },
    @{ guid = $G.sapphire; chance = "0.05" },
    @{ guid = $G.ruby; chance = "0.05" },
    @{ guid = $G.emerald; chance = "0.05" }
)
$runiteBonus = $mythriteBonus + @(
    @{ guid = $G.crystal; chance = "0.03" },
    @{ guid = $G.diamond; chance = "0.03" }
)
$celestiumBonus = $runiteBonus + @(
    @{ guid = $G.obsidian; chance = "0.01" }
)
$gemstoneBonus = @(
    @{ guid = $G.quartz; chance = "0.08" },
    @{ guid = $G.amethyst; chance = "0.08" },
    @{ guid = $G.citrine; chance = "0.08" },
    @{ guid = $G.topaz; chance = "0.08" },
    @{ guid = $G.sapphire; chance = "0.05" },
    @{ guid = $G.ruby; chance = "0.05" },
    @{ guid = $G.emerald; chance = "0.05" }
)

$items = @(
    @{ id = "iron_ore"; name = "Iron Ore"; desc = "Raw iron ore from an iron deposit."; fileId = "811745016679173300"; rarity = 1; value = 45; guid = (New-Guid32) },
    @{ id = "iron_bar"; name = "Iron Bar"; desc = "A smelted iron bar."; fileId = "950266229"; rarity = 1; value = 120; guid = (New-Guid32) },
    @{ id = "mythrite_ore"; name = "Mythrite Ore"; desc = "Raw mythrite ore from a mythrite deposit."; fileId = "-8182864516769833960"; rarity = 2; value = 110; guid = (New-Guid32) },
    @{ id = "mythrite_bar"; name = "Mythrite Bar"; desc = "A smelted mythrite bar."; fileId = "-1446843033"; rarity = 2; value = 300; guid = (New-Guid32) },
    @{ id = "runite_ore"; name = "Runite Ore"; desc = "Raw runite ore from a runite deposit."; fileId = "-5739646596214114644"; rarity = 3; value = 275; guid = (New-Guid32) },
    @{ id = "runite_bar"; name = "Runite Bar"; desc = "A smelted runite bar."; fileId = "-516466324"; rarity = 3; value = 750; guid = (New-Guid32) },
    @{ id = "celestium_ore"; name = "Celestium Ore"; desc = "Raw celestium ore from a celestium deposit."; fileId = "-7875622402969895686"; rarity = 4; value = 690; guid = (New-Guid32) },
    @{ id = "celestium_bar"; name = "Celestium Bar"; desc = "A smelted celestium bar."; fileId = "-1218338670"; rarity = 4; value = 1900; guid = (New-Guid32) }
)

$itemGuidById = @{}
foreach ($it in $items) {
    $assetPath = Join-Path $MiningItems "$($it.id).asset"
    New-ItemAsset $assetPath $it.id $it.name $it.desc $it.fileId $it.rarity $it.value
    Write-Meta "$assetPath.meta" $it.guid
    $itemGuidById[$it.id] = $it.guid
    Write-Host "Item: $($it.id) -> $($it.guid)"
}

$nodes = @(
    @{
        asset = "iron_deposit_node"; display = "Iron Deposit"; prefab = "IronDeposit"
        label = "Iron`r`n`r`n        Deposit"; level = 10; xp = 15; ore = "iron_ore"
        min = 10; max = 20; energy = 15; sprite = "-5248713350725480015"
        bonus = (Build-BonusLines $ironBonus); guid = (New-Guid32); prefabGuid = (New-Guid32)
    },
    @{
        asset = "mythrite_deposit_node"; display = "Mythrite Deposit"; prefab = "MythriteDeposit"
        label = "Mythrite`r`n`r`n        Deposit"; level = 20; xp = 38; ore = "mythrite_ore"
        min = 25; max = 50; energy = 19; sprite = "1041354706888159047"
        bonus = (Build-BonusLines $mythriteBonus); guid = (New-Guid32); prefabGuid = (New-Guid32)
    },
    @{
        asset = "runite_deposit_node"; display = "Runite Deposit"; prefab = "RuniteDeposit"
        label = "Runite`r`n`r`n        Deposit"; level = 30; xp = 95; ore = "runite_ore"
        min = 63; max = 125; energy = 24; sprite = "-3694752190506741879"
        bonus = (Build-BonusLines $runiteBonus); guid = (New-Guid32); prefabGuid = (New-Guid32)
    },
    @{
        asset = "gemstone_deposit_node"; display = "Gemstone Deposit"; prefab = "GemstoneDeposit"
        label = "Gemstone`r`n`r`n        Deposit"; level = 30; xp = 95; ore = "gem_quartz"
        min = 63; max = 125; energy = 24; sprite = "-2450355515067900020"
        bonus = (Build-BonusLines $gemstoneBonus); guid = (New-Guid32); prefabGuid = (New-Guid32)
        mainOreGuid = $G.quartz
    },
    @{
        asset = "celestium_deposit_node"; display = "Celestium Deposit"; prefab = "CelestiumDeposit"
        label = "Celestium`r`n`r`n        Deposit"; level = 40; xp = 238; ore = "celestium_ore"
        min = 158; max = 313; energy = 30; sprite = "4972051977966351520"
        bonus = (Build-BonusLines $celestiumBonus); guid = (New-Guid32); prefabGuid = (New-Guid32)
    }
)

foreach ($n in $nodes) {
    $oreGuid = if ($n.mainOreGuid) { $n.mainOreGuid } else { $itemGuidById[$n.ore] }
    $nodePath = Join-Path $MiningNodes "$($n.asset).asset"
    New-NodeAsset $nodePath $n.asset $n.display $n.level $n.xp $oreGuid $n.min $n.max $n.energy $n.bonus
    Write-Meta "$nodePath.meta" $n.guid

    $prefabPath = Join-Path $MiningPrefabs "$($n.prefab).prefab"
    New-PrefabVariant $prefabPath $n.prefab $n.label $n.sprite $n.guid
    Write-PrefabMeta "$prefabPath.meta" $n.prefabGuid
    Write-Host "Node: $($n.asset) -> $($n.guid) | Prefab: $($n.prefab)"
}

$db = Get-Content $ItemDbPath -Raw
$newRefs = @()
foreach ($it in $items) {
    if ($db -notmatch [regex]::Escape($it.guid)) {
        $newRefs += "  - {fileID: 11400000, guid: $($it.guid), type: 2}"
    }
}
if ($newRefs.Count -gt 0) {
    Write-Utf8NoBom $ItemDbPath ($db.TrimEnd() + "`n" + ($newRefs -join "`n"))
}
Write-Host "Updated ItemDatabase with $($items.Count) mining items."
Write-Host "Done."
