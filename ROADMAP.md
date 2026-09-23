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

- [x] **TASK 020 — The three missing project documents (sections 81, 82).**
  `GAME_DESIGN.md`, `STORY_BIBLE.md` and `TEST_PLAN.md`. The story bible
  separates canon transcribed from SPEC.md (authoritative) from canon invented
  across TASK 003–018 (provisional), states section 82's "propose it first"
  rule at the top, and ends with four story questions the implementation has
  already answered without anyone deciding them — including a direct conflict
  with section 8.1 over Agniya's boss name. Those need the author, not an agent.
- [x] **TASK 021 — Debug mode (section 52).** All seventeen developer tools:
  `DebugCommands` (twelve headless statics that drive the real systems, so a
  state reached with them is a state the game can be in), `DebugOverlay` (five
  read-only readouts), `DebugConsole` (IMGUI, backquote) and `DebugMode`, the
  gate. Two-layer gating: a compile-time `#if` so a release player has no
  console in the binary at all, plus a runtime switch that starts off.
- [x] **TASK 022 — Falling out of the world (section 50, edge cases 9 and 10).**
  `WorldBounds` (kill plane plus horizontal footprint; height alone is never an
  escape), `PlayerBoundsGuard` (back to the latest valid checkpoint, with a
  message and the fall speed cleared) and `EnemyBoundsGuard` (back to its
  authored home — put back, never deleted, per section 55). Sized into both
  gameplay scenes from their own geometry.
- [x] **TASK 023 — Boss arena escape (edge case 7, sections 55 and 56).**
  `BossArena` ends a fight the player walks out of: full health, phase 1, back
  to its start. One rule answering three requirements — the arena resets
  safely, chipping from outside stops paying, and walking away is allowed
  rather than punished. A grace period keeps a dodge across the line from
  ending an encounter.
- [x] **TASK 024 — The `[Dialogue unavailable]` fallback (section 50).** A
  missing graph now shows the exact specified string as a real, dismissable
  one-line conversation, while `Begin` still returns false so the caller's own
  consequences never fire for a conversation that did not happen.
  `NpcInteractable` no longer hides its prompt when it has no dialogue — a
  hidden prompt cannot display a fallback.
- [x] **TASK 025 — Device and window edge cases (20–24).** `DeviceWatcher`
  handles controller loss and return and window focus, pausing through
  `AutoPauseRequestedEvent` so there is always a menu on screen and nothing
  resumes by itself. Resolution, graphics quality and fullscreen added to the
  Settings screen, with a change made during a scene load deferred until the
  load finishes.
- [x] **TASK 026 — Map screen (section 42).** `MapUI` draws the scene's own
  markers — player with facing, checkpoints, people, objectives, discovered
  memories, bosses — each with a letter as well as a colour. Bound to M and
  D-pad up. Drawn from the scene rather than authored, so it cannot go out of
  date as the world changes.

  All seven came from the SPEC.md audit after TASK 019 and were the only
  requirements found with neither an implementation nor a roadmap entry. The
  residual limitations of each are in KNOWN_ISSUES.md, along with three gaps the
  second pass found: the motion blur toggle (section 43) has nothing to gate, a
  dozen section 54 edge cases are handled but untested, and colour-blind support
  is satisfied by convention rather than by a mode.

## Next — closing the SPEC audit backlog (see RESOLUTION_PLAN.md)

Triage of all 113 open `KNOWN_ISSUES.md` entries found 8 real bugs; the rest is
content, placeholders and deferred work with reasons. These tasks close the bugs and
the infrastructure gaps that let them hide. Ordered so the things that protect the
work come before the work.

### Tier 0 — let the project protect itself

- [x] **TASK 027 — Reconcile `KNOWN_ISSUES.md`.** Twelve entries describe work that
  shipped in TASK 004–018 and were never marked closed (scene-aware loading, the
  save/load menus, the corruption message being shown, the journal, difficulty in the
  settings panel, enemy AI, the section 53 test claim, the enemy archetype count, fire
  VFX, the dash's visuals, memory visualisation, persistence across a load). Close each
  naming the task that closed it; merge the three duplicate pairs; tag every remaining
  entry BUG / ARCHITECTURAL GAP / DESIGN DECISION / MISSING CONTENT / PLACEHOLDER /
  POLISH / DEFERRED. First, because every other task and every outside reader starts
  from this file — a stale log already caused one external analysis to propose
  rewriting a working save system.
- [x] **TASK 028 — Fix `Avarsha.unity` serialization.** The project is set to Force
  Text; the hub scene has been binary since its first commit, so its diffs are opaque
  and its merges impossible. Confirm the mode, re-save, commit alone with no other
  change so the one-time reformat is not mixed into a real diff.
- [x] **TASK 029 — Scene integrity tests.** Seven systems were wired into the scenes by
  editor script in TASK 020–026 and nothing would catch a nulled reference. Per shipped
  scene assert: managers present, NavMesh baked, player spawn, canvas references, and
  the `WorldBounds` / bounds guards / `BossArena` / `DeviceWatcher` / `MapUI` /
  `DevTools` wiring. Closes "nothing tests the shipped scenes" (TASK 005).
- [x] **TASK 030 — CI.** Both suites are run by hand and nothing records which commit
  last passed. GitHub Actions: compile → EditMode → PlayMode. Closes "nothing runs the
  tests automatically" (TASK 005).

### Tier 1 — the eight bugs

- [x] **TASK 031 — Hit detection sweep.** `Hitbox` samples overlaps once on activation
  then relies on `OnTriggerEnter`; a fast swing can cross a thin collider between
  physics steps and register nothing. Sweep between previous and current position each
  active frame, keeping the `AttackId` deduplication so one swing still cannot hit the
  same target twice.
- [x] **TASK 032 — Line of sight for lock-on and interaction.** Neither
  `LockOnController` nor `PlayerInteractor` casts anything, so both treat "in range" as
  "can be reached" — you can lock onto and talk to things through walls. One shared
  visibility helper, two callers. Also raises `PlayerInteractor`'s fixed 16-collider
  buffer off a hard-coded literal.
- [x] **TASK 033 — Directional guard and guard-break reaction.** A hit from directly
  behind is currently blocked exactly like one from the front, and a broken guard is a
  silent 0.8 s stun. Both were deferred pending feedback systems that now exist —
  `VfxSpawner`, `SfxSpawner` and `PlayerCamera.Shake` all shipped in TASK 017/018.
- [x] **TASK 034 — Act on the three decisions.** Requires answers first
  (RESOLUTION_PLAN.md section 4): what resets on death, relationships defined or
  deleted, motion blur built or the section 43 claim withdrawn. Relationship counters
  are currently written by `DialogueRunner` and read by nothing — the same
  dead-control failure as the Master Volume slider the TASK 019 audit found.

### Tier 2 — spec completeness

- [x] **TASK 035 — Quest objective adapters and rewards.** Six of ten `ObjectiveType`
  values have no driver, so most objective kinds cannot complete; `rewardsSummary` is a
  display string and the only reward mechanism. Add a `RewardDefinition` covering item,
  ability unlock, memory restoration, skill points and world-state change.
- [x] **TASK 036 — Dialogue validation command.** An editor menu item reporting
  duplicate ids, dangling `NextDialogueId` links, unreachable nodes and unknown flags or
  consequences, before runtime rather than after.
- [x] **TASK 037 — Memory integrity consequences.** Three of SPEC.md section 20's four
  effects still read nothing from `Integrity` — dialogue variation, incomplete
  flashbacks and NPC recognition. Also closes "memory corruption is a number nothing
  consumes".
- [x] **TASK 038 — Edge case test coverage.** SPEC.md section 54's cases 1–4, 8, 12–14,
  19 and 27–30 are handled in code but have no test naming them.

### Current implementation status (23 September 2026)

TASK 027–038 are implemented locally. Unity 6000.6.2f1 with the user's Personal license ran 180 EditMode and 130 PlayMode tests, all passing. Dialogue validation found 3 graphs and 0 errors or warnings. TASK 028 moved the embedded baked NavMesh to `AvarshaNavMesh.asset`, converted `Avarsha.unity` to YAML and was committed separately as `8a96485`; all 8 scene integrity checks pass after conversion. TASK 030's free GameCI workflow is pushed. Its first GitHub run could not start Unity tests because the repository has no `UNITY_LICENSE`, `UNITY_EMAIL` or `UNITY_PASSWORD` secrets; configure those Personal license secrets to activate remote CI. Manual gameplay and visual checks remain for release.

## Later (after the vertical slice)

- Phase 5 — remaining six temples, each with one unique mechanic.
- Phase 6 — celestial realm, the seven gods, The First Memory, final boss, ending
  system (section 72), ending cinematics.
- Phase 7 — polish, optimisation (section 45), localisation (section 73), telemetry
  (section 76).
