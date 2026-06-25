# NEW SKILL ENTRY — AGENT CHECKLIST

> **Required:** Open and follow this checklist **every time** you add or edit a skill, ability, minion summon, or related HUD/tooltip/VFX work — before considering the task done.

**Location:** [`ProjectDocumentation/NEW_SKILL_ENTRY_AGENT_CHECKLIST.md`](NEW_SKILL_ENTRY_AGENT_CHECKLIST.md) (outside `Assets/` so Unity does not import it).

Use this file when adding or editing skills and abilities (ability assets, passives tied to skills, HUD buff rows for abilities, tooltips, action bar). Work top-to-bottom for a new ability; jump to the section that matches your task.

**Details panel (required for every ability + combat major passive):** middle column = **SCALING** (blue `#B0C8DD`) then **EFFECT** (paragraph gaps via `\n\n`). See **SKILL DETAILS PANEL — MIDDLE COLUMN** and **EFFECT SPACING** before shipping tooltip copy.

Do **not** use this file for map travel, save/load, quests, NPCs, or other systems — those live elsewhere.


### PROJECT DOCUMENTATION (read when relevant — outside Assets/)
- [PROJECT__RULES.md](PROJECT__RULES.md)
  - Constraints for all gameplay/UI code; §8 pre-change checklist (Find/GetComponent in Update,
      combat Instantiate/Destroy, LINQ in hot paths, material instances, scene scans).
- [SYSTEMS_MAP.md](SYSTEMS_MAP.md)
  - Architecture map, manager list, folder reference (§13).
  - §12 Known risky scripts — read BEFORE adding per-frame work, new minion types, or heavy VFX.
  - Update SYSTEMS_MAP if you add a new per-frame script, manager, or major ability subsystem.
- [MEMORY_INVESTIGATION.md](MEMORY_INVESTIGATION.md)
  - Memory Profiler workflow and session test scripts (use when validating summon spam, channels,
      or particle-heavy abilities in long combat sessions).
- [MEMORY_SNAPSHOT_REVIEW.md](MEMORY_SNAPSHOT_REVIEW.md)
  - How to review captured Memory Profiler snapshots after profiling new abilities.
- [ICON_TEXTURE_AUDIT.md](ICON_TEXTURE_AUDIT.md)
  - Ability/buff icon texture audit — keep new ability/HUD sprites ≤256px; avoid duplicating huge
      source textures for presentation.icon and debuff strip sprites.

  New ability or minion pipeline: skim PROJECT__RULES §8 + SYSTEMS_MAP §12 first, then this file.

### UNITY .META GUID RULES (CRITICAL — read before creating assets)

Every ScriptableObject needs a sibling file: `YourAsset.asset.meta`  
The `.meta` file MUST contain a line: `guid: <32 hex characters>`

**VALID** (32 chars, lowercase hex only):

```
guid: b47a7e40c0ab4719a8b7c6d5e4f30101
guid: cef0107d3357c9b4e86a08e4b6e8a0a7
```

**INVALID** (Unity YAML parser fails — breaks ALL references):

```
guid: b47a7e40c0ab4719a8b7c6d5e4f3a01     # 31 characters (one digit short)
guid: b47a7e40-c0ab-4719-a8b7-c6d5e4f3a01 # dashes not allowed
guid: 00000000000000000000000000000000     # never use placeholder zeros in .meta
```

**Symptoms when GUID is wrong:**

- Console: "cannot be extracted by the YAML Parser" on .meta
- Console: ".meta file does not have a valid GUID"
- Console: "Broken text PPtr ... guid 00000000..." in melee.asset or AbilityDatabase.asset
- Ability missing from database / skill tree shows Missing script

**PREFERRED workflow** (agents + humans):

1. Create the `.asset` in Unity (duplicate existing `Ability_*` or `Presentation_*`), OR create `.asset` only → open Unity once → let Unity generate `.meta` automatically.
2. Copy the `guid:` line from the generated `.meta` (must be exactly 32 hex chars).
3. Wire references in other YAML files using that exact guid:

```
ability: {fileID: 11400000, guid: <paste 32-char guid here>, type: 2}
presentation: {fileID: 11400000, guid: <presentation .meta guid>, type: 2}
- {fileID: 11400000, guid: <paste>, type: 2}   # AbilityDatabase.asset list entry
```

**If you MUST hand-write a `.meta` GUID:**

- Generate 32 random hex digits (0-9, a-f). Example:

```bash
python -c "import secrets; print(secrets.token_hex(16))"
```

- Verify length BEFORE committing:

```bash
python -c "g='yourguidhere'; print(len(g), len(g)==32)"
```

- Each new asset needs its OWN unique GUID (never reuse across files).

**After adding assets, scan project** (optional):

```bash
python -c "
import re, pathlib
for path in pathlib.Path('Assets').rglob('*.meta'):
    m=re.search(r'^guid: ([^\\r\\n]+)', path.read_text(errors='ignore'), re.M)
    if m and (len(m.group(1))!=32 or not re.fullmatch(r'[0-9a-fA-F]{32}', m.group(1))):
        print('BAD', len(m.group(1)), path, m.group(1))
"
```


### QUICK PICK — WHAT ARE YOU ADDING?
  A) Timed ability buff     → tag Buff, fixed duration, HUD countdown + overlay sweep
  B) Toggle ability buff    → tag Toggle Buff, on until player toggles off, HUD full overlay
  C) Minion summon          → tag Minion (or None + minionSpawnDefinition), no fixed HUD timer
                              unless swarm/timed variant (see Soulforged)
  D) Active (instant/CD)    → tag Active, usually no HUD buff row
  E) Gathering Lv15 major   → MajorPassive rows in .skill asset + GatheringPassiveTooltipText
  F) Major passive HUD only → PlayerController *HudBuffId + TryGetHudBuffTooltip branch
  G) Skill-tree row only    → SkillDefinition unlock, optional presentation, no new ability
  H) Channeled / delayed hit → tag Active; coroutine in PlayerAbilityController; VFX tracks target
                              over time (see Executioner's Descent)
  I) Mobility / teleport strike → tag Active; instant reposition + hit; does NOT use attack cycle
                              (see Shadow Strike)
  J) Enemy debuff mark      → new component on enemy + UnitOverheadUI icon slots (not AilmentController)
                              (see Shadow Strike enhancements)
  K) Ranged combat major passive → MajorPassive row, no ability; SCALING + EFFECT details panel;
                              RangedMajorPassiveTooltipText + combat partial (see K2 Seeker Arrows)
  O) Magic spell (staff/wand)  → tag Spell (UI type line = Active); fixed base damage + spell stat scaling;
                              staff requires offhand runes per cast; wand does not (see section O)

### ABILITY TAGS (AbilityDefinition.tag)
  None        — Hides category line in tooltips. Use when tag is obvious from effects.
  Active      — Standard press-to-use ability (damage, utility, short CD).
  Minion      — Spawns a minion from minionSpawnDefinition. Tooltip shows minion damage rules.
  Buff        — Timed self-buff; shows in HUD buff strip while active; Duration on ability
                tooltips (skills page / action bar). NOT duplicated on HUD buff hover.
  Toggle Buff — Player turns on/off at will; stays active until toggled off. Same HUD strip
                as Buff but use persist overlay (no numeric countdown). Tooltip: no fixed
                Duration line on action bar; HUD hover shows "Remaining: Until dismissed".

  Legacy: untagged assets with minionSpawnDefinition still show "Minion" in tooltips.

  Spell (internal classification)
  - tag = Spell on AbilityDefinition. Tooltip **type line** still shows **Active** (not "Spell") —
      ResolveAbilityCategoryTagLabel maps Spell → "Active" for UI.
  - Combat/rules path uses AbilityTag.Spell via SpellCombatRules.IsSpellAbility(def).
  - Spells do NOT use weaponDamageMultiplier, Ability Power, or weapon hit split damage.
  - Damage = fixed base (MagicStarterSpellRules / AbilityCombatPower constants) × spell scaling
      (SpellDamageScaling: Magic %, Spell %, element % from gear + buffs + skill-tree minors).
  - Crit still applies to spell hits (not shown in scaling section).
  - CDR affects spell cooldowns; attack speed does NOT affect spell cast speed.
  - See **section O** for staff rune costs, wand exemption, and tooltip lines.

### WHEN TO USE BUFF vs MINION vs TOGGLE BUFF
  Buff (timed)
  - Fixed duration (e.g. Lumber Frenzy 20s).
  - PlayerAbilityController: track _endsAt + _duration, Sync*HudBuff each activation.
  - SetHudAbilityBuff(abilityId, stacks: 1, endTime, durationSeconds, persistOverlay: false).
  - HUD: radial overlay sweeps down; icon shows ceil(remaining); tooltip body = effects only
      + BuffIconUI appends "Remaining: Xs".

  Toggle Buff
  - No expiry until player toggles off (or forced end e.g. death, zone restriction).
  - Cast handler: if active → deactivate + ClearHudAbilityBuff; else → activate +
      SetHudAbilityBuff(abilityId, 1, endTime: 0, duration: 0, persistActiveOverlay: true).
  - Action bar: IsHudAbilityBuffActive(abilityId) already lights slot while buff row exists.
  - Do NOT use tooltipBuffMinionDurationSeconds for toggle abilities (leave 0).

  Minion
  - minionSpawnDefinition + runtime prefab. Cooldown often on dismiss, not cast.
  - HUD optional: Soulforged Weapon pattern — live minion count as displayStacks;
      swarm = timed overlay; indefinite = persist overlay; single timed = summonDuration.
  - Remove duplicate "lingers for X seconds" prose — use Duration: line on ability tooltips only
      (AppendSoulforgedWeaponDurationLine / GetTooltipBuffMinionDisplayDurationSeconds).

  Active
  - Typically no SetHudAbilityBuff unless you add a separate short-lived HUD row.

### NUMERIC TOOLTIPS (single source of truth)
  Do NOT put final % or damage numbers in presentation shortDescription.

  AbilityTooltipDamagePreview.cs
  - BuildAbilityTooltipStatsSection — full "Effects:" for skills page / ability list.
  - BuildAbilityTooltipScalingSection — blue scaling line(s) between flavor and Effects.
  - TryBuildHudBuffTooltip — HUD buff strip (includeDuration: FALSE → no "Duration:" in body).
  - TryBuildActionBarCompactBody — action bar hover (short desc + effects; Duration when timed buff).

  GatheringPassiveTooltipText.cs
  - Woodcutting / Fishing Lv15 majors (skill tree + abilities panel bullets).
  - HUD ids ONLY for major passives (not ability ids):
        PlayerController.WoodcuttingFlowStateHudBuffId
        PlayerController.FishingCalmWatersMajorHudBuffId
  - Timed ability buffs (Lumber/Fishing Frenzy, etc.) use abilityId + AbilityTooltipDamagePreview.

  CombineShortDescriptionWithBody — flavor line + numeric effects block.

  Runtime must use the SAME constants as tooltips (GatheringPassiveTooltipText or shared consts
  next to Append*TooltipEffects in AbilityTooltipDamagePreview / PlayerAbilityController).

### WEAPON-SCALED DAMAGE TOOLTIPS (Active combat abilities — Power Slash, Crescent, Whirlwind, etc.)
  When AbilityDefinition.weaponDamageMultiplier > 0, show BOTH of the following (do not pick one):

- Blue scaling line (BuildAbilityTooltipScalingSection) — after flavor, before Effects:
       "Deals {weaponMult * 100}% of your weapon damage"
  - weaponMult comes from def.weaponDamageMultiplier + AbilityTooltipAdjustments.ApplySkillTreeChoices
       (e.g. Power Slash Brutal Cut +0.25 → 175% on a 1.5 base asset).
  - Do NOT suppress this line when adding combined damage totals in Effects.
  - Do NOT put this % in presentation shortDescription.

- Effects block — full hit TOTALS, not "+ bonus over auto attack":
  - Use UsesCombinedTotalHitDamageTooltip(def) OR the same pattern in a dedicated branch:
         ComputeAverageAbilityHitSplit(def, stats, weaponMult, allM, out phys, out mag, out corr)
         AppendAbilityTotalHitDamageEffects(body, O, phys, mag, corr, " on hit")
       → e.g. "47 Physical damage on hit" (includes weapon × mult + ailment/element lines + AP).
  - Do NOT use AppendWeaponScaledHitScalerEffects (+20 Physical only) for these abilities.
  - Do NOT add a separate "+X damage from Ability Power" line when totals already include AP
       (ComputeAverageAbilityHitSplit applies GetAbilityPowerDamageMultiplier).

  Special cases:
  - Crescent Slash Elemental Crescent (choice 0): after ComputeAverageAbilityHitSplit, move 50% of
      phys total to magic on the tooltip (matches runtime conversion preview).
  - Final Severance Thousand Cuts: AppendPerHitDamageEffectLines with fraction × hit count.
  - Executioner's Descent: main hit totals + separate shockwave total line (second ComputeAverageAbilityHitSplit
      with shockwave weapon mult constant).
  - Whirlwind Twin Cyclone: totals for first wave + orange line for second-wave % (do not double-count in total).
  - Rend / Envenom / Cleaving Strikes / minions: weaponDamageMultiplier = 0 on asset — no blue % line;
      use their dedicated Effects branches.

  New ability with weapon scaling:
  - Set weaponDamageMultiplier on AbilityDefinition asset.
  - If UsesCombinedTotalHitDamageTooltip would return true (weapon mult > 0, not rend/envenom/minion/buff-only),
      default else-branch in BuildAbilityTooltipStatsSection already applies — only add a custom branch if you
      need conversion, multi-hit, or extra effect lines beyond the standard total.
  - AbilityTooltipAdjustments.ApplySkillTreeChoices: mirror runtime mult/CD tweaks used in combat.

  Skills page — right panel tooltip position (AbilityEntryUI + SkillsAbilityPageUI):
  - SetTooltipDocking(abilityPanelRect, FlipInsideBounds.PreferredSide.Left) on each row.
  - OnPointerEnter: measure/height = full row RectTransform (transform), NOT the icon — tooltip docks
      beside the list row instead of overlapping the ability name.
  - FlipInsideBounds may still flip if there is not enough space inside bounds.

### HUD BUFF STRIP (BuffsDebuffsPanel + BuffIconUI + PlayerBuffController)
  Register: PlayerBuffController.SetHudAbilityBuff(abilityId, displayStacks, endTime, duration,
            persistActiveOverlay = false).

  abilityId MUST match AbilityDefinition.abilityId exactly.

  Tooltip on hover:
  - Title/body from TryBuildHudBuffTooltip (or GatheringPassiveTooltipText for major ids).
  - Body has effect lines only — NO "Duration: Xs" (stripped via includeDuration: false).
  - BuffIconUI appends "Remaining: Xs" OR "Remaining: Until dismissed" (persist overlay).

  Icon visuals:
  - Timed: activeOverlay fillAmount = remaining/total; timerText = ceil(remaining).
  - Toggle / indefinite: persistActiveOverlay → full overlay, no icon timer.
  - Stacks: pass displayStacks > 1 to show corner count (Cleaving Strikes charges, Soulforged swarm count).

  Clear: ClearHudAbilityBuff(abilityId) when effect ends. Hide HUD row while on cooldown if your
  ability uses that pattern (see SyncFishingFrenzyHudBuff).

### TOOLTIP TEXT SCALE
- HUD buff strip tooltips: default scale (useHudTooltipScale: true).
- Skills ability list + skill tree: ShowTextAt(..., useHudTooltipScale: false) in AbilityEntryUI,
    SkillTreeViewUI — matches action bar size (avoids 0.8× docked HUD scale).

### LEAGUE ABILITY TOOLTIP LAYOUT (skills page — DO NOT REORDER PER ABILITY)
  Assembled by AbilityTooltipDamagePreview.AssembleLeagueStyleAbilityTooltipBody via AbilityEntryUI.

  Order (top → bottom):
  - Tag line (Active / Minion / Buff) — BuildAbilityTooltipTagLine
  - Required weapon — BuildWeaponRequirementRichLine (green when met, red when not)
  - Flavor description — presentation shortDescription only (no numbers, no range)
  - Blue scaling block — BuildAbilityTooltipScalingSection
         For weapon-scaled actives: "Deals X% of your weapon damage" (see WEAPON-SCALED DAMAGE TOOLTIPS).
  - Effects block — BuildAbilityTooltipStatsSection
         Starts with "Effects:" then orange numeric bullets, Duration, Energy • Cooldown.
         Weapon-scaled actives: full hit totals (e.g. "47 Physical damage on hit"), not "+ bonus" lines.
         Also append short gameplay effect lines for the committed enhancement (no choice name).
  - Active Enhancement — green footer via FormatActiveEnhancementLine (name + full choice description).
         Keep this footer for ALL abilities with a committed choice; Effects may summarize the same upgrade.

  Presentation assets (shortDescription):
  - Flavor only. No "within X units", hit caps, % damage, or enhancement names.
  - Put range, target count, channel time, and damage in AbilityTooltipDamagePreview.

  Action bar / HUD compact hover:
  - CombineShortDescriptionScalingAndEffects = intro + blue scaling + extracted effects.
  - TryBuildActionBarCompactBody / TryBuildHudBuffTooltip already use this.

  Enhancement choice styling:
  - Effects (orange): short effect-only lines from AbilityTooltipDamagePreview (no enhancement name).
  - Footer (green): "Active Enhancement: Title (full description)" — always show when a choice is committed.
  - Skill tree choice rows: SkillTreeViewUI uses same green for selected enhancement.

### SKILL DETAILS PANEL — MIDDLE COLUMN (SkillNodeDetailsPanelUI)
  The details panel uses **two separate blocks** in the middle column for abilities AND combat major
  passives: **SCALING** (blue) then **EFFECT** (white/cream). Do not put scaling % inside EFFECT.

  #### Abilities (AbilityDefinition on row)
  Wiring:
  - BindMiddleColumn → BuildAbilityTooltipScalingSection (blue SCALING block).
  - BindMiddleColumn → BuildAbilityTooltipEffectsSection(def, skillsManager, committedChoiceIndex).
        Pass enhancementChoiceOverride only from skillsManager.GetSkillChoiceSelection (committed row).
        Do NOT pass preview index when the player clicks a different choice card.
  - ExtractEffectLinesFromStatsSection strips "Effects:" header and Energy/Cooldown footer for the panel.
        **Must preserve paragraph gaps** (`\n\n` between logical effect groups) — see EFFECT SPACING below.

  When adding a new ability with enhancement-dependent effect lines:
  1) Implement effect lines in BuildAbilityTooltipStatsSection (or dedicated Append* helper).
  2) Read committed choice via skillsManager + AbilityCombatPower.*EnhancementParentSpineNodeId
        (or enhancementChoiceOverride when passed from the details panel).
  3) Mirror the same constants in runtime (PlayerAbilityController) — tooltip and cast must match.
  4) Right-column enhancement cards: preview description only in enhancementDetailText.
        Middle EFFECT column updates after SELECT/CHANGE ENHANCEMENT commits (Show → BindMiddleColumn).

  Zero-damage fallback: AppendElementAwareDamageLines uses "+0 damage" when totals are 0
  (no arrows equipped, missing weapon, etc.) — never "Base hit damage" in the details panel.

  #### Combat major passives (no ability asset — e.g. Seeker Arrows)
  Wiring:
  - SkillTreeNodeTooltipFormatter.DetailsContent: separate ScalingText + EffectText fields.
  - RangedMajorPassiveTooltipText.TryBuildDetailsPanelSections(spineId, committedChoice, stats,
        out scalingText, out effectText) — or MeleeMajorPassiveTooltipText for melee majors.
  - BindMiddleColumn(ability: null, majorPassiveScalingText, majorPassiveEffectText):
        shows SCALING section + EFFECT section (same layout as abilities).
  - ResolveMajorPassiveFlavorDescription: check **skill type first** (Ranged before Melee) when spine
        ids collide (e.g. both use Lv10_0 at different skill trees).

  Scaling line color:
  - Abilities: `<color=#B0C8DD>` via BuildAbilityTooltipScalingSection.
  - Major passives: AbilityTooltipDamagePreview.WrapDetailsScalingAccentLine(line) — same blue hex.
        Do NOT rely on TMP label.color alone (ApplyEffectBodyTextStyle resets to body white).

  Major passive scaling copy pattern:
  - "Deals {fraction * 100}% of your weapon damage" for weapon-scaled passives (no Ability Power line
        unless the passive explicitly scales with AP — Seeker Arrows does NOT).

  Major passive effect copy pattern (RangedMajorPassiveTooltipText.Build*EffectBody):
  - Computed damage line (from stats.MinSplitDamage / MaxSplitDamage × fraction).
  - Proc / mechanical lines (proc chance, chain rules, etc.).
  - **Enhancement stat merge:** fold numeric enhancement bonuses into the parent stat line when they
        modify the same stat (e.g. +50% → +100% seeker proc). Separate paragraphs only for new mechanics.
  - **Committed enhancement line** (append when selectedChoice >= 0) for mechanics that are not a stat
        bump on an existing line — not on preview click unless previewing that choice card.
  - Use DetailsEffectParagraphGap (`\n\n`) between each logical paragraph — see EFFECT SPACING.

  Skill-tree list / hover tooltips: TryBuildSkillTreeBody may combine scaling + effect for compact display;
  details panel always splits them into SCALING + EFFECT sections.

### EFFECT SPACING — DETAILS PANEL (abilities + major passives)
  TMP paragraph spacing (EffectBodyParagraphSpacing = 14f) only applies between **paragraphs**
  separated by `\n\n`. Single `\n` = tight lines within one group.

  Helper (AbilityTooltipDamagePreview):
  - DetailsEffectParagraphGap = "\n\n"
  - AppendDetailsEffectParagraph(body, line) — inserts gap before each new logical block.

  Rules when writing Append*TooltipHitDamage / major passive effect bodies:
  1) **Group related lines with single `\n` only** (no blank line between them).
  2) **Separate logical blocks with `\n\n`** (or AppendDetailsEffectParagraph).

  Required patterns:
  - **Snipe:** no-charge + full-charge damage = ONE group (single `\n` between lines, no paragraph gap).
        Wording: "{N} Physical damage at no charge" / "{N} Physical damage at full charge".
        Then `\n\n` before "Charge time: Xs".
        Then `\n\n` before committed enhancement line (e.g. bleed).
  - **Triple Shot:** arrow damage lines = ONE group.
        Then `\n\n` before "Fires N arrows (...)" line.
  - **Penetrating Shot:** damage range lines = ONE group.
        Then `\n\n` before hit-cap line, range line, and "Consumes 1 arrow on cast."
  - **Magic spells (staff):** damage range line(s) = ONE group.
        Then `\n\n` before "Spell hit range: X".
        Then `\n\n` before "Consumes N rune(s) per cast" **only when a staff is equipped**
        (wand players never see rune cost — SpellRuneCombatRules.StaffRequiresSpellRunes).
  - **Seeker Arrows (major passive):** separate paragraphs for:
        damage per hit | proc chance | committed enhancement (each `\n\n` apart).
        Scaling stays in SCALING section — never duplicate in EFFECT.

  Pipeline integrity:
  - BuildAbilityTooltipStatsSection / Append* helpers must emit `\n\n` between groups.
  - ExtractEffectLinesFromStatsSection must **preserve** blank lines as paragraph gaps (do NOT
        join all lines with single `\n` — that strips spacing for the details panel).
  - SkillNodeDetailsPanelUI.ApplyEffectBodyTextStyle sets paragraphSpacing on effectText label.

  New ability checklist (spacing):
  - [ ] Damage sub-lines that belong together use one StringBuilder group, then append to body once.
  - [ ] Charge time / duration / enhancement / secondary mechanics each start a new paragraph.
  - [ ] Verify in play mode: open skill details panel, confirm visible gaps match Snipe reference.
## A) NEW ABILITY (full pipeline — repeat in order)

1) Gameplay asset
- Duplicate Ability_*.asset in Assets/3.ScriptableObjects/AbilitiesDefinitions/
     (preferred: duplicate in Unity so .meta GUID is valid — see UNITY .META GUID RULES).
- abilityId (stable string), displayName, sourceSkill, unlockLevel.
- tag: None | Active | Minion | Buff | Toggle Buff (see sections above).
- cooldown, energyCost, requiredWeaponType, scaling multipliers.
- **Spell abilities:** tag = Spell; leave weaponDamageMultiplier = 0; set supportRunesConsumedPerCast
     and requiredChargedRuneElement on AbilityDefinition (see section O). Do NOT put % scaling on the asset.
- Buff (timed): tooltipBuffMinionDurationSeconds = BASE seconds before skill-tree bonuses.
     Leave 0 to use code fallback for that ability id.
- Toggle Buff: leave tooltipBuffMinionDurationSeconds at 0.
- Minion: assign minionSpawnDefinition, wire runtime prefab on MinionDefinition.

2) Presentation asset
- Duplicate Presentation_ability_<similar> → Presentation_ability_<newId> (+ .meta from Unity).
- Confirm Presentation_ability_<newId>.asset.meta guid is exactly 32 hex characters.
- Wire ability.presentation using the presentation asset's .meta guid (not the ability guid).
- icon, displayNameOverride (optional), shortDescription = FLAVOR ONE-LINER ONLY.
- Optional: primaryDescriptionOverride, flavourText, tooltipCategoryTagOverride.
- Do NOT duplicate numeric effect lines in presentation assets.

3) Tooltip / effects (CODE)
- AbilityTooltipDamagePreview.BuildAbilityTooltipStatsSection:
       if (IsYourAbility(def)) { AppendYourEffects(...); Duration if timed buff/minion; return; }
- **Details panel spacing:** use AppendDetailsEffectParagraph / `\n\n` between logical EFFECT blocks;
     group related sub-lines with single `\n` only (Snipe: both damage lines together). See EFFECT SPACING.
- ExtractEffectLinesFromStatsSection must preserve `\n\n` paragraph gaps for the details panel.
- REQUIRED for weapon-scaled actives (weaponDamageMultiplier > 0):
    - Blue scaling: BuildAbilityTooltipScalingSection → "Deals X% of your weapon damage"
         (include AbilityTooltipAdjustments for enhancement mults, e.g. Power Slash +25%).
    - Effects: ComputeAverageAbilityHitSplit + AppendAbilityTotalHitDamageEffects (combined total per type).
         See WEAPON-SCALED DAMAGE TOOLTIPS — never only AppendWeaponScaledHitScalerEffects (+bonus lines).
- AbilityDefinition: single weaponDamageMultiplier scales Physical/Magic/Corruption equally; leave at 0 for Rend/Envenom/minions
     on the asset (or minion inherit rules). Do not duplicate the % scaling line inside Effects.
- IsYourAbility: string.Equals(def.abilityId, "your_id", OrdinalIgnoreCase).
- Duration line: GetTooltipBuffMinionDisplayDurationSeconds(def, codeBase, additiveBonus).
- AbilityCombatPower.YourAbilityId for cross-script compares.
- AbilityEntryUI.BuildActiveEnhancementLineForAbility: add branch for enhancement at bottom (green).
- Compact UI auto-wires after step 3 (TryBuildHudBuffTooltip / TryBuildActionBarCompactBody).
- **Spell abilities:** use AppendMagicStarterSpellTooltipEffects / AppendChainLightningTooltipEffects;
     AppendSpellElementTooltipScaling (not weapon-% line); AppendSpellHitRangeEffectLine + rune cost
     when staff equipped. See section O.

4) Runtime (PlayerAbilityController unless gathering-only)
- Cast / toggle / minion spawn logic.
- **Spell abilities:** partial classes MagicStarterSpells.cs / ChainLightning.cs; gate + consume runes
     before cast when staff equipped (HasRequiredSpellRunesForAbility / TryConsumeSpellRunesForAbility).
     Wands skip rune logic entirely. See section O.
- Timed buff: _active flag, _endsAt, _duration, Cleanup*IfExpired, Sync*HudBuff.
- Toggle buff: toggle on/off, SetHudAbilityBuff(..., persistActiveOverlay: true) while on.
- Minion: spawn list, dismiss cooldown, SyncSoulforgedWeaponHudBuff as reference for HUD timing.
- Use tooltipBuffMinionDurationSeconds when > 0, then add same enhancement seconds as tooltip.

4b) World VFX (PlayerAbilityVfxController on Player prefab)
- Add [Header("Your Ability (...) VFX")] + [SerializeField] fields on PlayerAbilityVfxController.cs.
- Implement spawn/update/cleanup methods; call from PlayerAbilityController (not inline on controller).
- REQUIRED: register every new serialized field in PlayerAbilityVfxControllerEditor.cs:
    - Add a private static readonly string[] YourAbilityVfxFieldNames = { "fieldName", ... };
    - DrawFoldoutPropertyBlock(..., "YourAbilityVfx", "Your Ability (...) VFX", YourAbilityVfxFieldNames);
     Without the editor foldout, fields exist in code but do NOT appear on the Player prefab inspector.
- Assign sprites/colors/timing on Player → Player Ability Vfx Controller after compile.
- Channel / fullscreen tint: GameplayScreenOverlay (see Final Severance) — separate from VfxController.

5) Skill tree / unlocks
- SkillDefinition / SkillUnlockDefinition: wire AbilityDefinition on correct level row.
- Lv5 abilities: often two enhancement choices at Lv8 (pattern: Lumber/Fishing Frenzy).
- Register ability in Resources/Databases/AbilityDatabase.asset:
    - {fileID: 11400000, guid: <Ability_*.asset.meta guid — 32 hex>, type: 2}
     Same guid string as in melee.asset (or relevant .skill) ability: {fileID: ...} field.
- Enhancement choices: parent spine id MUST match SkillTreeViewUI.SpineNodeId(row):
       Lv{requiredLevel}_{slotAtLevel}  e.g. Lv25_0, Lv45_1
     Add AbilityCombatPower.*EnhancementParentSpineNodeId constant; read via:
       skillsManager.GetSkillChoiceSelection(SkillType.Melee, parentSpineId, -1)
     Legacy int level keys (e.g. "5", "45") still work as fallbacks on some older rows — prefer spine id.
- Choice rows in .skill asset: requiredLevel on each choice (often ability level + 3, e.g. 28 for Lv25 ability).
- Wire choice title/description in melee.asset (or relevant .skill); ability/presentation on choice stay {fileID: 0}
     unless the choice itself unlocks a different ability.

6) UI wiring
- ActionBarSlotUI → TryBuildActionBarCompactBody.
- BuffsDebuffsPanel → TryBuildHudBuffTooltip (+ optional custom icon in panel inspector).
- AbilityEntryUI → BuildLeagueStyleTooltip.
- SkillsAbilityPageUI → major passive bullets via GatheringPassiveTooltipText.
- **Skill details panel:** SkillTreeNodeTooltipFormatter + SkillNodeDetailsPanelUI.BindMiddleColumn.
     Abilities: scaling via BuildAbilityTooltipScalingSection; effects via BuildAbilityTooltipEffectsSection
     with committed choice only. Major passives: TryBuildDetailsPanelSections → separate ScalingText + EffectText.
     Apply EFFECT SPACING rules (AppendDetailsEffectParagraph, `\n\n` between logical blocks).

7) IDs / constants
- AbilityCombatPower.*AbilityId + gameplay numbers (cooldown fractions, radii, durations, mults).
- AbilityCombatPower.*EnhancementParentSpineNodeId when the row has Lv{N}_{slot} choices.
- PlayerAbilityController: private const string YourAbilityId = "your_id" (mirror AbilityCombatPower).
- Gathering: PlayerController.*ChoiceSpineId, *MajorPassiveSourceLevel, etc.
- Unity GUIDs: see UNITY .META GUID RULES at top of this file — every new .asset + .meta pair;
     every cross-reference in .skill / AbilityDatabase must use the exact 32-char guid from .meta.

8) Smoke test
- Skills page: tag → flavor → blue "Deals X% weapon damage" → Effects (full damage totals) →
     green Active Enhancement → Required weapon. Hover row: tooltip beside row, not on ability name.
- **Skill details panel:** SCALING section visible (blue) + EFFECT section with paragraph spacing;
     enhancement line appears in EFFECT only after commit (not on preview click).
- Action bar hover: short desc + scaling + numeric effects (timed buffs show Duration here).
- HUD buff strip: overlay + timer OR persist overlay; hover = effects + Remaining only.
- Cast/toggle from bar; values match tooltip.
- Player prefab: VFX foldout visible; sprites/materials assigned; cast shows world VFX in play mode.
## B) GATHERING SKILL — Lv15 MAJOR PASSIVE (three rows at same level)

- fishing.asset / woodcutting.asset: three unlockType MajorPassive at Lv15; two choices each at Lv18.
- GatheringPassiveTooltipText: constants + TryBuildSkillTreeMajorPassiveBody + Append*EffectLines.
- PlayerController: GetSkillAbilityRowPick + *Level15ChoiceSpineId; gameplay in DoOneGatherTick / TickGather.
- SkillsAbilityPageUI + SkillTreeViewUI: dynamic copy via BuildEffectiveGatheringMajorPassiveDescription.
- Optional HUD: new *HudBuffId in PlayerController, Sync*HudBuff, panel icon slot,
    GatheringPassiveTooltipText.TryGetHudBuffTooltip branch (stack-based majors e.g. Calm Waters).
## C) GATHERING — Lv5 ABILITY BUFF (e.g. Lumber / Fishing Frenzy)

- tag = Buff on AbilityDefinition asset.
- Mirror existing frenzy: PlayerAbilityController Activate/Cleanup/Sync*HudBuff.
- Append*FrenzyEffectLines in AbilityTooltipDamagePreview + shared constants in GatheringPassiveTooltipText.
- Enhancement choices at Lv8 in .skill asset; read choice in tooltip + runtime.
## D) TOGGLE BUFF ABILITY (new tag)

  Asset:
  - tag = Toggle Buff.
  - tooltipBuffMinionDurationSeconds = 0.

  Runtime (PlayerAbilityController):
  - On cast while OFF: apply effect, SetHudAbilityBuff(id, 1, 0f, 0f, persistActiveOverlay: true).
  - On cast while ON: remove effect, ClearHudAbilityBuff(id).
  - No Cleanup*IfExpired unless external force-end (death, zone, weapon swap).
  - Cooldown: only if design requires (often 0 or GCD on toggle).

  Tooltips:
  - Action bar: effects via TryBuildActionBarCompactBody (no Duration line — tag Toggle Buff).
  - HUD hover: "Remaining: Until dismissed" from BuffIconUI (not in body text).

  UI:
  - Buff strip shows icon with full active overlay while on.
  - Action bar treats as active while IsHudAbilityBuffActive(id).
## E) MINION ABILITY (Soulforged Weapon, Soulforged Warrior, Hawk, War Banner allies, etc.)

  Asset:
  - tag Minion (or None + minionSpawnDefinition — legacy still shows "Minion" in tooltips).
  - minionSpawnDefinition: runtimePrefab REQUIRED; combatConfig (inherit player weapon vs fixed minion damage).
  - weaponDamageMultiplier on ability asset = 0 for minion path (damage from MinionDefinition / config).
  - Ability-specific visuals: SoulforgedWeaponMinionPresentation on PlayerAbilityController when one
      MinionDefinition is shared across variants (swarm / indefinite / timed).

  MinionDefinition + prefab:
  - Assets under Minions/; prefab needs MinionCombatTarget + MinionCombatController (or Soulforged*Minion).
  - MinionCombatTarget registers in ActiveTargets list — used by EnemyAggro, calm-map provocation, War Banner buffs.
  - CharacterStats on minion root for HP/damage; do NOT use FindObjectOfType<CharacterStats> for player gear.

  Runtime (PlayerAbilityController):
  - TrySpawnMinionForAbility / ability-specific spawn lists (_activeSoulforgedWeaponMinions, etc.).
  - Dismiss-on-recast or stack cap per design; cooldown often on dismiss (Soulforged pattern), not on cast.
  - Bleed/Poison from minion hits: BleedPayload/PoisonPayload outgoingAttributeToMinion = true +
      outgoingDpsSourceLabel = ability display name → DpsDamageBucket.Minion (see section L).
  - Minion deaths: notify owner for HUD sync (SyncSoulforgedWeaponHudBuff pattern).

  Action bar — minion control (ActionBarMinionControlUI):
  - Aggressive / Assist / Passive stances; stored per save; ActionBarUI.RefreshMinionControlBar on loadout swap.
  - Conditional auto-battle gear swaps must call AlignCombatLoadoutToWeaponSet (see LoadoutSetButtonBinder).

  Aggro / calm maps:
  - Minion attacks call LevelAggroState.TriggerAggression(def, attacker) — can latch calm-until-provoked maps.
  - EnemyAggro.GetMinionCombatTargetFrom(attacker) for retaliation targeting.

  Tooltips:
  - No "This minion lingers for X seconds" prose — use Duration: line only (AppendSoulforgedWeaponDurationLine).
  - Skill choice variants (swarm / indefinite / default timed): mirror in HUD + tooltip duration helpers.
  - Minion damage rules in AbilityTooltipDamagePreview dedicated branches (not weapon-scaled % line).

  HUD:
  - SetHudAbilityBuff: swarm/timed → endTime + duration; indefinite → persistActiveOverlay.
  - displayStacks for live minion count (Soulforged swarm). ClearHudAbilityBuff on dismiss/expiry.

  Overhead UI:
  - UnitOverheadUI on minion prefab (HP bar); status popups use DamagePopupSystem.ResolveStatusStackAnchor(minion).
  - Custom debuff marks on enemies are NOT ailments — use component + UnitOverheadUI icon slots (Shadow Strike pattern).

  Smoke test (minions):
  - Spawn at cap; dismiss; respawn; stance buttons still work after gear set swap.
  - DPS window attributes minion damage to ability name, not Auto Attack.
  - Calm map: minion hit provokes map aggro when design expects it.

  Reference scripts:
    MinionDefinition.cs, MinionCombatController.cs, MinionCombatTarget.cs, MinionUnit.cs
    SoulforgedWeaponMinion.cs, SoulforgedWarriorMinion.cs, SoulforgedWeaponMinionPresentation.cs
    ActionBarMinionControlUI.cs, EnemyAggro.cs, LevelAggroState.cs
## F) SKILL-TREE ROW ONLY (no new ability asset)

- Edit .skill asset: level, unlockType, spine/choice ids, presentation on row.
- Passive stat: CharacterStats / PlayerController hook.
- Tooltip: SkillTreeViewUI existing paths or GatheringPassiveTooltipText for majors.
## G) ENHANCEMENT CHOICES (spine-keyed — Executioner's Descent, Shadow Strike, etc.)

  Skill tree layout:
  - Each ability row at level L with slot S → spine id "Lv{L}_{S}" (see SkillTreeViewUI.SpineNodeId).
  - Multiple abilities on the SAME level use different slots: Lv45_0 (Final Severance), Lv45_1 (Executioner's Descent).
  - Woodcutting Lv25: Cleaving Chop = Lv25_0, Spectral Axe = Lv25_1 (same level, different spines — do NOT share one int key).

  Code checklist (all four):
  - AbilityCombatPower.YourAbilityEnhancementParentSpineNodeId = "Lv25_0" (example)
  - PlayerAbilityController: GetYourAbilitySelectedChoice() → GetSkillChoiceSelection(Melee, spineId, -1)
  - AbilityTooltipDamagePreview: GetYourAbilityBranchChoice + IsYourAbility effects branch (enhance 0 / 1)
  - AbilityEntryUI.BuildActiveEnhancementLineForAbility: branch using same spine id

  .skill asset choice copy:
  - title + description on each choice node (shown in tree + green footer when committed).
  - Effects block summarizes mechanics in orange; footer repeats choice name + full description in green.
## H) CHANNELED / DELAYED ABILITY (Executioner's Descent pattern)

  When to use:
  - Wind-up before impact; target can move; optional AoE at impact point.
  - NOT a HUD buff — use Active tag + coroutine (_yourRoutine != null blocks recast).

  Ability asset:
  - tag = Active; weaponDamageMultiplier for main hit (shockwave may use code constant mult).
  - cooldown / energyCost on asset.

  PlayerAbilityController:
  - Early-out in TryUseAbility BEFORE generic instant-hit block (with isFinalSeverance / isExecutionersDescent).
  - Resolve target at cast start; store EnemyBaseController + impact point.
  - StartCoroutine(CoYourAbility(def, initialTarget)); set _yourRoutine; StartCooldown on cast.
  - Coroutine loop: each frame re-read combat.GetPrimaryEngagedEnemy() if you want engaged-target priority;
      update impact position from tracked transform; call abilityVfx Update* each frame.
  - On impact: ApplyAbilitySplitDamageToEnemy / shared BuildWhirlwindAbilityScaledSplit + ApplyOnHitEffects.
  - Secondary AoE: loop CombatEnemyRegistry.GetLiveEnemies(), radius check from impactPoint (X distance OK for lane game).
  - Enhancement 0 example: flag if target died during channel → ReduceAbilityCooldown(def, fraction).
  - Enhancement 1 example: EnemyCombatMitigationModifiers.ApplyArmourMrShred on shockwave victims.
  - finally: abilityVfx Stop* ; _yourRoutine = null.

  Targeting (important for idle combat):
  - ResolveYourAbilityTarget(): combat.GetPrimaryEngagedEnemy() FIRST (no camera visibility gate).
  - Fallback: combat.FindClosestEnemyInAttackRange(), then FindClosestVisibleLivingEnemy().
  - Do NOT use only "closest visible" — bleeds/minions on other enemies will steal the cast.

  VFX (PlayerAbilityVfxController):
  - Begin*(target, worldPos, duration), Update*(target, worldPos, elapsed), Stop*, optional Spawn*Impact*.
  - Foreground sorting layer if ability must draw above clouds (executionersDescentSortingLayer pattern).
  - Serialized sprites (axe, ground mark, shockwave ring) assigned on Player prefab after compile.

  Constants (AbilityCombatPower + mirror in tooltips):
  - Descent/channel seconds, primary/shockwave weapon mults, shockwave radius, CD refund fraction,
      sunder duration/mult, spawn/hang heights.

  Reference: executioners_descent, CoExecutionersDescent, ResolveExecutionersDescentTarget,
    ApplyExecutionersDescentHit / ApplyExecutionersDescentShockwave.
## I) MOBILITY / TELEPORT STRIKE (Shadow Strike pattern)

  When to use:
  - Instant gap-close + single hit; must NOT advance auto-attack timer (_nextAttackTime).

  Ability asset:
  - tag = Active; weaponDamageMultiplier = 1 for 100% weapon hit (or design mult).
  - cooldown / energyCost on asset.

  PlayerAbilityController:
  - Dedicated branch in TryUseAbility (isShadowStrike) BEFORE generic target-required instant handler.
  - TryExecuteYourAbility(def): resolve target → teleport → face → combat.SetTarget → hit → marks.
  - Do NOT call TryConsumeAttackCycleForAbilityCast().
  - Targeting: forward arc like Crescent Slash — Collect*ForwardHits(reach), facing from GetCombatFacingSign(),
      lane width ≈ reach * 0.35; prefer GetPrimaryEngagedEnemy() if in arc.
  - Teleport: place player at melee edge-to-edge range (mirror combat closing distance math:
      stats.Range + combat.GetMeleeRangePadding(), collider half-widths).
  - Damage: BuildWhirlwindAbilityScaledSplit + ApplyAbilitySplitDamageToEnemy (independent crit roll per ability).
  - player.TriggerAttackAnim() for feedback only.

  Enhancement — enemy marks (not ailments):
  - Component: EnemyShadowStrikeMarks on enemy (add on first apply).
  - ApplyMark(kind, PlayerAbilityController owner, AbilityDefinition def) after hit.
  - Lethal mark: EnemyBaseController.TakeDamage — if wasCrit && damage > 0, multiply by
      marks.TryConsumeLethalCritDamageMultiplier() (+80% → 1.8× total crit damage); mark consumed only on crit.
  - Execution mark: expires at Time.time + duration; EnemyBaseController.Die() → marks.NotifyEnemyDied()
      → owner.ReduceAbilityCooldownBySeconds(def, flatSeconds).
  - Do NOT use PlayerBuffController / SetHudAbilityBuff for these — they are enemy debuffs.

  Enemy overhead debuff icons:
  - UnitOverheadUI: new SerializeField Sprite slots (user assigns in prefab).
  - RefreshDebuffIcons: if EnemyShadowStrikeMarks.HasLethalCritMark / HasExecutionMark → SpawnDebuffIcon.
  - Subscribe to marks.OnMarksChanged in UnitOverheadUI (same as ailments.OnAilmentsChanged).

  VFX: SpawnShadowStrikeBurst(worldPos) — short coroutine burst at target (see CoShadowStrikeBurst).

  Constants: forward reach, lethal crit bonus fraction, execution mark seconds, CD refund seconds.

  Reference: shadow_strike, TryExecuteShadowStrike, EnemyShadowStrikeMarks,
    ShadowStrikeEnhancementParentSpineNodeId = "Lv25_0".
## J) TRYUSEABILITY GATING (avoid wrong code path)

  Abilities that need custom logic MUST be handled before the generic block at the bottom of TryUseAbility
  (the block that requires combat.CurrentTarget and fires a single instant hit).

  Add a bool next to existing flags:
    bool isYourAbility = string.Equals(def.abilityId, YourAbilityId, ...);
  Exclude from shared checks when needed:
  - Energy spend line: add && !isYourAbility to the batch with !isExecutionersDescent if cast spends energy separately.
  - Or handle energy inside your branch and return early.

  Also exclude from/minion-only paths as appropriate:
  - Crescent / Final Severance / Executioner's Descent / Shadow Strike each return true after own StartCooldown.

  Queued melee abilities (Power Slash, Rend, Envenom, Crescent queue) are separate — do not confuse with instant casts.
## J2) HOLD-TO-CHARGE RANGED (Snipe pattern)

  Ability asset:
  - tag = Active; tooltipCategoryTagOverride = "Active • Charged" on presentation asset.
  - Hold input: ActionBarSlotUI + ActionBarUI track button held state → SetSnipeActionBarHeld.
  - Auto-battle: pass snipeAutoBattleFullCharge into TryUseAbility so charge always runs full duration.

  Runtime (partial class PlayerAbilityController.Snipe.cs):
  - TryBeginSnipeCharge → spend energy, BeginSnipeChargeAttackAnim on PlayerController, BeginSnipeChargeVfx on VFX controller.
  - TickSnipeCharge each frame; release on button up or at full charge; CancelSnipeCharge refunds energy when appropriate.
  - ExecuteSnipeFire → ReleaseSnipeChargeAttackAnim, TryFireSnipeProjectile, delayed hit resolution after travel time.
  - Enhancement bleed: force ApplyBleedFromHit when choice index matches AbilityCombatPower.SnipeGuaranteedBleedChoiceIndex.

  Animation (PlayerController):
  - If not already in range_attack state, animator.Play("range_attack", 0, 0) immediately on charge start (not SetTrigger).
  - Hold pose after N frames (SnipeChargeAnimPauseFrameDelay); resume on ReleaseSnipeChargeAttackAnim.

  VFX (PlayerAbilityVfxController + Editor Range tab):
  - Charge: BeginSnipeChargeVfx(duration) — rising white orbit ring; EndSnipeChargeVfx on cancel/fire.
  - Projectile trail: GetSnipeTrailSettings() → SnipeLingeringTrailFollower.Create in PlayerCombatController.TryFireSnipeProjectile.
  - REQUIRED: register Triple Shot + Snipe field arrays in PlayerAbilityVfxControllerEditor.cs (Range tab foldouts).

  Reference: snipe, PlayerAbilityController.Snipe.cs, SnipeLingeringTrailFollower, AbilityCombatPower.Snipe*.

  Charge tuning (AbilityCombatPower):
  - Base charge: SnipeBaseChargeDurationSeconds (3s). Full charge = SnipeMaxChargeDamageMultiplier (300% weapon).
  - Fast Charge: SnipeFasterChargeReductionSeconds (0.5s) → SnipeEnhancedChargeDurationSeconds (2.5s).
  - GetSnipeDamageMultiplierAtElapsed scales step bonus from duration so max charge is always 300% regardless of charge time.
  - SCALING line: "Deals 100%-300% of your weapon damage" + Ability Power below (BuildAbilityTooltipScalingSection IsSnipe branch).
  - Details panel EFFECT spacing: initial + full-charge damage grouped; gap before charge time; gap before enhancement.
## K2) RANGED MAJOR PASSIVE — AUTO-ATTACK PROC (Seeker Arrows pattern)

  When to use:
  - Major passive on ranged skill tree (unlockType MajorPassive, no AbilityDefinition).
  - Procs phantom hits from auto attacks (and optionally chains on seeker hit).
  - NOT an ability — no Ability Power scaling, no on-hit ailments/stun, no ammo consume.

  Skill tree (.skill asset — e.g. ranged.asset):
  - requiredLevel row (e.g. Lv10) unlockType: 3 (MajorPassive).
  - **icon:** use the shared major passive sprite — same as melee (guid d4b4e65185d715544a6aa618c0842b18,
        fileID 7940577473758156257). Enhancement choice rows keep their own icons.
  - description = flavor only (what it does in plain English — no numeric % in description if
        effects panel computes them).
  - Two enhancement choices at Lv13 (typical pattern); title + description per choice.
  - Spine id from SkillUnlockPanelTooltipBuilder.ResolveSpineNodeIdForUnlock → e.g. Lv10_0 when
        first MajorPassive at that level (sort order: MajorPassive before Unlock rows).
  - Often pair with tier-unlock row at same level (unlockType: 2, tier 2 weapons) — different slot.

  Constants (AbilityCombatPower):
  - *MajorPassiveSpineNodeId, *MajorPassiveLevel, weapon damage fraction, proc chance,
        enhancement choice indices, volley proc chance/count, volley launch duration, outgoing DPS label.
  - Helper methods for expected arrows per proc (balance Echoes vs Volley when designing).

  CharacterStats:
  - Is*MajorPassiveActive() — row pick + AreRangedMajorPassiveEffectsEnabled() (ranged weapon equipped).
  - Get*EnhancementPick(), Can*ChainOnHit(), Get*ProcChanceFraction().

  Runtime (PlayerCombatController partial — e.g. PlayerCombatController.SeekerArrows.cs):
  - Hook proc from ranged auto attack only (consumeAmmo: true path — not Triple Shot phantoms).
  - Roll proc chance; enqueue volley (queue coroutines — do not interrupt in-flight volleys).
  - **Volley launch:** fire N projectiles over SeekerArrowVolleyTotalLaunchDurationSeconds (~2s)
        with parallel hit resolution — do NOT wait for each arrow to land before launching the next.
  - **Single / chain procs:** wait travel time then apply damage; chain rolls proc on hit if enhancement 0.
  - ApplySeekerArrowDamage: RollSplitAttackDamage × weapon fraction only — no AP mult.
  - Use dedicated ApplySplitDamageToTarget with outgoing source label — NOT full ResolveAttackHitNow
        (skip ailments, stun, cleave, lifesteal, queued hit effects).
  - Spawn: mirrored fire point (flip local X behind player); straight ProjectileVisual path;
        SnipeLingeringTrailFollower with slimmer trail settings; optional ±5% vertical spawn variance.

  VFX (PlayerAbilityVfxController + Editor Range tab):
  - seekerArrowTrail* serialized fields; GetSeekerArrowTrailSettings().
  - Register SeekerArrowVfxFieldNames foldout in PlayerAbilityVfxControllerEditor.cs.

  Tooltips (RangedMajorPassiveTooltipText.cs):
  - TryBuildFlavorDescription — description column (check skill type in formatter to avoid Lv10_0 melee collision).
  - TryBuildDetailsPanelSections — scalingText + effectText for details panel.
  - Build*ScalingBody → WrapDetailsScalingAccentLine("Deals X% of your weapon damage").
  - Build*EffectBody — damage line, proc line, committed enhancement; use DetailsEffectParagraphGap.
  - **Enhancement stat merge:** fold committed enhancement bonuses into the parent stat line in EFFECT
        (e.g. Lone Ranger +50% → +100% seeker proc when Sharpshooter is committed). Do NOT add a second
        orange paragraph naming the enhancement for the same stat — reserve separate lines for genuinely
        new mechanics (Echoes chain, Volley burst, Relaxed Companion ally threshold).
  - **Multiplicative proc bonuses:** when a passive says "+X% proc chance", apply as a relative multiplier
        on the base proc (25% base × (1 + 1.0) = 50% with +100% bonus), not flat addition (+25 pp).
        Show final computed chance in a footer line when helpful (e.g. Lone Ranger:
        `(Seeker arrow proc chance: X%)`).
  - **Seeker proc line copy:** default proc text is "X% chance to proc on auto attack" only (no "seeker arrow hit" suffix).
        Echoes chains at most once from the primary seeker arrow; echo / volley arrows do not chain again.
  - TryBuildChoiceTooltipBody — enhancement card / detail text.
  - Wire in SkillTreeNodeTooltipFormatter + SkillUnlockPanelTooltipBuilder + SkillNodeDetailsPanelUI
        ResolveEnhancementDetailEffectText.

  Balance notes (Seeker Arrows reference):
  - Base proc 25% on auto attack only; Echoes adds chain proc on seeker hit.
  - Echoes expected arrows per proc ≈ 1 / (1 − procChance).
  - Volley: (1 − volleyProc) × 1 + volleyProc × count — tune volleyProc so both enhancements are
        roughly even over long fights (10% × 5 ≈ competitive with geometric Echoes chain at 25%).

  Smoke test:
  - Details panel: DESCRIPTION = flavor; SCALING = blue %; EFFECT = spaced paragraphs + enhancement after commit.
        Compare visually to Snipe / Triple Shot / Seeker Arrows (see EFFECT SPACING).
  - Proc on auto; no ammo drain; no bleed/stun; DPS attributes "Seeker Arrow" not "Auto Attack".
  - Volley fires 5 arrows over ~2s; Echoes chains on hit; enhancement choice reflected in EFFECT after commit.

  Reference: seeker_arrows (conceptual), PlayerCombatController.SeekerArrows.cs,
    RangedMajorPassiveTooltipText.cs, AbilityCombatPower.SeekerArrow*.

## K2b) RANGED MAJOR PASSIVE — LONE RANGER (conditional ally proc bonus)

  When to use:
  - Major passive gated on ally presence in the same play area (minions + companions count as allies).
  - Multiplicative proc bonus on an existing proc (Seeker Arrows base chance × (1 + bonus)).

  Ally detection (LoneRangerAllyPresence):
  - Count player-owned minions / companions in the same play area — NO Update polling.
  - Register active companions on minion Initialize (HawkCompanionMinion, SoulforgedWeaponMinion,
        MinionCombatTarget) and unregister OnDestroy.
  - Refresh ally count from combat events when Lone Ranger bonus is evaluated (auto attack proc, tooltip bind).

  Tooltips:
  - Merge Sharpshooter into the seeker proc stat line (+50% → +100%); no separate Sharpshooter paragraph.
  - Footer: `(Seeker arrow proc chance: X%)` using GetSeekerArrowProcChanceFraction (live computed value).
  - Show "Currently active." / "Currently inactive." when the passive is unlocked.

  Constants (AbilityCombatPower):
  - LoneRangerMajorPassiveSpineNodeId, LoneRangerRangedDamageBonusFraction,
        GetLoneRangerSeekerProcRelativeBonusFraction, ApplyLoneRangerSeekerProcMultiplier.

  Skill tree (.skill asset):
  - **icon:** shared major passive sprite (guid d4b4e65185d715544a6aa618c0842b18) — same as Seeker Arrows.
  - Enhancement Relaxed Companion: ally threshold 0 → 1; Sharpshooter: +50% relative proc (100% total bonus).

  Smoke test:
  - Hawk Companion (or any minion) in same area → Lone Ranger inactive.
  - Solo in area → +10% ranged damage; seeker proc 25% → 37.5% (+50%) or 50% (+100% with Sharpshooter).
## K3) COOLDOWN HELPERS

  ReduceAbilityCooldown(def, reductionFraction) — multiplies remaining CD (Executioner's Claim 50%).
  ReduceAbilityCooldownBySeconds(def, seconds) — flat shave (Shadow Execution −3s).

  Both read/write _cooldownEndsById[def.abilityId]. Call only when enhancement condition met.
## O) MAGIC SPELLS (starter spells, Chain Lightning — staff runes + wand exemption)

  When to use:
  - Magic skill Active abilities with **fixed base elemental damage** scaled by spell stats (not weapon hit).
  - Player wields **wand** (Focus offhand) OR **staff** (Runes offhand).
  - Examples: fire_ball, ice_shard, energy_bolt, chain_lightning.

  Ability asset (AbilityDefinition):
  - tag = **Spell** (tooltip type line still shows **Active**).
  - requiredWeaponType = Magic.
  - weaponDamageMultiplier = 0 (spells never use weapon-scaled % line or AP).
  - requireRangeCheckToActivate = Yes for targeted spells.
  - **Spell rune consumption (staff only at runtime):**
    - supportRunesConsumedPerCast — runes removed from offhand stack per cast (0 = no rune cost on asset).
    - requiredChargedRuneElement — Fire | Ice | Lightning | None.
    - **Elemental runes** (ChargedRuneElement.Elemental) satisfy **any** spell's element requirement.
  - Reference values (starter spells):
    - fire_ball / ice_shard / energy_bolt → 1 rune/cast, element matches spell (Fire / Ice / Lightning).
    - chain_lightning → 3 runes/cast, Lightning (or 3 elemental runes).

  Staff vs wand (CRITICAL):
  - **Wand** (MainHandWeaponArchetype.Wand + Focus offhand): spells cast **without** rune cost.
        HasRequiredSpellRunesForAbility / TryConsumeSpellRunesForAbility no-op when main hand is not a staff.
  - **Staff** (MainHandWeaponArchetype.Staff + Runes offhand): spells **require** matching charged runes.
        Wrong element or insufficient stack → cast blocked with popup (like arrows for Penetrating Shot).
  - Rune **stat bonuses apply once per cast** from the equipped rune type — consuming 3 runes for
        Chain Lightning does **NOT** triple rune damage bonuses.

  Charged runes (CombatSupport items — like arrows for bows):
  - itemKind = CombatSupport; supportType = Runes; equipSlot = OffHand; consumableOnSpell = true.
  - combatSupportStats: flat elemental min/max, element % or spellDamagePercent, chargedRuneElement.
  - Consume **amount** comes from AbilityDefinition.supportRunesConsumedPerCast (not the item).
  - Assets: Assets/3.ScriptableObjects/ItemsDefinitions/CombatSupport/Runes/basic_*_rune.asset
  - Register new rune items in Resources/Databases/ItemDatabase.asset.

  Damage scaling (CODE — single source of truth):
  - MagicStarterSpellRules — base min/max per abilityId + element mapping.
  - SpellDamageScaling.ScaleElementBounds / RollScaledElementDamage — Magic %, Spell %, element %.
  - CharacterStats.TryGetEquippedRuneElementFlatBounds — adds staff rune flat damage once (staff only).
  - CharacterStats.SpellMagicDamageScalingPercentPoints — gear Magic % + buffs + magic skill-tree minors.
  - CharacterStats.SpellDamageTotalScalingPercentPoints — gear Spell % + SupportSpellDamagePercent from runes.
  - **Do NOT** add Ability Power to spell damage. **Do NOT** duplicate gear % on ability assets.

  Runtime (PlayerAbilityController):
  - Magic starter spells: PlayerAbilityController.MagicStarterSpells.cs → TryCastMagicStarterSpell.
  - Chain Lightning: PlayerAbilityController.ChainLightning.cs → TryCastChainLightning.
  - Before spending mana: combat.HasRequiredSpellRunesForAbility(def) → popup via ResolveMissingSpellRunesMessage.
  - After spending mana: combat.TryConsumeSpellRunesForAbility(def); refund mana on consume failure.
  - Spell hit range: same as auto-attack range check (combat.IsEnemyWithinAttackRange / stats.Range).
  - Chain Lightning: **initial cast** uses spell hit range; **chain jumps** use separate chain range constant.

  Tooltips (AbilityTooltipDamagePreview):
  - Effects: scaled elemental damage range (SpellDamageScaling.ScaleElementBounds).
  - AppendSpellHitRangeEffectLine(def, stats): "Spell hit range: X".
  - **If staff equipped:** next paragraph "Consumes N rune(s) per cast" (hidden for wand).
  - Scaling section: Magic %, Spell %, element % lines via AppendSpellElementTooltipScaling.
  - **Do NOT** show "per auto attack", weapon damage %, or Ability Power for spells.
  - Use AppendDetailsEffectParagraph / `\n\n` between damage, range, and rune-cost paragraphs.

  Helper scripts:
  - SpellCombatRules.cs — IsSpellAbility, GetSpellHitRange.
  - SpellRuneCombatRules.cs — staff detection, rune type match, missing-rune messages.
  - AbilityDefinition.OffhandRuneSatisfiesSpell(support) — Fire/Ice/Lightning or Elemental fallback.

  Smoke test (spells):
  - Wand + Focus: cast all spells with no runes equipped; no "Consumes runes" line on tooltip.
  - Staff + wrong rune (e.g. fire rune for Ice Shard): cast blocked with clear message.
  - Staff + elemental rune: all starter spells + Chain Lightning work (3 elemental for chain).
  - Stack decrements by supportRunesConsumedPerCast; rune stat bonus unchanged when consuming 3 vs 1.
  - Tooltip damage matches combat; Magic % on stats panel matches scaling section (includes skill-tree minors).

  Reference ability ids:
  - fire_ball, ice_shard, energy_bolt (Lv1 starters), chain_lightning (Lv15).

  Reference scripts:
  - MagicStarterSpellRules.cs, SpellDamageScaling.cs, SpellCombatRules.cs, SpellRuneCombatRules.cs
  - PlayerAbilityController.MagicStarterSpells.cs, PlayerAbilityController.ChainLightning.cs
  - PlayerCombatController.HasRequiredSpellRunesForAbility / TryConsumeSpellRunesForAbility
  - ItemDefinition.CombatSupportStats (chargedRuneElement, consumableOnSpell, elemental flat + %)
## L) DPS / OUTGOING DAMAGE ATTRIBUTION (if ability deals damage)

  Ability hits use GetAbilityOutgoingDamageSourceLabel(def.abilityId) → displayName on ability asset.
  ApplyAbilitySplitDamageToEnemy → TakeDamage → AwardCombatXp → RecordDamageForDps with Physical/Magic/Corruption buckets.

  Do NOT label ability hits as "Auto Attack". Weapon swings use SwingOutgoingAttribution / deferred DPS split.

  Minion-applied ailments: BleedPayload/PoisonPayload outgoingAttributeToMinion + outgoingDpsSourceLabel
  (e.g. Soulforged Weapon) → DpsDamageBucket.Minion, not player Ailments bucket.

  See PlayerCombatController outgoing DPS sections if adding new damage channels.
## M) PERFORMANCE — avoid stutter / GC / memory spikes (new abilities & minions)

  Read first: [SYSTEMS_MAP.md](SYSTEMS_MAP.md) §12 + PROJECT__RULES.md §8.

  PlayerAbilityController (largest risk):
  - Do NOT add unconditional per-frame work in Update — gate behind RequiresPerFrameAbilityRuntimeWork()
      or an existing _yourRoutine / channel flag (see Executioner's Descent, Whirlwind).
  - Coroutines over while-true Update loops for channels; stop routines in finally / OnDisable paths.
  - Cache enemy lists via CombatEnemyRegistry.GetLiveEnemies() — never FindObjectsByType per frame.
  - Reuse existing cooldown/damage helpers (ApplyAbilitySplitDamageToEnemy, BuildWhirlwindAbilityScaledSplit).

  VFX (PlayerAbilityVfxController):
  - Prefer sprite bursts + short coroutines over spawning many ParticleSystems per cast.
  - Stop/cleanup VFX in matching Stop* when channel ends or target dies.
  - Do not Instantiate UI or world objects every frame during channels — update positions only.

  Minions:
  - Hard-cap concurrent summons per ability; pool or reuse if spawning many short-lived objects.
  - Minion Update logic stays in MinionCombatController / Soulforged*Minion — not in PlayerAbilityController.Update.
  - Avoid per-minion Find* or GetComponent in Update (cache on spawn).

  Tooltips / UI:
  - AbilityTooltipDamagePreview runs on hover — OK to allocate; do not rebuild tooltips every frame.
  - No Instantiate of skill-tree rows or ability list entries per combat tick.

  Damage popups / status labels:
  - Use DamagePopupSystem.SpawnStatusPresentation + stack anchor (shared vertical stack per unit).
  - Floating damage uses pooling — do not Instantiate FloatingDamageTextUI per hit outside DamagePopupSystem.

  Profiling before shipping heavy abilities:
  - Follow [MEMORY_INVESTIGATION.md](MEMORY_INVESTIGATION.md) for 2 min / 20 min combat sessions.
  - F6–F12 DebugPerformanceToggles can disable overhead UI / floating text to isolate ability cost.

  After adding a hot path: note it in SYSTEMS_MAP §12 if it runs every frame or per enemy.

  Ammo / off-hand support stacks (ranged auto attacks + arrow-consuming actives):
  - NEVER call SaveManager.Save() / RequestImmediateSave on every stack decrement.
  - Use RequestSave(SaveRequestKind.InventoryChanged) (debounced) for stack-only changes.
  - Fire OnOffHandStackChanged for count-only UI (equipment slot xN label); reserve full
        OnOffHandChanged for item id swaps / empty off-hand (triggers vitals + combat power HUD).
  - Throttle expensive nearby-enemy scans (Lone Hunter, Hunter's Swiftness) — ~0.25s cache OK.
## N) COMMON MISTAKES (from recent melee abilities)

- Wrong enhancement spine id (e.g. using 25 instead of Lv25_0) → choice always -1, enhancements never apply.
- Putting numeric ranges/% in presentation shortDescription → duplicates and drifts from tooltip code.
- Removing blue "Deals X% weapon damage" when adding combined Effect totals → show BOTH scaling % and totals.
- Using AppendWeaponScaledHitScalerEffects (+bonus only) instead of AppendAbilityTotalHitDamageEffects for weapon actives.
- Duplicate "+X from Ability Power" under Effects when ComputeAverageAbilityHitSplit already includes AP.
- Tooltip anchored to ability icon on skills page → overlaps name; use full row measure in AbilityEntryUI.
- VFX fields in PlayerAbilityVfxController without Editor foldout → cannot assign on Player prefab.
- Executioner's Descent targeting only visible enemies → casts on wrong target; use GetPrimaryEngagedEnemy first.
- Shadow Strike consuming attack cycle → breaks idle swing rhythm; never call TryConsumeAttackCycleForAbilityCast.
- Lethal mark consumed on non-crit or zero damage → gate on wasCrit && finalDamage > 0 in TakeDamage.
- Using ailments for ability-specific marks → use EnemyShadowStrikeMarks + overhead icons instead.
- Invalid .meta GUID (not exactly 32 lowercase hex chars) → YAML parser errors, Broken PPtr,
    ability/database links show 00000000... — see UNITY .META GUID RULES; never hand-type 31-char guids.
- Wiring presentation with ability's guid (or vice versa) → missing presentation icon/copy.
- Forgetting AbilityDatabase.asset entry → ability works in editor direct reference but not loaded at runtime.
- Minion without MinionCombatTarget → aggro/retaliation/DPS attribution breaks.
- Minion ailment damage attributed to player Ailments bucket → set outgoingAttributeToMinion on payloads.
- Per-frame FindObjectsByType / LINQ in ability Update → stutter (see section M).
- Hand-placing status popup colours outside FloatingDamageTextUI Status Presentations section →
    overlapping labels; use DamagePopupSystem.SpawnStatusPresentation for all lingering status text.
- Putting scaling % in major passive EFFECT column → belongs in SCALING via TryBuildDetailsPanelSections
    + WrapDetailsScalingAccentLine (same #B0C8DD blue as abilities).
- Melee TryBuildFlavorDescription matching ranged Lv10_0 spine → wrong DESCRIPTION text; gate flavor
    by SkillType.Ranged vs Melee in ResolveMajorPassiveFlavorDescription.
- ExtractEffectLinesFromStatsSection joining all lines with `\n` → destroys EFFECT paragraph spacing;
    preserve blank lines as `\n\n` gaps.
- Snipe / Triple Shot details panel: single `\n` between paired damage lines; `\n\n` before charge time,
    volley info, and enhancement lines.
- Major passive enhancement shown on preview click in EFFECT column → only committed choice after
    SELECT/CHANGE ENHANCEMENT (pass committedChoiceIndex from skillsManager, not preview index).
- Putting weaponDamageMultiplier or Ability Power scaling on a Spell-tagged ability → wrong damage path.
- Showing "Consumes runes" on spell tooltip when player has a **wand** equipped → only show for staff.
- Multiplying rune stat bonuses by supportRunesConsumedPerCast → bonuses apply once per cast only.
- Forgetting supportRunesConsumedPerCast / requiredChargedRuneElement on new staff spells → free casts.
- Using AppendWeaponScaledHitScalerEffects or blue weapon-% line for spells → use SpellDamageScaling instead.
- Chain Lightning initial cast using chain range instead of spell hit range → initial target uses stats.Range.
- Registering charged rune items only in folder, not ItemDatabase.asset → item missing at runtime.

## Reference — presentation assets (Assets/3.ScriptableObjects/Presentation/)
  Presentation_ability_avatar_of_the_forest
  Presentation_ability_cleaving_chop
  Presentation_ability_cleaving_strikes
  Presentation_ability_crescent_slash
  Presentation_ability_envenom
  Presentation_ability_fishing_frenzy
  Presentation_ability_lumber_frenzy
  Presentation_ability_power_slash
  Presentation_ability_rend
  Presentation_ability_soulforged_weapon
  Presentation_ability_spectral_axe
  Presentation_ability_whirlwind
  Presentation_ability_final_severance
  Presentation_ability_executioners_descent
  Presentation_ability_shadow_strike
  Presentation_ability_battle_trance
  Presentation_ability_energy_infusion
  Presentation_ability_flame_charge
  Presentation_ability_lightning_rod
  Presentation_ability_penetrating_shot
  Presentation_ability_hunters_swiftness
  Presentation_ability_tornado
  Presentation_ability_fire_ball
  Presentation_ability_ice_shard
  Presentation_ability_energy_bolt
  Presentation_ability_chain_lightning

## Reference — ability assets (Assets/3.ScriptableObjects/AbilitiesDefinitions/)
  Melee/Ability_executioners_descent.asset   — executioners_descent (Lv45 slot 1)
  Melee/Ability_shadow_strike.asset          — shadow_strike (Lv25 slot 0)
  Melee/Ability_battle_trance.asset          — battle_trance (Lv35 slot 1, timed Buff)
  Magic/Ability_fire_ball.asset              — fire_ball (Spell, 1 fire/elemental rune with staff)
  Magic/Ability_ice_shard.asset              — ice_shard (Spell, 1 ice/elemental rune with staff)
  Magic/Ability_energy_bolt.asset            — energy_bolt (Spell, 1 lightning/elemental rune with staff)
  Magic/Ability_chain_lightning.asset        — chain_lightning (Spell, 3 lightning/elemental runes with staff)

## Reference — HUD ability buff ids (SetHudAbilityBuff abilityId)
  lumber_frenzy, fishing_frenzy, avatar_of_the_forest, cleaving_chop, cleaving_strikes,
  spectral_axe, soulforged_weapon, battle_trance, energy_infusion
  Major passives (GatheringPassiveTooltipText): woodcutting_flow_state, fishing_calm_waters_major
  Major passives (MeleeMajorPassiveTooltipText): BattleEngine_Overload, phoenix_soul_ashen_rebirth
  Major passives (RangedMajorPassiveTooltipText): Seeker Arrows Lv10_0 — no HUD buff id (combat proc only)

## Reference — enhancement parent spine ids (Ranged examples)
  Lv5_0 / Lv5_1           — Triple Shot / Snipe (abilities, not majors)
  Lv10_0                  — Seeker Arrows (first MajorPassive at Lv10 on ranged tree; choices at Lv13)
  Lv15_0                  — Lightning Rod
  Lv15_1                  — Penetrating Shot
  Lv25_0                  — Hunter's Swiftness
  Lv45_0                  — Tornado

## Reference — magic weapons & rune support
  basic_wand.asset        — Wand + Focus offhand; spells cost mana only (no runes).
  basic_staff.asset       — Staff + Runes offhand required; spells consume charged runes per cast.
  CombatSupport/Runes/basic_fire_rune.asset       — Fire, 3–4 flat, +3% fire, consumableOnSpell.
  CombatSupport/Runes/basic_ice_rune.asset        — Ice, 2–5 flat, +3% ice, consumableOnSpell.
  CombatSupport/Runes/basic_lightning_rune.asset  — Lightning, 1–6 flat, +3% lightning, consumableOnSpell.
  CombatSupport/Runes/basic_elemental_rune.asset  — +10% spell damage; satisfies any spell rune requirement.
  arcanist_merchant.asset — sells charged runes (10g + uncharged rune; elemental 20g + uncharged rune).

## Reference — enhancement parent spine ids (Melee examples)
  Lv5_0 / Lv5_1 / Lv5_2   — three Lv5 abilities (Power Slash, Rend, Envenom) — legacy int "5" still used in places
  Lv15_0 / Lv15_1 / Lv15_2 — Lv15 branch abilities (Whirlwind, Cleaving Strikes, Crescent)
  Lv25_0                  — Shadow Strike (Ability Milestone III replacement)
  Lv25_1                  — Energy Infusion
  Lv25_2                  — Flame Charge
  Lv35_0                  — Soulforged Weapon (Lv35 ability row slot 0)
  Lv35_1                  — Battle Trance (3 enhancements at Lv38)
  Lv35                    — Soulforged Weapon (legacy int key — prefer Lv35_0 for new code)
  Lv40_0                  — Phoenix Soul (Melee major passive; choices at Lv43)
  Lv40_1                  — Master of Venoms (second Lv40 major; choices at Lv43)
  Lv45_0                  — Final Severance
  Lv45_1                  — Executioner's Descent

## Reference — key scripts (skills & abilities only)
  AbilityDefinition.cs           — tag enum, tooltipBuffMinionDurationSeconds, minionSpawnDefinition,
                                   supportRunesConsumedPerCast, requiredChargedRuneElement (staff spells)
  AbilityTooltipDamagePreview.cs — tooltip builders (Effects, HUD buff, action bar)
  MagicStarterSpellRules.cs      — starter spell base damage + element per abilityId
  SpellDamageScaling.cs          — spell hit multiplier (Magic/Spell/element %; rune flat)
  SpellCombatRules.cs            — IsSpellAbility, spell hit range
  SpellRuneCombatRules.cs        — staff rune validation, missing-rune messages, elemental fallback
  PlayerAbilityController.cs     — cast, buff sync, minions (partials for large abilities)
  PlayerAbilityController.MagicStarterSpells.cs — fire_ball / ice_shard / energy_bolt cast path
  PlayerAbilityController.ChainLightning.cs     — chain_lightning cast + chain jumps
  PlayerAbilityVfxController.cs  — world VFX fields + spawn/update/cleanup
  Editor/PlayerAbilityVfxControllerEditor.cs — inspector foldouts (must list new VFX fields)
  PlayerBuffController.cs        — SetHudAbilityBuff / hudPersistActiveOverlay
  BuffIconUI.cs                  — buff icon overlay, Remaining line on hover
  BuffsDebuffsPanel.cs           — HUD buff strip tooltips
  GatheringPassiveTooltipText.cs — gathering Lv15 majors + frenzy constants
  RangedMajorPassiveTooltipText.cs — ranged combat major passives (Seeker Arrows); details SCALING + EFFECT
  MeleeMajorPassiveTooltipText.cs — melee major passives + capstones
  AbilityCombatPower.cs          — stable abilityId string constants + balance numbers
  EnemyCombatMitigationModifiers.cs — temporary armour/MR shred on enemies (Sundering Impact)
  EnemyShadowStrikeMarks.cs      — ability-specific enemy marks (crit amp / death CD refund)
  EnemyBaseController.cs         — TakeDamage (crit mark mult), Die() (mark death notify), ApplyDirectDotDamage
  PlayerCombatController.cs      — GetPrimaryEngagedEnemy, FindClosestEnemyInAttackRange (targeting)
  PlayerCombatController.SeekerArrows.cs — ranged major passive proc partial (pattern for new majors)
  SkillTreeNodeTooltipFormatter.cs — DetailsContent scaling/effect; ResolveMajorPassiveFlavorDescription
  SkillNodeDetailsPanelUI.cs     — BindMiddleColumn SCALING + EFFECT; paragraph spacing on effect label
  UnitOverheadUI.cs              — enemy/minion overhead debuff icons (ailments + custom mark sprites)
  GameplayScreenOverlay.cs       — optional fullscreen channel tint (Final Severance pattern)
  FloatingDamageTextUI.cs        — Status Presentations colours (all lingering status labels)
  DamagePopupSystem.cs           — SpawnStatusPresentation, status stack anchors, damage popup pool
  MinionDefinition.cs            — minion prefab + combat config asset
  MinionCombatController.cs      — minion combat tick
  MinionCombatTarget.cs          — active minion registry, aggro hooks
  SoulforgedWeaponMinion.cs      — reference timed/swarm minion implementation
  SoulforgedWeaponMinionPresentation.cs — per-ability minion visual overrides
  ActionBarMinionControlUI.cs    — Aggressive / Assist / Passive stance bar
  ActionBarUI.cs                 — AlignCombatLoadoutToWeaponSet, combat loadout sets
  SkillsAbilityPageNewUI.cs      — new skills page; preset ↔ weapon set sync when editing
  ConditionalAutoBattleSetController.cs — conditional gear/loadout swap (bypasses gear CD when enabled)
  [NEW_SKILL_ENTRY_AGENT_CHECKLIST.md](NEW_SKILL_ENTRY_AGENT_CHECKLIST.md) — this file
- [SYSTEMS_MAP.md](SYSTEMS_MAP.md) — architecture + §12 performance risks
- [PROJECT__RULES.md](PROJECT__RULES.md) — coding constraints + §8 checklist
- [MEMORY_INVESTIGATION.md](MEMORY_INVESTIGATION.md) — profiling workflow
- [ICON_TEXTURE_AUDIT.md](ICON_TEXTURE_AUDIT.md) — icon texture sizing
