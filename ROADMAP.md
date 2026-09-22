# ROADMAP

The ordered task list for THE GOD WHO WAS FORGOTTEN, derived from SPEC.md sections 60
(development phases), 61 (vertical slice) and 42 (required screens). One task is one
system (SPEC.md section 58). A task is ticked only when it compiles with no errors,
its tests pass, and `CHANGELOG.md`, `KNOWN_ISSUES.md` and `ARCHITECTURE.md` are updated.

**Working rule:** when a task closes, the next unticked task starts without waiting to
be asked. Reordering is a decision for the owner; say so and this file changes.

## Done

- [x] **TASK 001 — Project foundation.** Unity 6 project, folder layout, input, scene
  management, player controller, camera, pause UI, logging, settings, Git. *(Phase 0)*
- [x] **TASK 002 — Combat foundation.** Health, stamina, hitbox/hurtbox, Astra Blade
  light/heavy, dodge with i-frames, death and checkpoint respawn. *(Phase 1, part)*
- [x] **TASK 003 — Avarsha.** Hub blockout, NPCs, dialogue graphs, first quest, memory
  fragment, supernatural event, interaction and UI panels. *(Phase 2, part)*
- [x] **TASK 004 — Enemy AI.** All ten section 17 behaviours as a state machine,
  perception, navigation with fallback, telegraphs, poise/stagger, groups, difficulty.
- [x] **TASK 005 — PlayMode tests.** `TestArena` and a second suite covering what
  EditMode cannot: coroutines, physics, navigation.
- [x] **TASK 006 — Save system.** Envelope + checksum, four-step atomic write, backup
  fallback, quarantine, migration hook, auto-save on checkpoint. *(sections 31–33)*
- [x] **TASK 007 — Combat completion.** Block, parry (with perfect parry and divine
  energy), lock-on, section 14 combo chains, finisher, difficulty-scaled timing windows,
  and the HUD (health, stamina, divine energy, lock-on, combo). *(Phase 1 complete)*
- [x] **TASK 008 — Menus and save/load UI.** Main Menu, New Game, Continue, Save/Load,
  Settings, Controls and Credits screens (section 42); manual save wired to
  `SaveManager` via `SaveBrowser`; the section 32 recovery message actually shown;
  difficulty selectable in game. Also fixed a latent bug the menu's scene round-trips
  exposed: duplicate scene singletons were destroying their whole shared GameObject
  (see `KNOWN_ISSUES.md`).
- [x] **TASK 009 — World save identity.** `SaveIdentity` and `WorldObjectState` give
  enemies and pickups per-object save state via namespaced `WorldState` flags; hazards
  need none (stateless by design). `CheckpointManager.RestoreActiveCheckpoint` closes
  the TASK 008 checkpoint-restoration gap. Section 55's item/door anti-softlock rules
  are noted as not-yet-applicable (no inventory or doors exist).
- [x] **TASK 010 — Quest log and memory archive.** `JournalUI`'s two tabs (section 42);
  `MemoryManager.Corrupt` makes corruption a real player action from the archive screen
  (sections 20–21), ahead of the ability that will trigger it for real. Verified rather
  than rebuilt: a memory already changes dialogue/world state since TASK 003
  (`FIRST_MEMORY_FOUND` gates `Dialogue_Amara`/`Dialogue_Mira` nodes) — Phase 4's
  acceptance line was already satisfied.
- [x] **TASK 011 — Divine ability framework and Ember Step.** `CombatController.TryAbility`
  spends divine energy and records `ComboStep.Ability` (section 13 level 4, section 61's
  "one divine ability"); `EmberStepUsedEvent` lets `MemoryManager` apply the ability's
  SPEC-mandated memory cost without Combat referencing Memory. All nine section 13
  actions now exist. Available from the start rather than gated behind Agniya, which
  does not exist yet (KNOWN_ISSUES.md).
- [x] **TASK 012 — Puzzle system and first fire puzzle.** `IPuzzleElement` +
  `PuzzleController` + `PuzzleGate` (section 26) — reusable so later temples add
  mechanics (a new element type), not systems. First instance: three `FireBrazier`s
  with a visible burn-timer in Avarsha's temple district, sealing a passage
  (section 61's "one puzzle"). The gate currently opens onto nothing — no reward yet
  (KNOWN_ISSUES.md).
- [x] **TASK 013 — Boss framework and mini-boss.** `BossController` (section 18) layers
  three-phase difficulty onto the existing enemy components — later phases move and
  attack faster, never just gain HP — and boss health now shows on the HUD. First
  instance: the Temple Guardian, a mini-boss in a placeholder arena near Avarsha, with
  checkpoints either side of the fight and a memory-fragment reward. The unique
  arena/cinematic moment/victory sequence/music are placeholders and hooks pending
  TASK 014/018 (KNOWN_ISSUES.md).
- [x] **TASK 014 — Cinematic system and the first cinematic.** `CinematicPlayer`
  (section 69) is skippable and subtitled, and finally gives `GameManager`'s
  `GameState.Cutscene` — unused since TASK 001 — something to do. First cinematic:
  Nirvaan speaks after the first memory is found (section 68, scenes 9-10). Section
  61's "one cinematic". No camera control, actors or audio yet (KNOWN_ISSUES.md).
- [x] **TASK 015 — Agniya temple section.** A complete temple, entrance to boss
  (Phase 3), reusing the puzzle (TASK 012) and boss (TASK 013) frameworks rather than
  building temple-specific systems: a second fire puzzle, an `Ash Creature`/`Divine
  Guardian` encounter, a new `MemoryToll` mechanic for Phase 3's "memory cost", and
  `Boss_Agniya` — the project's first true `Boss`-class fight — guarding a divine-mark
  memory reward. Built inside Avarsha rather than a separate scene; see
  KNOWN_ISSUES.md for why and what a real second scene needs first.
- [x] **TASK 016 — Inventory, progression and skill tree.** Sections 28-30 and their
  screens (`ProgressionUI`); fills the `Inventory`/`Abilities` reserved save fields.
  Skill points come from real milestones (quest completion, boss defeats); only one
  skill per branch is mechanically wired so far, the other eight are real but inert
  (KNOWN_ISSUES.md). Along the way, fixed a save-restore ordering bug that would have
  reset a skilled-up player's max health on every load.
- [x] **TASK 017 — Accessibility, settings and remapping.** A real, working
  subset of section 43 rather than stubs: subtitle size/background, text
  scaling, a non-colour combat telegraph cue, camera shake and screen-effect
  toggles tied to real triggers, aim/lock-on assistance, and full
  controller/keyboard remapping (`RemapUI`). Also closes the TASK 001 gamepad
  look-speed bug and gives gamepad users a first-selected control on
  Journal/Progression/Pause. Deferred: motion blur (no effect exists to gate)
  and in-game/Pause-menu access to Settings (Main Menu only for now) —
  KNOWN_ISSUES.md.
- [x] **TASK 018 — Placeholder audio, animation and VFX pass.** 7 of section
  36's VFX categories and 5 of section 40's combat cues, all generated in code
  (`VfxSpawner`/`SfxSpawner`) per section 78 rather than imported, wired to
  real triggers (parries, guard breaks, a brazier lighting, puzzle solves,
  memory pickups, boss phase transitions). `PlaceholderAnimator` gives the
  player a cosmetic, blended tilt/lean/spin/flinch/collapse for section 39's
  animation list, reading combat's existing real timing rather than changing
  it. Deferred, with reasons in KNOWN_ISSUES.md: attack timings were not moved
  to animation events (no rig exists to author matching clips against);
  environmental ambience/music; several VFX categories with no content yet to
  trigger them; enemy locomotion animation.
- [x] **TASK 019 — Vertical slice acceptance.** Every section 61 item verified
  present and working in a live Play-mode session rather than by inspection
  alone: player/camera/combat, one enemy and one mini-boss (`Boss_TempleGuardian`),
  the Avarsha hub, a quest, a cinematic, the memory mechanic, the seven-brazier
  puzzle (lit live, solved), Ember Step (spent live, energy debited), the
  Agniya temple section, save/damage/load (round-tripped live, health restored
  correctly), settings (Main Menu panel opens), the pause menu, and the full
  Main Menu → difficulty select → Avarsha scene transition (including the
  duplicate-`GameManager`-destroyed guard firing correctly, not an error). No
  new bugs found: console stayed clean of new errors/exceptions across the
  whole session, only the one pre-existing corrupt-save test artifact and the
  expected duplicate-manager warning. No code changes were needed. The
  project's open gaps are exactly the ones already tracked in
  KNOWN_ISSUES.md's per-task sections — none of them block a fun, playable
  vertical slice, per section 62.

## Later (after the vertical slice)

- Phase 5 — remaining six temples, each with one unique mechanic.
- Phase 6 — celestial realm, the seven gods, The First Memory, final boss, ending
  system (section 72), ending cinematics.
- Phase 7 — polish, optimisation (section 45), localisation (section 73), telemetry
  (section 76).
