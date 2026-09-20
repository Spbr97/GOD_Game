# KNOWN ISSUES

Open limitations, carried forward until closed. Each entry says what is wrong, why it
was left, and what closing it involves.

## TASK 006 — Save system

### Six of the save's fields are reserved and always empty

`SaveData` carries every field SPEC.md section 31 names, but `Inventory`, `Abilities`,
`NpcStates`, `BossStates`, `DialogueFlags` and `EndingFlags` have no system behind them
and are written empty every time. They exist now so the file's shape is settled and a
later system fills a field instead of triggering a version migration. Nothing reads
them, and nothing validates them.

### The world is not saved, only the player's progress

Quest states, world flags and counters, memories and the player all round-trip. The
*world* does not: a killed enemy is alive again after a load, a collected memory pickup
is sitting in the world again, and a fired `LocationTrigger` has forgotten it fired.
Everything gated on a world flag behaves correctly, so story progression is safe; what
is wrong is everything gated on an object's own state. Closing this needs per-object
save identity — a stable id on each saveable object — which `ISaveParticipant` can
carry but nothing implements yet.

### Loading does not change scene

`SaveData.SceneName` is recorded and then ignored. `Load` applies a save into whichever
scene is already open, so loading a save made elsewhere silently puts the player at
coordinates that mean nothing. `GameSceneManager` exists and could do the load first;
the two are not connected, and doing so needs a defined point at which restoring
happens after the new scene's managers have woken.

### Nothing in the game offers to save or load

`SaveManager` sits on `GameSystems` in Avarsha and auto-saves when a checkpoint is
activated. That is the only save anything writes. There is no save menu, no load menu,
no slot browser and no way for a player to write a `Manual` or `Chapter` save —
`SaveSlot` declares all three and only one is ever used. Saving and loading are
currently reachable only from code and from tests.

### The required corruption message is published but never shown

`SaveManager` raises `SaveRecoveredFromBackupEvent` carrying SPEC.md section 32's
sentence verbatim, and a PlayMode test asserts the exact wording. No UI subscribes to
it, so in a real session the player is not told. The same is true of `SaveFailedEvent`.

### The checksum detects corruption, not editing

`SaveSerializer` uses FNV-1a, which catches a truncated, half-written or bit-rotted
file — the failure SPEC.md section 32 is about. It is not a signature: anyone who
wants to edit their own save can recompute it. That is a deliberate scope decision for
a single-player game, not an oversight, but it means the checksum must never be relied
on as an anti-cheat measure.

### Migration has never migrated anything

There is one save version, so `SaveMigration` has no steps and its loop has never run.
The refusal path is tested; the upgrade path cannot be until there is a version 2. The
first real migration should arrive with a test that loads a genuine version 1 file
captured from this build, not a hand-written one.

### Settings live in two places

`SettingsManager` persists the difficulty in `PlayerPrefs` and `SaveData.Difficulty`
persists it again in the save file. Loading a save applies the save's value, which can
then disagree with what the options screen would show. SPEC.md section 31 lists
`Settings` as save data and also names a separate settings save, so both are wanted —
but which one wins is currently decided by whichever ran last.

### Saving is only blocked for two of the cases that should block it

`SaveManager` refuses to save during a conversation and while the player is dead.
SPEC.md section 31 says never to save during a critical state transition, and SPEC.md
section 33 names irreversible story decisions, boss transitions and temple completions.
None of those exist yet, and none of them call `BlockSaves`. The hook is public and
reason-keyed so they can when they do.

### Quarantined saves accumulate forever

A save that fails validation is renamed `*.corrupt-<timestamp>` and left on disk,
because SPEC.md section 32 forbids deleting user saves. Nothing ever removes them,
counts them or tells the player they are there, so a player hitting repeated corruption
slowly fills their save folder with files they do not know about.

### Two tests deliberately log console errors

`Load_ACorruptPrimary_...` and `Storage_ACorruptPrimary_...` feed the loader a shredded
file on purpose, and `SaveStorage` logs that at error level as SPEC.md section 32 step
4 requires. The tests suppress the failure with `LogAssert.ignoreFailingMessages`, but
the entries still land in the Unity console, so a clean run of the suite leaves errors
behind that are expected rather than real.

## TASK 005 — PlayMode tests

### Whole systems still have no PlayMode coverage

`Assets/Tests/PlayMode/` covers combat, enemy AI, quests and memories — the systems
whose failures were expensive. Dialogue, the interaction prompt, every UI screen, the
pause menu, the third-person camera and player locomotion have none. Locomotion and
the camera are the two most obviously frame-dependent systems in the project and are
still checked only by eye.

### Nothing tests the shipped scenes

Every test builds its arena in code, including its own NavMesh. That is deliberate —
a test that depends on `Avarsha.unity` breaks whenever a designer moves a building —
but it means no test would notice if the hub scene lost its `NavMesh`, its
`QuestManager` or a serialized reference. A separate, small set of scene-integrity
tests would close this; they do not exist.

### The suite is wall-clock and tolerance-based

PlayMode tests run in real seconds: the current 25 take about 42. Assertions about
movement are distance thresholds ("closed more than 2m in two seconds") and every wait
has a deadline, so a heavily loaded machine could fail a test that is not actually
broken. Nothing is time-scaled or deterministic. If flakes appear, the fix is to drive
the systems from a fixed clock rather than to widen the tolerances.

### SPEC.md section 53 lists tests for systems that do not exist

Parry, combos and boss phases (combat), and save, load, backup, corrupted data and
version migration (save) are all named in section 53 and none are covered, because
none are implemented. They are listed here so the gap is not mistaken for an oversight
in the test suite.

### Nothing runs the tests automatically

Both suites are run by hand through the Unity CLI. There is no CI, no pre-commit hook
and no record of which commit last passed. The coverage only protects the project if
somebody remembers to run it.

### `CombatController.Configure` exists only for the tests

`CombatController` disables itself in `Awake` when it has no `InputActionAsset`, so a
test-built player needs one before activation. `Configure` is the seam for that,
matching the pattern already used across the project — but it is a public method on a
gameplay component that shipping code never calls.

## TASK 004 — Enemy AI

### Three enemy archetypes exist out of ten

SPEC.md section 16 lists ten enemy classes. `EnemyClass` declares all ten, but only
**Forgotten Soldier**, **Ash Creature** and **Stone Guardian** have archetype assets
and a place in the world. Naga-Raksh, Sky Hunter, Dream Stalker, Time Wraith, Divine
Guardian, mini-boss and boss are enum values with nothing behind them. Each needs an
archetype asset and, for the later ones, behaviour the current state machine does not
have — flight, phasing, phase transitions.

### Enemies have one attack, and it is a serialized timer

`EnemyCombatant` plays one swing: telegraph, active window, recovery. SPEC.md section
16 asks for attack *patterns*. There is no second attack, no ranged option, no combo
and no choice between them, so every enemy of a class fights identically. The timings
are serialized numbers rather than animation events, the same limitation recorded for
`WeaponController` in TASK 002, and for the same reason: there are no animations yet.

### The telegraph is a colour flash

SPEC.md section 16 requires readable telegraphs. What exists is the body tinting to
the archetype's telegraph colour during the wind-up. That is legible but it is not a
wind-up animation, a VFX or an audio cue — SPEC.md section 16 asks for audio cues and
death animations, and there is no audio system at all yet.

### `EnemyHealth` and `EnemyCombatant` tint through different mechanisms

`EnemyHealth` (TASK 002) writes `renderer.material.color`, which instantiates a
material. `EnemyCombatant` and `EnemyController` use a `MaterialPropertyBlock`. A
property block overrides the material, so a hit flash landing during a telegraph can
be masked, and clearing the block reveals whatever the material instance was left at.
Closing this means moving all three onto one tinting path — worth doing when real
materials replace the placeholder ones.

### Group coordination is attack slots and a shout

`EnemyGroup` limits how many members may swing at once and forwards an alert to
members within a radius. SPEC.md section 17's "group coordination" implies more:
flanking, surrounding, ranged members holding back, coordinated openings. None of
that exists. Grouping is also purely hierarchical — an enemy's group is whatever
`EnemyGroup` is on a parent — so enemies cannot join or leave a group at runtime.

### Factions are a three-value enum, not a relationship model

The first play-mode run had the Ash Creature killed by an ally's swing, because
`Hitbox` refused only hurtboxes belonging to its own owner. That is fixed: `Faction`
(`Neutral`, `Player`, `Hostile`) is compared before damage lands, and the re-run
showed an ally at 35 health before and after a group fight.

What exists is the minimum that stops friendly fire. There are no relationships
between factions, no neutrals that turn hostile, no allies who fight for the player,
and no way to change an entity's faction at runtime. `Neutral` opts out of the check
entirely so that anything predating factions — the player's weapon, the training
dummy — behaves exactly as before; that also means a neutral entity can still be hit
by its own side.

### Enemies cannot target anything but the player

`EnemyPerception` resolves its target by finding the one `PlayerDeath` in the scene.
There is no faction model, so an enemy cannot deliberately target an NPC and the
Avarsha civilians are in no danger. Same fix as the defect above.

### Retreat is the only self-preservation behaviour

An enemy backs off once when it drops below its archetype's health fraction, then
never again unless it heals. It does not call for help while retreating, does not
seek cover, and does not flee the encounter entirely. The one-shot rule exists to
stop a retreat/re-engage loop rather than because it is the right behaviour.

### Navigation failure returns to patrol but never retries

SPEC.md section 50's fallback is implemented in `EnemyNavigator.SetDestination`:
stop, recalculate, sample the nearest valid point, and failing that abandon the
destination so `EnemyController` sends the enemy back to patrol. What it does not do
is remember that a destination was unreachable, so an enemy can re-target the same
bad point on the next tick and log the same fallback repeatedly.

### Without a baked NavMesh, enemies walk through walls

`EnemyNavigator` falls back to steering the transform straight at its destination
when there is no `NavMeshAgent` or the agent is not on a NavMesh. That keeps an
unbaked scene playable instead of filling it with frozen enemies, but the fallback
ignores obstacles entirely. It logs once per enemy when it engages. Avarsha's NavMesh
is baked, so this only affects scenes built later.

### The NavMesh is baked once and does not react to the world

`Navigation` carries a `NavMeshSurface` baked at edit time over the whole scene.
Nothing rebuilds it, and nothing in the scene is a `NavMeshObstacle`, so a door, a
collapsing bridge or the act-by-act changes to Avarsha would not change where enemies
can walk. SPEC.md section 17 points at the AI Navigation package's runtime baking and
links for exactly this; neither is used yet.

### Difficulty is implemented but cannot be changed in game

`Difficulty` implements SPEC.md section 44's four modes and scales enemy damage,
telegraph length, attack cooldown and group aggression — deliberately not health, per
sections 15 and 44. `SettingsManager.SetDifficulty` persists it. But there is no
options menu, so the only way to leave Normal is from code. `PlayerTimingWindow` is
defined and nothing reads it: dodge i-frames are still fixed on `CombatController`,
and parry does not exist.

### `Q002 The Ash at the Gate` is a demonstration, not designed content

The quest exists so `ObjectiveType.DefeatEnemy` has a real driver end to end. Its
text is a placeholder, no NPC gives it, it is started by walking into a trigger, and
its reward is a sentence saying there is no reward system. It should be replaced when
Avarsha's Act I content is actually written.

### Killed enemies despawn and never come back

`EnemyHealth` destroys an enemy two seconds after death, and nothing respawns it.
There is no encounter reset, so reloading a checkpoint leaves the ruins empty — the
same gap recorded under TASK 002's "respawn does not reset world state", now with
something in the world it actually loses. `QuestTarget` guards against a kill being
counted twice, so a future respawn will not break the quest count.

### Knockback and the enemy tint are still unverified

`EnemyStagger` pushes an enemy along `DamageData.Direction` when poise breaks, and
`EnemyController` and `EnemyCombatant` tint the body. TASK 005 covers the stagger
itself — the swing is interrupted and the state machine enters `Stagger` — but not
that the enemy visibly moves, and not that either tint reaches the renderer. Both are
cosmetic and both will be rewritten when real art and animation arrive, which is why
they were left rather than tested.

## TASK 003 — Avarsha

### Avarsha is one flat scene, not the hub the spec describes

SPEC.md section 24 lists eight districts and says the hub changes across five acts.
What exists is one `Avarsha.unity` with all eight districts blocked out as primitives
on a single ground plane, in the Act I state only. There is no streaming, no interiors,
and no mechanism for the act-by-act changes. The world-state flags the act changes
would key off do exist (`WorldState`), so the hook is there; the content is not.

### The supernatural event is a scripted placeholder

`SupernaturalEvent` plays SPEC.md section 68 scenes 5 and 7 — everyone falls asleep,
then the statues bleed — as a rotation, a colour lerp and a light change. There is no
animation, no VFX, no audio and no camera work. It is one hard-coded sequence rather
than a reusable cutscene system; SPEC.md section 69 asks for cinematic direction that
does not exist yet. Scenes 1-4, 6, 8 and 10 of that sequence are not implemented at
all: no sunrise, no training scene, no empty-city exploration state, no dead soldier,
no Nirvaan.

### Dialogue has no voice, no localization and no history

`DialogueNode` carries `VoiceAssetId` and `Subtitle`, and nothing reads the first —
there is no audio (SPEC.md section 41). Text is authored in English directly in the
assets rather than through string keys, so SPEC.md section 73 localization would need
the data reworked. There is also no backlog or replay of lines already shown, and no
typewriter reveal or skip.

### Dialogue graphs are authored in code, not an editor

The three conversations were built by a script calling `DialogueGraph.Configure`. The
asset is editable as a flat list of nodes in the Inspector, but there is no graph view,
no validation that `NextDialogueId` links resolve, and no warning for an unreachable
node. A misauthored link is only caught at runtime, where it ends the conversation and
logs a fallback.

### Relationship values are stored but never read

`ConsequenceType.ChangeRelationship` writes to a `WorldState` counter named `REL_<id>`.
Nothing reads those counters — no NPC behaviour, dialogue gate or ending condition
depends on them (SPEC.md sections 22 and 72). The plumbing is real; the consequences
are not.

### Most quest objective types cannot complete

`ObjectiveType` declares the ten kinds SPEC.md section 23 requires. Only
`ReachLocation`, `Talk`, `Interact` and `ObtainMemory` have anything in the world that
can report them. `DefeatEnemy` needs enemy AI (TASK 004), `CollectItem` needs the
inventory system (section 28), `SolvePuzzle` needs section 26, and `Survive`, `Escort`
and `ChooseDialogue` have no driver. The enum value is currently only a label on the
quest log entry.

### Quest rewards are a string

`QuestDefinition.RewardsSummary` is descriptive text. There is no inventory,
progression or skill-tree system (SPEC.md sections 28-30) to grant anything, so
completing "The Queen's Charge" changes flags and nothing else.

### Memory corruption is a number nothing consumes

`MemoryManager.Integrity` implements the 0..1 model from SPEC.md section 20 and
protects `Critical` memories from degrading, which is tested. But nothing reduces
integrity during play — no divine ability exists to consume memory — and nothing reads
it, so the cosmetic consequences the spec describes (memories disappearing, dialogue
changing, incomplete flashbacks) do not happen.

### Memory visualization is a text panel

SPEC.md section 70 asks for memory visualization. `MemoryDiscoveryUI` shows the title
and description on a flat panel with a coloured bar. There is no flashback, no visual
representation per memory beyond a tint colour, and no narration.

### No memory log or quest log screen

The player can see the one tracked quest and a transient memory banner. There is no
screen listing memories collected or quests taken, so a player who dismisses the banner
cannot read that memory again. This matters more as soon as there is a second memory.

### Nothing persists across a scene load or quit

`WorldState`, `QuestManager` and `MemoryManager` hold everything in memory. Quitting
loses the quest, the flags and the memory. This is the save system (SPEC.md sections
31-32) and was deliberately not invented here. `WorldState` survives a scene load via
`DontDestroyOnLoad`; the quest and memory managers do not, because they are scene
objects.

### Entering Avarsha is a trigger, not a scene transition

Acceptance criterion 1 is satisfied by a `LocationTrigger` at the city gate inside
`Avarsha.unity`, which sets `ENTERED_AVARSHA`. There is no loading of Avarsha from
another scene and no door or transition system; `GameSceneManager` can load scenes but
nothing in the world calls it.

### The NPCs do nothing but talk

Amara, Dev and Mira are capsules that turn to face the player. No routines, no
schedules, no idle animation, no walking (SPEC.md section 25 asks for NPC routines).
They also have no colliders tuned for anything but standing still.

### Interaction selection is a sphere overlap capped at 16 colliders

`PlayerInteractor` uses `OverlapSphereNonAlloc` with a fixed 16-entry buffer. In a
denser scene than this one, interactables beyond the sixteenth collider in range would
be invisible to it. There is also no line-of-sight check, so an NPC on the other side
of a wall can be selected if they are within range.

### Conversation and interaction are still untested over frames

TASK 005 covers the quest and memory halves of this in PlayMode — trigger volumes
firing on entry, kills advancing an objective, a pickup granting once. What remains
uncovered is everything belonging to a conversation: the interaction prompt, the
dialogue UI, `PlayerDialogueLock` suspending and restoring control, and the
supernatural event's sequence over time. Those are still verified only by a scripted
play-mode run, which is a manual check rather than a regression test.

### Gamepad bindings are saturated

Interact is bound to `E` on keyboard and to **d-pad up** on gamepad, because all four
face buttons are already taken by Jump, Light Attack, Heavy Attack and Dodge. D-pad up
is a poor place for the most-used contextual button. The control scheme needs a pass
before gamepad play is reasonable — this was already flagged for dodge in TASK 002 and
has now forced a second bad binding.

## TASK 002 — Combat Foundation

### Combat actions not yet implemented

SPEC.md section 13 lists nine basic actions. TASK 002 implements **light attack, heavy
attack and dodge**. Still missing: **block, parry, divine ability, finisher, lock-on**.
Parry in particular carries a design requirement that nothing yet satisfies — SPEC.md
section 15 says difficulty must modify the timing window rather than multiply enemy
health, and there is no difficulty system to read that window from.

### Combo system is a counter, not a chain

SPEC.md section 14 asks for named chains (Light→Light→Heavy, Dodge→Light, Parry→Heavy,
and a finisher below a health threshold). What exists is an integer that counts swings
inside a time window and scales damage by 15% per step. It does not distinguish light
from heavy in the chain, and there is no finisher. Real chains need the attack types
recorded in sequence and matched against a table, which is worth doing once animations
exist to differentiate the steps.

### Attack timing is serialized numbers, not animation events

`WeaponController` opens and closes the hitbox on fixed delays because there are no
attack animations yet (SPEC.md section 78, placeholder-first). When animations land,
these should become animation events, or the damage window will drift out of sync with
what the player sees.

### Hitbox activation is a one-shot overlap plus trigger enter

A swing samples overlaps once on activation and then relies on `OnTriggerEnter`. A fast
swing passing entirely through a thin target between physics steps can miss. The usual
fix is sweeping the hitbox between its previous and current position each frame; not
done because placeholder swings are slow and the volumes are large.

### No hit reaction, stagger or knockback

`DamageData` carries a direction and hit point, and nothing consumes them. Enemies flash
a colour and keep standing. Hit reactions, stagger and poise are part of SPEC.md section
16 and need enemy AI (TASK 004) to be meaningful.

### Enemy has no AI and cannot attack

`Enemy_TrainingDummy` is a stationary capsule. The only thing that can damage the player
is `Hazard_AshPit`, a `DamageVolume`. Enemy attacks arrive with TASK 004.

### `DamageVolume` is outside the TASK 002 component list

It was added because "player can die" and "checkpoint restores player" are acceptance
criteria and nothing else in this task could damage the player. It is a real system
rather than a test fixture, but it was not part of the specified scope.

### `CheckpointManager` is not persisted

The active checkpoint lives in memory only and is lost on scene reload or quit. This
belongs with the save system (SPEC.md section 32) and was deliberately not invented here.
Until then, `CheckpointManager.CaptureFallback` uses the player's start position so the
first death before any checkpoint still resolves.

### Respawn does not reset world state

`PlayerDeath` restores the player and moves them to the checkpoint, but enemies stay dead
and hazards stay as they were. Whether death should roll the world back is a design
decision the spec does not settle, and implementing either answer needs the save system.

### Difficulty scaling is absent

SPEC.md section 15 requires difficulty to modify timing windows rather than enemy health,
and section 16 requires per-enemy difficulty scaling. Neither exists; all values are
fixed serialized fields.

### Dodge is bound to left Ctrl

A placeholder binding. Left Ctrl is conventionally crouch, and dodge is usually the same
button as sprint. It was not bound to shift or space because both are already taken by
sprint and jump, which is itself a sign the control scheme needs a pass.

## TASK 001 — Project Foundation

### Gamepad look speed is wrong

`PlayerCamera` applies one sensitivity value to the Look action, but mouse delta is
per-frame pixels while a gamepad stick is a -1..1 rate. The value is tuned for mouse, so
stick look is very slow. Fixing it means splitting the two paths and multiplying the
stick by `Time.deltaTime`; deferred until gamepad support is actually exercised.

### `Assets/Settings/SampleSceneProfile.asset` is unused

Carried over from the URP template and not referenced by `Test.unity`. Harmless; delete
it once a real volume profile per region exists (SPEC.md section 35).

### Placeholder art only

Everything in `Test.unity` is a Unity primitive with the default URP material, per the
placeholder-first rule (SPEC.md section 78). The Astra Blade is a stretched cube.
