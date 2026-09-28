# PRODUCTION BRIEF — 1.0

The production decisions for version 1.0, settled by the author on 28 September 2026.
This closes `STANDALONE_RELEASE_ROADMAP.md` TASK 040 together with the DECIDED CANON
section of `STORY_BIBLE.md`, which holds the story half.

Everything here is a decision, not a proposal. Where a decision changes something
already built, the entry says what changed.

---

## 1. Scope — 1.0 is the Act I vertical slice

**1.0 ships Act I as a polished, complete, playable introduction.** It establishes the
core story, the combat, the memory mechanics and the Agniya encounter, and it ends
there. The full game follows in later releases.

This is a decision about what "finished" means, and it changes the shape of the
remaining roadmap rather than its contents. `SPEC.md` describes the whole game and
remains the design of record; it is not narrowed. What narrows is the release.

**What is therefore in 1.0**

| Area | In 1.0 | Deferred past 1.0 |
|---|---|---|
| Regions | Avarsha (hub), Agniya (temple) | The other six temples |
| Acts | Act I | Acts II–V |
| Bosses | The Flame Sovereign, the Temple Guardian (mini-boss) | The remaining six temple bosses |
| Divine abilities | Ember Step | The other six temple abilities |
| Endings | None authored | All of `SPEC.md` section 72 |
| Enemy archetypes | 6 | The remaining 4 |
| Languages | English only | Everything else |

**What this does not license.** A vertical slice is not a demo with rough edges. It is
the first act of the real game, at release quality, and the parts that exist have to be
finished rather than representative. The deferred column above is the only thing the
narrower scope buys.

**Consequence for the roadmap.** TASK 045–050 (the remaining six temples) move behind
1.0. TASK 044 and TASKS 051–057 stay in front of it. Nothing already built is thrown
away — the divine-ability contract, the scene-travel machinery and the content
validator were all built to take six more temples, and they still will.

---

## 2. Reference PC and performance target

**1080p at 60 FPS**, on:

| | |
|---|---|
| CPU | Intel Core i7, 14th generation |
| GPU | NVIDIA GeForce RTX 4060 |
| Resolution | 1920 × 1080 |
| Frame rate | 60 FPS |

**This is a target to be validated, not a claim already earned.** Nothing has been
profiled. Until a profiling pass exists the correct statement is "1080p/60 on the
reference machine is the target", and `README.md` and the store page must not say
otherwise.

**What validating it requires**, so it is not mistaken for something a test can do:

1. A Release player, built from a clean checkout, run on the reference machine.
2. The Unity Profiler over a representative slice — the Avarsha hub with the ruins
   encounter live, and the Flame Sovereign's third phase, which is the heaviest thing
   in the slice.
3. A recorded frame-time distribution, not an average. A 60 FPS average with a 120 ms
   spike on every scene load is not 60 FPS to a player.
4. The result written into `WINDOWS_BUILD.md` with the date and the build commit.

This is scheduled work, not a gate on anything before it. It belongs with TASK 055,
which also owns the IL2CPP-versus-Mono decision, because the backend changes the
numbers and measuring twice is wasted.

---

## 3. Distribution

| Channel | Carries | Does not carry |
|---|---|---|
| **itch.io** | The player-facing release: the Windows build, the store page, screenshots, the changelog players read | Source |
| **GitHub** | Source, development history, issues, technical documentation | Player builds |

**Why the split matters practically.** A player never needs to see a repository, and a
developer never needs a store page. Keeping them apart means the release notes players
read can be written for players, and `CHANGELOG.md` can stay the engineering record it
already is.

**Consequences:**

- Releases go up as itch.io uploads. GitHub Releases are **not** used for player builds,
  so nothing in the build tooling should start attaching binaries to tags.
- `Tools/build-windows.ps1` already produces the ZIP an itch.io upload wants. Publishing
  is a separate manual step until somebody decides it is worth automating; itch.io's
  `butler` tool would be the way, and it is not set up.
- The release language is **English only** for 1.0. The localization machinery exists
  (`Strings`, `StringTable`, the pseudo-locale) and no second language is commissioned.
- Unsigned executables mean Windows SmartScreen will warn on first run. This is
  accepted for 1.0 — signing costs money and is TASK 057's call — and the itch.io page
  must say so plainly rather than let a player discover it and assume malware.

---

## 4. Act I content list

The content that must exist and be finished for 1.0. IDs are the authored ids, so this
table and the assets cannot drift apart.

**Owner is the author for everything.** The column is kept because the roadmap's gate
asks for it and because it will stop being uniform the moment anyone else contributes.

### Quests

| ID | Title | State | Acceptance |
|---|---|---|---|
| `Q001` | The Queen's Charge | Authored, rewards granted | Completable start to finish in a Release player; grants the Divine Mark and a skill point |
| `Q002` | The Ash at the Gate | Authored, rewards granted | Three ruin guards close it; grants two Ember Draughts and a skill point |

### Memories

| ID | Name | State | Acceptance |
|---|---|---|---|
| `MEM_001` | The Name Beneath the Stone | Authored | Discoverable in the ruins; appears in the Journal |
| `MEM_002` | The Guardian's Vow | Authored | Granted by the Temple Guardian's defeat; reads as a construct's vow (DECIDED CANON 4) |
| `MEM_003` | Agniya's Ember | Authored, text corrected | Granted by the Flame Sovereign's defeat; carries the eight-gods canon (DECIDED CANON 2) |

### Enemies

| ID | State | Acceptance |
|---|---|---|
| `ForgottenSoldier`, `AshCreature`, `StoneGuardian`, `DivineGuardian` | Authored | Visually distinct including under a deuteranopia simulation; telegraphs readable |
| `MINIBOSS_TEMPLE_GUARDIAN` | Authored | Unblockable every 4th attack; drops MEM_002 |
| `BOSS_AGNIYA` (The Flame Sovereign) | Authored | Three phases; unblockable every 3rd attack; drops MEM_003 |

### Dialogue

| ID | State | Acceptance |
|---|---|---|
| `Dialogue_Amara` | Authored | Starts `Q001`; closes it on return; never names Nirvaan |
| `Dialogue_Mira` | Authored | May hint she is Forgotten; may not say it |
| `Dialogue_Dev` | Authored | Validator reports no dangling links or unreachable nodes |

### Abilities and skills

| ID | State | Acceptance |
|---|---|---|
| `EMBER_STEP` | Authored, in the shared contract | Unlockable; costs energy; costs memory on repeated use |
| 12 skills across four branches | Authored | Every one changes play and survives a save |

### Scenes

| Scene | State | Acceptance |
|---|---|---|
| `Avarsha` | Built | Scene-integrity tests pass; NavMesh current; round trip to Agniya keeps everything |
| `Agniya` | Built, needs authoring finished (TASK 044) | Same, plus the temple's own quest and dialogue content |
| `MainMenu` | Built | New Game, Continue, Load, Settings, Controls, Credits, Quit all work |

### What Act I still lacks

Named so the table above is not mistaken for a finished list:

- **The temple's own quest and dialogue content.** Agniya became its own scene in
  TASK 041, and the split moved objects rather than content. This is TASK 044.
- **Essential item and door marking.** Both puzzle gates are marked; no item is,
  because Act I currently has no item the player can be permanently locked out by.
  TASK 044's real key items change that.
- **Ending conditions.** None authored, and none will be for 1.0 — Act I does not end
  the story. `SPEC.md` section 72's conditions stay unimplemented and the content
  validator reports one expected warning saying so.
- **A profiling pass** against section 2 above.
- **The manual passes.** `TEST_PLAN.md` M0, which no automated test can stand in for.

---

## 5. What this brief deliberately does not decide

- **The scripting backend.** Mono today, IL2CPP possible; TASK 055 decides it with
  measurements rather than opinion.
- **Code signing and an installer.** TASK 057.
- **Whether publishing to itch.io gets automated.** Manual until it is annoying enough
  to fix.
- **A second language.** The machinery is ready and nothing is commissioned.
- **Acts II–V's content.** Out of 1.0's scope entirely; `SPEC.md` remains the design.
