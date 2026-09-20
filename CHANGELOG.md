# CHANGELOG

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
