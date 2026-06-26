$ErrorActionPreference = "Stop"
Set-Location "d:\UNITY\Desktop Idle RPG Game"
$outDir = "Assets/3.ScriptableObjects/LevelDefinitions/LevelsDefinitions/Greenlands"

$signPost = @{ fileId = "8347273920747777605"; guid = "1d359e69b63b58e4a94ec38646014fa6" }
$prefabs = @{
    Stone      = @{ fileId = "1599336663878775552"; guid = "133ab72ffa5091c479ee7c613cb8f309" }
    Iron       = @{ fileId = "1392043344387743544"; guid = "2737b2113e4d40f39349f9b479f4ed4d" }
    Mythril    = @{ fileId = "1392043344387743544"; guid = "7df643b896f24475b793f5bc1bce53b0" }
    Runite     = @{ fileId = "1392043344387743544"; guid = "58070ec90b6e49e589b9b70ad02be460" }
    Celestium  = @{ fileId = "1392043344387743544"; guid = "0e959a6952e44253a3bb5007089d39e0" }
    Runestone  = @{ fileId = "1392043344387743544"; guid = "ad4915f3d5918804dbdb8cf3d7cb1173" }
    Gemstone   = @{ fileId = "1392043344387743544"; guid = "43d397895d334a95894770ba57edf294" }
    Splitwood  = @{ fileId = "7126874587929216248"; guid = "51e7c9c2e5f388141a486b8bbe03ff46" }
    Hardwood   = @{ fileId = "3575732909794473052"; guid = "87e49f7619ae6b345b0b823772b1d8f1" }
    Wildwood   = @{ fileId = "7126874587929216248"; guid = "fe81c90d3d33aae49bdb093dc4b67cd8" }
    EmberOak   = @{ fileId = "7126874587929216248"; guid = "e3a2b1c056789012def4b56789012345" }
    Spiritwood = @{ fileId = "7126874587929216248"; guid = "f4b3c2d167890123efa5c67890123456" }
    Pond       = @{ fileId = "6165540910069784205"; guid = "3f7498b5e783df94ba16c5b9d0d75259" }
}

function Format-PrefabRef($p) { "fileID: $($p.fileId), guid: $($p.guid), type: 3" }

function New-SpawnRow($pointName, $prefabKey, $portal = "") {
    $p = $prefabs[$prefabKey]
    $portalVal = if ($portal) { $portal } else { "" }
    @"
    - spawnPointGroupId: 
      spawnPointName: $pointName
      enemyDefinition: {fileID: 0}
      prefab: {$(Format-PrefabRef $p)}
      itemDefinition: {fileID: 0}
      itemAmount: 0
      itemRespawnTimer: 0
      respawnUntilSimpleWavesStart: 0
      levelOneShotPickupKey: 
      count: 1
      portalTargetMapNodeId: $portalVal
      teleporterLinkId: 
      teleporterDestinationLabel: 
"@
}

function New-SignpostRow($pointName, $portal) {
    $p = $signPost
    @"
    - spawnPointGroupId: 
      spawnPointName: $pointName
      enemyDefinition: {fileID: 0}
      prefab: {$(Format-PrefabRef $p)}
      itemDefinition: {fileID: 0}
      itemAmount: 0
      itemRespawnTimer: 10
      respawnUntilSimpleWavesStart: 0
      levelOneShotPickupKey: 
      count: 1
      portalTargetMapNodeId: $portal
      teleporterLinkId: 
      teleporterDestinationLabel: 
"@
}

function Add-GroupedSpawns([System.Collections.Generic.List[string]]$list, $startIndex, $count, $prefabKey) {
    for ($i = 0; $i -lt $count; $i++) {
        $list.Add((New-SpawnRow "SpawnPoint$($startIndex + $i)" $prefabKey))
    }
}

function Write-MapAsset($fileName, $nodeId, $displayName, $description, $biome, $gatheringMode, $gatheringLevel, $skillReqs, $notes, $spawnRows, $connected) {
    if ($skillReqs.Count -eq 0) {
        $skillYaml = "[]"
    } else {
        $lines = @()
        foreach ($sr in $skillReqs) { $lines += "  - skill: $($sr.skill)"; $lines += "    requiredLevel: $($sr.level)" }
        $skillYaml = "`n" + ($lines -join "`n")
    }

    if ($connected.Count -eq 0) {
        $connectedYaml = "[]"
    } else {
        $connectedYaml = "`n" + (($connected | ForEach-Object { "  - $_" }) -join "`n")
    }

    $spawnBlock = $spawnRows -join "`n"

    $yaml = @"
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
  m_Script: {fileID: 11500000, guid: 2935ef89b192ceb418b1107ddcd0fdf3, type: 3}
  m_Name: $fileName
  m_EditorClassIdentifier: Assembly-CSharp::MapNodeDefinition
  nodeId: $nodeId
  displayName: $displayName
  description: $description
  nodeType: 1
  enemyAggroMode: 1
  ignoreAggroRange: 0
  respawnHereIfDied: 0
  mapCombatScalingEnabled: 0
  mapCombatScalingSpecialDropsByLevel: []
  enemyRespawnEnabled: 0
  enemyRespawnDelaySeconds: 30
  eliteSpawnChance: 0
  fallbackRecommendedCombatPower: 1
  isRepeatable: 1
  markCompletedWhenReturningToMenu: 0
  entranceOnlyAccess: 0
  completedLabelRequiresEnteredNodeId: 
  mapUiUseClearedInsteadOfCompleted: 0
  requiresMapUnlock: 0
  icon: {fileID: 0}
  requiredSkillLevels:$skillYaml
  gatheringSkillGateMode: $gatheringMode
  gatheringRequiredLevel: $gatheringLevel
  combatSkillGateMode: 0
  combatSingleSkill: 0
  combatRequiredLevel: 1
  unlockRequirementNotes: $notes
  connectedNodeIds:$connectedYaml
  nextNodeIds: []
  biome: $biome
  spawnGroupPlans:
  - groupId: AllSpawns
    spawns:
$spawnBlock
    shuffleSpawnPoints: 0
  enabledSidePlayAreaIds: []
  useDefaultSpawnOnMapTeleport: 0
  defaultPlayerSpawnPointName: 
  inMapTeleporters: []
  requiredPreviousMapCompletions: []
  simpleCombatWaves: []
  simpleCombatWavesAutoStartOnLevelEnter: 1
  simpleCombatWavesRequireQuestRewardClaimed: 0
  simpleCombatWavesRequiredQuestId: 
  simpleCombatWavesRequireQuestAccepted: 0
  simpleCombatWavesQuestAcceptedId: 
  simpleCombatWavesRunOncePerSceneSession: 1
  enduranceWaves: []
  enduranceCompletionLoot: []
  enduranceCompletionLootByTier: []
  enduranceCompletionLootInterval: 0.5
  contentDesignerNotes: 
  sceneNamePlaceholder: 
  encounterIdPlaceholder: 
"@

    $path = Join-Path $outDir "$fileName.asset"
    [System.IO.File]::WriteAllText((Join-Path (Get-Location) $path), $yaml)
    Write-Host "Wrote $path"
}

function New-SpawnList { return New-Object System.Collections.Generic.List[string] }

# 1 Verdant Vale
$v = New-SpawnList
$v.Add((New-SignpostRow "SpawnPoint-27" "green_grove"))
$v.Add((New-SignpostRow "SpawnPoint60" "storm_coast"))
$v.Add((New-SignpostRow "SpawnPoint62" "ancient_forest"))
$v.Add((New-SignpostRow "SpawnPoint64" "deepstone_caverns"))
$v.Add((New-SpawnRow "SpawnPoint-24" "Runite"))
$v.Add((New-SpawnRow "SpawnPoint-21" "Mythril"))
$v.Add((New-SpawnRow "SpawnPoint-17" "Mythril"))
$v.Add((New-SpawnRow "SpawnPoint-10" "Runestone"))
$v.Add((New-SpawnRow "SpawnPoint-5" "EmberOak"))
$v.Add((New-SpawnRow "SpawnPoint14" "EmberOak"))
$v.Add((New-SpawnRow "SpawnPoint20_Middle" "Wildwood"))
$v.Add((New-SpawnRow "SpawnPoint26" "Wildwood"))
$v.Add((New-SpawnRow "SpawnPoint42" "Wildwood"))
$v.Add((New-SpawnRow "SpawnPoint48" "Pond"))
$v.Add((New-SpawnRow "SpawnPoint66" "Pond"))
Write-MapAsset "MapNode_verdant_vale" "verdant_vale" "Verdant Vale" "Upgraded gathering resources." 1 1 20 @() "Level 20 in Mining, Woodcutting, or Fishing" $v @("storm_coast","ancient_forest","deepstone_caverns")

# 2 Crystal Lake
$c = New-SpawnList
$c.Add((New-SignpostRow "SpawnPoint-27" "green_grove"))
$c.Add((New-SpawnRow "SpawnPoint20_Middle" "Pond"))
$c.Add((New-SpawnRow "SpawnPoint26" "Pond"))
Write-MapAsset "MapNode_crystal_lake" "crystal_lake" "Crystal Lake" "A quiet fishing spot." 1 0 1 @(@{skill=2;level=10}) "Fishing rod recommended" $c @()

# 3 Stone Quarry
$q = New-SpawnList
$q.Add((New-SignpostRow "SpawnPoint-27" "green_grove"))
Add-GroupedSpawns $q 1 5 "Stone"
Add-GroupedSpawns $q 6 6 "Iron"
Add-GroupedSpawns $q 12 4 "Mythril"
Add-GroupedSpawns $q 16 2 "Runestone"
$q.Add((New-SpawnRow "SpawnPoint18" "Gemstone"))
Write-MapAsset "MapNode_stone_quarry" "stone_quarry" "Stone Quarry" "Grouped mining deposits." 5 0 1 @(@{skill=0;level=10}) "Pickaxe recommended" $q @()

# 4 Timber Grove
$t = New-SpawnList
$t.Add((New-SignpostRow "SpawnPoint-27" "green_grove"))
Add-GroupedSpawns $t 1 5 "Splitwood"
Add-GroupedSpawns $t 6 6 "Hardwood"
Add-GroupedSpawns $t 12 4 "Wildwood"
Write-MapAsset "MapNode_timber_grove" "timber_grove" "Timber Grove" "Grouped woodcutting trees." 1 0 1 @(@{skill=1;level=10}) "Axe recommended" $t @()

# 5 Storm Coast
$s = New-SpawnList
$s.Add((New-SignpostRow "SpawnPoint-27" "verdant_vale"))
$s.Add((New-SpawnRow "SpawnPoint20_Middle" "Pond"))
$s.Add((New-SpawnRow "SpawnPoint26" "Pond"))
Write-MapAsset "MapNode_storm_coast" "storm_coast" "Storm Coast" "Coastal fishing spot." 4 0 1 @(@{skill=2;level=30}) "Fishing rod recommended" $s @()

# 6 Ancient Forest
$f = New-SpawnList
$f.Add((New-SignpostRow "SpawnPoint-27" "verdant_vale"))
Add-GroupedSpawns $f 1 5 "Wildwood"
Add-GroupedSpawns $f 6 6 "EmberOak"
Add-GroupedSpawns $f 12 4 "Spiritwood"
Write-MapAsset "MapNode_ancient_forest" "ancient_forest" "Ancient Forest" "High-tier woodcutting trees." 1 0 1 @(@{skill=1;level=30}) "Axe recommended" $f @()

# 7 Deepstone Caverns
$d = New-SpawnList
$d.Add((New-SignpostRow "SpawnPoint-27" "verdant_vale"))
Add-GroupedSpawns $d 1 5 "Mythril"
Add-GroupedSpawns $d 6 6 "Runite"
Add-GroupedSpawns $d 12 4 "Celestium"
Add-GroupedSpawns $d 16 3 "Runestone"
Add-GroupedSpawns $d 19 2 "Gemstone"
Write-MapAsset "MapNode_deepstone_caverns" "deepstone_caverns" "Deepstone Caverns" "High-tier mining deposits." 6 0 1 @(@{skill=0;level=30}) "Pickaxe recommended" $d @()

Write-Host "Done."
