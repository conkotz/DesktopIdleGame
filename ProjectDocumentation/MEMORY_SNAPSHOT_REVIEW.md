# Memory Snapshot Review — Template

**Purpose:** Structured review of Unity Memory Profiler snapshot(s). Fill one copy per snapshot or per compare session (e.g. 2m vs 20m).

**Do not infer root cause from a single category alone** — correlate across sections and across time (see [`MEMORY_INVESTIGATION.md`](MEMORY_INVESTIGATION.md)).

---

## Session metadata

| Field | Value |
|-------|-------|
| Date | |
| Unity version | |
| Memory Profiler package version | |
| Build type | Editor Play / Development / Release |
| Snapshot name / file | |
| Compare pair (if diffing) | e.g. `Dev_2m` vs `Dev_20m` |
| Elapsed play time at capture | |
| Scene / activity at capture | |
| Task Manager working set (MB) at capture | |

**Snapshot summary (from Memory Profiler top line):**

| Metric | This snapshot | Compare snapshot | Δ |
|--------|---------------|------------------|---|
| Total resident / tracked | | | |
| Native | | | |
| Managed | | | |
| Graphics / GPU-related (if shown) | | | |
| Untracked (if shown) | | | |

---

## 1. Textures

### Numbers from snapshot

| Item | Count | Total size | Top entries (name → size) |
|------|-------|------------|---------------------------|
| Textures (all) | | | |
| Largest single texture | | | |

### What a high number might mean

- Many unique texture assets loaded at once (UI skins, item icons, enemy sprites, VFX sheets, full-window backgrounds).
- Duplicate loads of the same logical art (same PNG imported multiple times, or copies not sharing an asset).
- Oversized imports (4K UI, uncompressed RGBA) for small on-screen use.
- Runtime-created textures (cursor, dashed lines, procedural UI, screenshot/readback) not released.
- Multiple atlases or loose sprites each pulling full source textures into memory.

### What to check next

- [ ] Sort by **Size** in Memory Profiler; list top 10 texture names and dimensions.
- [ ] In Project window, select top offenders → **Inspector** → max size, compression, Read/Write Enabled.
- [ ] Compare **2m vs 20m** snapshots: same texture names growing, or new names appearing?
- [ ] Filter: **Runtime** vs **Asset** — runtime entries warrant code/path review (no fixes here; note for follow-up).
- [ ] Check for duplicate asset paths or `(Clone)` texture names.

### Likely causes in a 2D idle RPG

- Large **strip + full-window** UI backgrounds and parallax layers kept loaded together.
- **Item/equipment icon** grids loading many full-size source textures instead of atlas entries.
- **Skill tree / world map** UI opening once and leaving textures resident.
- **Floating damage / gold popups** and buff icons referencing many small but numerous textures.
- **Read/Write Enabled** on sprites used for pixel hit tests or runtime tinting.
- **UniWindow / desktop overlay** capture or blur using temporary textures that accumulate if not released.

**Notes:**

```
Top textures:
Growth vs compare snapshot:
Action items (investigation only):
```

---

## 2. Sprite Atlases

### Numbers from snapshot

| Item | Count | Total size | Top entries |
|------|-------|------------|-------------|
| SpriteAtlas / atlas-related | | | |
| Packed vs unpacked (note) | | | |

### What a high number might mean

- Many atlases built and resident (per-biome, per-UI-screen, per-enemy-family).
- Sprites not atlased → each sprite may retain or pull larger backing textures.
- Atlas variants (multiple platforms or padding settings) duplicated in memory.
- Late-bound atlases loaded on first use and never unloaded during long idle sessions.

### What to check next

- [ ] **Window → 2D → Sprite Atlas** — list atlases; map largest snapshot entries to atlas names.
- [ ] Inspector on atlases: **Include in Build**, packing groups, duplicate atlas coverage.
- [ ] In snapshot, trace a large **Sprite** entry to its **Texture2D** / atlas parent.
- [ ] Compare snapshots: new atlas names at 20m vs 2m (suggests on-demand UI/map loads).

### Likely causes in a 2D idle RPG

- Separate atlases for **HUD**, **inventory**, **skills**, **world map**, **enemies**, **VFX** all loaded after menu navigation.
- **Addressables / Resources** (if used) loading atlas bundles on each map or shop open.
- Enemy or minion spawns referencing sprites from **different atlases** per type.
- Editor-like **debug atlases** or unbatched icon imports in `Assets/`.

**Notes:**

```
Atlases implicated:
On-demand load pattern observed:
```

---

## 3. Materials

### Numbers from snapshot

| Item | Count | Total size | Top entries |
|------|-------|------------|-------------|
| Materials | | | |
| Material instances (name contains `(Instance)` or duplicate shader+params) | | | |

### What a high number might mean

- Accessing `Renderer.material` or `Graphic.material` in gameplay creates **material instances** per object.
- Per-sprite or per-UI-element unique materials for outline, flash, pulse, rarity color.
- Leaked instances after `Destroy` on objects whose materials were duplicated.
- Many shader variants (2D lit, UI Default, custom VFX) each with separate material objects.

### What to check next

- [ ] Sort materials by count; filter names with **(Instance)**.
- [ ] Pick one large UI/combat object type in snapshot → count materials attached.
- [ ] Cross-check [`PROJECT__RULES.md`](PROJECT__RULES.md) §5 (runtime material creation).
- [ ] Compare 2m vs 20m: material **count** growth vs size growth.
- [ ] Note shader names on top materials (UI/Default, Sprites/Default, custom).

### Likely causes in a 2D idle RPG

- **Hover/outline** on resources, enemies, or tree-chop targets (see ability VFX / woodcut outlines).
- **Enhancement flash**, **buff pulse**, **rarity borders** on equipment slots.
- **Damage flash** or ailment tint on enemies and player.
- **Particle systems** with renderer material instancing per spawn.
- **TextMeshPro** font materials (counted separately below, but often linked).

**Notes:**

```
Instance material growth (2m → 20m):
Shaders involved:
```

---

## 4. Meshes

### Numbers from snapshot

| Item | Count | Total size | Top entries |
|------|-------|------------|-------------|
| Meshes | | | |
| Skinned / 3D meshes (if any) | | | |

### What a high number might mean

- For a **2D** project, unexpectedly high mesh memory often comes from **TextMeshPro** mesh buffers, **UI Canvas** geometry, particle meshes, or imported 3D props/VFX — not traditional level geometry.
- Duplicated mesh data per instance (particles, TMP submeshes).
- Mesh colliders with high vertex counts (uncommon in pure 2D, but check tilemaps / composite colliders).

### What to check next

- [ ] Identify mesh **names** in profiler: `TextMeshPro`, `Canvas`, `ParticleSystem`, etc.
- [ ] If **Tilemap** / composite collider meshes appear, note scene and size.
- [ ] Compare vertex counts on largest meshes.
- [ ] Correlate mesh growth with **GameObjects** and **TMP** sections.

### Likely causes in a 2D idle RPG

- **TMP** rebuilding word geometry frequently (combat log, damage numbers, tooltips, dialogue).
- **World-space Canvas** or overhead HP bars regenerating mesh data.
- **Particle bursts** (loot sparkle, ability hits) with mesh renderers.
- Occasional **3D models** on minions, weapons, or imported VFX packages.

**Notes:**

```
Mesh types dominating:
Linked UI/combat system (hypothesis):
```

---

## 5. AudioClips

### Numbers from snapshot

| Item | Count | Total size (decompressed) | Top entries |
|------|-------|-------------------------|-------------|
| AudioClip | | | |
| Streaming vs load-in-memory (note) | | | |

### What a high number might mean

- Clips set to **Load In Memory** / decompress on load for long music or ambient beds.
- Many one-shot SFX all preloaded at scene start.
- Duplicate clip references across scenes in a DDOL audio manager.
- Large WAV/PCM footprint if compression disabled.

### What to check next

- [ ] Inspector on top clips: **Load Type**, **Compression Format**, **Quality**, length.
- [ ] List whether audio lives in **Bootstrap**, **GamePlay**, or **DontDestroyOnLoad** objects.
- [ ] Compare snapshots: new clip names appearing during idle/combat loop.
- [ ] Note if **AudioMixer** groups hold references to unused clips.

### Likely causes in a 2D idle RPG

- **Menu + gameplay** music both resident after transition.
- **Combat / gather / UI** SFX banks fully loaded for instant playback during idle automation.
- **Ambient map loops** per biome loaded with `GameplayLevelBootstrapper` flow.
- Repeated **ability** sounds instantiated with ability spam (clip ref, not GameObject).

**Notes:**

```
Largest clips (name, length, load type):
New clips at 20m:
```

---

## 6. GameObjects

### Numbers from snapshot

| Item | Count | Notes |
|------|-------|-------|
| GameObjects (total) | | |
| Active vs inactive (if visible) | | |
| DDOL / DontDestroyOnLoad hierarchy size (estimate) | | |

### What a high number might mean

- Spawn-without-pool patterns: projectiles, damage text, loot, UI rows, debuff icons, minions.
- Enemies respawning while old references or children remain.
- UI list rebuild leaving inactive children in hierarchy.
- Duplicate managers or event systems across scene loads (singleton failure).
- Deep UI hierarchies (inventory grids, skill trees, world map nodes).

### What to check next

- [ ] Memory Profiler **All Objects** / hierarchy view: sort by type or search `(Clone)`.
- [ ] Count top prefab names: `ItemDrop`, `FloatingDamage`, `Projectile`, `InventorySlot`, `Enemy`, etc.
- [ ] Compare **GameObject count** 2m vs 20m — linear growth strongly suggests leak or unbounded spawn.
- [ ] Cross-reference [`SYSTEMS_MAP.md`](SYSTEMS_MAP.md) §5 Instantiate/Destroy and §12 risks.
- [ ] Note scenes loaded (single vs additive).

### Likely causes in a 2D idle RPG

- **Idle combat** spawning projectiles, popups, gold floaters without pooling.
- **LevelSpawnDirector** + respawn queue increasing enemy instances.
- **Soulforged minions**, **war banner**, **spectral axe** and other ability spawns.
- **Quest tracker / game log / database** UI rows accumulated or pooled poorly.
- **NPC dialogue** boxes cloned and not destroyed.
- **Helper/tutorial** overlay objects (see `HelperGameplayController` in systems map).

**Notes:**

```
(Clone) prefab counts:
Δ GameObjects (2m → 20m):
```

---

## 7. MonoBehaviours

### Numbers from snapshot

| Item | Count | Managed size (if shown) | Top types |
|------|-------|-------------------------|-----------|
| MonoBehaviour instances | | | |
| Top script types by instance count | | | |

### What a high number might mean

- Many components on spawned UI slots, enemies, projectiles, or timeline nodes.
- Static caches or registries holding references to destroyed objects (shows as count + managed growth).
- Large serialized fields on behaviours (lists, dictionaries) growing during play.
- Duplicate manager components (`QuestProgressManager`, `SaveManager`, etc.).

### What to check next

- [ ] Sort by **type name** in snapshot; list top 15 script class names.
- [ ] For dominant types, note typical **owner** (UI slot, enemy, player child).
- [ ] Compare **managed heap** section with MB count growth.
- [ ] Check for **event subscriptions** noted in code review follow-up (not in this doc).
- [ ] Inspect one high-count type in scene hierarchy at capture time.

### Likely causes in a 2D idle RPG

- **ActionBarSlotUI**, **InventorySlotUI**, **StorageSlotUI**, **DebuffIconUI** scale with UI size.
- **EnemyBaseController** × enemy count; **MinionCombatController** × minions.
- **ProjectileVisual** and ability marker components.
- **Quest** / **tracker** row components.
- **CharacterStats** / **PlayerAbilityController** — few instances but very large managed footprint (see Managed Heap).

**Notes:**

```
Top MonoBehaviour types:
Types that grew 2m → 20m:
```

---

## 8. TextMeshPro objects

### Numbers from snapshot

| Item | Count | Approx. size | Notes |
|------|-------|--------------|-------|
| TMP_Text / TextMeshProUGUI | | | |
| TMP_FontAsset | | | |
| TMP material presets | | | |
| Font atlas textures (link to §1) | | | |

### What a high number might mean

- Many live text fields updating often (DPS, timers, resource counts, combat log, tooltips).
- Multiple **font assets** or **fallback** chains loaded (CJK, emoji, separate weights).
- Dynamic font **atlas expansion** from unlisted characters during play.
- Each TMP object allocates mesh + material state; pooled damage numbers may still retain TMP instances.

### What to check next

- [ ] List **font assets** in snapshot and atlas texture sizes.
- [ ] Count **TMP_Text** vs **TMP_SubMesh** / submesh objects.
- [ ] Settings: **Dynamic OS Font**, **Atlas Population Mode**, material presets.
- [ ] Identify texts with frequent value changes (HUD, floating combat text, game log).
- [ ] Compare font atlas texture size 2m vs 20m (growth = dynamic atlas issue).

### Likely causes in a 2D idle RPG

- **Floating damage / gold / XP** text spawning.
- **Game log** and **quest tracker** appending lines.
- **Tooltip** and **dialogue typewriter** TMP.
- **Strip HUD**: CP, DPS, attack timer, resource bars with numeric labels.
- **Database / codex** pages with many stat lines.
- Multiple **SDF font assets** for header vs body vs monospace debug text.

**Notes:**

```
Font assets loaded:
TMP object count Δ:
Atlas texture growth:
```

---

## 9. RenderTextures

### Numbers from snapshot

| Item | Count | Total size | Resolutions |
|------|-------|------------|-------------|
| RenderTexture | | | |

### What a high number might mean

- Off-screen UI or world rendering (camera stacking, magnifier, minimap).
- Post-processing or blur passes allocating RTs per frame or per open window.
- Forgotten `Release()` on RTs created in code.
- Full-window desktop effects mirroring strip or game view at high resolution.

### What to check next

- [ ] Note **width × height × format** for each large RT.
- [ ] Map to cameras: strip vs full window (`StripCamera`, `FullCamera` — see systems map).
- [ ] Compare snapshot times: RT count stable or increasing?
- [ ] Search project for `RenderTexture`, `targetTexture` (follow-up; no code change in this pass).

### Likely causes in a 2D idle RPG

- **Desktop strip** setup with secondary camera rendering to texture.
- **FullWindowBackgroundPresenter** or sky clone pipeline.
- **UI blur** behind modals or helper overlays.
- Screenshot / **expand strip background** feature reading back framebuffer.

**Notes:**

```
RT resolutions and owners (if known):
Growth over session:
```

---

## 10. Managed Heap

### Numbers from snapshot

| Item | Value | Compare | Δ |
|------|-------|---------|---|
| Managed heap used | | | |
| Managed objects count | | | |
| Largest managed types | | | |

### What a high number might mean

- GC heap grew from allocations in hot paths: strings, LINQ, boxing, new lists every frame.
- Large long-lived objects: save data caches, quest/skill databases in memory, inventory snapshots.
- Fragmentation or retained garbage from frequent spawn/destroy without pool.
- **Few types, huge size** → suspect large arrays/dictionaries on player systems (`CharacterStats`, save payloads).

### What to check next

- [ ] Use Memory Profiler **Managed** breakdown; list top types by **Inclusive** size.
- [ ] Compare **2m vs 20m**: heap still climbing at 20m while gameplay stable?
- [ ] Note correlation with **MonoBehaviour** count growth.
- [ ] Optional: capture **Profiler** module **GC Alloc** during same session (separate pass).
- [ ] Cross-check [`PROJECT__RULES.md`](PROJECT__RULES.md) §4 (LINQ in hot paths).

### Likely causes in a 2D idle RPG

- **String formatting** every frame for HUD, DPS, timers, floating text.
- **SaveManager** building save payloads or scanning `ISaveable` with allocations.
- **Quest / skill** progress updates allocating collections.
- **Idle combat** targeting scans or ability logic allocating per tick.
- **JSON** or serialization buffers held in memory after autosave.
- Large **tooltip / database** string builds when opening UI.

**Notes:**

```
Top managed types (name → size):
Heap still growing after 2m? Y/N
Correlated category from above sections:
```

---

## Cross-section summary

Fill after all sections are reviewed.

### Dominant memory categories (rank 1–3)

1. 
2. 
3. 

### Grew meaningfully from compare snapshot (2m → 20m)

| Section | Grew? (Y/N/Unclear) | Magnitude (qualitative) |
|---------|---------------------|-------------------------|
| Textures | | |
| Sprite Atlases | | |
| Materials | | |
| Meshes | | |
| AudioClips | | |
| GameObjects | | |
| MonoBehaviours | | |
| TextMeshPro | | |
| RenderTextures | | |
| Managed Heap | | |

### Hypotheses (investigation only — not confirmed fixes)

| # | Hypothesis | Evidence from snapshot | Next validation step |
|---|------------|------------------------|----------------------|
| 1 | | | |
| 2 | | | |
| 3 | | | |

### Not in snapshot / needs another tool

- [ ] **Native plugin** memory (UniWindow, etc.) — may sit under Native untracked
- [ ] **GPU memory** not fully visible in CPU Memory Profiler
- [ ] **Asset Bundle** duplication across loads
- [ ] **Other:** 

---

## Related docs

| File | Use |
|------|-----|
| [`MEMORY_INVESTIGATION.md`](MEMORY_INVESTIGATION.md) | How snapshots were captured |
| [`SYSTEMS_MAP.md`](SYSTEMS_MAP.md) | Spawn/UI/combat systems to correlate |
| [`PROJECT__RULES.md`](PROJECT__RULES.md) | Rules violated by likely causes |

---

*Template only — fill per snapshot. Do not treat “likely causes” as confirmed without snapshot evidence and a second repro run.*
