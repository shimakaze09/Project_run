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
2. Press **Play**. Press **Space / W / ↑ / click / tap** to start, and again to jump.

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

Durations are configured per-pickup in the Inspector (5–8s by default). See
`Core/PowerUpEvents.cs` and the `OnPowerUpActivated`/`OnPowerUpExpired` handling in
`Player/PlayerController.cs`.

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
- `Generation/LevelGenerator`: selects, loads and pools prefab chunks in the
  **Runner Level** runtime scene. Reloading SampleScene unloads that scene too.
- `Generation/LevelChunk`: prefab width, selection weights, safe-start flag,
  jump requirements and pooled reset/shift behavior.
- `Common/Shapes`: runtime player artwork. Prefab artwork is saved under
  `Assets/Art`, so all level pieces also render in Prefab Mode.
- `Generation/PieceFactory` and `Segment` remain legacy authoring helpers;
  the runtime level loader no longer uses either.
- `Editor/RunnerSetup`: builds/repairs the gameplay shell.
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
