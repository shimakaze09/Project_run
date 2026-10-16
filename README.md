# Endless Runner (2D)

An endless side-scroller built on Unity 6 (URP 2D). Authored level prefabs stream
chunk by chunk into a separate runtime scene as you run. All art is placeholder:
editable layered sprite artwork keeps each entity readable at a glance.

| Entity            | Shape          |
|-------------------|----------------|
| Player            | Capsule (blue) |
| Coin              | Square (gold)  |
| Spikes            | Triangles      |
| Enemy (patrols)   | Circle         |
| Crate             | Rounded square |
| Ground / platform | Rectangle      |

## Running it

1. In Unity, run **`Tools ▸ Endless Runner ▸ Build Scene`** once. This creates the
   `Ground` layer, the camera rig, the player, the systems, and the HUD, then saves the
   scene. It is idempotent — safe to run again to repair the scene.
2. Run **`Tools ▸ Endless Runner ▸ Build Main Menu Scene`** once too, if
   `Assets/Scenes/MainMenu.unity` doesn't already exist. Also idempotent.
3. Open **`MainMenu`** and press **Play** — that's the game's actual entry point: Play loads
   `SampleScene` after you pick a mode. (Opening `SampleScene` directly and pressing Play still
   works for quick iteration; it just skips the title screen.)
4. Press **Space / W / ↑ / click / tap** to start, and again to jump.

## Controls

- **Confirm** (Space / W / ↑ / left-click / tap): start the run, jump, and restart.
- The player **auto-runs**; you only control jumping.
- **Menus** (Difficulty select, Pause): navigate with **arrow keys / W-A-S-D**,
  confirm the highlighted button with **Enter**, or just click/tap it directly.

### Jump

- **Single, variable-height jump** — hold for a higher jump, tap for a short hop.
- **Coyote time** and a **jump buffer** keep the timing forgiving.

You die by touching a hazard, falling into a pit, or being left behind the camera.

> Multi/triple jump and wall-jump are intentionally left out for now to keep the
> challenge honest. The player's `Visual` child and derived jump-reach are already in
> place, so they can be layered back in later without reworking the core.

## Menus

- **Main Menu** (`Assets/Scenes/MainMenu.unity`, `UI/MainMenu`): the title screen —
  **PLAY**, **SETTINGS**, **QUIT**. Play opens a **Choose Mode** panel (Runner / Environment,
  matching the Ready-screen picker below) before loading `SampleScene`. It lives in its own
  scene, not an overlay in `SampleScene`, so it never runs alongside — or shows through onto —
  gameplay.
- **Pause** (`UI/PauseMenu`, Escape or the Android back button during a run): **Resume /
  Settings / Restart / Main Menu / Quit**. Main Menu reloads `MainMenu.unity`, resetting
  `Time.timeScale` first since pausing freezes it at 0.
- **Settings** (`UI/SettingsMenu`): one component shared by both menus above (shown/hidden by
  whichever opened it). Master/Music/SFX volume sliders, persisted via `Core/VolumePreferences`
  and applied live to `Audio/AudioManager`. Also has the **High Contrast** toggle below.

### High Contrast Hazards

An accessibility option (`Core/AccessibilityPreferences`, persisted in `PlayerPrefs`) that
recolors hazards and standable ground to one fixed, high-contrast pair instead of the normal
varied palette, since hazards and scenery otherwise sit close in hue/lightness for some forms
of color blindness:

- Hazards (spikes, patrol enemies, drones) → a fixed bright magenta.
- Ground/platforms/crates → a fixed dark steel blue, with their top-edge trim picked out in a
  lighter shade so a run of ground still reads as a surface rather than one flat block.
- Coins keep their own gold color either way — they're a pickup, not something to tell apart
  from a hazard.

Applied at runtime per piece (`Generation/LevelChunk` for Runner mode's authored prefabs,
`EnvironmentMode/TrapFactory` for Environment mode's traps) rather than baked by **Apply
Twilight Art**, so toggling it takes effect immediately without needing that Editor tool.
Runner-mode pieces already on screen repaint live; Environment-mode traps pick it up on their
next placement.

## Difficulty

Before starting a run, pick **Easy / Normal / Hard** from the panel on the Ready
screen. The choice persists across sessions (`PlayerPrefs`) and is re-applied every
time a run starts, so switching difficulty and restarting always takes effect
immediately — it isn't just a one-time value read at scene load.

| Difficulty | Run speed | Speed ramp-up | Hazard ramp distance | Starting hazard level |
|------------|-----------|---------------|-----------------------|------------------------|
| Easy       | 0.6×      | 0.4×          | 3× (slower to ramp)   | 0 (easiest chunks only)|
| Normal     | 1×        | 1×            | 1×                    | 0.1                    |
| Hard       | 1.3×      | 2.2×          | 0.35× (fast ramp)     | 0.65 (near max)        |

Selecting the difficulty that's already active (or pressing Enter on it again)
starts the run immediately instead of requiring a separate confirm. See
`Core/DifficultySettings.cs`.

## Power-ups

Pickups placed in level chunks (`Collectibles/PowerUpPickup`) grant a timed effect
on contact, shown on the HUD and cleared automatically on expiry:

- **Shield**: absorbs the next hazard hit instead of killing the player, then
  breaks (with a spark burst) and expires.
- **Speed Boost**: multiplies run speed by 1.3× for the pickup's duration.
- **Double Jump**: grants one extra mid-air jump per landing while active.

Durations and the bob animation are configured per-pickup in the Inspector. A dead player can't
collect one (physics keeps running after Game Over; only the Pause menu stops time). See
`Core/PowerUpEvents.cs` and the `OnPowerUpActivated`/`OnPowerUpExpired` handling in
`Player/PlayerController.cs`. `PowerUpEvents.Active` reports the most recently granted type
without each listener needing to track it separately; `TriggerActivated` ignores
`PowerUpType.None` and non-positive durations.

## Environment mode

A single-player mode where **you are the level**. A computer runner plays the run, and you
drop traps ahead of it while the game keeps going; there is no pausing to build. Pick
**PLAY AS ▸ ENVIRONMENT** on the Ready screen (saved in `PlayerPrefs` like difficulty),
then start the run as usual.

- **Controls:** `1`–`5` or the mouse wheel pick a trap; **left-click** places it. A preview
  follows the cursor: green where the trap fits, red with a reason on the HUD where it doesn't.
- **Traps:** Crate (20 energy, a solid block to jump), Mud (15, cuts run speed while crossed),
  Tar (25, adds gravity while crossed so jumps fall short), Spikes (30, cost a heart), Drone (45,
  paces back and forth over its patch of ground).
- **Combos:** Mud and Tar are non-lethal and the computer runner never jumps them (see below), so
  placing one right before Spikes or a Drone narrows the runner's safe jump window instead of
  just slowing it down on open ground. Both effects linger briefly (0.6s) after the runner leaves
  the puddle, so they can still affect a jump taken just after — see
  `Player/PlayerController.ApplySlow`/`ApplyHeavy`.
- **Energy** starts at 40/100, refills 9 per second, and refunds 15 for every heart the runner loses.
- **Fair play:** a placed trap is a harmless pulsing ghost for 0.4 s before it turns real.
  Nothing can be placed in the faint red zone in front of the runner, which always covers that
  0.4 s plus 0.25 s of warning at its current speed. Traps need flat ground and 1 unit of space
  between them, and each level chunk holds at most two.
- **Goal:** stop the runner as early as you can. Game Over shows where it was stopped; the
  **fastest stop** (shortest distance) is your record. Environment runs never touch the
  runner records or the coin wallet.
- **Difficulty** sets the computer runner's skill (and the usual speed curve): Easy notices new
  traps after 0.45 s and makes the most timing mistakes; Hard notices after 0.18 s and rarely slips.
- The camera zooms out a little and looks further ahead in this mode so there is room to plan.

### How the computer runner works

`AI/RunnerAI` drives the same `PlayerController` as a person, through the `IRunnerInput`
interface, and jumps with `PlayerController.RequestJump` on the exact physics step it planned.
`AI/JumpPlanner` copies the nearby level into rectangles (every level piece is an axis-aligned
box) and simulates the runner with the controller's real numbers: gravity, variable jump height,
coyote time and the catch-up sprint. If running straight on would hurt it, the planner tries a
jump at each step before the danger, with several hold lengths, and keeps the ones that land
safely with solid footing. `AI/RunnerBrain` aims for the take-off furthest from both edges of
that safe window, keeps the target stable as it gets closer, and adds the skill-based timing
error. Placed traps reach it through `PlacedTrap.Active` only after its reaction delay, and a
crate is avoided the same way a hazard is - except Mud and Tar, which the planner deliberately
leaves out, since they're not solid and jumping them would just dodge the debuff they're meant to
land. Turn on **Log Decisions** on the Player's `RunnerAI` to see every jump in the Console; with
Gizmos on, the Scene view also draws the planned jump arc.

### Files and tuning

- `EnvironmentMode/EnvironmentPlayer`: energy, hotbar costs, arm delay, warning margin,
  spacing and per-chunk limits (all in the Inspector on **Environment Player**).
- `EnvironmentMode/PlacedTrap`, `TrapFactory`: the traps, drawn in code in the twilight style.
  They are parented to the chunk they sit on, so they scroll, re-base and recycle with it
  (`Generation/IChunkAttachment`).
- `EnvironmentMode/TrapEffectZone`: Mud/Tar's gameplay effect - a trigger that refreshes a short
  timed debuff on `Player/PlayerController` (`ApplySlow`/`ApplyHeavy`) every physics step the
  runner overlaps it, rather than tracking the exact moment they leave.
- `AI/RunnerAI`: per-difficulty skill (reaction time, timing jitter, mistake chance and size).
- `CameraRig/CameraFollow`: the Environment-mode lead, zoom and height.
- `UI/ModeMenu`, `UI/EnvironmentHud`; `UI/HudController` swaps its prompts and record.
- **Tools ▸ Endless Runner ▸ Add Environment Mode** adds just these objects to an existing
  scene (Build Scene also includes them).
- **Tools ▸ Endless Runner ▸ Verify Environment Mode** flies the computer runner through
  copies of every authored layout (and a drone) at speeds 6 to 20.8, checks the chunk-seam
  window, drone prediction, trap building and scoring. Player data is restored afterwards.

## Sprint one: distance target and currency

- The unlabeled HUD bar compares distance travelled to the **previous completed run**,
  not the all-time best. 50 m of a previous 100 m run gives exactly 50% fill.
- At 100%, the whole bar fades away over 0.6 seconds. The run continues: this is
  an endless runner, so there is no finite finish-line completion sequence.
- On the first run (or after a zero-distance run), the bar is hidden. There are
  no first-run / last-run text labels.
- The gold coin icon shows the saved total only. Picking up one coin adds one
  currency unit and displays a small floating, fading **+1** beside the count.
  Coins no longer add points to the distance score.
- The wallet is saved on pickup under PlayerPrefs key `run.coins`. No store or
  purchasing UI is implemented yet.
- Final distance is saved on death under `run.lastRunDistance`; the current bar's
  target stays fixed through Game Over. Restart loads the new target.
- All-time distance score uses `run.bestDistanceScore`. The old mixed coin/distance
  `run.bestScore` is deliberately not converted: its distance cannot be recovered.
- Distance remains stable across floating-origin shifts.

## Architecture

Scripts live under `Assets/Scripts`, namespaced `Run.*`.

- `Core/GameManager`: run state, distance events, previous-run target, best distance,
  separate saved currency, restart.
- `UI/HudController`: distance/best labels, unlabeled fading bar, coin count/popup.
- `UI/MainMenu`, `UI/PauseMenu`, `UI/SettingsMenu`: the title screen, pause overlay, and the
  shared volume/High Contrast panel they both open. See **Menus** above.
- `Core/VolumePreferences`, `Core/AccessibilityPreferences`: small `PlayerPrefs`-backed static
  classes, one per Settings option, each with a `Changed` event so other systems can react
  without knowing where the choice came from.
- `Generation/LevelGenerator`: selects, loads and pools prefab chunks in the
  **Runner Level** runtime scene. Reloading SampleScene unloads that scene too.
- `Generation/LevelChunk`: prefab width, selection weights, safe-start flag,
  jump requirements, pooled reset/shift behavior, and the High Contrast Hazards repaint.
- `Common/Shapes`: runtime player artwork. Prefab artwork is saved under
  `Assets/Art`, so all level pieces also render in Prefab Mode.
- `Common/Tags`: shared GameObject tag strings (currently just `Player`), so a rename shows up
  as one compile error instead of several silent `CompareTag` mismatches at runtime.
- `Generation/PieceFactory` and `Segment` remain legacy authoring helpers;
  the runtime level loader no longer uses either.
- `Editor/RunnerSetup`: builds/repairs the gameplay shell.
- `Editor/MainMenuSceneSetup`: builds/repairs the `MainMenu` scene.
- `Editor/SprintOneChecks`: repeatable deterministic acceptance checks.

### Editing levels without code

Open the prefabs under **Assets/Resources/Levels** in Prefab Mode. Eight starter
layouts are included: SafeStart, Flat, Gap, Elevated, Staircase, Spikes, Enemy,
and FloatingPlatforms. Move/resize their actual children (ground, hazards,
coins and enemies), then save the prefab.

The root `LevelChunk` Inspector sets Width, SafeStart, StartWeight, EndWeight,
RequiredJumpSpan and RequiredJumpHeight. Layouts span local X=0 through Width
and must start/end on ground at Y=0. Keep gaps, steps and reaction runways
playable; the loader filters by declared jump requirements but does not resize
hand-authored geometry. Test new layouts at both starting and maximum run speed.

Duplicate a prefab for another layout; no switch statement or Build method is
needed. The generator's prefab list can be assigned explicitly; an empty list
loads all chunks from Resources/Levels. At least one positive-width SafeStart
prefab is required. Weights change with distance, while the safe-start section
remains hazard-free. Complete prefab instances are pooled, including reset coins
and patrol enemies, and rebase together to avoid floating-point drift.

### Verification

Stop Play mode, then run **Tools > Endless Runner > Verify Sprint One**.
The checks exercise 50% fill, full-bar fade timing, unlabeled HUD, coin popup
timing/count, persistent currency, previous-run vs best distance, death,
floating origin, saved prefab sprites, coin reuse and enemy patrol bounds.
PlayerPrefs touched by these checks are restored in a finally block.

Manual Play check: finish a run, restart, and watch the bar fill/fade at that
distance; collect a coin to see the counter increase and +1 float away.
Open and edit a level prefab, then Play again to see its saved layout.

### Scrolling, catch-up, and the camera

`WorldScroller` owns a single **scroll line** — the X the player is expected to occupy —
which advances purely by integrating the run speed while playing. Both the camera and the
player read that one value, so they can never disagree about where "forward" is.

The line is deliberately **never snapped to the player**. Snapping lets rendering jitter
and collision losses ratchet it forward, which reads as the player being slowly dragged
left. Instead the loop is closed from the other side: `PlayerController` applies a
**catch-up** bonus proportional to how far it trails the line (capped, with a small
deadzone). So ground lost to a collision, a bad landing, or an obstacle is repaid, and the
player sprints back into their screen lane once they get free — but a player still pinned
against an obstacle keeps losing ground, slides off the left edge, and the run ends rather
than soft-locking.

The camera tracks the scroll line **exactly** (no smoothing): the line is already smooth,
so smoothing would only add a speed-proportional lag that shifts the framing as the run
speeds up. Smoothing is kept solely for the fallback path that follows the player transform
directly. The camera never pans backwards.

### No wall friction

Auto-run continuously presses the capsule into whatever it meets, so ordinary surface
friction would let it cling to a wall face and hang there. The player carries a
**frictionless** physics material (`PhysicsMaterials.Frictionless`), so a blocked player
always slides down under full gravity instead of sticking. 2D friction combines as
`sqrt(a · b)`, so zeroing it on the player alone guarantees frictionless contact against
every piece in the world — no per-piece setup needed.

## Extending it

- **New level layout:** duplicate and edit a level prefab as described above.
- **New collectible/hazard:** add its components to prefab children and reset any
  cached state from LevelChunk when reusing it.
- **Tuning:** serialized fields on PlayerController, LevelGenerator, LevelChunk,
  HudController and CameraFollow are editable in the Inspector.

## Twilight art and particle feedback

- Editable twilight scenery prefab: **Assets/Resources/Scenery/Twilight.prefab**.
  Moon, sky gradient, stars, mountain ridges and pine silhouettes form two parallax layers.
- Every level piece has an **Artwork** child. Ground has moss edges and stone flecks;
  coins are gold medallions; spikes are pink crystals; enemies are little visor drones.
  The runner has a cyan suit, visor, scarf and boots, with visual-only squash/stretch.
- Feedback covers running dust, jumping, landing, coin pickup, run start, death,
  previous-run target completion, ambient coin sparkle, hazard embers and drifting motes.
  The HUD keeps its unlabeled fading bar and simple coin count/+1 popup.
- **GameFeel** on GameManager owns a shared 256-sprite pool. Bursts recycle that fixed
  pool rather than spawning objects during gameplay. Intensity can be reduced to zero
  in the Inspector. Ambient item effects are camera-culled and stop at Game Over.
- Particle positions and parallax offsets respect floating-origin rebasing. Particle
  simulation uses game time, so pausing stops it while Game Over lets bursts finish.
- **Tools > Endless Runner > Apply Twilight Art** rebuilds the starter artwork and
  saves the scene/prefabs. This replaces Artwork children; do not run it after custom
  artwork edits unless you intend to restore the supplied look. It does not alter
  the layout or colliders. Build Scene also applies this starter art.
- **Tools > Endless Runner > Verify Visual Effects** checks every burst type, pool
  bounds under 1,000 bursts, particle expiry/fade, pause, rebasing, intensity zero,
  authored artwork, item sparkles and persistent scenery sprites.

### Camera impact shake

- `CameraShake` on Main Camera observes `PlayerHealth.HealthChanged`; damage logic is
  unchanged. The current heart-based game treats one lost heart as a heavy hit by
  default. Increase **Heavy Damage Threshold** if multi-heart hits are introduced.
- Inspector settings: **Intensity** (0.12 world units), **Duration** (0.22 seconds),
  and **Decay Rate** (2; higher settles faster). Set intensity to zero to disable.
- No explosion gameplay system exists yet. Connect a major explosion's UnityEvent
  to Main Camera's `CameraShake.TriggerMajorExplosion()`, or call that method from
  its callback. It only triggers the visual effect; it does not create explosions.
- Offset is removed before camera following, then applied after it. Repeated hits
  restart the effect without stacking. Pause removes the offset and freezes its
  timer; disabling restores the clean pose. Rotation and zoom stay unchanged.
- **Tools > Endless Runner > Verify Camera Shake** simulates damage and explosion
  callbacks in an isolated preview scene, including expiry, threshold, healing,
  pause, cleanup, rebasing and 1,000 repeated events. Stop Play mode before running.
