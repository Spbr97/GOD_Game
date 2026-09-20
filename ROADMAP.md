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

## Next

- [ ] **TASK 008 — Menus and save/load UI.** Main Menu, New Game, Continue, Save/Load,
  Settings, Controls and Credits screens (section 42); manual save slots wired to
  `SaveManager`; the section 32 recovery message actually shown; difficulty selectable
  in game. Closes the largest TASK 006 limitation.
- [ ] **TASK 009 — World save identity.** Per-object save ids so enemies, pickups,
  hazards and checkpoints restore with the player; respawn/world-reset rules (section 55).
- [ ] **TASK 010 — Quest log and memory archive.** The two screens (section 42), memory
  states and corruption made consumable (sections 20–21), a memory changing at least one
  dialogue/world state (Phase 4 acceptance).
- [ ] **TASK 011 — Divine ability framework and Ember Step.** The `Ability` combo step,
  divine energy spend, first ability (section 13 level 4, section 61).
- [ ] **TASK 012 — Puzzle system and first fire puzzle.** Section 26; reusable puzzle
  pieces so later temples add mechanics, not systems.
- [ ] **TASK 013 — Boss framework and mini-boss.** Three-phase bosses (section 18), boss
  health on the HUD, one mini-boss encounter in or near Avarsha.
- [ ] **TASK 014 — Cinematic system and the first cinematic.** Section 68's Act I
  sequence and section 69's direction; skippable, subtitled.
- [ ] **TASK 015 — Agniya temple section.** Phase 3: environment blockout, fire VFX
  placeholders, fire puzzles, enemy variants, boss, reward, memory cost.
- [ ] **TASK 016 — Inventory, progression and skill tree.** Sections 28–30 and their
  screens; fills the reserved save fields.
- [ ] **TASK 017 — Accessibility, settings and remapping.** Section 43 in full, plus the
  gamepad control scheme pass flagged since TASK 002.
- [ ] **TASK 018 — Placeholder audio, animation and VFX pass.** Sections 36, 39, 40 with
  placeholder assets (section 78); attack timings moved to animation events.
- [ ] **TASK 019 — Vertical slice acceptance.** Every section 61 item checked in a
  running build; section 62 criteria; bug fixing.

## Later (after the vertical slice)

- Phase 5 — remaining six temples, each with one unique mechanic.
- Phase 6 — celestial realm, the seven gods, The First Memory, final boss, ending
  system (section 72), ending cinematics.
- Phase 7 — polish, optimisation (section 45), localisation (section 73), telemetry
  (section 76).
