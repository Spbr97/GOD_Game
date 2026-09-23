# THE GOD WHO WAS FORGOTTEN
## Game Development Specification — `SPEC.md`

**Document version:** 1.0  
**Status:** Master implementation specification  
**Target:** Vibe-coded playable 3D action-adventure prototype, scalable toward a full game  
**Primary engine:** Unity 6 + C#  
**Primary platform:** Windows PC  
**Perspective:** Third-person  
**Genre:** Mythological action-adventure / dark fantasy RPG  
**Single-player:** Yes  
**Art direction:** Cinematic ancient-mythological fantasy  
**Working title:** `THE GOD WHO WAS FORGOTTEN`

---

# 1. PURPOSE

This document is the single source of truth for building the game.

Every implementation decision should follow this specification unless a later version explicitly supersedes it.

The game must combine:

1. A strong original mythological narrative.
2. Third-person exploration.
3. Responsive melee combat.
4. Mythological enemies and bosses.
5. Seven divine regions/temples.
6. A memory-based supernatural mechanic.
7. Environmental puzzles.
8. Cinematic storytelling.
9. A clear progression system.
10. Robust save/load and error recovery.
11. Modular architecture suitable for AI/vibe coding.
12. A visually impressive vertical slice that can later become a larger game.

**Important implementation principle:** do not attempt to build the entire imagined AAA game immediately. Build a polished vertical slice first, then expand the systems using the same architecture.

---

# 2. CORE CREATIVE VISION

The player should feel:

- small in front of an ancient supernatural world;
- curious about forgotten history;
- powerful but increasingly burdened by supernatural abilities;
- uncertain about what is true;
- emotionally connected to the protagonist;
- rewarded for exploration;
- threatened by mythological enemies;
- increasingly aware that the protagonist is part of the mythology itself.

The game should NOT feel like a generic fantasy game with mythology pasted onto it.

The mythology must affect:

- world design;
- combat;
- puzzles;
- dialogue;
- progression;
- environmental storytelling;
- music;
- UI;
- enemy design;
- endings.

---

# 3. ORIGINALITY AND IP SAFETY

The game must use an original fictional mythology.

Do NOT directly copy:

- existing commercial game characters;
- existing game maps;
- copyrighted dialogue;
- existing game logos;
- existing game-specific visual designs;
- proprietary assets;
- exact names, costumes, creatures, locations, or story sequences from another franchise.

The game may be aesthetically inspired by broad ancient-world concepts, but its fictional gods, cosmology, kingdoms, characters, symbols, creatures, history, and terminology must be original.

Do not market the game as an adaptation of a real religion or mythology.

The world is fictional.

---

# 4. STORY OVERVIEW

Thousands of years ago, humanity lived under the protection of seven gods.

Each god governed one fundamental force:

- Agniya — Fire
- Varuna — Ocean
- Vayu — Wind
- Dhara — Earth
- Surya — Sun
- Chandra — Moon
- Kaal — Time

There was an eighth divine being:

## NIRVAAN — GOD OF MEMORY

Nirvaan did not rule a physical element.

He ruled remembrance.

He believed humanity should eventually exist without divine control.

The seven gods considered this dangerous.

They could not kill Nirvaan, so they erased him from history.

His temples were destroyed.

His name disappeared.

His followers forgot him.

His existence was removed from collective memory.

But the act of erasing Nirvaan damaged reality.

Thousands of forgotten people became trapped between life and death.

Centuries later, the kingdom of Avarsha experiences an impossible event.

Everyone falls asleep simultaneously.

Only a young warrior named Ishan remains awake.

He discovers that the statues of the seven gods are bleeding.

A voice speaks:

> "They erased my name. But they could not erase my memory."

Ishan begins investigating the forgotten history.

He discovers seven temples.

Each temple contains part of the truth.

Each divine power grants him a supernatural ability.

But every divine mark has a cost.

The more power Ishan obtains, the more memories he loses.

Eventually he discovers:

## Ishan is the reincarnation of Nirvaan.

But the seven gods were not simply jealous.

Nirvaan discovered a cosmic entity called:

## THE FIRST MEMORY

The First Memory existed before the gods.

It feeds on memories and uses civilizations as a source of existence.

The gods erased Nirvaan because they believed removing his memory would prevent humanity from discovering the truth.

The gods were wrong.

The forgotten god returned.

---

# 5. THEMES

Primary themes:

- Memory
- Identity
- Free will
- Sacrifice
- Power
- History
- Truth
- Mortality
- Whether gods should rule humanity
- Whether a person remains themselves after losing their memories

Secondary themes:

- The reliability of history
- Generational trauma
- The cost of power
- The difference between being remembered and being alive

---

# 6. MAIN CHARACTER

## Ishan

Role:
- Young warrior
- Protagonist
- Reincarnation of Nirvaan

Personality:
- Determined
- Curious
- Protective
- Initially loyal to Avarsha
- Gradually questions the gods
- Becomes increasingly uncertain about his identity

Character progression:

### Act I
"I must protect my kingdom."

### Act II
"The kingdom has lied to me."

### Act III
"The gods have lied to humanity."

### Act IV
"I may be one of the gods."

### Final Act
"Do I preserve the world, the gods, or myself?"

---

# 7. MAIN SUPPORTING CHARACTERS

## Queen Amara

Leader of Avarsha.

Purpose:
- introduces the political world;
- gives Ishan early missions;
- represents humanity's dependence on divine protection.

Secret:
She has been receiving memories from Nirvaan in her dreams.

---

## Dev

Ishan's childhood friend.

Purpose:
- emotional anchor;
- combat companion;
- comic relief in early chapters;
- demonstrates the effect of memory loss.

Potential tragedy:
Ishan eventually forgets Dev.

---

## Mira

A mysterious historian.

Purpose:
- explains ancient inscriptions;
- guides the player toward the temples;
- knows more about Ishan than she admits.

Twist:
Mira is one of the Forgotten — a person erased from history.

---

## THE ARCHIVIST

A masked supernatural figure.

Purpose:
- recurring antagonist;
- observes Ishan;
- manipulates events;
- provides contradictory information.

Truth:
The Archivist is a previous manifestation of Nirvaan who failed to break the cycle.

---

# 8. THE SEVEN GODS

## 8.1 AGNIYA — FIRE

Domain:
Fire, destruction, purification.

Visual identity:
- obsidian;
- molten stone;
- gold;
- red-orange light;
- giant furnaces;
- volcanic architecture.

Region:
**The Burning City**

Gameplay:
- fire puzzles;
- burning bridges;
- heat hazards;
- fire creatures;
- destructible wooden structures.

Ability:
**Ember Step**

Allows Ishan to perform a short fire dash.

Cost:
Repeated use temporarily removes minor memories.

Boss:
**The Flame Sovereign**

---

## 8.2 VARUNA — OCEAN

Domain:
Water, depth, forgotten dead.

Region:
**The Drowned Palace**

Gameplay:
- underwater sections;
- water-level puzzles;
- submerged ruins;
- aquatic enemies;
- pressure zones.

Ability:
**Tide Veil**

Allows temporary underwater traversal and water manipulation.

Boss:
**The Leviathan of the Deep**

---

## 8.3 VAYU — WIND

Domain:
Wind, freedom, movement.

Region:
**The Sky Kingdom**

Gameplay:
- floating islands;
- vertical traversal;
- wind currents;
- gliding;
- moving platforms.

Ability:
**Wind Leap**

Allows extended aerial movement.

Boss:
**The Storm Serpent**

---

## 8.4 DHARA — EARTH

Domain:
Earth, mountains, stone, endurance.

Region:
**The Buried Kingdom**

Gameplay:
- caves;
- earthquakes;
- movable stone structures;
- underground civilization.

Ability:
**Earth Break**

Allows destruction of marked stone barriers.

Boss:
**The Stone Colossus**

---

## 8.5 SURYA — SUN

Domain:
Light, truth, heat.

Region:
**The Endless Desert**

Gameplay:
- day/night contrast;
- mirrors;
- light puzzles;
- sandstorms.

Ability:
**Solar Sight**

Reveals hidden memories and invisible objects.

Boss:
**The Sun-Eater**

---

## 8.6 CHANDRA — MOON

Domain:
Dreams, illusion, night.

Region:
**The Dreaming Forest**

Gameplay:
- reality shifts;
- illusion puzzles;
- duplicate environments;
- enemies appearing/disappearing.

Ability:
**Moonwalk**

Allows Ishan to enter temporary memory echoes.

Boss:
**The Dream Queen**

---

## 8.7 KAAL — TIME

Domain:
Time, decay, destiny.

Region:
**The Temple Outside Time**

Gameplay:
- time manipulation;
- reversed events;
- younger/older versions of NPCs;
- time-loop puzzles.

Ability:
**Moment Break**

Slows nearby enemies and objects.

Boss:
**The Clockless King**

---

# 9. THE EIGHTH GOD

## NIRVAAN — MEMORY

Nirvaan should not be presented as a conventional combat boss.

His power is narrative and systemic.

His domain affects:

- save/load behavior;
- dialogue;
- world state;
- NPC knowledge;
- environmental changes;
- player memories.

Nirvaan's symbol:

A closed eye surrounded by eight broken rings.

---

# 10. THE FIRST MEMORY

The ultimate cosmic entity.

It should NOT resemble a normal humanoid villain.

Visual concept:

- enormous floating eye;
- fragments of ancient statues;
- thousands of floating memories;
- human silhouettes;
- broken temples;
- cosmic darkness;
- golden threads connecting memories.

It exists outside normal time.

Its voice should sound layered, ancient, and non-human.

---

# 11. GAME STRUCTURE

The game is divided into five acts.

## ACT I — THE NIGHT WITHOUT DREAMS

Objectives:

- Introduce Avarsha.
- Introduce Ishan.
- Establish combat.
- Establish exploration.
- Show simultaneous sleep event.
- Introduce first supernatural event.
- Reveal bleeding god statues.

Final scene:
Ishan hears Nirvaan's voice.

---

## ACT II — THE SEVEN TEMPLES

Player explores the first four regions.

Objectives:

- obtain divine marks;
- discover forgotten history;
- meet Mira;
- encounter the Archivist;
- introduce memory costs.

---

## ACT III — THE FORGOTTEN

Player discovers:

- erased civilizations;
- Forgotten enemies;
- evidence of Nirvaan;
- contradictions in official history.

The player starts questioning whether the gods are good.

---

## ACT IV — THE GOD WITH NO NAME

Player discovers:

- Ishan is Nirvaan's reincarnation;
- the Archivist is another failed manifestation;
- the seven gods are hiding the truth.

---

## ACT V — THE FIRST MEMORY

Player reaches the celestial realm.

All seven gods appear.

They reveal the existence of The First Memory.

Final confrontation.

Player chooses ending.

---

# 12. CORE GAMEPLAY LOOP

The core loop must be:

1. Explore.
2. Discover lore.
3. Fight enemies.
4. Solve environmental puzzles.
5. Discover memory fragments.
6. Unlock divine abilities.
7. Return to previously inaccessible areas.
8. Fight stronger enemies.
9. Discover story revelations.
10. Make meaningful choices.
11. Progress toward the next temple.

---

# 13. COMBAT SYSTEM

Combat must be responsive and readable.

Basic actions:

- Light attack
- Heavy attack
- Dodge
- Block
- Parry
- Jump
- Divine ability
- Finisher
- Lock-on

Weapon:
**Astra Blade**

Weapon progression:

Level 1:
Basic attacks.

Level 2:
Combo extensions.

Level 3:
Elemental interaction.

Level 4:
Divine ability integration.

Level 5:
Memory-based attacks.

---

# 14. COMBO SYSTEM

Minimum combo set:

Light → Light → Light

Light → Light → Heavy

Light → Heavy

Heavy → Heavy

Dodge → Light

Parry → Heavy

Ability → Light

Finisher when enemy health is below threshold.

Combat must not depend on long animation locks.

Player must retain reasonable control.

---

# 15. DODGE AND PARRY

Dodge:

- short invulnerability window;
- stamina cost;
- directional movement.

Parry:

- narrow timing window;
- successful parry staggers enemy;
- perfect parry restores a small amount of divine energy.

Failed parry:
- player receives damage.

Difficulty must modify timing windows rather than simply multiplying enemy health.

---

# 16. ENEMY ARCHETYPES

Every enemy must have:

- unique visual identity;
- attack patterns;
- telegraphs;
- hit reactions;
- death animation;
- audio cues;
- navigation behavior;
- difficulty scaling.

Enemy classes:

1. Forgotten Soldier
2. Ash Creature
3. Naga-Raksh
4. Stone Guardian
5. Sky Hunter
6. Dream Stalker
7. Time Wraith
8. Divine Guardian
9. Mini-boss
10. Boss

---

# 17. ENEMY AI

AI must support:

- patrol;
- investigate;
- chase;
- attack;
- retreat;
- stagger;
- search;
- return-to-home;
- group coordination;
- death.

Use navigation meshes for movement.

Unity's current AI Navigation package supports runtime/edit-time NavMeshes, dynamic obstacles, and links for special traversal actions. Use those capabilities instead of building unnecessary pathfinding from scratch.

Source:
https://docs.unity.com/engine/6000.7/manual/packages-list/packages-all/pack-safe/com-unity-ai-navigation

---

# 18. BOSS DESIGN

Each boss must have:

### Phase 1
Readable attacks.

### Phase 2
Environmental interaction.

### Phase 3
Major supernatural transformation.

Boss requirements:

- health bar;
- phase transitions;
- unique music;
- unique arena;
- telegraphed attacks;
- stagger window;
- scripted cinematic moment;
- victory sequence;
- reward.

Do not make bosses difficult by only increasing HP.

---

# 19. MEMORY SYSTEM

This is the game's signature mechanic.

Every major memory is represented as a collectible object.

Memory categories:

- Personal
- Historical
- Divine
- Forbidden
- Lost
- False

Each memory contains:

- ID
- title
- description
- owner
- location
- importance
- associated characters
- associated quest
- visual representation
- audio narration

---

# 20. MEMORY CORRUPTION

Some divine powers consume memory.

Implement a safe abstract system:

`MemoryIntegrity = 0.0 - 1.0`

Do not permanently destroy critical progression data because of ordinary gameplay.

Instead:

- cosmetic memories can disappear;
- optional dialogue can change;
- visual flashbacks can become incomplete;
- minor NPC recognition can change.

Critical quest flags must remain protected.

---

# 21. MEMORY STATES

Possible states:

`KNOWN`

`PARTIALLY_REMEMBERED`

`FORGOTTEN`

`CORRUPTED`

`RESTORED`

`FALSE_MEMORY`

Critical story memories must never become permanently inaccessible because of a bug.

---

# 22. DIALOGUE SYSTEM

Dialogue must be data-driven.

Do not hard-code every conversation into gameplay scripts.

Recommended data:

```text
DialogueID
Speaker
Text
RequiredFlags
RequiredMemoryState
Choices
Consequences
NextDialogueID
VoiceAsset
Subtitle
```

Dialogue choices can modify:

- relationship effects once their behaviour is designed (deferred in the vertical slice);
- quest flags;
- memory states;
- NPC behavior;
- optional ending conditions.

---

# 23. QUEST SYSTEM

Quest object:

```text
QuestID
Title
Description
Objectives
CurrentObjective
RequiredFlags
Rewards
FailureConditions
CompletionFlags
```

Quest objectives should support:

- reach location;
- defeat enemy;
- collect item;
- interact;
- talk;
- solve puzzle;
- survive;
- escort;
- choose dialogue;
- obtain memory.

---

# 24. WORLD DESIGN

Main hub:

## AVARSHA

Contains:

- royal palace;
- marketplace;
- temple district;
- warrior training ground;
- library;
- residential district;
- ancient ruins;
- hidden underground passage.

The hub changes as the story progresses.

Examples:

Act I:
Normal kingdom.

Act II:
Strange disappearances.

Act III:
Forgotten citizens appear.

Act IV:
Reality starts breaking.

Act V:
The city partially exists outside time.

---

# 25. ENVIRONMENTAL STORYTELLING

Do not rely exclusively on dialogue.

Use:

- statues;
- inscriptions;
- murals;
- abandoned weapons;
- destroyed villages;
- skeleton arrangements;
- offerings;
- environmental sound;
- NPC routines;
- hidden rooms;
- memory echoes.

Every major area should contain at least:

- 1 main narrative clue;
- 2 optional lore discoveries;
- 1 visual mystery;
- 1 environmental storytelling moment.

---

# 26. PUZZLE SYSTEM

Puzzle categories:

1. Element matching
2. Pressure plates
3. Light reflection
4. Water movement
5. Wind direction
6. Time manipulation
7. Memory reconstruction
8. Symbol sequences
9. Environmental traversal
10. Multi-stage temple puzzles

Puzzle design rule:

The player should understand the underlying logic before solving it.

Avoid arbitrary "guess the combination" puzzles.

---

# 27. EXPLORATION

Traversal abilities unlock progressively.

Initial:

- walk;
- sprint;
- jump;
- climb basic surfaces.

Later:

- fire dash;
- underwater movement;
- wind glide;
- stone breaking;
- solar sight;
- dream traversal;
- time manipulation.

Previously inaccessible areas should become accessible after acquiring abilities.

---

# 28. INVENTORY

Categories:

- Weapons
- Divine Marks
- Memory Fragments
- Quest Items
- Lore
- Consumables

Inventory must be simple.

Avoid excessive item clutter.

---

# 29. PROGRESSION

Three progression layers:

## Character

Health, stamina, divine energy.

## Weapon

Damage, combo upgrades.

## Divine

Abilities from the seven gods.

## Memory

Narrative and supernatural progression.

---

# 30. SKILL TREE

Branches:

### Warrior

- attack damage;
- combo extensions;
- parry upgrades.

### Guardian

- health;
- block;
- damage reduction.

### Divine

- ability power;
- cooldown;
- divine energy.

### Memory

- memory detection;
- memory restoration;
- memory manipulation.

---

# 31. SAVE SYSTEM

Use multiple layers of protection.

Save types:

- automatic checkpoint;
- manual save;
- chapter save;
- settings save.

Never save during a critical state transition if doing so could corrupt progression.

Before replacing a save:

1. Write temporary save.
2. Validate.
3. Replace primary save.
4. Maintain backup.

Save data should contain:

```text
Version
Timestamp
PlayerPosition
PlayerStats
Inventory
Abilities
QuestStates
WorldStates
NPCStates
MemoryStates
BossStates
DialogueFlags
EndingFlags
Settings
```

---

# 32. SAVE CORRUPTION HANDLING

If a save fails validation:

1. Do not overwrite it.
2. Load latest valid backup.
3. Display:
   "Your previous save could not be loaded. A safe backup has been restored."
4. Log technical details.
5. Continue from the backup.

Never silently delete user saves.

---

# 33. CHECKPOINT SYSTEM

Checkpoints occur:

- before boss fights;
- after boss fights;
- after major story scenes;
- after temple completion;
- before irreversible story decisions.

Checkpoint activation must be idempotent.

Calling it twice must not duplicate rewards.

---

# 34. GRAPHICS DIRECTION

Target visual style:

**Cinematic ancient dark fantasy.**

Key visual elements:

- enormous temples;
- weathered stone;
- gold ornamentation;
- carved symbols;
- dramatic skies;
- volumetric fog;
- fire;
- water;
- dust;
- sand;
- glowing divine energy;
- ancient vegetation;
- atmospheric lighting.

Avoid generic medieval-European fantasy architecture.

Architecture should use a fictional ancient civilization with recurring visual motifs.

---

# 35. COLOR LANGUAGE

Use environmental color identities without making the entire game monochromatic.

Agniya:
- warm fire tones.

Varuna:
- deep ocean tones.

Vayu:
- sky and cloud tones.

Dhara:
- earth and stone tones.

Surya:
- bright gold/desert tones.

Chandra:
- cool moonlit tones.

Kaal:
- desaturated temporal distortion.

Nirvaan:
- pale gold + deep shadow.

The First Memory:
- black void + fragmented luminous memories.

---

# 36. VFX REQUIREMENTS

Required effects:

- fire particles;
- sparks;
- smoke;
- dust;
- water caustics;
- underwater particles;
- wind trails;
- lightning;
- divine energy;
- memory fragments;
- time distortion;
- dream distortion;
- glowing symbols;
- boss transformations.

VFX must be optimized.

Avoid excessive transparent particle effects that destroy performance.

---

# 37. LIGHTING

Lighting should be story-driven.

Examples:

Avarsha:
Warm natural sunlight.

Forgotten areas:
Low ambient light.

Temple of Chandra:
Moonlight and fog.

Temple of Kaal:
Impossible shadows.

The First Memory:
Light sources appear to come from memories rather than physical objects.

---

# 38. CAMERA

Third-person camera.

Features:

- smooth follow;
- collision avoidance;
- adjustable sensitivity;
- lock-on camera;
- cinematic camera;
- boss camera;
- cutscene camera.

Camera must not clip through major geometry.

---

# 39. ANIMATION

Required player animations:

- idle;
- walk;
- sprint;
- jump;
- fall;
- land;
- light attack;
- heavy attack;
- combo attacks;
- dodge;
- block;
- parry;
- hit;
- stagger;
- death;
- interact;
- climb;
- swim;
- ability animations;
- finishers.

Use animation blending to prevent abrupt transitions.

---

# 40. AUDIO

Music should use:

- ancient percussion;
- low drones;
- strings;
- atmospheric vocals;
- wind;
- bells;
- ritual instruments.

Each god needs a unique musical identity.

Environmental audio:

- temple ambience;
- wind;
- fire;
- water;
- insects;
- distant creatures;
- stone movement;
- supernatural whispers.

Combat audio:

- weapon impact;
- armor impact;
- enemy vocalizations;
- parry;
- dodge;
- divine ability;
- boss phase transition.

---

# 41. VOICE ACTING

The game should be playable without voice acting.

Every spoken line must have subtitles.

Voice acting can be added later.

Do not block gameplay progression on missing audio files.

If a voice asset is missing:

- use subtitle;
- use non-verbal fallback audio if appropriate;
- log warning;
- continue.

---

# 42. UI

Required screens:

- Main Menu
- New Game
- Continue
- Settings
- Controls
- Pause
- Inventory
- Map
- Quest Log
- Memory Archive
- Skill Tree
- Save/Load
- Credits

HUD:

- health;
- stamina;
- divine energy;
- equipped ability;
- objective;
- optional minimap;
- boss health.

HUD must be hideable.

---

# 43. ACCESSIBILITY

Include:

- subtitle size;
- subtitle background;
- text scaling;
- colorblind-friendly indicators;
- camera shake toggle;
- motion blur toggle if a motion blur effect is introduced;
- screen effects toggle;
- aim/lock-on assistance;
- difficulty settings;
- controller remapping;
- keyboard remapping;
- audio volume controls.

Important gameplay information must never be communicated by color alone.

---

# 44. DIFFICULTY

Modes:

### Story
Lower combat pressure.

### Normal
Default intended experience.

### Warrior
Stronger enemies and stricter timing.

### Mythic
Advanced mode.

Difficulty should primarily affect:

- enemy behavior;
- damage;
- timing windows;
- resource availability.

Do not dramatically increase grind.

---

# 45. PERFORMANCE TARGET

Primary target:

**1080p / 60 FPS on a reasonable modern gaming PC.**

Fallback:

**1080p / 30 FPS.**

Performance systems:

- LOD;
- occlusion culling;
- object pooling;
- texture compression;
- GPU particle limits;
- streaming;
- efficient AI updates;
- reduced update frequency for distant NPCs;
- baked/static optimization where appropriate.

For large-world expansion, use streaming rather than keeping every region active simultaneously.

---

# 46. ENGINE ARCHITECTURE

Use modular systems.

Suggested folders:

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

---

# 47. CORE SOFTWARE ARCHITECTURE

Use interfaces and data-driven configuration.

Major systems:

```text
GameManager
SaveManager
SceneManager
PlayerController
CombatSystem
AbilitySystem
HealthSystem
StaminaSystem
EnemyAI
QuestManager
DialogueManager
MemoryManager
InventoryManager
ProgressionManager
AudioManager
UIManager
CheckpointManager
SettingsManager
EventBus
```

Do not make every system depend directly on every other system.

Prefer:

- events;
- interfaces;
- ScriptableObjects;
- dependency injection where practical;
- centralized state management.

---

# 48. SCRIPTABLEOBJECT DATA

Use ScriptableObjects for:

- weapons;
- enemies;
- abilities;
- items;
- quests;
- dialogue;
- memories;
- bosses;
- regions;
- audio configurations.

This allows content changes without rewriting gameplay code.

---

# 49. EVENT SYSTEM

Implement an event-driven architecture.

Examples:

```text
OnQuestStarted
OnQuestCompleted
OnMemoryDiscovered
OnMemoryLost
OnMemoryRestored
OnBossStarted
OnBossDefeated
OnAbilityUnlocked
OnPlayerDied
OnCheckpointActivated
OnDialogueStarted
OnDialogueCompleted
OnWorldStateChanged
```

Avoid global static state unless necessary.

---

# 50. ERROR HANDLING

Every system must fail gracefully.

## Missing asset

Fallback:
- placeholder;
- default material;
- default audio;
- warning log.

## Missing dialogue

Display:
"[Dialogue unavailable]"

Do not crash.

## Missing quest data

Prevent quest activation and log an actionable error.

## Missing save

Create a new save profile.

## Corrupted save

Use backup.

## Invalid player position

Teleport player to latest valid checkpoint.

## Navigation failure

Fallback:
- stop;
- recalculate;
- choose nearest valid point;
- return to patrol.

## Missing animation

Use fallback idle/movement animation.

## Missing audio

Continue gameplay silently.

---

# 51. LOGGING

Use categorized logs:

```text
[GAME]
[PLAYER]
[COMBAT]
[AI]
[QUEST]
[DIALOGUE]
[MEMORY]
[SAVE]
[UI]
[AUDIO]
[PERFORMANCE]
[ERROR]
```

Every error must answer:

1. What failed?
2. Where?
3. Why?
4. What fallback occurred?

Avoid logging every frame.

---

# 52. DEBUG MODE

Create developer tools:

- teleport;
- unlock ability;
- complete quest;
- reset quest;
- spawn enemy;
- kill all enemies;
- restore memory;
- corrupt test memory;
- set world state;
- trigger boss;
- refill health;
- refill stamina;
- show FPS;
- show AI state;
- show navigation;
- show quest flags;
- show save version.

Debug mode must be disabled in release builds.

---

# 53. TESTING

Every major system needs automated and manual tests.

## Combat

Test:

- damage;
- death;
- parry;
- dodge;
- combos;
- boss phases.

## Memory

Test:

- acquisition;
- loss;
- restoration;
- corrupted state;
- critical memory protection.

## Quest

Test:

- start;
- progress;
- completion;
- failure;
- reload;
- duplicate completion.

## Save

Test:

- save;
- load;
- backup;
- corrupted data;
- version migration.

## AI

Test:

- patrol;
- chase;
- attack;
- death;
- navigation failure.

---

# 54. EDGE CASES

The following must be explicitly handled:

1. Player dies during a cutscene.
2. Player closes the game during a boss transition.
3. Player saves immediately before a scripted event.
4. Player loads an older save after acquiring an ability.
5. Player completes a quest twice.
6. Player interacts with an NPC while another dialogue is active.
7. Player leaves a boss arena during combat.
8. Boss dies during phase transition.
9. Enemy falls outside the world.
10. Player falls outside the world.
11. Navigation mesh becomes unavailable.
12. Missing asset bundle.
13. Missing audio.
14. Missing animation.
15. Corrupted save.
16. Save version mismatch.
17. Player loses a non-critical memory.
18. Player attempts to lose a protected memory.
19. Player pauses during a cinematic.
20. Player changes graphics settings during loading.
21. Player disconnects a controller.
22. Controller reconnects.
23. Window loses focus.
24. Resolution changes.
25. Player rapidly presses interaction.
26. Player repeatedly triggers a checkpoint.
27. Boss is defeated before dialogue trigger.
28. NPC is killed before required quest dialogue.
29. Player enters an area before the intended story trigger.
30. Player reaches a late-game area using unintended traversal.

---

# 55. ANTI-SOFTLOCK RULES

No player action should permanently block the main story unless explicitly designed as an ending.

Critical:

- quest items cannot be permanently lost;
- required NPCs cannot disappear permanently;
- critical memories cannot become permanently corrupted;
- essential doors must have recovery states;
- important boss arenas must reset safely;
- death must return player to a valid checkpoint.

---

# 56. ANTI-CHEESE RULES

Prevent obvious progression-breaking exploits:

- out-of-bounds traversal;
- boss arena escape;
- permanent enemy aggro;
- infinite resource loops;
- duplicate item rewards;
- repeated quest completion;
- animation cancellation abuse where it breaks progression.

Do not over-restrict harmless experimentation.

---

# 57. CONTENT PIPELINE

Every content item must have:

1. Unique ID.
2. Description.
3. Owner/system.
4. Dependencies.
5. Asset references.
6. Test case.
7. Completion criteria.

Naming convention:

```text
SCN_Avarsha_Hub
SCN_Temple_Agniya
NPC_Ishan
NPC_Amara
NPC_Dev
NPC_Mira
BOSS_Agniya
ENEMY_ForgottenSoldier
ABILITY_EmberStep
MEMORY_Nirvaan_001
QUEST_Main_001
```

---

# 58. VIBE-CODING RULES

When generating code with AI:

1. Never generate an entire game in one script.
2. Create one system at a time.
3. Keep scripts small.
4. Explain dependencies before adding them.
5. Prefer reusable components.
6. Do not duplicate logic.
7. Do not hard-code story content into combat scripts.
8. Do not hard-code coordinates where data can be used.
9. Every generated system must compile before continuing.
10. Every major feature must include a test.
11. Never silently change architecture.
12. Update this specification when architecture changes.

---

# 59. AI CODING COMMAND FORMAT

When asking a coding agent to implement something, use:

```text
TASK:
Implement [FEATURE].

CONTEXT:
Use SPEC.md as the source of truth.

CONSTRAINTS:
- Do not modify unrelated systems.
- Follow existing architecture.
- Keep code modular.
- Add error handling.
- Add debug logging.
- Add tests.

ACCEPTANCE:
[List measurable acceptance criteria.]

OUTPUT:
- Files created
- Files modified
- Dependencies
- Test results
- Known limitations
```

---

# 60. DEVELOPMENT PHASES

## PHASE 0 — PROJECT FOUNDATION

Build:

- Unity project;
- folder structure;
- input system;
- scene management;
- player controller;
- camera;
- basic UI;
- logging;
- settings;
- Git repository.

Acceptance:
Player can move in a test scene without errors.

---

## PHASE 1 — COMBAT PROTOTYPE

Build:

- sword;
- light attack;
- heavy attack;
- dodge;
- block;
- parry;
- health;
- enemy;
- death;
- checkpoint.

Acceptance:
Player can fight and defeat three enemy types.

---

## PHASE 2 — AVARSHA VERTICAL SLICE

Build:

- hub;
- NPCs;
- dialogue;
- first quest;
- basic exploration;
- first memory;
- first cinematic.

Acceptance:
Player can complete approximately 20–30 minutes of gameplay.

---

## PHASE 3 — AGNIYA TEMPLE

Build:

- full environment;
- fire VFX;
- fire puzzles;
- Ember Step;
- enemy variants;
- boss;
- reward;
- memory cost.

Acceptance:
Complete temple from entrance to boss.

---

## PHASE 4 — MEMORY SYSTEM

Build:

- memory archive;
- memory states;
- memory corruption;
- memory restoration;
- narrative consequences.

Acceptance:
Changing a memory changes at least one non-critical world/dialogue state.

---

## PHASE 5 — REMAINING TEMPLES

Implement temples using reusable systems.

Each temple must introduce at least one unique gameplay mechanic.

---

## PHASE 6 — FINAL ACT

Build:

- celestial realm;
- seven gods;
- The First Memory;
- final boss;
- ending system;
- ending cinematics.

---

## PHASE 7 — POLISH

Add:

- VFX;
- sound;
- music;
- animation polish;
- UI polish;
- optimization;
- accessibility;
- bug fixing.

---

# 61. VERTICAL SLICE DEFINITION

Before expanding to all seven temples, the project must contain:

- playable Ishan;
- polished third-person camera;
- sword combat;
- one enemy;
- one mini-boss;
- Avarsha hub;
- one quest;
- one cinematic;
- one memory mechanic;
- one puzzle;
- one divine ability;
- one small temple section;
- save/load;
- settings;
- pause menu;
- working UI.

The vertical slice must be fun without requiring future content.

---

# 62. SUCCESS CRITERIA

The vertical slice succeeds only if:

### Gameplay

- movement feels responsive;
- combat is readable;
- enemy attacks are telegraphed;
- player can understand objectives without external instructions.

### Story

- player understands the initial mystery;
- player wants to discover who Nirvaan is;
- player understands that memory is important;
- ending of the slice creates curiosity.

### Visuals

- environment clearly feels ancient;
- gods have distinct visual identities;
- VFX communicate abilities;
- lighting creates atmosphere.

### Technical

- no game-breaking errors;
- save/load works;
- player cannot become permanently stuck;
- missing assets do not crash the game;
- scene transitions work;
- acceptable frame rate.

---

# 63. FULL GAME SUCCESS CRITERIA

The complete game succeeds when:

1. All seven temples are playable.
2. Every god has unique mechanics.
3. Memory system affects gameplay and narrative.
4. All major quests are completable.
5. All endings are reachable.
6. Save/load survives repeated testing.
7. No known progression-blocking bugs remain.
8. Accessibility options work.
9. Performance target is met.
10. Art, audio, gameplay, and narrative share a coherent identity.

---

# 64. DEFINITION OF DONE

A feature is NOT done merely because it works once.

A feature is done when:

- implementation exists;
- happy path works;
- edge cases are handled;
- errors are logged;
- fallback exists where appropriate;
- save/load works;
- UI feedback exists;
- performance is acceptable;
- code is documented where non-obvious;
- manual test completed;
- automated test exists where practical;
- no unrelated functionality was broken.

---

# 65. FIRST IMPLEMENTATION TASK

The coding agent must NOT attempt the entire game.

Start with:

## TASK 001 — PROJECT FOUNDATION

Create:

```text
Unity project
+
folder structure
+
input actions
+
GameManager
+
SceneManager
+
SettingsManager
+
logging system
+
basic third-person player
+
camera
+
test scene
+
pause menu
```

Acceptance:

- project opens without compile errors;
- player moves;
- camera follows;
- input works;
- pause works;
- scene loads;
- logs appear correctly;
- no unnecessary dependencies are introduced.

---

# 66. SECOND IMPLEMENTATION TASK

## TASK 002 — COMBAT FOUNDATION

Create:

- HealthComponent;
- StaminaComponent;
- CombatController;
- WeaponController;
- Hitbox;
- Hurtbox;
- DamageData;
- EnemyHealth;
- PlayerDeath;
- Checkpoint.

Acceptance:

- player can attack;
- enemy takes damage;
- enemy dies;
- player can die;
- checkpoint restores player;
- no duplicate damage events.

---

# 67. THIRD IMPLEMENTATION TASK

## TASK 003 — AVARSHA

Create:

- Avarsha scene;
- basic architecture;
- Queen Amara;
- Dev;
- Mira placeholder;
- NPC interaction;
- dialogue system;
- first quest;
- first memory fragment.

Acceptance:

Player can:

1. enter Avarsha;
2. speak to Amara;
3. receive quest;
4. explore;
5. find memory;
6. return;
7. trigger first supernatural event.

---

# 68. FIRST PLAYABLE STORY SEQUENCE

The first 15–20 minutes should follow:

### Scene 1
Avarsha at sunrise.

### Scene 2
Ishan trains.

### Scene 3
Queen Amara gives mission.

### Scene 4
Ishan returns to city.

### Scene 5
Everyone suddenly falls asleep.

### Scene 6
Ishan explores empty city.

### Scene 7
Statues begin bleeding.

### Scene 8
A dead soldier appears.

### Scene 9
First memory fragment discovered.

### Scene 10
Nirvaan speaks.

### Scene 11
Title reveal:

# THE GOD WHO WAS FORGOTTEN

---

# 69. CINEMATIC DIRECTION

Do not make every story scene a cutscene.

Use gameplay storytelling wherever possible.

Major cinematics:

- simultaneous sleep;
- bleeding statues;
- first Nirvaan voice;
- first god encounter;
- temple boss transformation;
- Ishan identity reveal;
- seven gods kneeling;
- First Memory reveal;
- final choice;
- endings.

---

# 70. MEMORY VISUALIZATION

Memories should appear as:

- floating particles;
- translucent environments;
- ghostly characters;
- fragmented audio;
- golden threads;
- incomplete geometry.

When memory is corrupted:

- geometry breaks;
- dialogue becomes incomplete;
- audio reverses;
- visual artifacts appear;
- characters may have missing faces.

Use this sparingly.

---

# 71. WORLD STATE SYSTEM

Create a central world-state model.

Example:

```text
WORLD_STATE:
    AvarshaThreatLevel
    AgniyaDefeated
    VarunaDefeated
    VayuDefeated
    DharaDefeated
    SuryaDefeated
    ChandraDefeated
    KaalDefeated
    NirvaanAwakened
    ArchivistRevealed
    FirstMemoryAwakened
```

World state changes should trigger events rather than manually editing dozens of objects.

---

# 72. ENDING CONDITIONS

Ending A:

`AcceptDivinity = true`

Ending B:

`DestroyGods = true`

Ending C:

`SelfSacrifice = true`

Hidden Ending:

Requires a defined combination of:

- restored memories;
- optional lore;
- specific choices;
- Archivist discoveries;
- temple secrets.

Do not make the hidden ending dependent on random conditions.

---

# 73. LOCALIZATION

All user-facing text must be externalized.

Never hard-code text into scripts.

Support future:

- English;
- Hindi;
- other languages.

---

# 74. RESOURCE MANAGEMENT

Avoid loading every asset into memory.

Use:

- addressable/streaming systems where appropriate;
- scene-based loading;
- pooled enemies/projectiles;
- unload unused assets.

Large worlds must not keep all regions resident simultaneously.

---

# 75. SECURITY AND DATA INTEGRITY

Local game saves must be treated as untrusted input.

Validate:

- numeric ranges;
- IDs;
- enum values;
- array lengths;
- object references;
- save version.

Never allow invalid save data to crash the game.

Implement save version migration.

---

# 76. TELEMETRY

For the offline prototype:

Do not collect personal user information.

Developer analytics may be added later.

If analytics are introduced:

- make them opt-in where appropriate;
- document collected events;
- avoid collecting unnecessary personal data.

---

# 77. ASSET ACQUISITION RULES

Use:

- original assets;
- properly licensed marketplace assets;
- CC0/public-domain assets where license permits;
- generated placeholder assets.

Track asset licenses in:

```text
ASSET_LICENSES.md
```

Do not use ripped game assets.

Do not use copyrighted music without a license.

---

# 78. PLACEHOLDER-FIRST RULE

Vibe coding must never stall because final art is unavailable.

Use placeholders first:

- primitive geometry;
- placeholder humanoid;
- temporary materials;
- placeholder sounds.

Replace assets progressively.

Gameplay systems must not depend on final art.

---

# 79. AI-GENERATED ASSET RULE

If AI-generated art/audio is used:

- keep prompts and source information documented;
- ensure commercial-use rights are appropriate;
- maintain consistency across character designs;
- avoid direct imitation of living artists;
- avoid copying recognizable copyrighted characters.

---

# 80. GITHUB WORKFLOW

Recommended branches:

```text
main
develop
feature/player
feature/combat
feature/ai
feature/memory
feature/quests
feature/world
feature/ui
```

Commit style:

```text
feat: add player controller
feat: add combat system
fix: prevent duplicate checkpoint rewards
fix: handle missing dialogue asset
refactor: separate memory state manager
```

Never commit:

- Library/
- Temp/
- Logs/
- UserSettings/
- build artifacts
- secrets.

---

# 81. PROJECT DOCUMENTATION

Maintain:

```text
SPEC.md
README.md
ARCHITECTURE.md
GAME_DESIGN.md
STORY_BIBLE.md
ASSET_LICENSES.md
CHANGELOG.md
TEST_PLAN.md
KNOWN_ISSUES.md
```

SPEC.md remains the authoritative requirements document.

---

# 82. STORY BIBLE REQUIREMENT

Maintain a separate story bible containing:

- character biographies;
- timeline;
- mythology;
- god relationships;
- geography;
- terminology;
- dialogue rules;
- lore;
- secrets;
- endings.

Do not allow AI coding agents to invent new canon casually.

If a new story element is needed, propose it first and update the story bible.

---

# 83. AI/VIBE-CODING MASTER RULE

The coding agent must follow this sequence:

```text
READ SPEC
↓
READ ARCHITECTURE
↓
IDENTIFY CURRENT IMPLEMENTATION
↓
PLAN SMALL CHANGE
↓
IMPLEMENT
↓
COMPILE
↓
TEST
↓
FIX
↓
DOCUMENT
↓
REPORT
```

Never:

```text
READ SPEC
↓
GENERATE ENTIRE GAME
```

---

# 84. RESPONSE FORMAT FOR CODING AGENT

After every implementation task, report:

```text
IMPLEMENTED:
[List]

FILES CREATED:
[List]

FILES MODIFIED:
[List]

DEPENDENCIES:
[List]

TESTS:
[List]

ERRORS FIXED:
[List]

KNOWN LIMITATIONS:
[List]

NEXT RECOMMENDED TASK:
[One task]
```

---

# 85. FINAL QUALITY BAR

The project should feel like:

> "A forgotten ancient civilization is hiding a supernatural truth."

Not:

> "A collection of fantasy levels."

Every system should reinforce:

**MEMORY → GODS → IDENTITY → TRUTH → CHOICE**

That is the central identity of the game.

---

# 86. NON-NEGOTIABLE REQUIREMENTS

The following must never be compromised:

- Original fictional mythology.
- Responsive player controls.
- Reliable save system.
- No permanent progression softlocks.
- Data-driven quests.
- Data-driven dialogue.
- Modular code.
- Graceful error handling.
- Memory mechanic must matter.
- Every god must feel mechanically different.
- Visual identity must remain consistent.
- Critical story content must never become inaccessible because of ordinary player actions.
- Placeholder assets must be replaceable without rewriting systems.
- AI-generated code must be incremental and testable.

---

# 87. FINAL IMPLEMENTATION PRINCIPLE

Build the game from the inside out:

```text
FOUNDATION
    ↓
PLAYER
    ↓
COMBAT
    ↓
AI
    ↓
WORLD
    ↓
QUESTS
    ↓
MEMORY
    ↓
DIVINE POWERS
    ↓
TEMPLES
    ↓
BOSSES
    ↓
STORY
    ↓
POLISH
```

Do not start with the final boss.

Do not start with seven huge worlds.

Do not start by generating thousands of assets.

First prove that the player can walk through Avarsha, fight an enemy, discover a memory, speak to an NPC, complete a quest, save the game, die, reload, and continue.

Then expand.

---

# END OF SPECIFICATION

**Next action:** Implement `TASK 001 — PROJECT FOUNDATION`.
