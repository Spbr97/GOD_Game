# KNOWN ISSUES

Open limitations, carried forward until closed. Each entry says what is wrong, why it
was left, and what closing it involves.

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

### The leash range itself has only ever been unit-tested

Giving up on a lost target is verified in play: the Forgotten Soldier moved to
`Search` 3.0s after losing sight. But that is the *lose-target* path. The separate
anti-cheese rule — break off when further than `leashRange` from home even with the
player in plain sight (SPEC.md section 56) — is covered only by
`Decide_BeyondLeashRange_ReturnsHomeEvenWithTheTargetInSight`. No play-mode run has
dragged an enemy past its leash while keeping it in line of sight, so the rule is
proven as a decision but not as a behaviour.

### The telegraph has not been observed firing in play

`sawTelegraph=False` in both play-mode runs: the probe polled
`EnemyCombatant.IsTelegraphing` every frame during the engage phase and never caught
it true, because the enemy under observation stayed in `Chase` and never entered its
attack. Enemy attacks do land — the first run took the player from 93 to 83 health —
so the attack path works; what is unproven is that the wind-up actually tints the
body for the authored duration. It needs either a PlayMode test or a probe that waits
on `EnemyState.Attack` before it starts watching.

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

### Test coverage is EditMode-only for the third task running

`Assets/Tests/EditMode/EnemyAiTests.cs` covers what can be decided without a frame:
the whole transition table via `EnemyController.Decide`, the sight-cone geometry,
poise and stagger, group attack slots, patrol route order, difficulty scaling and the
quest hook — 33 tests. It does not cover anything needing a NavMesh, a physics step
or elapsed time: actual pathing, the perception raycast, the attack coroutine's
timing, the telegraph, knockback, or the leash playing out over seconds. Those were
verified by a scripted play-mode run, which is a manual check rather than a
regression test. This gap is now three tasks old and is the largest single risk in
the project.

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

### Test coverage is EditMode-only, again

`Assets/Tests/EditMode/AvarshaTests.cs` covers the world-state, dialogue, quest and
memory rules — 24 tests over the logic the acceptance criteria rest on. It does not
cover anything needing a frame or a physics step: trigger volumes firing on entry,
the interaction prompt, the dialogue UI, player control locking, or the supernatural
event's sequence over time. Those were verified by a scripted play-mode run, which is
a manual check rather than a regression test. This is the same gap recorded for TASK
002 and it is now twice as large.

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

### Test coverage is EditMode-only

`Assets/Tests/EditMode/CombatTests.cs` covers the damage, stamina and checkpoint rules —
the logic the acceptance criteria rest on. It does **not** cover anything needing a
physics step or a frame: hitbox trigger behaviour, swing timing, dodge invulnerability
windows, and the death-to-respawn sequence. Those were verified by a scripted play-mode
run instead, which is a manual check rather than a regression test. They should become
PlayMode tests.

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
