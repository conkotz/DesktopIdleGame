$maps = @{
    "MapNode_verdant_vale" = "f1a2b3c4d5e6478990ab1c2d3e4f5a6b"
    "MapNode_crystal_lake" = "8c4e2f1a9b3d5e7046a8c1d2f3b5e709"
    "MapNode_stone_quarry" = "b3c4d5e6f7a8490123cd4e5f6a7b8c9d"
    "MapNode_timber_grove" = "c4d5e6f7a8b9501234de5f6a7b8c9d0e"
    "MapNode_storm_coast" = "d5e6f7a8b9c0512345ef6a7b8c9d0e1f"
    "MapNode_ancient_forest" = "e6f7a8b9c0d1623456fa7b8c9d0e1f2a"
    "MapNode_deepstone_caverns" = "f7a8b9c0d1e2734567ab8c9d0e1f2a3b"
}
$dir = "d:\UNITY\Desktop Idle RPG Game\Assets\3.ScriptableObjects\LevelDefinitions\LevelsDefinitions\Greenlands"
foreach ($kv in $maps.GetEnumerator()) {
    $meta = @"
fileFormatVersion: 2
guid: $($kv.Value)
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 

"@
    [System.IO.File]::WriteAllText((Join-Path $dir "$($kv.Key).asset.meta"), $meta)
    Write-Host "Meta $($kv.Key)"
}
