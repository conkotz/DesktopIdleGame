# Project Rules — Desktop Idle RPG Game

**Read this file before making code changes in this repository.**

These rules apply to all gameplay, combat, UI, and save/load work under `Assets/6.Scripts`. They complement the architecture reference in [`SYSTEMS_MAP.md`](SYSTEMS_MAP.md).

---

## 1. No scene-wide lookup in gameplay loops

- **Do not** call `FindObjectOfType`, `FindObjectsOfType`, `FindFirstObjectByType`, `FindObjectsByType`, or `Resources.FindObjectsOfTypeAll` inside:
  - `Update`, `FixedUpdate`, `LateUpdate`
  - Combat tick paths (enemy AI, ability channels, projectile motion, idle combat)
  - Coroutines that run every frame or at high frequency during combat
- **Prefer instead:**
  - Serialize references in the Inspector
  - Cache refs in `Awake` / `Start` / `OnEnable`
  - Use existing services: `CombatEnemyRegistry`, `CombatPlayerRefs`, `PlayerTransformCache`, `SkillsManager.Instance`, `SaveManager.Instance`, etc.
  - Inject or register via events when a system comes online

**Known violations to avoid repeating:** `ResourceNode.Update` (player find until cached), `DesktopOverlayClickThrough.LateUpdate` (UniWindowController find). Fix these when touched; do not add new ones.

---

## 2. No repeated Instantiate/Destroy during combat unless pooled

- **Do not** spawn or destroy GameObjects every attack, hit, frame, or enemy death without a pool.
- **Pool or reuse** for: damage numbers, projectiles, debuff icons, overhead UI rows, loot VFX, minion teardown in sustained fights.
- **Acceptable one-off spawn/destroy:** map load (`LevelSpawnDirector`), UI panel open/close, quest/map list rebuild, player bootstrap, death/respawn transitions.
- When adding combat feedback, extend existing systems (`DamagePopupSystem`, `GoldPopupSpawner`, projectile prefabs with self-destroy lifetime) or add an explicit pool.

---

## 3. No UI rebuilds every frame

- **Do not** rebuild layouts, destroy/recreate slot rows, or run full grid `Rebuild()` in per-frame methods.
- **Do** use dirty flags and coalesced refresh (see `InventoryUiRefreshCoordinator`, `ActionBarUI` slot refresh interval, `CharacterStats` stats coalesce in `LateUpdate`).
- Per-frame UI is limited to: position follow, cooldown fill, alpha pulse, text value change on cached widgets — not hierarchy changes.

---

## 4. No LINQ in hot paths

- **Do not** use LINQ (`.Where`, `.Select`, `.First`, `.Any`, `.OrderBy`, `.ToList`, `.ToArray`, `.Sum`, `.All`, `OfType<>().`) in:
  - `Update`, `FixedUpdate`, `LateUpdate`
  - Per-enemy combat loops, ability execution, targeting scans, projectile updates
- **Use** explicit `for` loops, cached lists, and registries (`CombatEnemyRegistry.GetLiveEnemies()`).
- LINQ is acceptable in: save/load (infrequent), editor scripts, one-time UI build on panel open.

---

## 5. No accidental runtime material creation

- **Do not** call `new Material(...)`, `Renderer.material` (creates instance), or `Material.Instantiate` in gameplay loops unless the feature explicitly requires unique per-instance materials (document why).
- **Prefer** `sharedMaterial`, MaterialPropertyBlocks, or pre-assigned materials on prefabs.
- Audit when touching VFX, outlines, hover highlights, and UI Graphic color tweens.

---

## 6. Communicate through events or services, not scene scans

- Systems should **publish/subscribe** or call **narrow services**, not search the scene for collaborators.
- **Good:** `OnStatsChanged`, `OnLevelStarted`, `CombatEnemyRegistry`, `ISaveable` via `SaveManager`, static `Instance` with a single clear owner.
- **Avoid:** Finding `PlayerController`, `Inventory`, `ActionBarUI`, or `SharedTooltipUI` from arbitrary UI slots on every interaction.
- When adding a cross-cutting concern, add or extend a **service or registry** rather than a new `Find*`.

---

## 7. Change log for every feature

Any new feature or non-trivial fix must include a short **change list** (in PR description, commit body, or handoff note):

```text
## Scripts changed
- Path/To/Script.cs — why it was touched
- Path/To/Other.cs — why it was touched
```

If the work introduces a **new system** (new manager, director, registry, or major gameplay domain), also update [`SYSTEMS_MAP.md`](SYSTEMS_MAP.md):

- Add the system to **§2 Main gameplay systems** (or the relevant subsection)
- Add managers to **§3** if applicable
- Note **per-frame**, **Instantiate/Destroy**, or **risk** entries if the system affects performance
- Update **§13 Folder reference** if a new folder or major file group is added

---

## 8. Pre-change checklist

Before submitting gameplay or UI code:

| Check | Question |
|-------|----------|
| Loops | Any `Find*` or `GetComponent` in `Update` / `FixedUpdate` / `LateUpdate`? |
| Combat alloc | Any `Instantiate` / `Destroy` in combat without pooling? |
| UI | Any hierarchy rebuild or `Instantiate` UI rows per frame? |
| LINQ | Any LINQ in tick or per-enemy paths? |
| Materials | Any new material instances per frame or per hit? |
| Coupling | Could this use an event or existing registry instead of a scene scan? |
| Docs | Change list written? `SYSTEMS_MAP.md` updated if new system? |
| Skills / abilities | New or changed ability? Followed [`NEW_SKILL_ENTRY_AGENT_CHECKLIST.md`](NEW_SKILL_ENTRY_AGENT_CHECKLIST.md) end-to-end? |

---

## 9. Related documentation

| File | Purpose |
|------|---------|
| [`SYSTEMS_MAP.md`](SYSTEMS_MAP.md) | Architecture, managers, per-frame scripts, known risks |
| [`NEW_SKILL_ENTRY_AGENT_CHECKLIST.md`](NEW_SKILL_ENTRY_AGENT_CHECKLIST.md) | Skills & abilities pipeline checklist (assets, tooltips, HUD, minions, VFX) |
| `PROJECT__RULES.md` (this file) | Constraints for all future changes |

---

*Agents and contributors: read `PROJECT__RULES.md` and skim `SYSTEMS_MAP.md` before editing `Assets/6.Scripts`.*
