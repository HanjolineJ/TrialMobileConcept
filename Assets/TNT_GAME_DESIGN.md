# TNT Game — One-Page Design Document

**Genre:** 2D physics demolition puzzler · **Platform:** iOS / Android (portrait) · **Engine:** Unity 6 LTS
**Pitch:** Place a limited number of TNT charges on a building, detonate, and earn 1–3 stars by leveling as much of the structure as possible.

## 1. Core Loop

```
┌────────────────────┐
│  PLACE EXPLOSIVES  │  Drag limited TNT charges onto the structure
└─────────┬──────────┘
          ▼
┌────────────────────┐
│ DESTROY BUILDING   │  Tap DETONATE → physics collapse + debris
└─────────┬──────────┘
          ▼
┌────────────────────┐
│       SCORE        │  % of structure destroyed → 1–3 stars + coins
└─────────┬──────────┘
          ▼
┌────────────────────┐          ┌───────────────┐
│      UPGRADE       │ ───────► │  NEXT LEVEL   │ ──► loop (harder structure)
│ (blast size, +TNT) │ post-MVP │               │
└────────────────────┘          └───────────────┘
   Under 1 star ──► RETRY same level (restart)
```

**Vertical slice scope (MVP):** one level, one explosive type (TNT), building destruction, scoring, restart. Upgrades, multiple levels, and extra explosive types are post-MVP.

## 2. MVP Acceptance Criteria

- **Level loads:** `Level_01` playable from cold start in ≤ 3 s on target device.
- **Placement:** Player can drag up to 3 TNT charges; a charge attaches to the block touched (no floating in air); HUD shows remaining count; placement locks after detonation.
- **Destruction:** DETONATE applies a radial impulse to all blocks in blast radius; blocks start static and switch to dynamic on blast so the building is stable pre-detonation; debris persists until scoring is done.
- **Scoring:** When physics settles (all bodies asleep, or 3 s timeout), score = % of blocks displaced past the demolition line; result panel shows % and 1–3 stars (<40% = 1★, 40–75% = 2★, >75% = 3★).
- **Restart:** One tap restores the level to its exact initial state in ≤ 1 s — full TNT count, no leftover debris.
- **Performance:** ≥ 30 FPS sustained during collapse on a mid-tier device (iPhone 8 / Android equivalent).
- **Stability:** 10 consecutive place → detonate → restart cycles with no crash or soft-lock.

## 3. Technical Requirements

- **Unity 6 LTS (6000.0.x)**, 2D (URP) template, portrait orientation.
- **Physics:** built-in 2D physics (Box2D) — no package needed. No `AddExplosionForce` in 2D; implement blast via `Physics2D.OverlapCircleAll` + `AddForce(..., ForceMode2D.Impulse)` with distance falloff.
- **Packages:** `com.unity.2d.sprite`, Input System (touch), TextMeshPro, URP, Device Simulator (testing).
- **Data:** ScriptableObjects for tuning (`ExplosiveData`: radius/force; `LevelData`: TNT count, star thresholds) so designers tune without code changes.
- **Version control:** Git + standard Unity `.gitignore` from day one.

## 4. Folder Structure (`Assets/`)

```
Assets/
├── Scenes/            # Main, Level_01
├── Scripts/
│   ├── Core/          # GameManager, LevelManager, ScoreManager
│   ├── Gameplay/      # Explosive, BlastForce2D, DestructibleBlock, PlacementController
│   └── UI/            # HUDController, ResultPanel
├── Prefabs/
│   ├── Gameplay/      # TNT, Block variants, DemolitionLine
│   └── UI/
├── Art/               # Sprites (placeholders fine for slice)
├── Audio/             # Explosion SFX (placeholder)
├── Data/              # ExplosiveData / LevelData ScriptableObjects
└── Settings/          # URP pipeline asset, InputActions
```

## 5. Implementation Tasks (by priority)

**P0 — required for the slice:**

1. **Project bootstrap** — Unity 6 project, packages, folder structure, Git init + Unity `.gitignore`, portrait, 30/60 FPS target settings.
2. **Destructible building** — `Block` prefab (SpriteRenderer + Rigidbody2D + `DestructibleBlock`, starts static), assemble `Level_01` structure, place demolition-line marker.
3. **TNT placement** — touch-drag placement that attaches to blocks, 3-charge limit enforced, HUD counter.
4. **Explosion** — DETONATE button → `OverlapCircleAll` radial impulse with falloff, static blocks switch to dynamic, charges consumed.
5. **Scoring + result** — count blocks displaced past demolition line after settle → % → 1–3 stars on result panel.
6. **Restart** — reset level state (plain scene reload is acceptable for the slice); verify the 10-cycle stability criterion.

**P1 — polish for the slice:**

7. **HUD/UX pass** — placement valid/invalid feedback, detonate & restart buttons, star reveal animation, camera framing for one-hand play.
8. **Juice + performance** — explosion particles, SFX, camera shake, off-screen debris cleanup, on-device profiling against the 30 FPS floor.
