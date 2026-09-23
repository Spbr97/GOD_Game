# RESOLUTION PLAN

A triage of every open entry in `KNOWN_ISSUES.md`, and an ordered plan for closing the
ones worth closing.

`SPEC.md` remains the requirements. `KNOWN_ISSUES.md` remains the inventory. This
document is the *judgement* — what each entry actually is, which ones matter now, and
which are deliberately parked. It is expected to go out of date; when it does, delete
it rather than trusting it.

---

## 1. Why this document exists

An external gap analysis of this project was produced from `KNOWN_ISSUES.md` alone. It
concluded the save system ignored scene names, that manual saves did not exist, that
recovery messages were never shown, that dialogue was authored in code, that audio did
not exist, and that memory integrity drove nothing. It proposed replacing the save
system with seven new services.

Every one of those conclusions was wrong, and none of them were the analyst's fault.
**`KNOWN_ISSUES.md` still describes them as open.** They were closed in TASK 006–018
and never marked.

A reader coming to this project today, human or agent, will conclude it is at roughly
TASK 007. It is at TASK 026 with a complete, playable vertical slice and 281 passing
tests. That gap between the record and the reality is the single most expensive problem
in the repository right now, because it causes correct work to be thrown away and
working systems to be rewritten.

So the first task below is not a feature. It is fixing the record.

The second finding is quieter but just as useful: the external analysis found **nothing
that `KNOWN_ISSUES.md` did not already contain.** The binary scene, lock-on's missing
line-of-sight check, the undirected block, hitbox tunnelling, absent scene tests, no
CI — all are already logged, with reasons. Nothing was discovered. What was missing was
a decision to act, and an order to act in. That is the rest of this document.

## 2. How entries are classified

Adopted from the external analysis, which got this part right. `KNOWN_ISSUES.md`
currently mixes all seven categories in continuous prose, which is exactly why it reads
as a list of defects rather than a list of mostly-deliberate choices.

```text
BUG                 wrong behaviour in shipped code
ARCHITECTURAL GAP   works today, will get expensive to change later
DESIGN DECISION     blocked on a choice nobody has made
MISSING CONTENT     the system exists, the content does not
PLACEHOLDER         deliberately temporary, with a named replacement
POLISH              works, and is not ugly enough to matter yet
DEFERRED            has a stated reason and a trigger condition
```

**A placeholder is not a bug. A missing feature is not a bug. A deferred item with a
reason is not debt.** Only the first category is something broken.

### The numbers

| | Count |
|---|---|
| Entries in `KNOWN_ISSUES.md` | 128 |
| Already marked closed | 15 |
| **Open** | **113** |

Of those 113:

| Category | Count | Action |
|---|---|---|
| **BUG** | **8** | Fix — TASK 031–034 |
| STALE (closed, never marked) | 12 | Close — TASK 027 |
| DUPLICATE | 3 | Merge — TASK 027 |
| ARCHITECTURAL GAP | 24 | 8 actionable now (TASK 029–030, 035–038); 16 parked |
| DESIGN DECISION | 11 | 3 need your answer (§4); 8 follow from those |
| MISSING CONTENT | 17 | Parked — needs content, not code |
| PLACEHOLDER | 19 | Parked — needs art, audio or a rig |
| DEFERRED | 19 | Parked — reason and trigger already recorded |

**Eight real bugs out of 113 open entries.** Everything else is content the project has
not reached, placeholders doing their job, or decisions nobody has made yet.

---

## 3. The eight bugs

Each verified against the code, not against the log.

### BUG 1 — `Avarsha.unity` is binary under Force Text

```text
ProjectSettings/EditorSettings.asset   m_SerializationMode: 2   (Force Text)

Avarsha.unity    457,672 bytes      454 lines    opens: h???i0     ← binary
MainMenu.unity   355,955 bytes   11,942 lines    opens: %YAML 1.1
Test.unity       172,532 bytes    5,857 lines    opens: %YAML 1.1
```

The main game scene has been binary since its first commit. Every change to it produces
an unreadable diff and an unmergeable conflict — you cannot review whether a commit
moved a building or deleted a district. It has silently degraded every scene commit in
the repository's history, including the seven systems wired into it in TASK 020–026.

**Fix:** confirm the serialization mode, open the scene, re-save, commit alone with no
other change so the one-time reformat is not mixed into a real diff.
**Cost:** minutes. **Value:** restores reviewability of the most-edited file in the repo.

### BUG 2 — a fast swing can pass through a thin target

`Assets/Scripts/Combat/Hitbox.cs` samples `Physics.OverlapBox` once in `Activate`, then
relies on `OnTriggerEnter`. The activation sweep correctly covers the common
"already overlapping" case, so this is narrower than it first looks — but between two
physics steps a fast hitbox can still cross a thin collider entirely and register
nothing. Placeholder swings are slow and the volumes are large, which is why it has
never been seen.

**Fix:** track the hitbox's previous and current position and sweep between them each
frame while active, keeping the existing `AttackId` deduplication so one swing still
cannot hit the same target twice.

### BUG 3 — lock-on works through solid walls

No `Physics`, `Raycast` or `Linecast` call appears anywhere in
`Assets/Scripts/Combat/LockOnController.cs`. An enemy behind a wall is as acquirable as
one in the open.

### BUG 4 — interaction works through walls, and stops at sixteen candidates

`Assets/Scripts/World/PlayerInteractor.cs:28` declares `new Collider[16]` and feeds it
to `Physics.OverlapSphereNonAlloc` at line 108. Two faults: an NPC on the far side of a
wall can be selected, and in a denser scene anything past the sixteenth collider in
range is invisible to the player.

BUG 3 and BUG 4 are the same mistake in two places — "in range" being treated as "can be
reached". They share a fix and are one task.

### BUG 5 — the guard has no direction

No `Dot`, `forward` or angle comparison in `Assets/Scripts/Combat/GuardController.cs`.
A hit from directly behind is blocked exactly like one from the front, so positioning —
the thing the combat is otherwise built around — does not matter defensively.

This was logged as a deliberate omission "until there is a facing-aware hit reaction
system". That reasoning has expired: TASK 018 shipped the VFX, SFX and camera-shake
needed to communicate a failed block.

### BUG 6 — a broken guard has no visible reaction

A 0.8 s stun and a HUD line. No animation, no VFX, no sound, no camera shake. The
player learns their guard broke by discovering they cannot act.

Also cheap now for the same reason: `VfxSpawner`, `SfxSpawner` and `PlayerCamera.Shake`
all exist and are already wired to other events. Grouped with BUG 5.

### BUG 7 — relationship values are dead data

`Assets/Scripts/Dialogue/DialogueRunner.cs:321` writes
`REL_<target>` counters into `WorldState` on every `ChangeRelationship` consequence.
Nothing anywhere reads them. Dialogue authors are writing numbers into a void, and the
save file carries them forever.

This is the same failure as the Master Volume slider found in the TASK 019 audit: a
control that appears to work and does nothing. It is a **DESIGN DECISION** before it is
a fix — see §4.

### BUG 8 — SPEC.md §43 promises a motion blur toggle that cannot exist

§43 lists a motion blur toggle among required accessibility options. The game renders no
motion blur — no post-processing volume carries the override — so a toggle would gate
nothing.

Deliberately *not* shipped as a checkbox that lies, which is the right call. But the
requirement is unmet either way, and pretending otherwise is how the Master Volume bug
survived eighteen tasks. Also a decision — see §4.

---

## 4. Three decisions only the author can make

No code should be written for these until each is answered.

### 4.1 What resets when the player dies?

Today: nothing. `PlayerDeath` restores the player's health and moves them to the
checkpoint, and the world is untouched — a boss left at 5% health is still at 5% when
you walk back in. `KNOWN_ISSUES.md` records this as intentional, and the *stated*
reason is sound (death must never roll back quest or memory progress). But "don't roll
back progress" does not imply "reset nothing", and nobody has decided the middle case.

**Recommendation** — three tiers:

| Tier | On death | Examples |
|---|---|---|
| Permanent world change | persists | temple unlocked, boss defeated, memory restored, quest completed |
| Encounter state | resets | enemy health, aggro, positions, the boss's phase |
| Player state | restores | health, stamina, position |

This is what `BossArena.ResetEncounter` (TASK 023) already does when the player walks
out of a fight. Extending the same rule to death would make the two paths consistent,
and the machinery exists.

### 4.2 Relationships — define them or delete them

Options: (a) specify which NPCs have relationships, what the values mean, what moves
them and what thresholds do; or (b) remove `ConsequenceType.ChangeRelationship` and the
`REL_` counters entirely, until there is a design that uses them.

**Recommendation: (b), for now.** Three NPCs and no branching content that reads a
relationship value. Dead data invites someone to build on top of it and discover later
that nothing underneath was ever real. Deleting it is reversible; the dialogue assets
that use it can be re-authored when there is a system to serve.

### 4.3 Motion blur — build it or drop the claim

Options: (a) add a motion blur override to the URP volume profile and gate it with the
§43 toggle; or (b) record in `KNOWN_ISSUES.md` that the project does not render motion
blur and the §43 line is therefore not applicable until it does.

**Recommendation: (b) until there is a reason to add motion blur.** Adding a post effect
solely so a checkbox has something to switch off is the tail wagging the dog.

---

## 5. Closure plan — TASK 027–038

Ordered so that the things which protect the work come before the work.

### Tier 0 — let the project protect itself

| Task | Why it is first |
|---|---|
| **027 — Reconcile `KNOWN_ISSUES.md`** | Everything downstream reads this file, and it is currently misleading. Close the 12 stale entries, merge the 3 duplicates, tag every remaining entry with its category. |
| **028 — Fix `Avarsha.unity` serialization** (BUG 1) | Cheap, and every scene commit after it is reviewable. Its own commit. |
| **029 — Scene integrity tests** | Seven systems were wired into the scenes by editor script in TASK 020–026. Nothing would catch a nulled reference. Assert per scene: managers present, NavMesh baked, player spawn, canvas references, and the TASK 020–026 wiring. |
| **030 — CI** | Both suites are run by hand. There is no record of which commit last passed. GitHub Actions: compile → EditMode → PlayMode. |

### Tier 1 — the eight bugs

| Task | Closes |
|---|---|
| **031 — Hit detection sweep** | BUG 2 |
| **032 — Line of sight for lock-on and interaction** | BUG 3, BUG 4 — one shared helper, two callers |
| **033 — Directional guard and guard-break reaction** | BUG 5, BUG 6 |
| **034 — Act on the three decisions** | BUG 7, BUG 8, and §4.1 |

### Tier 2 — spec completeness

| Task | Closes |
|---|---|
| **035 — Quest objective adapters and rewards** | 6 of 10 `ObjectiveType` values have no driver; `RewardDefinition` replaces the `rewardsSummary` string |
| **036 — Dialogue validation command** | Editor menu item: duplicate ids, dangling `NextDialogueId`, unreachable nodes, unknown flags and consequences |
| **037 — Memory integrity consequences** | 3 of SPEC.md §20's 4 effects; also closes "memory corruption is a number nothing consumes" |
| **038 — Edge case test coverage** | SPEC.md §54 cases 1–4, 8, 12–14, 19, 27–30 |

---

## 6. Deliberately parked

Not forgotten, not debt. Each has a trigger condition — the thing that has to exist
before the work is possible or worth doing.

| Group | Entries | Trigger |
|---|---|---|
| **Needs a rig** | animation-event attack timing, enemy locomotion animation, hit reactions, real character animation | A rigged character exists. Until then, moving timing into clips would either duplicate the data or destroy per-difficulty rescaling. |
| **Needs art** | all placeholder visuals, the HUD's flat bars, the gate that is a box, the arena, memory visualisation, the finisher, telegraph presentation | Art production begins. SPEC.md §78 says placeholder-first; these are placeholders doing their job. |
| **Needs audio** | ambience, music, unique boss music, brazier burnout cue, voice acting | Audio production begins. TASK 018's generated tones cover combat cues only. |
| **Needs content** | 4 of 10 enemy archetypes, 6 of 7 temples, bosses beyond two, cinematics beyond one, 9 of 10 puzzle categories, 8 of 12 skill effects, NPC routines, a real Q002 | ROADMAP Phases 5–6. |
| **Needs scale** | scene-per-region restructuring, streaming, dynamic NavMesh, journal scrolling, the 16-collider interaction cap growing past a real limit | A scene large enough to need it. Avarsha is not. |
| **Needs a decision first** | combo tuning, memory cost tuning, corruption cost, difficulty curve, Ember Step availability, input layout | A design pass with the numbers in front of you. Tuning placeholders against each other is guesswork twice over. |
| **Phase 7** | optimisation, localisation, telemetry, colour-blind palette mode | ROADMAP Phase 7. |
| **Informational** | notes kept so a past claim is understood in context (deprecated overloads, tests that log deliberately, `DamageVolume`'s scope) | Never. These are history, not work. |

---

## 7. What not to do

Adapted from the external analysis, which was right about this even where it was wrong
about the facts.

- **Do not rewrite the save system.** It handles slots, backups, checksums, version
  migration, corruption recovery and scene-aware loading, with 35 tests behind it. The
  proposal to replace it with seven new services would discard all of that to reach the
  same behaviour.
- **Do not re-lay-out `PlayerControls.inputactions`.** Bindings are already rebindable
  and overrides are persisted per player; renumbering them invalidates every saved
  rebind.
- **Do not build all seven temples, ten enemy types or the final bosses now.**
- **Do not polish placeholder assets before gameplay is stable.**
- **Do not optimise before measuring.** The debug overlay's FPS readout exists for this.
- **Do not rewrite a working system because a cleaner architecture could exist.**
- **Do not trust an issue log without checking it against the code.** This document
  exists because someone did, in good faith, and lost their work to it.

---

## 8. Keeping this from happening again

The stale entries accumulated because closing an issue lives in a different file from
doing the work. Two habits fix it, both cheap:

1. **When a task closes an issue, edit the entry in the same commit** — mark it closed
   and name the task, as the TASK 019 audit entries do. `KNOWN_ISSUES.md` is already in
   every task's definition of done; this makes it specific.
2. **Tag every new entry with its category when it is written.** A future reader can
   then tell a deliberate placeholder from something broken without reading the code,
   which is the whole job this file is doing by hand today.

## 9. Implementation status — 23 September 2026

The category counts and closure order above are the original audit baseline. TASK 027's issue-log reconciliation is implemented. TASK 031–037 source changes, TASK 029 and 038 tests, and TASK 030's free GameCI workflow are present. The user's TASK 034 decisions are applied: ordinary enemies and live bosses reset on death while quests and memories persist; unused relationship counters are removed; motion blur is documented as conditional on adding that effect.

Unity 6000.6.2f1 ran with the user's Personal license: 180 EditMode and 130 PlayMode tests passed. Dialogue validation checked 3 graphs with 0 errors and 0 warnings. TASK 028 is complete: the embedded NavMesh data was moved to `Assets/Scenes/AvarshaNavMesh.asset`, the scene is YAML, all 8 scene tests pass, and commit `8a96485` contains only the conversion. The free GameCI workflow is pushed. The first GitHub run stopped before Unity tests because the repository has no Personal license secrets (`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`). A manual gameplay and visual pass remains before release; no paid license or service is required.