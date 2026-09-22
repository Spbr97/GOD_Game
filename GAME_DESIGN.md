# GAME DESIGN — THE GOD WHO WAS FORGOTTEN

Required by SPEC.md section 81. SPEC.md says *what the game must be*; this
document says *what it currently is and why it is tuned that way*. Where they
disagree, SPEC.md wins and this file is out of date.

ARCHITECTURE.md covers how the code is arranged. STORY_BIBLE.md covers the
fiction. KNOWN_ISSUES.md covers what does not work yet.

---

## 1. What the game is

A third-person action-adventure about a warrior who is investigating a god
nobody remembers, and who is that god. Combat is deliberate and
stamina-gated — closer to a parry-and-punish duel than a crowd brawler.
Exploration is rewarded with memory, and memory is a currency: the powers that
let you reach further cost the things that make you you.

The **vertical slice** (SPEC.md section 61) is the city of Avarsha plus the
Agniya temple, and is the only content that exists. Everything below describes
the slice unless it says otherwise.

## 2. The core loop

```text
explore  →  find a memory or an NPC  →  learn something the histories deny
   ↑                                                 ↓
   └──  spend memory for power  ←  fight  ←  the world reacts
```

The loop is closed by cost. The player is never asked "do you want more
power?" — they are asked "what are you prepared to stop remembering?" Ember
Step spends memory integrity; the Agniya temple's memory toll spends it again
to pass a barrier. Both are refusable, and the game stays completable if the
player refuses every time. That is deliberate: SPEC.md section 55 forbids a
permanent block, and a cost the player cannot decline is not a choice.

## 3. Combat

### Shape
One weapon (the Astra Blade), light and heavy attacks, a dodge with
invulnerability frames, a hold-to-block guard with a parry window inside it, and
one divine ability. Lock-on is optional and widens with aim assist on.

### Tuning as built

| Action | Stamina | Notes |
|---|---|---|
| Light attack | 12 | |
| Heavy attack | 25 | |
| Dodge | 20 | i-frames from 0.05 s to 0.26 s of a 0.35 s roll |
| Finisher | 10 | Only below 20% target health, within 2.5 m |
| Ember Step | 20 stamina + 20 divine energy | 3 s cooldown, 0.25 s dash at 14 m/s, i-frames 0.03–0.18 s |

Stamina is 100, regenerates at 25/s after a 0.6 s delay. Divine energy is 100
and starts empty — it is earned, chiefly by perfect parries (15 each).

Guard: the parry window is the first 0.2 s of a block, and the first 0.08 s of
*that* is a perfect parry. A parry staggers the attacker for 1.2 s. Blocking
otherwise costs 0.6 stamina per point of damage absorbed; running out breaks the
guard and stuns for 0.8 s.

### Why these numbers
The dodge costs more than a light attack (20 versus 12) so that dodging is not
strictly better than swinging. The parry window is short and the reward is
large — a 2× damage riposte and the game's main source of divine energy — which
makes parrying the skill the combat is *about* rather than one option among
several. The finisher's health threshold and range exist so it reads as
finishing a fight, not as a move.

### Combos
Seven chains, matched longest-first inside a 0.7 s window:

| Chain | Multiplier |
|---|---|
| Parry → Heavy (*Parry Riposte*) | 2.0 |
| Light → Light → Heavy | 1.6 |
| Heavy → Heavy | 1.4 |
| Light → Light → Light | 1.3 |
| Ability → Light | 1.3 |
| Light → Heavy | 1.25 |
| Dodge → Light | 1.2 |

Every chain that starts with a defensive action pays better than the pure
offence of the same length. That is the combat's argument in numbers.

## 4. Enemies

Ten classes exist as data (SPEC.md section 16); six are authored:

| Enemy | Health | Damage | Role |
|---|---|---|---|
| Ash Creature | 35 | 7 | Fast, fragile, arrives in numbers |
| Forgotten Soldier | 60 | 10 | The baseline duel |
| Divine Guardian | 100 | 18 | Punishes greed |
| Stone Guardian | 140 | 22 | Slow, heavy, out-waits you |
| Temple Guardian (mini-boss) | 320 | 26 | Three phases |
| Agniya, the First Flame (boss) | 500 | 32 | Three phases |

AI is a state machine over Idle / Patrol / Investigate / Search / Chase /
Attack / Retreat / ReturnHome / Stagger / Dead, driven by a pure `Decide`
function so the whole table is testable without a scene. Groups hold a limited
number of attack slots, so a crowd surrounds rather than dogpiles — the count
scales with difficulty.

Boss phases raise **pace, not numbers**: attack cooldown down to 0.75× then
0.55×, movement up to 1.1× then 1.25×. SPEC.md section 18 asks explicitly that
bosses not be made hard by adding health, and a boss whose damage grew would
simply shorten the fight rather than change it.

## 5. Difficulty

Four modes (SPEC.md section 44), applied as multipliers on enemy damage, the
player's timing windows, enemy telegraph length, simultaneous attackers and
resource availability. Never on enemy health — grind is not difficulty.

| Mode | Enemy damage | Player windows | Telegraphs | Attackers | Resources |
|---|---|---|---|---|---|
| Story | 0.6× | 1.35× | 1.4× | 0.5× | 1.3× |
| Normal | 1× | 1× | 1× | 1× | 1× |
| Warrior | 1.3× | 0.8× | 0.8× | 1.5× | 0.85× |
| Mythic | 1.6× | 0.65× | 0.6× | 2× | 0.7× |

Difficulty can be changed mid-run from the Settings screen. Locking it would
punish a player for a guess made before they had played.

## 6. Memory

Memory is both the story and a resource. A fragment has a state — Unknown,
Known, PartiallyRemembered, Forgotten, Corrupted, Restored, FalseMemory — and
the player holds an **integrity** value from 0 to 1.

Spending integrity is how divine power is paid for. Ember Step spends a little
per use; the Agniya temple's toll spends a lump to open a barrier. Integrity
reaching zero does not fail the run and never blocks progress — the toll opens
regardless, which is tested.

**Critical memories cannot be corrupted or forgotten.** MEM_001, *The Name
Beneath the Stone*, is critical. This is a hard rule (SPEC.md section 55) and it
is what stops the memory economy from being able to delete the plot.

The Memory Archive (in the journal) lets the player corrupt a non-protected
memory deliberately. There is no reward for doing so yet; it exists because the
mechanic must be reachable and visible before content can be built on it.

## 7. Progression

Twelve skills across four branches (SPEC.md section 30), unlocked with points
held as a world-state counter, each with at most one prerequisite:

- **Warrior** — Heavier Strikes (+15% damage) → Extended Combos (+30% window) →
  Parry Timing (+20% window).
- **Guardian** — Vitality (+20 max health) → Steady Guard (+10% block
  efficiency) → Thick Skin (−10% damage taken).
- **Divine** — Ember Reach (+20% ability power) → Swift Renewal (−20%
  cooldown) → Deep Well (+20 max energy).
- **Memory** — Keen Eye (+50% detection) → Steady Mind (−30% restoration cost) →
  Careful Hand (−25% manipulation cost).

Each branch is a straight line rather than a tree. With twelve skills, a tree
would be a tree with no forks in it; the honest shape is a line, and it can be
branched when there is enough content to branch.

## 8. Exploration and the world

Avarsha is one scene with ten districts (see STORY_BIBLE.md for what is in
each). Traversal is walk, sprint and jump; climbing and swimming belong to Vayu
and Varuna and do not exist yet.

The world is bounded by a `WorldBounds` volume with a kill plane below it.
Leaving the world is treated as a level bug, not a player mistake: the player is
returned to their last checkpoint with a message, and an enemy that falls
through is returned to where it was authored rather than deleted.

### Puzzles
Two brazier puzzles, both stateful and both save-persistent: three braziers in
the temple district and four in the Agniya temple. Solving opens a gate. A
solved puzzle stays solved even if an element later reverts — a puzzle that
could un-solve itself behind the player is a softlock waiting to happen.

## 9. Bosses and their arenas

A boss is an ordinary enemy with phases, not a separate creature type, so
health, telegraphs, stagger, death and save-persistence are correct by reuse.

Each boss has an arena — a radius wider than the boss can see, plus a margin and
a three-second grace period. Leaving the arena during a fight does not trap or
kill the player; it **ends** the fight. The boss returns to full health, phase 1
and its start position, and the encounter can be begun again from the top.

That single rule answers three requirements at once: the arena resets safely
(section 55), chipping a boss from outside does not pay (section 56), and
walking away is allowed rather than punished (section 56 again — "do not
over-restrict harmless experimentation").

## 10. Death and checkpoints

Death returns the player to the last activated checkpoint with health and
stamina restored, after a short delay. The world is not reset — enemies stay as
they were. Checkpoints auto-save.

There is no death penalty beyond lost ground. A game about losing memory should
not also take the player's things when they fail.

## 11. Saving

Three slots — Checkpoint (automatic), Manual, Chapter. Each save is versioned
and checksummed, with a backup; a corrupt primary falls back to the backup and
says so in the exact words SPEC.md section 32 requires. A save from a newer
build is refused rather than guessed at.

Saving is blocked during moments that would restore badly, and the blockers are
named so a stuck one can be found.

## 12. UI

HUD: health, stamina and divine energy bars, lock-on target, last combo, and a
boss bar that appears only during an encounter. Every bar carries a text label,
so nothing is carried by colour alone (SPEC.md section 43).

Screens: Main Menu, New Game, Continue, Save/Load, Settings, Controls, Credits,
Pause, Journal (Quest Log + Memory Archive), Progression (Inventory + Skills),
and Map.

The Map is drawn from the scene at the moment it opens rather than from an
authored image — markers for the player, checkpoints, people, objectives,
discovered memories and bosses, each carrying a letter as well as a colour. A
hand-drawn map would be wrong the first time a building moved, and there are six
more temples to come.

## 13. Accessibility

Subtitle size and background, text scaling, camera shake toggle, screen effects
toggle, aim assist, difficulty, full keyboard and controller remapping, master
volume, resolution and graphics quality.

The governing rule is section 43's last line: important gameplay information is
never communicated by colour alone. Enemy telegraphs pulse in scale as well as
colour; every bar has a number; map markers have letters.

**Not yet:** a motion blur toggle. The game renders no motion blur, so the
toggle would govern nothing — a dead control is worse than a missing one and
harder to notice. See KNOWN_ISSUES.md.

## 14. Art and audio

Everything is a placeholder, generated in code rather than imported (SPEC.md
sections 78 and 79). Particles are built from `ParticleSystem` presets; sound
effects are procedurally generated sine tones; animation is transform tweening
on a visual child rather than a rig.

This is a deliberate stance, not a shortcut. Placeholder content that is
*wired to real triggers* proves the systems work and can be replaced one asset
at a time. Placeholder content that is merely absent proves nothing.

## 15. What the slice does not have

Six of the seven temples, Acts III–V, the endings, The First Memory, the
Archivist, climbing and swimming, voice acting, real art, music, localisation
and telemetry. All are phased in ROADMAP.md; none are lost.
