# Endless Runner (2D)

A endless side-scroller built on Unity 6 (URP 2D). The world is generated
procedurally, chunk by chunk, and streams forever as you run. All art is placeholder:
every entity is a distinct runtime-generated **shape** so it reads at a glance.

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

### Jump

- **Single, variable-height jump** — hold for a higher jump, tap for a short hop.
- **Coyote time** and a **jump buffer** keep the timing forgiving.

You die by touching a hazard, falling into a pit, or being left behind the camera.

> Multi/triple jump and wall-jump are intentionally left out for now to keep the
> challenge honest. The player's `Visual` child and derived jump-reach are already in
> place, so they can be layered back in later without reworking the core.

## Architecture

Scripts live under `Assets/Scripts`, namespaced `Run.*`. Systems are decoupled: they talk
through `GameManager` events and locate each other by tag/singleton, so the scene needs
almost no manual wiring.

```
Common/       Shapes (runtime sprite shapes), RunInput (input facade),
              IWorldShiftable (floating-origin hook)
Core/         GameManager (state machine, score, restart, events)
Player/       PlayerController (run, jump, death)
CameraRig/    CameraFollow (forward-scrolling smoothed follow)
Generation/   LevelGenerator (the chunk algorithm + floating origin)
              PieceFactory (builds + pools every shape)
              Segment (one pooled chunk)
Hazards/      Hazard (kill-on-contact), PatrolEnemy
Collectibles/ Coin
UI/           HudController (score, best, prompts — built in code)
Editor/       RunnerSetup (the Build Scene menu item)
```

### Generation algorithm

`LevelGenerator` keeps a `frontier` X. While the frontier is within `spawnAhead` of the
camera's right edge it builds the next **segment** and advances the frontier by that
segment's width; segments that scroll `despawnBehind` the camera are recycled to the pool.

Each segment is one of a weighted set of **patterns** (flat, gap, elevated, staircase,
spikes, enemy, floating platforms). The weights shift with a `difficulty` value that ramps
up with distance, so breathers thin out and hazards grow more common. Every pattern begins
and ends on solid ground so chunks connect seamlessly, and every gap/step is clamped
against the player's **current** jump reach (`MaxJumpSpan` / `MaxJumpHeight`) — so nothing
unclearable is ever spawned, even as the run speed rises.

### Endless without drift (memory + precision)

Two things keep an unbounded run healthy:

- **Memory** is bounded by **pooling**: chunks and pieces are recycled, so the live object
  count stays flat no matter how far you run — distance never allocates.
- **Float precision** is protected by a **floating origin**: once the player passes
  `rebaseDistance`, the whole world (player, camera, every live piece, the generator's
  anchors, and the score origin) is shifted back toward the origin by the same amount.
  Nothing moves relative to anything else — the run is seamless — but world coordinates
  stay small, so physics never loses precision on a marathon run. Components that cache an
  absolute X (coin bob origin, enemy patrol bounds) implement `IWorldShiftable` to correct
  themselves during a shift.

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

- **New level pattern:** add a weight to the table in `LevelGenerator.ChoosePattern`, a
  `case` in `BuildSegment`, and a `Build*` method that lays out pieces and returns the
  chunk width. Use `MaxGap()` / `MaxStepUp()` to stay clearable.
- **New obstacle/collectible:** add a `PieceKind`, a branch in `PieceFactory.Build`
  (shape + colour + collider + component), and a `Spawn*` helper. Lethal things just need a
  trigger collider and the `Hazard` component (or a subclass, like `PatrolEnemy`). If it
  caches an absolute world X, implement `IWorldShiftable`.
- **New shape:** add a `ShapeType` and its point-in-shape test in `Shapes.IsInside`.
- **Tuning:** most feel lives in serialized fields on `PlayerController`, `LevelGenerator`,
  `PieceFactory`, and `CameraFollow` — editable in the Inspector, no code changes needed.
