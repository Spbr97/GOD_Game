# CHANGELOG

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
