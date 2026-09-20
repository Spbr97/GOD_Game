# ARCHITECTURE

This document tracks the actual implemented architecture. Update it whenever a system changes (SPEC.md section 58, rule 12).

## Folder structure

```text
Assets/
  Art/
  Audio/
  Animations/
  Materials/
  Prefabs/
  Scenes/
  UI/
  VFX/
  Data/
    Dialogue/
    Quests/
    Memories/
  Scripts/
    Core/
    Player/
    Combat/
    AI/
    Quests/
    Dialogue/
    Memory/
    Inventory/
    Save/
    World/
    UI/
    Audio/
    VFX/
    Debug/
  Resources/
  Tests/
```

## Implemented systems

### Core (`Assets/Scripts/Core/`)

- `GameManager` — singleton, persists across scenes, holds top-level game state (`Boot`, `Playing`, `Paused`, `Cutscene`), exposes `Pause()` / `Resume()`.
- `GameSceneManager` — wraps `UnityEngine.SceneManagement` for scene loads (sync + async), logs scene transitions. Named `GameSceneManager` to avoid colliding with the engine's own `SceneManager`.
- `SettingsManager` — singleton, holds runtime settings (master volume, mouse sensitivity, invert-Y), persists to `PlayerPrefs` as JSON.
- `EventBus` — static generic publish/subscribe utility (`EventBus.Subscribe<T>`, `EventBus.Publish<T>`) so systems can react to state changes without direct references to each other (SPEC.md section 49).
- `WorldState` — the central world-state model (SPEC.md section 71): named boolean flags plus integer counters, published as `WorldFlagChangedEvent`. Deliberately a flat named store rather than one field per story beat, because dialogue, quests and memories reference flags by name from ScriptableObject data. `WorldFlags` names the flags the engine itself reads. Persists across scene loads.
- `Difficulty` — SPEC.md section 44's four modes and what each one changes: enemy damage, telegraph length, attack cooldown, how many enemies may attack at once, and player timing windows. Static rather than a component because every enemy reads it on every swing and an enemy spawned before a manager's `Awake` must still get a correct answer; `SettingsManager` owns persisting it. There is deliberately **no health multiplier** — SPEC.md sections 15 and 44 require difficulty to move behaviour and timing, not hit points.
- `SceneSingleton` — backs the `Instance` property of the scene-level managers, resolving on first access rather than only in `Awake`. See **Manager lookup** below.
- `GameLogger` — static categorized logging (`[GAME] [PLAYER] [COMBAT] [AI] [QUEST] [DIALOGUE] [MEMORY] [SAVE] [UI] [AUDIO] [PERFORMANCE] [ERROR]`, SPEC.md section 51).

### Player (`Assets/Scripts/Player/`)

- `PlayerController` — `CharacterController`-based movement (move, sprint, jump, gravity), reads input via the Input System through a serialized `InputActionAsset`, looking actions up by name. There is deliberately no generated C# wrapper class for the asset, so the actions can be re-authored in the Editor without a code-gen step.
- `PlayerCamera` — third-person orbit/follow camera with spherecast-based collision avoidance so it does not clip through geometry (SPEC.md section 38).

### Combat (`Assets/Scripts/Combat/`)

- `DamageData` — the value passed for one damage application: amount, type, source, hit point, direction, and an `AttackId` allocated per swing.
- `HealthComponent` — the single health pool used by the player and every enemy. All damage goes through `TakeDamage`, which is the one place that rejects self-damage, damage to the dead or invulnerable, and repeated attack ids.
- `StaminaComponent` — pool gating attacks and dodges, with a regeneration delay after each spend. `Regenerate` takes time as an argument so tests can step it deterministically.
- `Hitbox` — the damaging volume of a swing. Disabled except during an attack's active window; remembers which hurtboxes it touched during the current swing.
- `Hurtbox` — a collider that forwards damage to a `HealthComponent`, with a per-collider multiplier for weak points. Resolves its health pool lazily rather than only in `Awake`.
- `WeaponController` — owns swing timing (windup / active / recovery) and opens the hitbox for the active window only.
- `CombatController` — turns player input into light attacks, heavy attacks and dodges; owns stamina costs, the combo counter and the dodge invulnerability window.
- `EnemyHealth` — enemy reaction to the shared health pool: hit flash, death cleanup, despawn.
- `PlayerDeath` — player reaction to death: disables controls, then respawns at the current checkpoint.
- `Checkpoint` — idempotent respawn point. Only the first activation raises `CheckpointActivatedEvent`.
- `CheckpointManager` — singleton holding the current respawn point, so `Checkpoint` and `PlayerDeath` do not have to know about each other.
- `DamageVolume` — hazard trigger that damages what stands in it on a cadence.
- `CombatEvents` — the `EventBus` payloads: `DamageAppliedEvent`, `EntityDiedEvent`, `PlayerDiedEvent`, `PlayerRespawnedEvent`, `CheckpointActivatedEvent`.

#### How one hit flows

`CombatController` → `WeaponController.TrySwing` → (windup) → `Hitbox.Activate` → trigger or overlap → `Hurtbox.ApplyDamage` → `HealthComponent.TakeDamage` → `EventBus`.

`Faction` on `Hitbox` and `Hurtbox` decides who an attack is willing to hurt. `Neutral` is the default and opts out of the check entirely, so anything authored before factions existed — the player's weapon, the training dummy — behaves exactly as it did. Enemies are `Hostile`, which is what stops two of them swinging at the same player from killing each other.

Duplicate damage is prevented at two levels, because the two failure modes are different. `Hitbox` remembers the hurtboxes it has already touched, which stops repeated trigger callbacks within one swing. `HealthComponent` remembers recent attack ids, which stops two separate hurtboxes on the same entity both landing the same swing.

### World (`Assets/Scripts/World/`)

- `Interactable` — abstract base for everything the player can press Interact on, with shared flag gates (`requiredFlags` / `blockingFlags`) and a prompt line. NPCs and memory pickups derive from it so `PlayerInteractor` knows exactly one type.
- `PlayerInteractor` — finds the best interactable near the player and runs it. Selection is angle-weighted distance, not nearest-wins, so facing the thing you mean to talk to selects it. Publishes `InteractionTargetChangedEvent` so the prompt UI does no polling.
- `LocationTrigger` — a volume that reports a quest objective and/or sets a flag on entry. It does not know which quest it serves, so one volume can satisfy several.
- `SupernaturalEvent` — the scripted Act I sleep and bleeding-statue sequence (SPEC.md section 68, scenes 5 and 7). Starts when a named flag is set. `ApplyEndStateImmediately` reaches the same end state with no sequence, for loaded saves and tests.

### AI (`Assets/Scripts/AI/`)

All ten behaviours SPEC.md section 17 asks for exist as values of `EnemyState`.

- `EnemyArchetype` — one enemy class as data (ScriptableObject): vitals, poise, speeds, leash, perception ranges, attack timings, retreat threshold, placeholder tint. Difficulty is applied when a number is *read* (`TelegraphFor`, `DamageFor`, `CooldownFor`) rather than baked in, so changing difficulty mid-run takes effect on the next swing.
- `EnemyController` — the state machine. It owns transitions and per-state behaviour and nothing else.
- `EnemySenses` — everything the enemy knows on one tick, gathered before any decision.
- `EnemyPerception` — sight cone, hearing radius, line-of-sight raycast, and the last known target position that Chase and Search both steer to.
- `EnemyNavigator` — `NavMeshAgent` movement plus SPEC.md section 50's navigation-failure fallback. Falls back to steering the transform directly when no NavMesh is usable, so an unbaked scene is playable rather than full of frozen enemies.
- `EnemyCombatant` — one attack: telegraph, active window, recovery, cooldown. Drives a `Hitbox` directly rather than reusing `WeaponController`, because enemy timings and damage come from the archetype and are rescaled by difficulty per swing.
- `EnemyStagger` — poise, hit reactions and knockback. `ForceStagger` is the hook SPEC.md section 15 needs for "successful parry staggers enemy".
- `EnemyGroup` — group coordination: a limited number of attack slots so a mob rotates instead of swinging as one, and a shared alert so a patrol reacts as a squad. Grouping is hierarchy — members find their group with `GetComponentInParent`.
- `PatrolRoute` — waypoints, shared between enemies, drawn as gizmos.
- `AIEvents` — state changed, alerted, staggered, lost target.

**Why the state machine is split this way.** `EnemyController.Decide` is a `static` pure function from `(current state, EnemySenses, archetype, time in state)` to the next state. Seeing, moving, swinging and reeling all live in the other components. That is what makes the rules worth testing — whether an enemy gives up, leashes, retreats or yields its attack slot — testable without a NavMesh, a frame or a physics step. The whole transition table is covered by unit tests; everything below it needed a play-mode run.

### Dialogue (`Assets/Scripts/Dialogue/`)

- `DialogueData` — the serializable pieces: `DialogueNode`, `DialogueChoice`, `DialogueConsequence`, `ConsequenceType`. Field-for-field the shape SPEC.md section 22 recommends.
- `DialogueGraph` — one conversation as a ScriptableObject: a flat list of nodes plus the `NextDialogueId` links between them, so it stays editable in the Inspector without a custom graph editor. `GetEntryNode` picks the first entry whose gates pass, which is how an NPC says something different after the quest is given.
- `DialogueRunner` — walks a graph and applies consequences. Logic only: it holds no UI references and draws nothing, so dialogue can be driven headlessly by tests.
- `NpcInteractable` — an NPC that hands its graph to the runner. Adding a character is authoring an asset, not writing a script.
- `DialogueEvents` — `DialogueStartedEvent`, `DialogueNodeShownEvent`, `DialogueChoiceMadeEvent`, `DialogueConsequenceEvent`, `DialogueCompletedEvent`.

### Quests (`Assets/Scripts/Quests/`)

- `QuestData` — `QuestObjective`, `ObjectiveType`, `QuestStatus`.
- `QuestDefinition` — a quest as authored content (ScriptableObject, immutable at runtime).
- `QuestProgress` — the player's state within one quest. Kept out of the definition because mutating a ScriptableObject at runtime writes progress into the asset in the Editor.
- `QuestManager` — the only thing that knows a quest's shape. Triggers, NPCs and memory pickups report "objective X happened" by id and this decides whether that advances anything.
- `QuestTarget` — marks an entity as counting towards an objective when it dies, giving `ObjectiveType.DefeatEnemy` a driver. It lives here rather than in `AI` so nothing in `Game.AI` has to know quest ids exist.
- `QuestEvents` — started, objective advanced, objective completed, completed, failed.

### Memory (`Assets/Scripts/Memory/`)

- `MemoryFragment` — one collectible memory (ScriptableObject), with the category, state and importance enums from SPEC.md sections 19 and 21.
- `MemoryManager` — which memories the player has and what state each is in, plus the 0..1 `Integrity` model from SPEC.md section 20. The safety rule is enforced here rather than left to callers: a `Critical` memory cannot be moved to a degraded state, so no ordinary gameplay can make story progression unreachable.
- `MemoryPickup` — a fragment in the world. It stays in the scene after collection rather than being destroyed, so a memory cannot be lost to a destroyed object before the grant is recorded.
- `MemoryEvents` — discovered, state changed, integrity changed.

### Save (`Assets/Scripts/Save/`)

- `SaveData` — the whole save, as a plain `[Serializable]` class shaped for `JsonUtility`. It carries every field SPEC.md section 31 names; six of them are reserved and written empty because the systems behind them do not exist. They are there on purpose: the file's shape is what version migration has to reason about, and adding a field later is a migration where filling an existing empty one is not.
- `SaveSerializer` — the file format. A save is an envelope of version, checksum and payload rather than the payload alone, so a truncated or half-written file is detectable without parsing game data out of it. Validation also includes a plausibility pass, which catches a file that parses and checksums correctly but cannot describe a real game.
- `SaveMigration` — brings an older save forward. It has no steps yet; it exists now because the first migration is otherwise written under pressure, with a player's save already broken.
- `SaveStorage` — the four-step replace from SPEC.md section 31, and the corruption rules from section 32. Its root directory is a parameter so tests never touch a player's real save folder.
- `SaveManager` — the aggregator, and the only class allowed to know about every system at once. It also decides when saving is unsafe.
- `ISaveParticipant` — the escape hatch for a system that wants to save something `SaveData` has no field for.

**Why saving is a leaf, not a peer.** SPEC.md section 58 forbids a system depending on every other one. The save system unavoidably has to read them all, so the dependency is made to run one way: `Save` reads `Core`, `Combat`, `Quests` and `Memory`, and none of them reference `Save`. Anything that wants to opt in without being referenced implements `ISaveParticipant` and is found at save time.

**Why restoring is silent.** Applying a save replays no quest, memory or damage events. The player already lived through those beats; re-firing them would set their completion flags and grant their rewards a second time. This is why `QuestProgress.Restore`, `MemoryManager.RestoreState` and `HealthComponent.RestoreTo` exist as separate paths rather than reusing `ReportObjective`, `Discover` and `TakeDamage`.

**Order matters when applying.** World flags go back before quests, because setting a flag publishes an event that quest logic reacts to. Restoring the quests last means their saved status wins over whatever that reaction decided.

### UI (`Assets/Scripts/UI/`)

- `PauseMenu` — listens for the Pause input action, toggles a Canvas, sets `Time.timeScale`, calls into `GameManager`.
- `DialogueUI` — renders whatever the runner is showing and calls `Advance`/`Choose`. Presentation only; it holds no conversation state.
- `InteractionPromptUI` — shows the current interactable's prompt, driven entirely by events.
- `QuestTrackerUI` — the active quest's title and current objective.
- `MemoryDiscoveryUI` — the banner shown when a fragment is recovered.

## Dependencies between systems

`PlayerController` → `GameLogger` only (no dependency on GameManager for movement).
`PauseMenu` → `GameManager`, `GameLogger`.
`GameManager` → `EventBus`, `GameLogger`. Does not depend on Player/Combat/AI directly.
`Combat` → `Core` (`EventBus`, `GameLogger`) and, in `CombatController` and `PlayerDeath` only, `Player.PlayerController`.

`AI` → `Core` and `Combat`. It does **not** reference `Quests`, `Dialogue` or `Memory`: an enemy that counts towards a quest carries a `QuestTarget`, which belongs to `Quests`.
`World` → `Core`, and `Quests` for reporting objectives and starting quests.
`Dialogue` → `Core` and `World` (for `Interactable`) only. It does **not** reference `Quests` or `Memory`.
`Quests` → `Core`, and reads `Dialogue`'s consequence payload.
`Memory` → `Core`, `Quests`, and `Dialogue`'s consequence payload.
`Save` → `Core`, `Combat`, `Quests`, `Memory` and `Dialogue`'s event payloads. Nothing references `Save`.
`UI` → every content system, read-only, through `EventBus` payloads.

`Core` does not reference `Combat`. `Combat` does not reference `UI`; UI reacts to combat through `EventBus` payloads instead.

### How a conversation changes the world

`PlayerInteractor` → `NpcInteractable.Interact` → `DialogueRunner.Begin` → node shown → consequences applied.

The runner applies flag and relationship consequences itself, because it already depends on `Core`. Quest and memory consequences it **publishes** as `DialogueConsequenceEvent` instead, and `QuestManager` and `MemoryManager` subscribe. This is what keeps Dialogue from referencing every content system at once (SPEC.md section 47): the dependency runs one way, and Dialogue knows nothing about quests or memories.

The one place this is inverted is the memory *gate* on a dialogue node. Dialogue needs to ask "what state is memory X in?", which is a question, not an event. `DialogueGraph.MemoryStateResolver` is a static hook that `MemoryManager` installs on enable; while it is null, memory gates are treated as unsatisfied, so memory-specific lines stay hidden rather than showing wrongly.

### Manager lookup

`WorldState`, `DialogueRunner`, `QuestManager` and `MemoryManager` expose `Instance` through `SceneSingleton.Resolve`, which caches the reference and falls back to a scene search when it is null.

Setting `Instance` in `Awake` alone is not enough. Unity does not guarantee which `Awake` runs first, so a component looking a manager up in its own `Awake` can find null even though the manager is in the same scene; and Unity does not call `Awake` at all in EditMode, which makes such managers untestable. This is the same lesson as `Hitbox` and `Hurtbox` in TASK 002, generalized.

A consequence for tests: because the lookup searches the loaded scenes, EditMode tests that use these managers must run in a scene of their own, or the project's own scene would answer instead. `AvarshaTests` opens an empty scene in `[OneTimeSetUp]` and restores the previous one afterwards.

The one deliberate exception is `CombatController` → `PlayerController.BeginDodge`. A dodge is an impulse that has to be applied through the same `CharacterController.Move` call as ordinary movement, so combat asks locomotion to move rather than moving the player itself. Combat still owns the stamina cost and the invulnerability window.

No system depends on more than one layer below it. Gameplay scripts do not reference story/quest content (SPEC.md section 58, rule 7) — none exists yet in this phase.

## Assemblies

Runtime code compiles into `Game.Runtime` (`Assets/Scripts/Game.Runtime.asmdef`) rather than the default `Assembly-CSharp`. This exists so `Assets/Tests/EditMode/` can reference game code: an assembly definition cannot reference the predefined `Assembly-CSharp`, so tests were impossible without it.

Anything driving the Editor by reflection must therefore use `Game.Runtime`, not `Assembly-CSharp`, as the assembly name.

## Tests

`Assets/Tests/EditMode/` holds EditMode tests (`Game.Tests.EditMode` assembly): `CombatTests.cs` covers the combat damage and resource rules, `AvarshaTests.cs` the world-state, dialogue, quest and memory rules, `EnemyAiTests.cs` the enemy transition table, perception geometry, poise, group slots, patrol order, factions and difficulty scaling, and `SaveTests.cs` the save file format, its checksum and plausibility rules, version migration and the backup and quarantine behaviour. 93 tests in total.

Each test class opens an empty scene in `[OneTimeSetUp]` and restores the previous one in `[OneTimeTearDown]`. The managers resolve themselves by searching the loaded scenes, so leaving the project's own scene open would let Avarsha's managers answer instead of the ones a test set up, and results would depend on which scene the developer happened to have open. `EnemyAiTests` also resets the static `Difficulty` in `[TearDown]`.

Note that Unity does not call `Awake` on plain MonoBehaviours in EditMode. Components that resolve references must therefore do so lazily rather than only in `Awake`, or they will be unconfigured under test — and, more importantly, also unconfigured at runtime if they are added or reparented after `Awake`. `Hitbox` and `Hurtbox` both resolve on demand for this reason. Components expose a `Configure(...)` method as an explicit test seam where serialized fields need setting.

`Assets/Tests/PlayMode/` holds PlayMode tests (`Game.Tests.PlayMode` assembly): `CombatPlayModeTests.cs`, `EnemyAiPlayModeTests.cs`, `ProgressionPlayModeTests.cs` and `SavePlayModeTests.cs`, 33 tests in total. They cover what EditMode structurally cannot — coroutines, physics triggers, navigation, and anything that only goes wrong after several frames. Every defect found at the end of TASK 004 was of that kind.

The split between the two suites is a split of claims, not of convenience. EditMode proves a rule is right: `EnemyController.Decide` is a pure function over a senses snapshot, so the whole transition table is 17 tests with no scene at all. PlayMode proves a system wired into a scene actually obeys that rule — which is a different claim, and the one that was false when TASK 004 first ran.

`TestArena` builds each PlayMode test its own world in code: floor, walls, a NavMesh baked at runtime, actors and manager singletons, all destroyed afterwards. Three things about it are load-bearing. Objects are built inactive and activated last, because in PlayMode `AddComponent` runs `Awake` immediately and a `Configure(...)` call after that is too late. The NavMesh is baked after the ground and before the actors, or an enemy standing on the floor is carved out of the mesh it needs. And `WorldState` is emptied rather than destroyed between tests, because it is `DontDestroyOnLoad` and its `Awake` destroys any duplicate.

The assembly references `Unity.AI.Navigation` where the EditMode one does not, so tests can call `NavMeshSurface.BuildNavMesh()`. Game code needs no such reference: `EnemyNavigator` uses only `UnityEngine.AI.NavMeshAgent`, which is an auto-referenced engine module.

## Scenes

`Assets/Scenes/Test.unity` is the Phase 0 test scene and the only scene in Build Settings. It contains a ground plane, three collision blockers (one of which is a low step for testing grounding), the player capsule, the camera rig, a `GameSystems` object holding the Core singletons and `CheckpointManager`, and a UI canvas with the pause overlay.

Combat additions: the player carries a `Hurtbox` and an `AstraBlade` child whose `HitVolume` holds the `Hitbox`; `Enemy_TrainingDummy` (60 health) stands at (3, 1.1, 6); `Checkpoint_01` at (-3, 1, 5); `Hazard_AshPit`, a `DamageVolume`, at (-7, 0.5, 5).

The dummy's 60 health is a deliberate placeholder chosen so a light-attack chain finishes it in five swings, which keeps the scripted play-mode check short. It is not a balance decision.

`Assets/Scenes/Avarsha.unity` is the hub scene (SPEC.md section 24), created by copying `Test.unity` so the player's full locomotion and combat wiring carried over intact, then replacing the Phase 0 props with the city. It holds all eight districts blocked out in primitives — city gate, royal palace, marketplace, training ground, temple district, library, residential district and the ancient ruins — plus the three NPCs, the memory fragment at the ruin stone, two `LocationTrigger` volumes, and the `SupernaturalEvent`. Both scenes are in Build Settings.

AI additions to Avarsha: a `Navigation` object carrying a baked `NavMeshSurface` over the whole scene, and `Encounter_Ruins` — an `EnemyGroup` holding one enemy of each archetype, a shared four-point `PatrolRoute`, and a `Trigger_RuinAmbush` that starts `Q002`. Each enemy's pivot sits at ground level with the capsule as a child, because a `NavMeshAgent` snaps its transform to the NavMesh and a centre-pivoted capsule would sink to the waist.

## Content data (`Assets/Data/`)

Authored ScriptableObjects, per SPEC.md section 48:

```text
Assets/Data/
  Dialogue/   Dialogue_Amara, Dialogue_Dev, Dialogue_Mira
  Quests/     Quest_TheQueensCharge   (Q001)
              Quest_TheAshAtTheGate   (Q002)
  Memories/   Memory_NameBeneathTheStone   (MEM_001, Critical)
  Enemies/    Enemy_ForgottenSoldier, Enemy_AshCreature, Enemy_StoneGuardian
```

These were generated by a script calling each type's `Configure` seam rather than typed into the Inspector, so they can be regenerated and diffed. Gameplay scripts reference them only through the manager catalogues and the NPC/pickup components — no gameplay script names a quest or memory id in code (SPEC.md section 58, rule 7).

Unity render-pipeline configuration comes from `Assets/Settings/` (URP), generated from the `com.unity.template.urp-blank` template rather than hand-authored.

## Working on this project with the Unity CLI

The Unity CLI can drive the open Editor directly, which is how the test scene was built and verified. Scene and asset files should be changed this way rather than by hand-editing `.unity` YAML, because fileIDs and GUIDs are assigned by the Editor and raw edits are invisible to a running Editor until a reimport.

```bash
unity status                                  # confirm an Editor is connected (state "ready")
unity command --project-path <repo>           # list what the Editor exposes
unity command eval_file --file <script.cs>    # run C# against the live Editor
unity command console --level error           # read compile/runtime errors
unity command run_tests --mode editor         # run the EditMode suite
unity command run_tests --mode playmode --async_tests
unity command test_status                     # poll the async run for results
```

PlayMode tests cannot run synchronously over the CLI: entering play mode triggers a
domain reload that drops the request. Start them with `--async_tests` and poll
`test_status` until it reports `completed`.

Two things about `eval_file` that are easy to trip over:

- It compiles against the Editor's current assemblies, so **recompile first** after adding or changing a type, or it will not resolve.
- The file is a **statement body, not a compilation unit**: `using` directives at the top are a syntax error. Write type names fully qualified (`UnityEditor.AssetDatabase`, `Game.Quests.QuestManager`), and use local `System.Func`/`System.Action` variables where a helper method would normally go.

## Not yet implemented

Inventory, Audio, VFX and the debug console. Within combat, block, parry, divine ability, finisher and lock-on are still outstanding. Quests, Dialogue, Memory, World and AI exist but are first passes: three of ten enemy classes are built, and memory integrity is never consumed. The save system exists and round-trips the player's progress, but not the state of the world around them — a killed enemy is alive again after a load — and nothing in the game offers to save or load except the checkpoint auto-save. See `KNOWN_ISSUES.md` for the full list.
