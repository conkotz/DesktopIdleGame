# Rebuilds mining.asset minor passives while keeping non-minor unlocks (Stone, Miners Frenzy, Runestone).
$ErrorActionPreference = "Stop"
$MiningAssetPath = "D:\UNITY\Desktop Idle RPG Game\Assets\3.ScriptableObjects\SkillsDefinitions\mining.asset"
$PassiveIcon = "{fileID: 3534972268811701710, guid: 7adac55ced66def4ca2ad1c0880b555c, type: 3}"

function Get-MinorYaml([int]$level, [string]$title, [string]$description, [int]$option) {
    $descYaml = if ($description -match "[\r\n:]") { "`"$description`"" } else { $description }
@"
  - requiredLevel: $level
    title: $title
    description: $descYaml
    icon: $PassiveIcon
    unlockType: 0
    meleeMinorStatOption: 0
    rangedMinorStatOption: 0
    woodcuttingMinorStatOption: 0
    miningMinorStatOption: $option
    fishingMinorStatOption: 0
    enduranceMinorStatOption: 0
    magicMinorStatOption: 0
    legacySkillMinorStatOption: 0
    ability: {fileID: 0}
    presentation: {fileID: 0}
    choices: []

"@
}

$passives = @(
    @(2, "Mining Speed 1", "+2% Mining Speed", 10),
    @(3, "Mining Efficiency 1", "+1% Mining Stamina Efficiency", 13),
    @(4, "Mining Grit 1", "+1% Mining Grit Chance", 16),
    @(6, "Mining Bonus Find 1", "+1% Mining Bonus Find Chance", 19),
    @(7, "Mining Speed 2", "+2% Mining Speed", 10),
    @(8, "Mining Efficiency 2", "+1% Mining Stamina Efficiency", 13),
    @(9, "Mining Grit Restore 1", "Mining Grit procs restore +10 Stamina", 30),
    @(10, "Mining Grit 2", "+1% Mining Grit Chance", 16),
    @(11, "Mining Bonus Find 2", "+1% Mining Bonus Find Chance", 19),
    @(12, "Mining Speed 3", "+2% Mining Speed", 10),
    @(13, "Mining Efficiency 3", "+2% Mining Stamina Efficiency", 14),
    @(14, "Mining Double XP 1", "3% chance to gain double Mining XP", 31),
    @(16, "Mining Grit 3", "+2% Mining Grit Chance", 17),
    @(17, "Mining Speed 4", "+2% Mining Speed", 10),
    @(18, "Gem Finder 1", "+2% Mining Bonus Find Chance", 20),
    @(19, "Tireless Swing 1", "3% chance mining actions cost no stamina", 32),
    @(20, "Mining Efficiency 4", "+2% Mining Stamina Efficiency", 14),
    @(21, "Ore Conservation 1", "+10% chance this mining tick does not count toward ore depletion", 35),
    @(22, "Mining Speed 5", "+3% Mining Speed", 11),
    @(23, "Gem Finder 2", "+2% Mining Bonus Find Chance", 20),
    @(24, "Mining Momentum 1", "After Mining Grit: +5% Mining Speed for 7 seconds", 33),
    @(26, "Mining Speed 6", "+3% Mining Speed", 11),
    @(27, "Mining Efficiency 5", "+2% Mining Stamina Efficiency", 14),
    @(28, "Mining Grit Restore 2", "Mining Grit procs restore +10 Stamina", 30),
    @(29, "Mining Grit 4", "+2% Mining Grit Chance", 17),
    @(30, "Gem Finder 3", "+3% Mining Bonus Find Chance", 21),
    @(31, "Deep Focus 1", "While continuously mining: +3% Speed, +3% Stamina Efficiency", 34),
    @(32, "Ore Conservation 2", "+10% chance this mining tick does not count toward ore depletion", 35),
    @(33, "Mining Double XP 2", "3% chance to gain double Mining XP", 31),
    @(34, "Rare Gem Discovery 1", "+2% Rare Gem Discovery when finding a gem", 50),
    @(36, "Mining Speed 7", "+3% Mining Speed", 11),
    @(37, "Mining Efficiency 6", "+3% Mining Stamina Efficiency", 15),
    @(38, "Gem Finder 4", "+3% Mining Bonus Find Chance", 21),
    @(39, "Tireless Swing 2", "3% chance mining actions cost no stamina", 32),
    @(40, "Mining Grit 5", "+3% Mining Grit Chance", 18),
    @(41, "Mining Speed 8", "+4% Mining Speed", 12),
    @(42, "Mining Momentum 2", "After Mining Grit: +5% Mining Speed for 7 seconds", 33),
    @(43, "Gem Finder 5", "+5% Mining Bonus Find Chance", 22),
    @(44, "Deep Focus 2", "While continuously mining: +3% Speed, +3% Stamina Efficiency", 34),
    @(46, "Ore Conservation 3", "+10% chance this mining tick does not count toward ore depletion", 35),
    @(47, "Mining Grit 6", "+3% Mining Grit Chance", 18),
    @(48, "Rare Gem Discovery 2", "+4% Rare Gem Discovery when finding a gem", 51),
    @(49, "Mining Mastery", "5% chance a Mining Grit proc immediately triggers a second grit", 52)
)

$header = @"
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
  m_Script: {fileID: 11500000, guid: b135f695c35f199438922318ab9539c0, type: 3}
  m_Name: mining
  m_EditorClassIdentifier: Assembly-CSharp::SkillDefinition
  skillType: 0
  displayName: Mining
  description: Gather mining resources used to make weapons and armour
  presentation: {fileID: 0}
  icon: {fileID: -1493139528197004052, guid: df4be2da5eb7ec04da437f7d8e75814c, type: 3}
  themeColor: {r: 0.21568629, g: 0.21568629, b: 0.21568629, a: 1}
  category: 0
  listSortOrder: 0
  unlocks:
  - requiredLevel: 1
    title: Stone
    description: Can mine stone deposits
    icon: {fileID: 21300000, guid: 1a2a5b7fb636e2643a41f61d0ff4f055, type: 3}
    unlockType: 2
    meleeMinorStatOption: 0
    rangedMinorStatOption: 0
    woodcuttingMinorStatOption: 0
    miningMinorStatOption: 0
    fishingMinorStatOption: 0
    enduranceMinorStatOption: 0
    magicMinorStatOption: 0
    legacySkillMinorStatOption: 0
    ability: {fileID: 0}
    presentation: {fileID: 0}
    choices: []

"@

$minersFrenzy = @"
  - requiredLevel: 5
    title: Miners Frenzy
    description: Enter a focused mining state for 20 seconds. +20% Mining Speed
      and +10% Mining Grit Chance.
    icon: {fileID: -7398982564601289243, guid: 5ccfea8603f5b2c4999727f178d4b82e, type: 3}
    unlockType: 1
    meleeMinorStatOption: 0
    rangedMinorStatOption: 0
    woodcuttingMinorStatOption: 0
    miningMinorStatOption: 0
    fishingMinorStatOption: 0
    enduranceMinorStatOption: 0
    magicMinorStatOption: 0
    legacySkillMinorStatOption: 0
    ability: {fileID: 11400000, guid: 9c4bd915cc8a4067aadae1807bed63d6, type: 2}
    presentation: {fileID: 11400000, guid: e3a5b602cac84d6f84db30449063deb0, type: 2}
    choices:
    - requiredLevel: 8
      title: Sturdy Grip
      description: +15% Mining Stamina Efficiency during Miners Frenzy.
      icon: {fileID: 3534972268811701710, guid: 7adac55ced66def4ca2ad1c0880b555c, type: 3}
      ability: {fileID: 0}
      presentation: {fileID: 0}
    - requiredLevel: 8
      title: Iron Grit
      description: +5% Mining Grit Chance during Miners Frenzy.
      icon: {fileID: 3534972268811701710, guid: 7adac55ced66def4ca2ad1c0880b555c, type: 3}
      ability: {fileID: 0}
      presentation: {fileID: 0}

"@

$runestone = @"
  - requiredLevel: 10
    title: Runestone
    description: Can mine runestone deposits
    icon: {fileID: -3267488870146170370, guid: ca56f4a66fb9cd145a055cda7e049823, type: 3}
    unlockType: 2
    meleeMinorStatOption: 0
    rangedMinorStatOption: 0
    woodcuttingMinorStatOption: 0
    miningMinorStatOption: 0
    fishingMinorStatOption: 0
    enduranceMinorStatOption: 0
    magicMinorStatOption: 0
    legacySkillMinorStatOption: 0
    ability: {fileID: 0}
    presentation: {fileID: 0}
    choices: []

"@

$sb = New-Object System.Text.StringBuilder
[void]$sb.Append($header)

foreach ($p in $passives) {
    if ($p[0] -lt 5) {
        [void]$sb.Append((Get-MinorYaml $p[0] $p[1] $p[2] $p[3]))
    }
}

[void]$sb.Append($minersFrenzy)

foreach ($p in $passives) {
    if ($p[0] -ge 5 -and $p[0] -lt 10) {
        [void]$sb.Append((Get-MinorYaml $p[0] $p[1] $p[2] $p[3]))
    }
}

[void]$sb.Append($runestone)

foreach ($p in $passives) {
    if ($p[0] -ge 10) {
        [void]$sb.Append((Get-MinorYaml $p[0] $p[1] $p[2] $p[3]))
    }
}

Set-Content -Path $MiningAssetPath -Value $sb.ToString().TrimEnd() -NoNewline -Encoding utf8
Write-Host "Applied $($passives.Count) mining minor passives to mining.asset"
