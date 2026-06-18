# Memory Investigation — Testing Plan

**Purpose:** Compare memory usage across build types and over session time.  
**Scope:** Measurement and recording only — no code changes, no root-cause conclusions until data is collected.

**Read first:** [`PROJECT__RULES.md`](PROJECT__RULES.md) · [`SYSTEMS_MAP.md`](SYSTEMS_MAP.md) (for scene/context names when writing notes)

**Project scenes (reference):**
- `Assets/1.Scenes/Bootstrap.unity`
- `Assets/1.Scenes/GamePlay.unity`

---

## 0. Before you start

### Install / verify Memory Profiler

1. Open the project in Unity.
2. **Window → Package Manager**.
3. Search for **Memory Profiler** (Unity package `com.unity.memoryprofiler`).
4. If not installed: select it → **Install**.
5. After install: **Window → Analysis → Memory Profiler** should be available.

### Prepare a results table (copy for each run)

Use one table per **build type × test session**. Example columns:

| Time | Elapsed | Task Manager (MB) | Memory Profiler snapshot? | Snapshot name / file | Notes (scene, activity) |
|------|---------|-------------------|---------------------------|----------------------|-------------------------|
| T0   | 0:00    |                   | No                        | —                    | Baseline at idle        |
| T1   | 2:00    |                   | Yes / No                  |                      |                         |
| T2   | 20:00   |                   | Yes / No                  |                      |                         |

Also record for every run:

- **Date / time**
- **Unity version** (Help → About Unity)
- **Build type** (Editor / Development / Release)
- **Platform** (e.g. Windows 64-bit)
- **Resolution / window mode** (strip size, full window if relevant)
- **Save slot** (new game vs load) — note only; do not assume impact yet
- **What the player was doing** at each checkpoint (menu, map, combat, idle, etc.)

### Control variables (keep the same across runs when possible)

- Same machine, same monitor setup.
- Close unrelated apps (browsers, other games) before each run.
- Same entry path: e.g. always load the same save slot, or always new game — pick one per comparison series and note it.
- Same in-game activity script for the 2 min and 20 min windows (see §4).

---

## 1. Unity Editor memory usage

### 1.1 Setup

1. Open the project.
2. Note Unity version: **Help → About Unity**.
3. Open **Task Manager** (Ctrl+Shift+Esc).
4. **Details** tab → right-click column headers → enable **Memory (active private working set)** or **Working set (memory)** (wording varies by Windows version).
5. Find process **`Unity.exe`** (main editor). Optionally note **`Unity Hub.exe`** separately — do not mix into game numbers.

### 1.2 Play Mode session

1. Open the scene you normally use to start Play Mode (Bootstrap and/or GamePlay — note which).
2. **T0 — Baseline:** With editor open, **not** in Play Mode, record Task Manager memory for `Unity.exe`.
3. Press **Play**.
4. Perform your **standard activity script** (§4) from game start through the checkpoints.
5. At **2 minutes** and **20 minutes** of Play Mode elapsed time, record Task Manager and take snapshots per §3.
6. Stop Play Mode.
7. Wait **60 seconds** (editor settling).
8. Record Task Manager memory for `Unity.exe` again (**T_end**).

### 1.3 Memory Profiler snapshot (Editor)

Follow §3 with the editor in **Play Mode** at the 2 min and 20 min checkpoints.

**Note in results:** Editor memory includes tooling overhead; it is **not** comparable 1:1 to player build numbers. Use it to spot growth patterns and object categories, not as shipping RAM.

### 1.4 Editor checklist

- [ ] Memory Profiler package installed
- [ ] Unity version recorded
- [ ] T0 (not playing), T+2m, T+20m, T_end (after Play) recorded in Task Manager
- [ ] Snapshots taken at 2m and 20m (if following full protocol)
- [ ] Activity script and scene entry path written in notes
- [ ] “Memory grew / flat / unclear” recorded (§5)

---

## 2. Development build memory usage

### 2.1 Build settings

1. **File → Build Settings**.
2. Platform: **Windows** (or your target).
3. Scenes in build: include scenes used for normal play (at minimum Bootstrap + GamePlay if that is the shipping flow).
4. Enable **Development Build**.
5. Optional (note in results if toggled): **Autoconnect Profiler**, **Script Debugging** — these can affect memory; keep consistent across dev runs.
6. **Build** (or Build And Run) to a **new folder** with a clear name, e.g. `Builds/MemoryTest_Dev/`.

### 2.2 Run without Editor

1. Quit Unity Editor (recommended so `Unity.exe` does not skew Task Manager).
2. Launch the **development build `.exe`** from the build folder.
3. In Task Manager **Details**, find the **player executable name** (your product name, not `Unity.exe`).
4. Use the same column as in §1 (**Working set** or **Private bytes** — pick one and use it for **all** runs in this investigation).

### 2.3 Session timing

1. **T0:** Launch exe → reach stable first screen (main menu or first gameplay frame) → record memory.
2. Run the **same activity script** (§4) as the Editor run.
3. **T+2m** and **T+20m:** record Task Manager; capture Memory Profiler snapshots per §3 if attaching profiler.
4. Close the build normally (in-game quit if available, then close window).
5. Wait 60s; confirm process exited Task Manager.

### 2.4 Memory Profiler with Development build

Development builds can connect to the editor profiler:

1. Before launching build: Unity Editor can stay closed, or open editor with **Window → Analysis → Memory Profiler**.
2. Launch development build with **Autoconnect Profiler** enabled (if you enabled it at build time).
3. In Memory Profiler, connect to the **Player** (not Editor) when prompted.
4. Capture snapshots at 2m and 20m per §3.

If Autoconnect is **not** used, note “Dev build — Task Manager only” and still complete timing checkpoints.

### 2.5 Development build checklist

- [ ] Build folder path recorded
- [ ] Development Build = ON
- [ ] Editor closed during play (recommended)
- [ ] Correct `.exe` process identified in Task Manager
- [ ] T0, T+2m, T+20m recorded
- [ ] Snapshots at 2m / 20m (or note why not)
- [ ] Activity script matches other build types
- [ ] Growth assessment filled (§5)

---

## 3. Standalone release build memory usage

### 3.1 Build settings

1. **File → Build Settings** → same platform and scenes as §2.
2. **Disable** Development Build.
3. **Disable** Autoconnect Profiler / Script Debugging (unless you deliberately keep them on — note if so).
4. Use the same **release-oriented** settings you ship (IL2CPP vs Mono, managed stripping, etc. — record what the project uses).
5. **Build** to a separate folder, e.g. `Builds/MemoryTest_Release/`.

### 3.2 Run

Same as §2.2–2.3:

1. Quit Editor.
2. Launch release `.exe`.
3. Task Manager on the **player process only**.
4. T0, T+2m, T+20m with the **same activity script**.

### 3.3 Memory Profiler with release build

Release builds often **do not** expose the same profiler attachment as Development builds.

- If Memory Profiler **cannot** attach: record **Task Manager only** for release runs and note “No profiler snapshot — release build.”
- If your pipeline supports a **Release + limited profiling** build for internal testing only, note that variant separately; do not mix with true shipping release without labeling.

### 3.4 Release build checklist

- [ ] Build folder path recorded
- [ ] Development Build = OFF
- [ ] Scripting/backend settings recorded (Mono/IL2CPP, etc.)
- [ ] T0, T+2m, T+20m Task Manager recorded
- [ ] Profiler snapshot: Yes / No / Not supported (reason)
- [ ] Same activity script as Editor and Dev
- [ ] Growth assessment filled (§5)

---

## 4. Standard activity script (use for every run)

Define **one** script and reuse it for Editor, Dev, and Release so times are comparable.

**Template — fill in and lock before first run:**

```
Entry: [ e.g. Load save slot 1 / New game from Bootstrap ]

0:00–0:30   [ e.g. Main menu → enter GamePlay ]
0:30–2:00   [ e.g. Idle combat on map X / gather / stand in menu ]
2:00        CHECKPOINT — record memory + snapshot
2:00–20:00  [ e.g. Same loop: combat + UI open/close / map travel ]
20:00       CHECKPOINT — record memory + snapshot
```

**Rules:**

- Do not change the script mid-investigation.
- Write what you actually did in the **Notes** column if you deviate.

---

## 5. Recording whether memory grows over time

For each **build type**, compute and record:

| Metric | How |
|--------|-----|
| **Δ 0→2 min** | Task Manager at 2m minus T0 (MB) |
| **Δ 2→20 min** | Task Manager at 20m minus 2m (MB) |
| **Δ 0→20 min** | Task Manager at 20m minus T0 (MB) |

**Classification (per build type):**

- [ ] **Flat** — Δ 2→20 min under your noise threshold (suggest noting threshold, e.g. &lt; 50 MB, in results; adjust after first run)
- [ ] **Gradual growth** — steady increase 2m → 20m
- [ ] **Spike then flat** — jump early, then stable
- [ ] **Unclear** — high variance between readings; repeat run

**Repeat run rule:** If a single run is unclear, repeat the **same** build type + activity script once and note Run A / Run B.

**Do not interpret causes** in this document — only record numbers and classification.

### Task Manager — exact steps (Windows)

1. Ctrl+Shift+Esc → **Details** (or **Processes** on newer Windows).
2. Locate process:
   - Editor: `Unity.exe`
   - Player: your `.exe` name
3. Note memory column value **at a glance moment** (not during scene load spike).
4. For consistency, always read the same column:
   - **Working set (memory)** — common default
   - Or **Private working set** — document which you chose in row 0 of your table
5. Optional second source: **Performance** tab → **Memory** graph while process selected (screenshot for archive).

### Memory Profiler — exact snapshot steps

**A. Editor Play Mode**

1. Enter Play Mode and reach checkpoint time (2m or 20m).
2. **Window → Analysis → Memory Profiler**.
3. In the **Capture** tab (or main toolbar): target **Unity Editor** / **Play Mode** (wording depends on package version).
4. Click **Capture** (or **Take Snapshot**).
5. Wait for capture to finish (can take tens of seconds; do not spam capture).
6. **File → Save** or use the snapshot save control; name file:  
   `YYYY-MM-DD_Editor_Play_2m` / `_20m`
7. Note snapshot path in your results table.

**B. Development build (player connected)**

1. Build with Development Build + Autoconnect (if using).
2. Launch player; open Memory Profiler in Editor.
3. Select connected **Player** as capture target.
4. At 2m / 20m on the same clock as Task Manager: **Capture**.
5. Save: `YYYY-MM-DD_Dev_Player_2m` / `_20m`.

**C. What to note from each snapshot (observation only, no diagnosis yet)**

- Total tracked memory (as shown in summary).
- Top categories listed in the snapshot UI (e.g. Native, Managed, Graphics) — copy labels and sizes into notes.
- Whether **Managed** heap size at 20m is larger than at 2m for the same build type.

---

## 6. Side-by-side comparison matrix

Fill after all three build types complete at least one full 20-minute run.

| Build type | T0 (MB) | T+2m (MB) | T+20m (MB) | Δ 0→20m | Grows 2→20m? | Profiler snapshots |
|------------|---------|-----------|------------|---------|--------------|-------------------|
| Unity Editor (Play Mode) | | | | | | |
| Development build | | | | | | |
| Release build | | | | | | |

**Comparison questions (answer from data only):**

1. Is **Release** lower than **Editor** at T+20m? (expected for many projects; record yes/no.)
2. Is **Dev** close to **Release** at T+20m? Record delta.
3. Does **Δ 2→20m** show growth in **Release**? (Primary shipping concern.)
4. Does **Editor** grow faster than **Release**? (Often yes; record either way.)

---

## 7. Optional follow-ups (still no code changes)

Only after baseline tables are complete:

- [ ] Second 20-minute run for the build type that showed growth (confirm reproducibility).
- [ ] Same run with **minimal activity** (stand still, no UI) vs **heavy activity** script — separate rows, same build.
- [ ] Snapshot diff: Memory Profiler **Compare Snapshots** (2m vs 20m) for one build type; export or screenshot summary.
- [ ] Archive: build hashes/folders, Unity version, `ProjectSettings/ProjectVersion.txt` contents in notes.

---

## 8. File / artifact naming convention

```
MemoryInvestigation/
  YYYY-MM-DD/
    notes.md                    ← copy of your tables + activity script
    Editor_Play_2m.snap         ← or package default extension
    Editor_Play_20m.snap
    Dev_Player_2m.snap
    Dev_Player_20m.snap
    screenshots/
      taskmanager_release_20m.png
```

Keep artifacts **outside** `Assets/` unless your team explicitly versions them (avoids bloating the repo).

---

## 9. What this plan does **not** do

- Does not assign blame to scripts, assets, or systems.
- Does not recommend fixes until growth is **reproduced** and **quantified** in §6.
- Does not replace Unity’s **Profiler** (CPU) or **Frame Debugger** — memory-only scope here.

When investigation data exists, a separate analysis pass can cross-check [`SYSTEMS_MAP.md`](SYSTEMS_MAP.md) sections on Instantiate/Destroy, per-frame scripts, and known risks — **after** measurements are recorded.

---

*Testing plan only. Update this file if your team adds platforms (macOS Activity Monitor, Linux `/proc`) or CI memory gates.*
