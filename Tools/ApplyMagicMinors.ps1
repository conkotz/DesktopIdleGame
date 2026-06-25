# Regenerates minor-passive unlock rows in magic.asset (keeps non-minor unlocks).
$assetPath = Join-Path $PSScriptRoot "..\Assets\3.ScriptableObjects\SkillsDefinitions\magic.asset"
$passiveIcon = "{fileID: 3534972268811701710, guid: 7adac55ced66def4ca2ad1c0880b555c, type: 3}"

$passives = @(
    @{ Level = 2; Title = "Spellcraft I"; Desc = "+5% Spell Damage"; Opt = 5 },
    @{ Level = 3; Title = "Spellcraft II"; Desc = "+5% Spell Damage"; Opt = 5 },
    @{ Level = 4; Title = "Arcane Flow I"; Desc = "+2% Cooldown Reduction"; Opt = 6 },
    @{ Level = 6; Title = "Spellcraft III"; Desc = "+5% Spell Damage"; Opt = 5 },
    @{ Level = 7; Title = "Arcane Precision I"; Desc = "+2% Crit Chance"; Opt = 7 },
    @{ Level = 8; Title = "Arcane Flow II"; Desc = "+2% Cooldown Reduction"; Opt = 6 },
    @{ Level = 9; Title = "Rune Conservation I"; Desc = "+5% Chance to not Consume a Rune"; Opt = 8 },
    @{ Level = 11; Title = "Arcane Knowledge I"; Desc = "+50 Max Mana"; Opt = 9 },
    @{ Level = 12; Title = "Spellcraft IV"; Desc = "+5% Spell Damage"; Opt = 5 },
    @{ Level = 13; Title = "Arcane Flow III"; Desc = "+2% Cooldown Reduction"; Opt = 6 },
    @{ Level = 14; Title = "Mana Flow I"; Desc = "+2 Mana Regeneration per second"; Opt = 10 },
    @{ Level = 16; Title = "Fire Mastery I"; Desc = "+5% Fire Damage"; Opt = 12 },
    @{ Level = 17; Title = "Burning Touch I"; Desc = "+10% Burn Chance"; Opt = 13 },
    @{ Level = 18; Title = "Arcane Precision II"; Desc = "+2% Crit Chance"; Opt = 7 },
    @{ Level = 19; Title = "Burning Embers I"; Desc = "Burn Tick Rate +0.25/s"; Opt = 14 },
    @{ Level = 21; Title = "Spellcraft V"; Desc = "+5% Spell Damage"; Opt = 5 },
    @{ Level = 22; Title = "Arcane Flow IV"; Desc = "+2% Cooldown Reduction"; Opt = 6 },
    @{ Level = 23; Title = "Frost Mastery I"; Desc = "+5% Ice Damage"; Opt = 15 },
    @{ Level = 24; Title = "Arcane Fury I"; Desc = "+8% Crit Multiplier"; Opt = 16 },
    @{ Level = 26; Title = "Lightning Mastery I"; Desc = "+5% Lightning Damage"; Opt = 17 },
    @{ Level = 27; Title = "Static Touch I"; Desc = "+10% Shock Chance"; Opt = 18 },
    @{ Level = 28; Title = "Inferno I"; Desc = "+4% Burn Multiplier"; Opt = 19 },
    @{ Level = 29; Title = "Storm Surge I"; Desc = "+5% Chance for Lightning Damage to be Lucky"; Opt = 20 },
    @{ Level = 31; Title = "Arcane Knowledge II"; Desc = "+50 Max Mana"; Opt = 9 },
    @{ Level = 32; Title = "Arcane Flow V"; Desc = "+2% Cooldown Reduction"; Opt = 6 },
    @{ Level = 33; Title = "Archmage's Insight I"; Desc = "+4% Spell Damage while above 70% Mana"; Opt = 21 },
    @{ Level = 34; Title = "Arcane Fury II"; Desc = "+8% Crit Multiplier"; Opt = 16 },
    @{ Level = 36; Title = "Frozen Touch I"; Desc = "+10% Chill Chance"; Opt = 23 },
    @{ Level = 37; Title = "Deep Freeze I"; Desc = "10% Chance to Apply 2 Chill Stacks"; Opt = 24 },
    @{ Level = 38; Title = "Rune Conservation II"; Desc = "+5% Chance to not Consume a Rune"; Opt = 8 },
    @{ Level = 39; Title = "Mana Flow II"; Desc = "+3 Mana Regeneration per second"; Opt = 11 },
    @{ Level = 41; Title = "Spellcraft VI"; Desc = "+5% Spell Damage"; Opt = 5 },
    @{ Level = 41; Title = "Arcane Knowledge III"; Desc = "+50 Max Mana"; Opt = 9 },
    @{ Level = 42; Title = "Arcane Flow VI"; Desc = "+2% Cooldown Reduction"; Opt = 6 },
    @{ Level = 43; Title = "Arcane Precision III"; Desc = "+2% Crit Chance"; Opt = 7 },
    @{ Level = 44; Title = "Arcane Fury III"; Desc = "+8% Crit Multiplier"; Opt = 16 },
    @{ Level = 46; Title = "Inferno II"; Desc = "+4% Burn Multiplier"; Opt = 19 },
    @{ Level = 47; Title = "Storm Surge II"; Desc = "+5% Chance for Lightning Damage to be Lucky"; Opt = 20 },
    @{ Level = 48; Title = "Burning Embers II"; Desc = "Burn Tick Rate +0.25/s"; Opt = 14 },
    @{ Level = 49; Title = "Archmage's Insight II"; Desc = "+6% Spell Damage while above 70% Mana"; Opt = 22 }
)

function New-PassiveBlock([hashtable]$p) {
    $title = $p.Title
    $desc = $p.Desc
    @"
  - requiredLevel: $($p.Level)
    title: $title
    description: $desc
    icon: $passiveIcon
    unlockType: 0
    meleeMinorStatOption: 0
    rangedMinorStatOption: 0
    woodcuttingMinorStatOption: 0
    miningMinorStatOption: 0
    fishingMinorStatOption: 0
    enduranceMinorStatOption: 0
    magicMinorStatOption: $($p.Opt)
    legacySkillMinorStatOption: 0
    ability: {fileID: 0}
    presentation: {fileID: 0}
    choices: []
"@
}

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
  m_Name: magic
  m_EditorClassIdentifier: Assembly-CSharp::SkillDefinition
  skillType: 5
  displayName: Magic
  description: Fight enemies using magic combat
  presentation: {fileID: 0}
  icon: {fileID: -8088392421741576743, guid: 40b7955c7c201214aaa90ac2a4aabdfd, type: 3}
  themeColor: {r: 0.13147244, g: 0.122855045, b: 0.5471698, a: 1}
  category: 1
  listSortOrder: 0
  unlocks:
  - requiredLevel: 1
    title: Magic Combat
    description: Can use tier 1 magic weapons
    icon: {fileID: 21300170, guid: 15b2d2faaecad4bfb9edc6585d94f509, type: 3}
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
  - requiredLevel: 1
    title: Fire Ball
    description: Hurl a ball of fire at your target (8-10 fire damage, 2s cooldown, 8 mana).
    icon: {fileID: -317914268, guid: 3676b3313adfc364e858ba0ad6ffa832, type: 3}
    unlockType: 1
    meleeMinorStatOption: 0
    rangedMinorStatOption: 0
    woodcuttingMinorStatOption: 0
    miningMinorStatOption: 0
    fishingMinorStatOption: 0
    enduranceMinorStatOption: 0
    magicMinorStatOption: 0
    legacySkillMinorStatOption: 0
    ability: {fileID: 11400000, guid: c4f54f58ef4b47928f009228926bc522, type: 2}
    presentation: {fileID: 11400000, guid: 97a52048aa814570976ed277a43e59fe, type: 2}
    choices: []
  - requiredLevel: 1
    title: Ice Shard
    description: Launch a shard of ice at your target (6-12 ice damage, 2s cooldown, 8 mana).
    icon: {fileID: 1411015114, guid: 3676b3313adfc364e858ba0ad6ffa832, type: 3}
    unlockType: 1
    meleeMinorStatOption: 0
    rangedMinorStatOption: 0
    woodcuttingMinorStatOption: 0
    miningMinorStatOption: 0
    fishingMinorStatOption: 0
    enduranceMinorStatOption: 0
    magicMinorStatOption: 0
    legacySkillMinorStatOption: 0
    ability: {fileID: 11400000, guid: fe6756bbc661438c9e8dd8a7baf998ec, type: 2}
    presentation: {fileID: 11400000, guid: 5e0c89c88d8f45b08fa81ffcef319ab2, type: 2}
    choices: []
  - requiredLevel: 1
    title: Energy Bolt
    description: Fire a bolt of energy at your target (3-15 lightning damage, 2s cooldown, 8 mana).
    icon: {fileID: -69107095, guid: 3676b3313adfc364e858ba0ad6ffa832, type: 3}
    unlockType: 1
    meleeMinorStatOption: 0
    rangedMinorStatOption: 0
    woodcuttingMinorStatOption: 0
    miningMinorStatOption: 0
    fishingMinorStatOption: 0
    enduranceMinorStatOption: 0
    magicMinorStatOption: 0
    legacySkillMinorStatOption: 0
    ability: {fileID: 11400000, guid: e16711448fbc4e8c95d39d3a577a647f, type: 2}
    presentation: {fileID: 11400000, guid: 56b8f3643b09433f9b62ea5137b98e42, type: 2}
    choices: []
"@

$passiveBlocks = ($passives | ForEach-Object { New-PassiveBlock $_ }) -join "`n"

$chainLightning = @"
  - requiredLevel: 15
    title: Chain Lightning
    description: Conjure lightning from your hands towards the closest enemy in front
      of you and then chains to additional nearby enemies.
    icon: {fileID: 3635194895896409061, guid: 40bf976827de79147b880b19bd35a1fe, type: 3}
    unlockType: 1
    meleeMinorStatOption: 0
    rangedMinorStatOption: 0
    woodcuttingMinorStatOption: 0
    miningMinorStatOption: 0
    fishingMinorStatOption: 0
    enduranceMinorStatOption: 0
    magicMinorStatOption: 0
    legacySkillMinorStatOption: 0
    ability: {fileID: 11400000, guid: 8c2ef1d27f864a649eba4dd9eb6d38fe, type: 2}
    presentation: {fileID: 11400000, guid: 947f154cd45d42f2ba66d4d0f571b4a9, type: 2}
    choices:
    - requiredLevel: 18
      title: Extended Chain
      description: Chains +1 additional time and increases chain range by +2.
      icon: {fileID: 9049550073910843714, guid: 0c95731d8c650294d858e152aba2e5d8, type: 3}
      ability: {fileID: 0}
      presentation: {fileID: 0}
    - requiredLevel: 18
      title: Shocking Arc
      description: Gains +15% chance to shock and adds +3% shock effect if applied
        by this spell.
      icon: {fileID: 9049550073910843714, guid: 0c95731d8c650294d858e152aba2e5d8, type: 3}
      ability: {fileID: 0}
      presentation: {fileID: 0}
"@

$content = $header + "`n" + $passiveBlocks + "`n" + $chainLightning + "`n"
Set-Content -Path $assetPath -Value $content -Encoding UTF8
Write-Host "Updated $assetPath with $($passives.Count) magic minor passives."
