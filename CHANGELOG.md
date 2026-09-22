# CHANGELOG

## Unreleased — TASK 020-026: Closing the SPEC.md audit

The seven requirements the post-TASK 019 audit found with neither an
implementation nor a roadmap entry. SPEC.md sections 42, 50, 52, 54 (edge cases
7, 9, 10, 20-24), 55, 56, 81 and 82.

### Added

- **Developer tools** (`Assets/Scripts/Debug/`, section 52). `DebugMode` gates
  them twice over: a compile-time `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, so a
  release player has no console in the binary rather than a hidden one, plus a
  runtime switch that starts off. `DebugCommands` holds the twelve
  state-changing tools as headless statics - no UI, no MonoBehaviour, no scene -
  so the same operations an automated test needs are the ones a developer types.
  `DebugOverlay` holds the five read-only readouts; `DebugConsole` is the IMGUI
  front end on backquote.
- **`WorldBounds`, `PlayerBoundsGuard`, `EnemyBoundsGuard`** (section 50, edge
  cases 9 and 10). A kill plane and a horizontal footprint per scene; the player
  goes back to the latest valid checkpoint with a message, an enemy goes back to
  its authored home. Neither is deleted or killed - falling through the floor is
  a level bug, and section 55 forbids a required NPC disappearing.
- **`BossArena`** (edge case 7, sections 55 and 56). Leaving a fight ends it:
  the boss returns to full health, phase 1 and its start position. That single
  rule resets the arena safely, stops chipping from outside paying, and still
  lets the player walk away.
- **The `[Dialogue unavailable]` fallback** (section 50), as a real one-line
  conversation the player dismisses normally.
- **`DeviceWatcher`** (edge cases 21, 22, 23) and **resolution / graphics
  quality / fullscreen settings** (edge cases 20 and 24), with a display change
  made during a scene load deferred until it finishes.
- **`MapUI`** (section 42's Map screen), drawn from the scene at the moment it
  opens. Bound to M and D-pad up.
- **`GAME_DESIGN.md`, `STORY_BIBLE.md`, `TEST_PLAN.md`** (sections 81 and 82).
- 30 tests (16 EditMode, 14 PlayMode). Suite: **170 + 111 = 281, all passing.**

### Changed

- `DialogueRunner.Begin` shows the fallback on a missing graph but still returns
  `false`, so the caller's own consequences do not fire. `NpcInteractable` no
  longer hides its prompt when it has no dialogue, because a hidden prompt
  cannot display a fallback.
- `PauseMenu.OpenPause()` is public and subscribes to `AutoPauseRequestedEvent`,
  so a pause nobody asked for still leaves a menu on screen. Setting
  `GameManager`'s state alone would freeze the game with nothing on it, and the
  next Pause press would then resume without ever having shown one.
- `RecoveryMessageUI` also carries the out-of-world and device-change sentences.
- `EnemyController.ResetToHome()` and `BossController.ResetEncounter()` added.
- `GameSceneManager.IsLoading` added, so things that need to know a load is in
  flight do not need a reference to it.
- `PlayerController.CancelVerticalVelocity()` added - by the time a fall out of
  the world is noticed, the accumulated speed would punch the player straight
  back through the floor on the next frame.
- A `Map` action added to `PlayerControls.inputactions` (keyboard M, D-pad up).

### Fixed

- The **Master Volume slider did nothing**. It wrote and persisted
  `SettingsManager.Current.MasterVolume`, and nothing ever read it. Harmless
  while the game had no audio; a genuinely broken control from TASK 018 onward.
  `Apply()` now pushes it to `AudioListener.volume`, and `MainMenuController`
  applies on change rather than only saving. Found by the audit, which is also
  why TEST_PLAN.md's manual pass now says explicitly that a control which
  changes nothing is a bug rather than a placeholder.

### Known limitations

Recorded in full in `KNOWN_ISSUES.md`. The ones worth naming here: the debug
console is unstyled IMGUI and cannot spawn an enemy type absent from the current
scene; the map has no walls, zoom or pan; boss arenas are circles and their
reset is not explained to the player; `WorldBounds` is one box per scene, which
will not fit Vayu's floating islands; focus-loss pausing is off in the Editor so
PlayMode tests are not frozen by a stray click. Newly found and still open: the
section 43 motion blur toggle has no motion blur to gate, a dozen section 54
edge cases are handled but untested, and colour-blind support is satisfied by
convention rather than by a palette mode.

## Unreleased — TASK 019: Vertical slice acceptance

The final ROADMAP task: verifying SPEC.md section 61's vertical-slice
checklist and section 62's success criteria against a live, running build
rather than against the code alone.

### Fixed

- `VfxSpawner`'s cached placeholder material used `sharedMaterial ??= new
  Material(...)` to build it once. `??=`'s `is null` check does not call
  Unity's `==` override, so a domain reload (recompiling, or entering/exiting
  Play mode) that destroys the cached `Material` as a native object leaves the
  static C# field pointing at a "fake null" the `??=` never catches — the next
  call would hand every particle a destroyed material instead of building a
  fresh one. Surfaced by this task's own repeated Play-mode sessions flipping
  an EditMode test that had passed cleanly right after TASK 018 shipped.
  Fixed with an explicit `if (sharedMaterial == null)` check instead.

### Verified live (Play mode, both `MainMenu.unity` and `Avarsha.unity`)

- Player, third-person camera, sword combat, one enemy archetype and one
  mini-boss (`Boss_TempleGuardian`) all present and reachable.
- The Avarsha hub, its one quest, one cinematic, the memory mechanic, and the
  Agniya temple section all present.
- The seven-brazier fire puzzle actually solves when every brazier is lit.
- Ember Step actually spends divine energy and starts its ability window.
- A save/damage/load round trip actually restores health, not just in a test
  harness.
- The Main Menu's Settings panel actually opens; the pause menu is present.
- New Game → difficulty select → Avarsha is a working scene transition,
  including the duplicate-`GameManager`-destroyed guard firing as designed
  (a warning, not an error) rather than leaving two persistent managers alive.
- The console stayed clean of new errors across the whole session — the only
  entries were one pre-existing corrupt-save test artifact and the expected
  duplicate-manager warning above.

### Outcome

Every open gap the project has is already tracked in KNOWN_ISSUES.md under
its originating task (TASK 016's eight inert skills, TASK 017's Main-Menu-only
settings and partial gamepad navigation, TASK 018's undone animation-event
timing migration and missing environmental audio, and the others). None of
them block a fun, playable vertical slice per section 62's criteria. This
task's job was to confirm that, not to close them — closing them is future
work beyond the vertical slice, per section 61's own framing ("before
expanding to all seven temples").

## Unreleased — TASK 018: Placeholder audio, animation and VFX pass

A working placeholder pass at SPEC.md sections 36 (VFX), 39 (animation) and 40
(audio), per section 78's placeholder-first rule: everything here is generated
in code rather than imported, since no art or audio asset exists in the
project and none can be authored without external tools.

### Added

- `VfxSpawner`/`SfxSpawner` — stateless factories, the same class of utility
  `GameLogger` already is. `VfxSpawner.Spawn(VfxKind, Vector3)` builds a
  short-lived coloured `ParticleSystem` burst from primitive settings;
  `SfxSpawner.Play(SfxKind, Vector3)` generates and caches a short sine-wave
  tone per kind and plays it from a throwaway `AudioSource`. Both self-destroy
  once finished, and both are safe to call from EditMode (the delayed
  `Destroy` used in Play mode is skipped there, since it is runtime-only and
  would otherwise log a console error the way `PuzzleTests` exercises).
- `CombatVfx`/`CombatAudio` — pure `EventBus` listeners, `HudUI`'s one-way
  shape: sparks on a parry, dust on a broken guard, divine energy and a tone
  on Ember Step, tones on a weapon impact/parry/block/guard break.
- `FireBrazierVfx` — a companion component reacting to `FireBrazier.Changed`
  with a fire burst, added to every brazier in Avarsha.
- `BossPresentationHooks` — fills `BossController`'s `onPhase3Entered`/
  `onDefeated` `UnityEvent` hooks (left empty since TASK 013 for exactly this)
  with a transformation burst, wired on both bosses.
- `PuzzleController.Solve`/`MemoryPickup.Interact` now spawn a glyph/memory
  VFX and tone directly on a live solve/collection — never on a save's
  restore path, the same rule `MemoryPickup`'s visual toggle already follows.
- `PlaceholderAnimator` — a placeholder for the player's animation list
  (`EnemyController.ApplyPhaseTint`/`EnemyCombatant`'s telegraph pulse applied
  to the player instead): a procedural, `Slerp`-blended tilt/lean/spin/crouch/
  flinch/collapse on a new `Body` child, reading `WeaponController.IsSwinging`,
  `CombatController.IsDodging`/`IsGuarding`, `PlayerController.IsSprinting`/
  `IsGrounded` and `DamageAppliedEvent`/`PlayerDiedEvent` — all already-public
  state it does not own or change. The player's `MeshFilter`/`MeshRenderer`
  moved off the root and onto this `Body` child so the tilt cannot fight the
  root's `CharacterController`. `PlayerController` gained `IsGrounded`/
  `IsSprinting` read-only properties for this to read.
- 4 EditMode tests (`PresentationTests.cs`) for the two spawners, and 6
  PlayMode tests (`PresentationPlayModeTests.cs`) proving the listeners react
  to real triggers: a real parry, a real broken guard, a real brazier
  lighting, `BossPresentationHooks` called directly, and `PlaceholderAnimator`
  tilting during a real swing (and relaxing after) and collapsing on a real
  `PlayerDiedEvent`.

### Known limitations

- **"Attack timings moved to animation events" (this task's ROADMAP wording)
  was not done as a literal timing-authority migration.**
  `WeaponController`/`EnemyCombatant`'s windup/active/recovery durations are
  read from archetype data and rescaled by `Difficulty` at swing time; moving
  that authority into real `AnimationEvent`s needs clips whose lengths already
  match the rescaled duration, which cannot exist without a rig to author them
  against. Doing it anyway would mean duplicating timing data into clips that
  immediately drift from `Difficulty`, or making clip length authoritative and
  losing per-difficulty rescaling — a regression, not a placeholder.
  `PlaceholderAnimator` reads `IsSwinging` to drive a cosmetic tilt instead;
  timing authority is unchanged.
- Environmental ambience and music (the rest of SPEC.md section 40) are
  undone — there is nothing to loop or compose from yet, and a placeholder
  tone reads as a bug for a texture meant to be continuous, unlike a one-shot
  combat cue.
- Water, underwater, lightning, time/dream distortion and wind-trail VFX are
  deferred — nothing in the game yet has water, a dream sequence, or open air
  worth trailing.
- Enemies have no placeholder locomotion animation; only the player does.
  Their existing tint and telegraph pulse (TASK 017) already cover combat
  readability, and a second placeholder-animation system for AI was judged
  lower priority than finishing the player's.

## Unreleased — TASK 017: Accessibility, settings and remapping

A working subset of SPEC.md section 43's accessibility list, plus the gamepad
control-scheme pass flagged since TASK 001, rather than stubs for all twelve
bullets: subtitle size and background, a colourblind-friendly non-colour combat
cue, camera shake and screen-effect toggles tied to real triggers, aim/lock-on
assistance, and full controller/keyboard remapping.

### Added

- `SettingsManager` gained five new `GameSettings` fields — `TextScale`,
  `SubtitleBackground`, `CameraShakeEnabled`, `ScreenEffectsEnabled`,
  `AimAssistEnabled` — plus ownership of the shared `PlayerControls`
  `InputActionAsset` and `LoadBindingOverrides`/`SaveBindingOverrides`/
  `ResetBindingOverrides`, persisting rebinds to `PlayerPrefs` as JSON
  independently of `GameSettings` itself. Also fixed a latent gap: every other
  singleton already nulled `Instance` in `OnDestroy`; `SettingsManager` did not.
- `RemapUI` — a full interactive remapping screen for all 12 remappable actions,
  `JournalUI`/`ProgressionUI`'s runtime-row shape applied to
  `InputActionRebindingExtensions.PerformInteractiveRebinding`, with a
  Reset-to-Defaults button. Wired into the Main Menu's Controls panel against the
  real `PlayerControls.inputactions` asset.
- `ScreenEffectsUI` — a full-screen damage flash gated by `ScreenEffectsEnabled`,
  `HudUI`'s lazy-player-lookup pattern.
- `UIScaleApplier` — rescales a canvas's `CanvasScaler` factor by `TextScale`.
  Added to every scene's `UI_Canvas`.
- `DialogueUI`/`CinematicUI` now apply `SubtitleBackground` and `TextScale` to
  their text on every line shown.
- `PlayerCamera.Shake` — a decaying random offset applied in `LateUpdate`, gated
  by `CameraShakeEnabled`, triggered automatically by a perfect parry and a
  broken guard (`ParryEvent`/`GuardBrokenEvent`).
- `LockOnController` widens its acquisition cone by a fixed multiplier while
  `AimAssistEnabled` is on — the same ranking, a wider net.
- `EnemyCombatant`'s attack telegraph gained a non-colour scale pulse alongside
  its existing tint (SPEC.md section 43's "never colour alone").
- `JournalUI`/`ProgressionUI`/`PauseMenu` now select a first control through
  `EventSystem` when their panel opens, so a gamepad has somewhere to start —
  closes part of "the journal has no gamepad navigation" (KNOWN_ISSUES). Dynamic
  rows within those panels still have no explicit `Selectable.navigation`
  wiring; see KNOWN_ISSUES.md.
- Fixed the gamepad look-speed bug carried since TASK 001: `PlayerCamera` was
  applying one mouse-tuned `sensitivity` multiplier to both mouse delta and
  gamepad stick input. Gamepad look now uses its own
  `gamepadLookDegreesPerSecond * Time.deltaTime`.
- `MainMenuController`'s Settings panel gained five rows for the new toggles/
  slider above, saved through the existing `Save()` path.
- 5 PlayMode tests (`AccessibilityPlayModeTests.cs`): screen flash on/off, camera
  shake on/off, aim-assist cone widening, the telegraph pulse firing
  independently of the tint, and a rebind override surviving a save/reload round
  trip through `SettingsManager`'s real `PlayerPrefs` JSON.

### Known limitations

- Motion blur is not gated — no URP effect exists in the project to toggle.
- Settings and remapping are reachable only from the Main Menu, before a game
  is running; there is no in-game or Pause-menu path to either yet.
- Gamepad navigation onto dynamically built rows (journal entries, quest list,
  remap rows themselves) still relies on default uGUI traversal rather than
  explicit `Selectable.navigation` wiring.

## Unreleased — TASK 016: Inventory, progression and skill tree

A simple inventory (SPEC.md section 28) and a skill tree spending points earned
through play (section 30), filling `SaveData.Inventory`/`Abilities` — reserved since
the save format was written.

### Added

- `InventoryItem`/`InventoryManager` — a count per item id across SPEC.md section 28's
  six categories, restored fresh by `SaveManager.Apply` on every scene load like
  `QuestManager`/`MemoryManager`. `UseConsumable` heals the player and spends one.
  Automatically grants a Divine Mark on discovering any Divine-category memory
  (Act II's "obtain divine marks"), reacting to `MemoryDiscoveredEvent`'s payload
  rather than any content asset naming a specific god — TASK 015's Agniya memory
  triggers it for free. `startingItems` grants a new game's loadout once, without
  double-granting on a save's own restore.
- `ItemPickup` — a generic item in the world, `MemoryPickup`'s shape applied to
  `InventoryItem` instead. One placed: an Ember Draught in the Marketplace.
- `SkillDefinition`/`SkillTreeManager` — four branches, three nodes each, unlocked
  with points earned on `QuestCompletedEvent`/`BossDefeatedEvent` (kept as an ordinary
  `WorldState` counter rather than a new save field) and gated by cost and an optional
  prerequisite. `GetBonus(effectType)` sums every unlocked skill's value for that type.
  Only **four** of the twelve skills are read by a system today — one per branch,
  proving the mechanism works end to end: Warrior's attack damage (`WeaponController`),
  Guardian's health and Divine's divine energy (both via the new
  `PlayerProgressionStats`), and Memory's manipulation cost (`MemoryManager.Corrupt`).
  The other eight are real, unlockable, persisted nodes with no consumer yet; see
  KNOWN_ISSUES.md.
- `PlayerProgressionStats` — applies the skill tree's health/energy bonuses to the
  player, recomputing "base + total bonus" from scratch on every change so a live
  unlock and a save's restore replaying the same set can never double-count.
- `HealthComponent.SetMaxHealth`/`DivineEnergyComponent.SetMaxEnergy` — small new
  seams `PlayerProgressionStats` needed; neither heals/refills on a cap change, so
  raising the cap mid-fight is not also a free top-up.
- `ProgressionUI` — the Inventory and Skill Tree screens (SPEC.md section 42), the
  same two-tab shape `JournalUI` already uses, on a new `Progression` input action.
- Fixed a real ordering gap while wiring save restore: `SaveManager.Apply` now
  restores `SkillTreeManager`'s unlocked set (recomputing max health/energy) *before*
  `RestorePlayer`, whose own `HealthComponent.RestoreTo` clamps to whatever max is
  current when it runs — after would have clamped a skilled-up player back down to
  their unboosted max on every load, a bug that had no way to surface before this
  task gave max health a reason to change at all.
- 11 EditMode tests (`ProgressionTests.cs`) and 6 PlayMode tests
  (`SkillTreePlayModeTests.cs`, distinct from TASK 011's `ProgressionPlayModeTests.cs`)
  plus one save/load round trip in `SavePlayModeTests.cs`.

## Unreleased — TASK 015: Agniya temple section

The first complete temple, entrance to boss (SPEC.md Phase 3), reusing every framework
built since TASK 002 rather than inventing temple-specific systems: fire puzzle
(TASK 012), boss (TASK 013), memory-cost mechanic, enemy variants, and a placeholder
environment and fire VFX (section 78).

### Added

- `MemoryToll` — a one-time barrier paid in memory integrity rather than an item,
  distinct from Ember Step's per-use drain: a cost of entering a place, not of using
  an ability (Phase 3's "memory cost"). Never a hard stop — integrity clamps at zero
  rather than refusing to pay, so the barrier can always be opened (section 55's
  anti-softlock rule). Restores from a save the same way `FireBrazier`/`MemoryPickup`
  do, via a new `WorldObjectState.PaidFlag`.
- `Enemy_DivineGuardian` and `Enemy_Boss_Agniya` archetypes — the project's first use
  of `EnemyClass.DivineGuardian` and its first true `Boss`-class encounter (every
  earlier boss content was `MiniBoss`).
- `Memory_AgniyasEmber` — the boss's reward, discovering `AGNIYA_MARK_OBTAINED`
  (SPEC.md Act II's "obtain divine marks").
- The Agniya temple itself, built in the Avarsha scene rather than a second Unity
  scene (see the note below and ARCHITECTURE.md): an entrance, warm point-light fire
  VFX placeholders, a second `FireBrazier` puzzle (proving the TASK 012 framework's
  reuse claim for the first time), an `EnemyGroup` of `Ash Creature`s and a `Divine
  Guardian`, the `MemoryToll`, checkpoints either side of the boss, `Boss_Agniya` in
  its own pillared arena, and the reward memory fragment.
- 5 EditMode tests (`MemoryTollTests.cs`) and 1 PlayMode save/load test for
  `MemoryToll`.

### A scene-transition gap this task deliberately did not build around

SPEC.md section 57 names `SCN_Temple_Agniya` as its own scene, the naturally-implied
structure for a temple. It was not built that way: `QuestManager`/`MemoryManager`/
`DialogueRunner` are scene-scoped (ARCHITECTURE.md's "Manager lookup" section), and
nothing currently carries their state across an *ordinary* scene transition the way
`SaveManager.PendingLoad` carries it across a menu Continue/Load — a real second scene
today would silently reset quest and memory progress on every temple visit. Closing
this properly (an area-transition helper that saves progress-only state, without the
player-position/checkpoint restore a real Load performs) is its own piece of work,
scoped out of "build a temple" and left for whichever task first needs a genuine
second scene. See KNOWN_ISSUES.md.

## Unreleased — TASK 014: Cinematic system and the first cinematic

A skippable, subtitled cinematic system (SPEC.md section 69) and its first use: Nirvaan
speaks after the player finds the first memory (section 68, scenes 9–10). Section 61's
"one cinematic" vertical-slice item.

### Added

- `CinematicPlayer`/`CinematicBeat` — a flat array of subtitle-and-duration beats
  rather than a timeline asset, since nothing yet needs camera or actor data. `Play`
  hands the world to `GameManager.EnterCutscene`, a new method alongside `Pause`/`Resume`
  that freezes `Time.timeScale` the same way and finally gives `GameState.Cutscene`
  something to do — it has existed, unused, since TASK 001. Beats are paced on
  unscaled time so the freeze `Play` just caused does not also stop the cinematic
  itself. `Skip` ends it on the spot (section 69's "skippable"); `ApplyEndStateImmediately`
  reaches the same end state with no sequence, for a loaded save and for tests, mirroring
  `SupernaturalEvent`'s method of the same name and purpose.
- `CinematicUI` — letterbox bars, the current subtitle, and a "press to skip" hint,
  driven entirely by `CinematicPlayer`'s events. Reads its own `Skip` input action
  directly, since a frozen `Time.timeScale` does not stop the Input System.
- A new `Skip` input action (Enter / gamepad South).
- `SupernaturalEvent` gained a doc note on why it is deliberately *not* a
  `CinematicPlayer`: section 69 asks for gameplay storytelling there, which means
  keeping the player in control, the opposite of what a cinematic is for.
- The first cinematic: `Cinematic_NirvaanSpeaks` in Avarsha, four beats, triggered by
  `WorldFlags.FirstMemoryFound` and completing to `WorldFlags.NirvaanAwakened`.
- 2 EditMode tests (`AvarshaTests.cs`) for the end state and its idempotency, and 5
  PlayMode tests (`CinematicPlayModeTests.cs`): entering/exiting `GameState.Cutscene`
  for real, a beat event per line in order, skipping ending it immediately and
  restoring control, refusing to play twice, and starting from its trigger flag.

## Unreleased — TASK 013: Boss framework and mini-boss

A three-phase boss framework (SPEC.md section 18) layered on the existing enemy
components rather than a new creature type, plus its first encounter: the Temple
Guardian, a mini-boss in a placeholder arena near Avarsha.

### Added

- `BossController` — phase transitions driven by real health fractions
  (`PhaseFor`, a pure function in the same shape as `EnemyController.Decide`). Each
  phase raises `EnemyController`'s movement speed and `EnemyCombatant`'s attack
  cooldown rather than damage or max health, directly satisfying section 18's "do not
  make bosses difficult by only increasing HP"; phase 3 also applies a placeholder
  supernatural-transformation tint. Publishes `BossEncounterStartedEvent` on first
  alert, `BossPhaseChangedEvent` on every phase change and `BossDefeatedEvent` on
  death, and reveals a `reward` GameObject then. `onEncounterStarted`/
  `onPhase2Entered`/`onPhase3Entered`/`onDefeated` are `UnityEvent` hooks for the
  cinematic system (TASK 014) and the VFX/animation pass (TASK 018) — section 18's
  scripted cinematic moment and victory sequence exist as extension points, not yet as
  real content (KNOWN_ISSUES.md).
- `EnemyController.SetSpeedMultiplier`/`ApplyPhaseTint` and
  `EnemyCombatant.SetCooldownMultiplier` — the small seams `BossController` needed on
  the existing TASK 004 components to make a phase change felt immediately, without
  duplicating any of the state machine, telegraph or attack logic a boss shares with
  every other enemy.
- A defeated boss survives a save through the same `SaveIdentity`/`WorldObjectState`
  check-now-and-subscribe pattern as `EnemyHealth`/`MemoryPickup`/`PuzzleController`
  before it — reveals the reward and tells listeners the boss is gone, without
  replaying the victory fanfare. A live kill and a save's restore path can both mark
  the same flag from two different components on the boss (`EnemyHealth` and
  `BossController`) in an order that would otherwise let the restore path steal the
  fanfare from a real kill; `BossController.fanfarePlayed`'s doc comment explains the
  race and how it is resolved.
- `HudUI` gained a boss health bar, hidden until `BossEncounterStartedEvent` and
  hidden again on `BossDefeatedEvent` — the "boss health on the HUD" the roadmap asks
  for.
- The Temple Guardian: a `MiniBoss`-class archetype, a placeholder arena (a tinted
  floor and four corner pillars) in open ground near Avarsha, a checkpoint before and
  after the fight (section 33 — the first `Checkpoint`s placed anywhere in Avarsha),
  and a reward memory fragment (`Memory_TempleGuardiansVow`, Divine, Supporting)
  revealed on defeat.
- 5 EditMode tests (`BossTests.cs`) for `PhaseFor`'s phase table, and 6 PlayMode tests
  (`BossPlayModeTests.cs`) plus one save/load round trip in `SavePlayModeTests.cs`:
  the encounter-started event firing once, phase 2/3 entering on real damage with
  their multipliers applied to a live `NavMeshAgent`, a phase never un-entering on a
  heal or a lighter follow-up hit, the reward/defeated event on death, and the
  defeated state and revealed reward surviving a save.

## Unreleased — TASK 012: Puzzle system and first fire puzzle

A reusable puzzle framework (SPEC.md section 26) and its first instance: three
braziers in Avarsha's temple district that must all be lit at once to open a sealed
passage. Section 61's "one puzzle" vertical-slice item.

### Added

- `IPuzzleElement` — the only thing a puzzle piece has to be: something with
  `IsSatisfied` and a `Changed` event. A new puzzle category (pressure plates, light
  reflection, water movement, ...) is a new class implementing this, not a new
  controller — the "reusable puzzle pieces so later temples add mechanics, not
  systems" the roadmap asks for.
- `PuzzleController` — solves the moment every listed element is satisfied at once,
  and stays solved afterwards even if an element later reverts (matches
  `Checkpoint`'s activation idempotency rather than inventing a re-lock rule SPEC.md
  never asks for). Sets a world flag, reports an optional quest objective, and
  publishes `PuzzleSolvedEvent`. Restores correctly from a save via the same
  check-now-and-subscribe pattern as `EnemyHealth`/`MemoryPickup` (TASK 009), applying
  the remembered solve directly rather than replaying the flag/objective side effects.
- `FireBrazier` — the first concrete `IPuzzleElement`: an `Interactable` the player
  lights, which burns out on its own after a set duration unless relit. The burn timer
  is the puzzle's entire logic and it is directly visible (a lit brazier, watched, goes
  out) — SPEC.md section 26's design rule against "guess the combination" puzzles.
- `PuzzleGate` — a blocking collider that opens permanently once its puzzle solves,
  matched to its `PuzzleController` by an id string through `PuzzleSolvedEvent` rather
  than a direct reference, the same decoupling `Checkpoint`/`CheckpointManager` use.
- The first fire puzzle: three braziers and a gate in Avarsha's temple district,
  sealing a small passage behind it (`TEMPLE_FIRE_PUZZLE`).
- 5 EditMode tests (`PuzzleTests.cs`) for the controller's pure combination logic, and
  12 PlayMode tests: a brazier's burn timer and interaction, the full
  brazier-controller-gate wiring solving and staying solved, solving too slowly never
  solving it, and the solved state surviving a save/load round trip.

## Unreleased — TASK 011: Divine ability framework and Ember Step

The `Ability` combo step wired to a real ability, divine energy spend, and Ember Step
(SPEC.md section 8.1, section 13 level 4, section 61's "one divine ability"). Closes
the last of SPEC.md section 13's nine combat actions.

### Added

- `CombatController.TryAbility` — Ember Step: a short fire dash on its own input
  (`Ability`: R / gamepad right trigger), paid for with divine energy rather than
  stamina, on a cooldown separate from the dodge's. Cancels a swing or guard in
  progress, records `ComboStep.Ability`, and opens a brief invulnerability window the
  same way the dodge does (`ScaledInvulnerabilityDuration`'s sibling for the ability,
  scaled by the same `Difficulty.Modifiers.PlayerTimingWindow`).
- `EmberStepUsedEvent` — published on every use, carrying the player's running use
  count. `MemoryManager` listens (a deliberate one-way exception to Memory otherwise
  never referencing Combat, the same shape `EnemyStagger` reacting to `ParryEvent`
  already uses) and applies the ability's SPEC-mandated cost: a small integrity drain
  every use, and — every third use by default — one Optional memory the player knows
  is temporarily forgotten and restores itself after a delay, unless something else
  changed its state in the meantime.
- `MemoryManager.ConfigureEmberStepCost` and `CombatController.ConfigureAbility` — test
  and tuning seams for the cost, cooldown and forgetting cadence.
- Input: `Ability` (R / gamepad right trigger).
- 8 PlayMode tests: the ability's cost/cooldown/combo-step wiring against a real
  `CombatController`, and the memory-side cost (integrity drain, periodic forgetting,
  restore-after-delay, and that a deliberate later change is never clobbered by the
  restore timer).

### Changed

- `CombatController`'s doc comment updated: all nine SPEC.md section 13 actions now
  live there (Jump excepted, which is locomotion).

The two screens SPEC.md section 42 lists (Quest Log, Memory Archive), memory
corruption made a real player-triggerable action (sections 20–21), and verification
that a memory already changes dialogue and world state (Phase 4 acceptance).

### Added

- `JournalUI` — a combined Quest Log / Memory Archive screen with a tab each, opened
  and closed on its own `Journal` input action (keyboard J, gamepad Select) rather than
  sharing Pause's, so the two never race to read `GameManager`'s state after the other
  has already changed it in the same input event. Rows are built at runtime, since the
  number of quests and memories changes as the game is played; there is no scroll view
  yet (see KNOWN_ISSUES.md).
  - Quest Log lists every active and finished quest with its objectives, a checkbox
    mark and progress count for each, and a colour per status (nothing shown by colour
    alone without also being shown by the checkbox and the `[Status]` text, per section
    43).
  - Memory Archive lists every discovered memory with its category, state and
    description, overall Memory Integrity as a percentage, and — for anything not
    Critical — a Corrupt button.
- `MemoryManager.Corrupt` — corrupts a memory and spends a slice of overall integrity
  as its cost in one call (SPEC.md section 20's "some divine powers consume memory"),
  surfaced now as a player action from the Memory Archive screen ahead of the ability
  that will trigger it for real (ROADMAP TASK 011). A refused corruption (a Critical
  memory) spends no integrity either.
- Input: `Journal` (J / gamepad Select).
- 2 EditMode tests for `Corrupt` and 6 PlayMode tests for the journal's open/pause
  behaviour, quest/objective listing, and memory listing/corruption against real
  `QuestManager`/`MemoryManager` data.

### Verified, not built

- Phase 4's acceptance line — "a memory changes at least one dialogue/world state" —
  was already true as of TASK 003 and is unchanged here: discovering
  `Memory_NameBeneathTheStone` sets the world flag `FIRST_MEMORY_FOUND`, which gates
  dialogue nodes in both `Dialogue_Amara` and `Dialogue_Mira`. Checked the actual
  scene content this task, rather than assuming the mechanism being wired means it is
  used anywhere.

## Unreleased — TASK 009: World save identity

Per-object save ids so enemies and pickups restore with the player (SPEC.md TASK 009),
and the checkpoint restoration gap flagged in TASK 008 closed.

### Added

- `SaveIdentity` (`Game.Core`) — a stable per-object id, independent of scene hierarchy
  or GameObject name, for anything whose state needs to survive a save. Optional and
  opt-in; falls back to the GameObject's name when unset.
- `WorldObjectState` (`Game.Core`) — namespaced world flags (`OBJ_DEAD_<id>`,
  `OBJ_COLLECTED_<id>`) layered on `WorldState`'s existing flat flags. An enemy's death
  or a pickup's collection round-trips through a save via the same mechanism story
  flags already use — no change to `SaveData`'s shape, no new save version.
- `EnemyHealth` marks `OBJ_DEAD_<id>` on death (if it carries a `SaveIdentity`) and
  reacts to that flag already being set — on its own `OnEnable` and via
  `WorldFlagChangedEvent` for a load applied later — by restoring the death directly
  (`HealthComponent.RestoreTo(0, dead: true)`, then despawns) rather than replaying
  `HealthComponent.Kill`, which would re-fire `Died` and double-report anything hanging
  off it (a quest kill).
- `MemoryPickup` marks `OBJ_COLLECTED_<id>` on collection the same way, and restores a
  remembered collection by hiding itself directly rather than calling `Discover` again.
- `CheckpointManager.RestoreActiveCheckpoint` and `Checkpoint.RestoreActivated` — finds
  the checkpoint a save named by id and makes it current without publishing
  `CheckpointActivatedEvent` (which auto-saves; replaying it during a load would
  overwrite the save being loaded). Closes the TASK 008 known limitation: Continue now
  actually restores which checkpoint the player last activated, not just their saved
  position.
- `SaveIdentity` added to all three Avarsha enemies, its memory fragment, and the
  Test.unity training dummy.
- 6 EditMode tests (`WorldObjectStateTests.cs`) and 4 PlayMode tests covering enemy
  death, pickup collection, and checkpoint restoration surviving a save/load round
  trip, plus the "flag already set before the object existed" ordering case.

### Fixed

- A death or collection marked its own flag and then reacted to its own notification,
  since the object that just died/collected is still subscribed to the event it
  publishes. Harmless for a pickup (both paths are idempotent) but would have skipped
  an enemy's despawn delay on every ordinary kill, destroying the corpse instantly
  instead of after the placeholder death delay. Both `EnemyHealth.ApplyRestoredDeath`
  and `MemoryPickup.ApplyRestoredCollection` now guard on state already reflecting the
  change (`health.IsDead`, `Collected`) before acting, telling a genuine restore apart
  from an object hearing about its own action.

## Unreleased — TASK 008: Menus and save/load UI

Main Menu, New Game, Continue, Save/Load, Settings, Controls and Credits, per SPEC.md
section 42; manual saves and the section 32 recovery message wired to real UI; a
scene-persistence bug that silently broke Continue/Load discovered and fixed along the
way.

### Added

- `MainMenu.unity` — new first scene in Build Settings. Holds its own
  `GameManager`/`GameSceneManager`/`SettingsManager` (so settings and difficulty chosen
  here carry into the game) and a `SaveManager` of its own for reading save metadata
  before any gameplay scene exists.
- `MainMenuController` — one canvas, six panels (root, New Game, Load, Settings,
  Controls, Credits), all code-wired (no persistent Inspector events, since the whole
  hierarchy is generated). New Game asks for a difficulty, resets any `WorldState` left
  over from a previous playthrough, then loads Avarsha. Continue loads whichever save
  slot has the newest timestamp; Load lists all three slots with their scene and time,
  or "(empty)".
- `SaveBrowser` — reads a save's metadata without touching the live game
  (`Peek`, `TryFindMostRecent`). Used by the menu before any gameplay scene exists, and
  by `MainMenuController` for the Load list; independent of `SaveManager` so it is
  testable with no scene at all.
- `SaveManager.PendingLoad` — set by Continue/Load before the scene loads, consumed by
  the `SaveManager` that wakes up in the destination scene's own `Start()`. This is how
  a save gets applied to a scene the menu itself never touches.
- `RecoveryMessageUI` — shows the exact section 32 sentence when a corrupt save is
  replaced by its backup, and a shorter one when a save fails outright. Added to
  `Avarsha.unity` and `Test.unity`'s canvases.
- Pause panel gained Resume, Save and Main Menu buttons in both gameplay scenes
  (`PauseMenu.OnSaveButtonPressed`, `OnMainMenuButtonPressed`).
- 3 EditMode tests for `SaveBrowser` and 1 PlayMode test proving `PendingLoad` survives
  a real destroy-and-recreate of `SaveManager`.

### Fixed

- **Duplicate singletons destroyed their whole GameObject instead of just themselves.**
  `GameManager`, `GameSceneManager`, `SettingsManager`, `WorldState` and
  `CheckpointManager` all share a `GameSystems` object with scene-scoped systems
  (`QuestManager`, `MemoryManager`, `DialogueRunner`, and — until this task — even
  `SaveManager`). Their duplicate-detection called `Destroy(gameObject)`, so the moment
  a scene was revisited after the persisted originals from an earlier scene already
  existed, the *entire* `GameSystems` object was torn down — silently deleting the new
  scene's quest, memory, dialogue and checkpoint systems along with it. Nothing
  exercised this before TASK 008 because nothing ever returned to a previously loaded
  scene. All five now call `Destroy(this)`.
- **`SaveManager` was sharing a GameObject with `WorldState`/`GameManager`.**
  `WorldState.Awake` (and `GameManager.Awake`) call `DontDestroyOnLoad` on their own
  GameObject to survive a normal level transition; a `SaveManager` sitting on the same
  object was dragged into that persistence even though it never calls
  `DontDestroyOnLoad` itself. A persisted `SaveManager`'s `Start()` — where
  `PendingLoad` is consumed — only ever runs once per application session, so Continue
  and Load silently stopped applying the save the moment the player went back to a
  scene they had already visited, while still reporting success. `SaveManager` now
  lives on its own GameObject in `Avarsha.unity` and `MainMenu.unity`, so it is
  destroyed and recreated fresh by every scene load, the way its `PendingLoad`
  mechanism assumes.

## Unreleased — TASK 007: Combat completion and HUD

Block, parry, lock-on, combo chains and the finisher, per SPEC.md sections 13, 14 and
15; the HUD, per section 42; and `ROADMAP.md`.

### Added

- `GuardController` — block and parry on one input. The press opens a parry window
  (0.2s, perfect within 0.08s), holding past it is a block. Parry deflects any attack
  including heavies and publishes `ParryEvent`; a perfect parry restores divine energy.
  Block absorbs at a stamina cost per point of damage; running out, or an unblockable
  heavy, breaks the guard and stuns the player for 0.8s. Environmental damage is
  never guarded. Windows scale with `Difficulty.Modifiers.PlayerTimingWindow`.
- `IDamageGuard` and `HealthComponent.Guard` — the guard gets first look at damage,
  after the attack id is registered so a parried swing cannot land later from another
  collider.
- `EnemyStagger` listens for `ParryEvent` and staggers itself when it is the attacker;
  entering Stagger cancels the swing in flight. Combat still does not reference AI.
- `ComboChain` and `ComboTracker` — the seven section 14 chains as a table and a
  suffix matcher over recent inputs, longest chain wins. Dodge and parry are recorded
  as steps, so Dodge→Light and Parry→Heavy work. Replaces the TASK 002 swing counter.
- `AttackType.Finisher` — a light attack against an enemy at or below 20% health,
  in range and in front (lock-on target first), becomes an unblockable lethal swing.
- `LockOnController` — acquires the best hurtbox owner in view by angle-weighted
  distance, drives `PlayerController.FacingTarget` (strafing) and
  `PlayerCamera.LookTarget`, drops on death or beyond 20 m, toggles off on a second
  press.
- `DivineEnergyComponent` — the resource abilities will spend; starts empty. Saved and
  restored through `PlayerStatsData.DivineEnergy`.
- `HudUI` and a HUD under `UI_Canvas` in both scenes: health, stamina and divine
  energy bars each with a text label (nothing by colour alone, section 43), the lock-on
  target's name, and a flash for combos, parries and guard breaks. Hideable via
  `SetVisible`.
- Input: `Guard` (Q / left shoulder) and `LockOn` (middle mouse / right stick press).
- Events: `ParryEvent`, `AttackBlockedEvent`, `GuardBrokenEvent`, `LockOnChangedEvent`,
  `ComboPerformedEvent`, `FinisherStartedEvent`.
- `ROADMAP.md` — the ordered task checklist; the next unticked task starts without
  being asked.
- 18 EditMode tests in `CombatActionTests.cs` and 7 PlayMode tests in
  `CombatActionPlayModeTests.cs`.

### Changed

- `WeaponController.TrySwing` takes a damage multiplier instead of a combo index, and
  knows the finisher's timings. `CurrentAttack` and `BaseDamage` exposed.
- `CombatController` owns the guard input, the finisher decision and the combo
  tracker; dodge i-frame length now scales with difficulty. `Configure` unchanged;
  `ConfigureCombos` added.
- `PlayerDeath` also disables `GuardController` and `LockOnController` while dead.
- `UI_Canvas` in both scenes renders in Screen Space – Camera (plane 0.5) so it
  appears in front of the camera in the Scene view instead of as a 1920×1080 sheet at
  the origin; the scaler matches width and height equally so a short Game view panel
  no longer clips it.
- `GuardController`, `LockOnController` and `DivineEnergyComponent` added to the
  player in `Avarsha.unity` and `Test.unity`.
- Deprecated `FindObjectsByType<T>(FindObjectsSortMode)` overloads replaced in
  `SaveManager` and `EnemyAiPlayModeTests` (pre-existing) and the two new sites.

### Verified

Checked against a running Editor, not by inspection:

- Compiles with 0 errors and 0 warnings.
- EditMode suite: 111 tests, 111 passed (18 new).
- PlayMode suite: 40 tests, 40 passed (7 new), in 50.7s, first run.
- Parry against a live enemy: the enemy staggered mid-swing, the player took no damage,
  the parry was recorded as a combo step.
- Block against a live enemy: no damage, 15 stamina for a 25-damage hit, no stagger.
- Light→Light→Heavy on a dummy dealt exactly 1.6× base heavy damage; a lone heavy dealt
  base damage.
- Finisher: a healthy dummy took a light attack; the same dummy at 15% took a finisher
  and died.
- Avarsha entered Play mode with the new components and HUD and logged no warnings or
  errors; the HUD was screenshotted showing all three bars.

## Unreleased — TASK 006: Save system

Saving, loading and the corruption rules, per SPEC.md sections 31, 32 and 33.

### Added

- `Assets/Scripts/Save/`: `SaveData`, `SaveSerializer`, `SaveMigration`, `SaveStorage`,
  `SaveManager`, `SaveEvents` and `ISaveParticipant`.
- `SaveData` carries every field SPEC.md section 31 names. Six of them are reserved and
  written empty, because the systems behind them do not exist; see `KNOWN_ISSUES.md`.
- Writing follows the spec's four steps literally — temporary file, validate by reading
  it back off disk, move the current save aside as the backup, then put the new one in
  its place. A failure at any step leaves every existing file untouched.
- Reading falls back to the backup when the primary fails validation, raises
  `SaveRecoveredFromBackupEvent` carrying SPEC.md section 32's sentence verbatim, and
  quarantines the bad file as `*.corrupt-<timestamp>` rather than deleting it.
- Validation is an envelope with an FNV-1a checksum plus a plausibility pass, so a file
  that parses but cannot describe a real game is rejected too.
- `SaveManager` refuses to save during a conversation or while the player is dead
  (SPEC.md section 31: never save during a critical state transition), and auto-saves
  when a checkpoint is activated.
- `ISaveParticipant` — a system can save data `SaveData` has no field for without the
  save system referencing it. Implementers are found in the loaded scenes at save time.
- `SaveManager` added to `GameSystems` in `Avarsha.unity`.
- 15 EditMode tests in `SaveTests.cs` and 8 PlayMode tests in `SavePlayModeTests.cs`,
  covering all five cases SPEC.md section 53 names for saves.

### Changed

- Snapshot and restore seams on the systems a save has to read: `WorldState.Flags`,
  `Counters` and `SetCounter`; `QuestManager.FinishedQuests`, `RestoreQuest` and
  `ClearAllProgress`; `QuestProgress.Counts`, `CompletedObjectives` and `Restore`;
  `MemoryManager.States`, `RestoreState`, `RestoreIntegrity01` and `ClearAll`;
  `HealthComponent.RestoreTo` and `StaminaComponent.RestoreTo`.
- Restoring is deliberately silent. A load replays no quest, memory or damage events,
  because the player already lived through those beats and re-firing them would grant
  their flags and rewards a second time.

### Verified

Checked against a running Editor, not by inspection:

- Compiles with 0 errors and 0 warnings.
- EditMode suite: 93 tests, 93 passed (15 new).
- PlayMode suite: 33 tests, 33 passed (8 new), in 43.6s.
- Round trip: a part-finished quest, a discovered memory, world flags and counters, the
  player's position, health and difficulty all survive being saved, wrecked and loaded.
- Backup recovery: with the primary overwritten by garbage, the load came back from the
  backup holding the save before last, and announced it in the specified words.
- No overwrite on failure: a save that fails read-back validation leaves the previous
  file byte-for-byte identical and removes its own temporary file.
- Refusals: saving mid-conversation and over a corpse both fail and write nothing.

## Unreleased — TASK 005: PlayMode tests

Automated coverage for everything that only fails once the game is running, per
SPEC.md section 53.

### Added

- `Assets/Tests/PlayMode/` — a second test assembly, `Game.Tests.PlayMode`, which
  unlike the EditMode one references `Unity.AI.Navigation` so tests can bake a NavMesh.
- `TestArena` — builds a throwaway arena per test: floor, runtime-baked NavMesh, walls,
  a player, dummies, enemies, patrol routes, groups and the manager singletons. Every
  wait in the suite goes through `TestArena.Until`, which has a deadline, so a broken
  test fails with a message instead of hanging the run.
- `CombatPlayModeTests` — 7 tests: swing timing across windup/active/recovery, damage
  through a real physics query, one swing landing once on a target with three
  hurtboxes, the dodge invulnerability window opening and closing, dodge cancelling a
  swing, a hazard volume ticking, and death disabling controls then respawning at the
  active checkpoint.
- `EnemyAiPlayModeTests` — 12 tests on a baked NavMesh: patrolling unprompted, chasing,
  a wall blocking line of sight, the telegraph preceding the damage window, damage in
  reach, the leash breaking off a visible target, stagger interrupting a swing, death
  ending all action, a destination off the NavMesh failing rather than silently
  succeeding, direct steering with no usable agent, the group attack limit, and no
  friendly fire.
- `ProgressionPlayModeTests` — 6 tests: a quest started by walking into a trigger,
  objectives advancing as targets actually die, a revived-and-rekilled target not
  completing a quest twice, re-giving an active quest not resetting progress, a memory
  pickup granting once under repeated interaction, and critical memories surviving an
  attempt to forget them.

### Changed

- `CombatController.Configure` — a test seam for supplying an `InputActionAsset`
  without the Inspector. The component disables itself in `Awake` when it has none, so
  a test-built player cannot drive it otherwise.

### Closed

- The leash range is now proven as a behaviour, not only as a decision: an enemy with a
  6m leash and 60m sight breaks off while the player is still in plain view.
- The telegraph is now observed firing. `Attack_TelegraphsBeforeTheHitboxOpens` measures
  the wind-up and asserts the hitbox stays shut throughout it.
- The EditMode-only coverage gap, carried since TASK 002, is closed for combat and
  narrowed for TASK 003. What remains uncovered is recorded in `KNOWN_ISSUES.md`.

### Verified

Checked against a running Editor, not by inspection:

- Compiles with 0 errors and 0 warnings.
- PlayMode suite: 25 tests, 25 passed, in 41.5s.
- EditMode suite: 78 tests, 78 passed — unchanged by the `CombatController` seam.

## Unreleased — TASK 004: Enemy AI

Enemy behaviour, per SPEC.md sections 16 and 17.

### Added

- AI in `Assets/Scripts/AI/`: `EnemyState`, `EnemySenses`, `EnemyController`,
  `EnemyPerception`, `EnemyNavigator`, `EnemyCombatant`, `EnemyStagger`,
  `EnemyGroup`, `PatrolRoute`, `EnemyArchetype`, `AIEvents`. All ten behaviours from
  SPEC.md section 17 have a state; the transition table is the pure function
  `EnemyController.Decide`.
- `Difficulty` in `Assets/Scripts/Core/` — SPEC.md section 44's four modes, scaling
  enemy damage, telegraph length, attack cooldown and how many enemies may attack at
  once. Deliberately not health: sections 15 and 44 forbid that.
- Poise, stagger and knockback, closing the TASK 002 limitation that `DamageData`
  carried a hit direction nothing consumed.
- `QuestTarget` in `Assets/Scripts/Quests/` — gives `ObjectiveType.DefeatEnemy` a
  driver for the first time.
- Content: three archetypes (`Forgotten Soldier`, `Ash Creature`, `Stone Guardian`)
  in `Assets/Data/Enemies/`, and quest `Q002 The Ash at the Gate`.
- `Avarsha.unity`: an `Encounter_Ruins` group of three enemies on a shared patrol
  route, a `Trigger_RuinAmbush` that starts `Q002`, and a baked `NavMeshSurface`.
- 33 EditMode tests in `Assets/Tests/EditMode/EnemyAiTests.cs`.

### Changed

- `Faction` (`Neutral`, `Player`, `Hostile`) on `Hitbox` and `Hurtbox`. `Neutral` is
  the default and opts out of the check, so everything authored before factions
  behaves unchanged.
- `Hitbox` gained `SetOwner`, and `EnemyCombatant` calls it, so an enemy parented
  under an encounter object cannot damage itself through a shared `transform.root`.
- `LocationTrigger` can now start a quest by id as well as report an objective.
- `SettingsManager` persists the difficulty mode and applies it on load.

### Fixed

Three defects found by the first play-mode run, all fixed and re-verified:

- **Patrolling enemies never moved.** `Decide` sent any enemy further than
  `homeArrivalDistance` from its spawn back home — including one walking its beat, so
  it flipped Patrol↔ReturnHome every frame and thrashed between two destinations. A
  patrolling enemy with a route now stays patrolling.
- **Enemies killed each other.** Two enemies swinging at the same player stand inside
  each other's hitboxes, and `Hitbox` refused only its own owner's hurtboxes. Fixed by
  the faction check above.
- **Enemies could damage themselves.** An enemy parented under an encounter object
  shared a `transform.root` with its allies, so its own hurtbox was no longer
  recognised as its own.

### Verified

Checked against a running Editor, not by inspection:

- Compiles with 0 errors and 0 warnings; the play-mode run produced no errors from
  game code.
- EditMode suite: 78 tests, 78 passed (37 new, 41 from TASK 003).
- Patrol: the Forgotten Soldier walked 3.8m of its route in four seconds unprompted.
- Detection: standing in the Stone Guardian's sight cone moved it to `Chase` and
  raised 3 alert events as the group was told.
- Attack: an enemy closed and damaged the player, 93 to 83 health.
- Stagger: a 70-damage blow against 60 poise moved the guardian to `Stagger`,
  cancelled its swing in progress, and `IsAttacking` went false.
- Group coordination: with three enemies engaged and a limit of 2, exactly 2 were in
  `Attack`.
- No friendly fire: an ally held 35 health before and after a group fight.
- Giving up: 3.0s after losing line of sight the soldier moved to `Search`.
- Quest: `Trigger_RuinAmbush` started `Q002`, and three kills completed it with both
  `QUEST_Q002_COMPLETE` and `RUIN_GUARDS_DEFEATED` set — the first time
  `ObjectiveType.DefeatEnemy` has completed anything.

## Unreleased — TASK 003: Avarsha

The hub city, and the first pass at the systems that carry the story, per SPEC.md
section 67.

### Added

- `WorldState` in `Assets/Scripts/Core/` — the central world-state model (SPEC.md
  section 71): named flags and counters, published as events. `WorldFlags` names the
  flags the engine reads.
- Interaction in `Assets/Scripts/World/`: `Interactable`, `PlayerInteractor`,
  `LocationTrigger`, `SupernaturalEvent`.
- Dialogue in `Assets/Scripts/Dialogue/`: `DialogueData`, `DialogueGraph`,
  `DialogueRunner`, `NpcInteractable`, `DialogueEvents`. Data-driven per SPEC.md
  section 22 — conversations are ScriptableObjects, not code.
- Quests in `Assets/Scripts/Quests/`: `QuestData`, `QuestDefinition`, `QuestProgress`,
  `QuestManager`, `QuestEvents`.
- Memories in `Assets/Scripts/Memory/`: `MemoryFragment`, `MemoryManager`,
  `MemoryPickup`, `MemoryEvents`, including the 0..1 integrity model and the
  protection of `Critical` memories from SPEC.md section 20.
- UI: `DialogueUI`, `InteractionPromptUI`, `QuestTrackerUI`, `MemoryDiscoveryUI`.
- `PlayerDialogueLock` — suspends movement, camera, combat and interaction during a
  conversation and restores each to the state it was actually in.
- `Assets/Scenes/Avarsha.unity` — the hub, with all eight districts from SPEC.md
  section 24 blocked out, Queen Amara, Dev, Mira, the ruin stone and its memory
  fragment, the city gate and ruins triggers, and the first supernatural event.
- Content in `Assets/Data/`: three dialogue graphs, quest `Q001 The Queen's Charge`,
  and memory `MEM_001 The Name Beneath the Stone`.
- An Interact action bound to `E` and gamepad d-pad up.
- 24 EditMode tests in `Assets/Tests/EditMode/AvarshaTests.cs`.

### Changed

- `WorldState`, `DialogueRunner`, `QuestManager` and `MemoryManager` resolve their
  `Instance` on first access via the new `SceneSingleton`, not only in `Awake`. Unity
  does not guarantee `Awake` order and does not call it in EditMode, so the previous
  pattern could hand out null in a correctly built scene. Same lesson as `Hurtbox` in
  TASK 002.
- `Game.Runtime` now references `UnityEngine.UI`.

### Verified

Checked against a running Editor, not by inspection:

- Compiles with 0 errors and 0 warnings; the play-mode run produced 0 errors.
- EditMode suite: 41 tests, 41 passed (24 new, 17 from TASK 002).
- Enter Avarsha: crossing the gate set `ENTERED_AVARSHA`.
- Speak to Amara: the conversation opened at `AMARA_INTRO`.
- Receive quest: 3 nodes later `Q001` was Active with `QUEENS_CHARGE_GIVEN` set and
  `REACH_RUINS` as the current objective.
- Explore: reaching the ruins completed `REACH_RUINS` and advanced to `FIND_MEMORY`.
- Find memory: `MEM_001` discovered in state `Known`, objective and flag both set.
  Interacting a second time left the count at 1 — no duplicate grant.
- Return: the conversation opened at `AMARA_RETURN`, and `Q001` finished Completed with
  `QUEST_Q001_COMPLETE` and `RETURNED_TO_AMARA` set.
- First supernatural event: played to completion 3.5 s later and set
  `FIRST_SUPERNATURAL_EVENT`.

## Unreleased — TASK 002: Combat Foundation

Damage, resources and death, per SPEC.md section 66.

### Added

- Combat core in `Assets/Scripts/Combat/`: `DamageData`, `HealthComponent`, `StaminaComponent`, `Hitbox`, `Hurtbox`, `WeaponController`, `CombatController`, `EnemyHealth`, `PlayerDeath`, `Checkpoint`.
- `CheckpointManager` — holds the current respawn point so `Checkpoint` and `PlayerDeath` need not know about each other.
- `DamageVolume` — hazard trigger. Added beyond the specified list because nothing else in this task could damage the player; see `KNOWN_ISSUES.md`.
- `CombatEvents` — `EventBus` payloads for damage, death, respawn and checkpoint activation.
- Light attack, heavy attack and dodge bound in `PlayerControls.inputactions` (mouse buttons / left Ctrl, and gamepad face buttons).
- `PlayerController.BeginDodge` — combat supplies the dodge direction, locomotion applies the motion, so all `CharacterController.Move` calls stay in one place.
- Scene content in `Test.unity`: player hurtbox and Astra Blade hitbox, `Enemy_TrainingDummy`, `Checkpoint_01`, `Hazard_AshPit`.
- First automated tests: `Assets/Tests/EditMode/CombatTests.cs`, 15 EditMode tests over the damage, stamina and checkpoint rules.

### Changed

- Runtime code now compiles into a `Game.Runtime` assembly instead of `Assembly-CSharp`, so the test assembly can reference it. Editor scripts using reflection must use the new assembly name.
- `Hitbox` and `Hurtbox` resolve their references on demand rather than only in `Awake`, which also fixes them being unconfigured when added or reparented at runtime.

### Verified

Checked against a running Editor, not by inspection:

- Compiles with 0 errors and 0 warnings; play-mode run produced 0 errors and 0 warnings.
- EditMode suite: 15 tests, 15 passed.
- Player attacks: one light swing took the dummy from 60 to 48 health, exactly the configured light damage.
- Enemy dies: dead after 5 accepted swings.
- No duplicate damage: 5 accepted swings produced exactly 5 damage events.
- Checkpoint idempotence: two `Activate()` calls raised exactly 1 `CheckpointActivatedEvent`.
- Player dies: health to 0 disabled `PlayerController` and published the death.
- Checkpoint restores player: respawned at (-3, 1.08, 5) — the checkpoint position — at full health with controls re-enabled.

## Unreleased — TASK 001: Project Foundation

Unity 6 (6000.6.2f1) project on URP, scaffolded per SPEC.md sections 46 and 65.

### Added

- Unity project: `Assets/` folder structure (SPEC.md section 46), `Packages/manifest.json` and `ProjectSettings/` generated from the `com.unity.template.urp-blank` template, URP settings in `Assets/Settings/`.
- Core systems: `GameLogger` (categorized logging, section 51), `EventBus` (generic pub/sub, section 49), `GameManager` (game state + pause), `GameSceneManager` (scene loading), `SettingsManager` (persisted user settings).
- Player: `PlayerController` (move/sprint/jump/gravity on `CharacterController`), `PlayerCamera` (third-person orbit with spherecast collision avoidance, section 38).
- UI: `PauseMenu` (Escape toggles overlay and `Time.timeScale`).
- `Assets/Input/PlayerControls.inputactions` — Move, Look, Jump, Sprint, Pause, bound for keyboard/mouse and gamepad.
- `Assets/Scenes/Test.unity` — Phase 0 test scene, registered in Build Settings.
- Project docs: `SPEC.md`, `README.md`, `ARCHITECTURE.md`, `KNOWN_ISSUES.md`, `ASSET_LICENSES.md`, `.gitignore`.
- `com.unity.pipeline` so the Unity CLI can drive the Editor for scripted scene work.

### Configuration

- Product name set to "The God Who Was Forgotten"; default resolution 1920x1080 (section 45).
- Active input handling set to the Input System package.

### Verified

Checked against a running Editor rather than by inspection:

- Project compiles with 0 errors and 0 warnings.
- Play mode runs clean; logs appear as `[GAME] GameManager initialized.` and `[GAME] State changed: Boot -> Playing`.
- Holding W moves the player 0 → 4.494 on Z (≈4 m/s walk speed) with Y pinned at 1.080, so grounding is stable.
- Camera follows at a constant 5.03 distance.
- Escape pauses (`Playing`→`Paused`, `timeScale` 1→0, overlay shown) and toggles back.
