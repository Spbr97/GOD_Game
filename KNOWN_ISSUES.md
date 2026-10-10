# KNOWN ISSUES

Open limitations, carried forward until closed. Each entry says what is wrong, why it
was left, and what closing it involves.

## TASK 044 — Finish Avarsha and Agniya (in progress, 29 September 2026)

The temple now has a quest and Ember Step is its reward. What that leaves:

### CLOSED — Amara now directs the player to Agniya

After `Q001`, Amara's `AMARA_AFTER` dialogue points toward the temple and starts
`Q003`. The scene entrance starts it as a fallback for players who arrive first.
`AMARA_TEMPLE_COMPLETE` replaces the direction after Q003 is finished. This was
added on 9 October 2026 and verified by the EditMode content check. The wording
still needs the manual Release playthrough below.

### The temple is still one encounter and one puzzle

Carried forward from TASK 015 unchanged: one enemy group before the boss, one puzzle.
Authoring a quest over it does not add pacing. **Closing it:** more encounters, which is
content rather than mechanism.

### TASK 044's gate needs a manual run

"A new player can finish Act I and Agniya in the Windows build without debug commands,
dead ends or missing story beats; save/load works before, during and after the temple."
The automated half is done — the quest chain is verified unbroken by
`ActOneContentTests`, and the smoke test now checks the quest starts, its first objective
closes, and Ember Step is still locked on the way in. **The rest is a person playing it,**
and the task stays unticked until that happens.

**One thing the tests deliberately cannot answer:** whether Ember Step being locked makes
the temple's traversal *feel* worse on the way in. Nothing in Agniya requires the dash —
that is asserted — but "possible without it" and "pleasant without it" are different
questions, and only the manual pass settles the second.

## TASK 040 — Production brief and story canon (28 September 2026)

Closed. The decisions are in `STORY_BIBLE.md`'s DECIDED CANON section and the new
`PRODUCTION_BRIEF.md`. What that leaves:

### 1.0's scope narrowed, which changes what "unfinished" means

1.0 is the Act I vertical slice. The six other temples, Acts II–V, the endings and six
of seven divine abilities are now **out of 1.0** rather than pending in it. **Why this
is recorded as a limitation and not only a decision:** most of this file's
"MISSING CONTENT" entries were written against the whole game, and a reader could now
mistake a deliberate deferral for an oversight. `PRODUCTION_BRIEF.md` section 1 has the
authoritative in/out table. Nothing was thrown away — the ability contract, the scene
travel machinery and the validator were all built for seven temples and still are.

### The 1080p/60 target is a target, not a measurement

The reference machine is an i7 14th-gen with an RTX 4060. **Nothing has been profiled.**
No frame time has ever been recorded on any machine. Until TASK 055 runs the profiling
pass, `README.md` and any store page must say "target" and not "runs at". **Closing it:**
`PRODUCTION_BRIEF.md` section 2 lists the four steps, and the frame-time *distribution*
matters more than the average — a 60 FPS average with a 120 ms hitch on every scene load
is not 60 FPS to a player.

### Publishing to itch.io is entirely manual

itch.io carries player builds; GitHub carries source. `Tools/build-windows.ps1` already
produces the ZIP an upload wants, and **nothing uploads it**. `butler` is the tool for it
and is not set up. **Why left:** it is one manual step per release and there has not been
a release. **Also:** builds are unsigned, so SmartScreen warns on first run; the store
page has to say so plainly rather than let a player conclude malware.

### PARTLY CLOSED — Localization keys on authored content

All 21 shipped quest, memory, item and skill assets now carry keys with matching
English entries. Quest objectives and reward summaries use derived keys too. The
pseudo-locale exercises the same paths, and an EditMode test compares each English
entry with its authored asset. Dialogue lines, ability assets once authored, and
scene-authored labels still need the same treatment before another language can ship.

### A run is only ever dirty for one reason

`HasUnsavedRunChanges` exists and exactly one thing sets it: a mid-run difficulty change.
That is deliberate — flagging ordinary play would make the warning meaningless — but it
means **nothing yet reads the flag.** No quit path or pause menu asks about it, so today
it is a correctly-maintained value with no consumer. **Closing it:** the confirmation
prompt belongs with TASK 051's UI pass; `UnsavedRunChangeReason` is already written in
words a player can read.

### Autosave-on-travel is proved only in the standalone player

`StandaloneSmokeTest` asserts it, because a real crossing cannot happen in an EditMode or
PlayMode arena: a destination Build Settings knows about is genuinely loaded and destroys
the arena, and one it does not know about is refused before the autosave. **Consequence:**
this one behaviour is covered by a test that takes a full build to run rather than by the
474 that take 80 seconds. The three PlayMode tests originally written for it were deleted
rather than left passing vacuously — they named a nonexistent scene, so travel was refused
and they asserted nothing.

## Closure pass (28 September 2026)

A pass over the open entries from TASK 039–043, taking the ones that needed no decision
from anybody. Thirteen were addressed; the entries below are amended in place and say
what closed them. Two genuine content defects were found by the new checks rather than
by review, and are recorded here because finding them is the argument for the checks.

### Found: two enemies were the same colour to a colour-blind player

`Enemy_Boss_Agniya` (0.90, 0.25, 0.05) and `Enemy_DivineGuardian` (0.75, 0.35, 0.10)
are two oranges. They pass an RGB-distance check comfortably and collapse to a
simulated distance of 0.11 under deuteranopia — indistinguishable to roughly one man in
twelve. The Divine Guardian is now a pale gold (0.82, 0.68, 0.30), which separates them
by luminance rather than by hue and therefore survives every dichromacy.
`Enemy_MiniBoss_TempleGuardian`'s telegraph was brightened from (1, 0.40, 0.15) to
(1, 0.55, 0.25) for the same reason: against the violet unblockable flash it had a
luminance gap of 0.15, which is too close to call in the half-second available.

### Resolved: the earlier clean checkout did not contain the build system

`Tools/build-from-clean-checkout.ps1` was written to test TASK 039's reproducibility
clause. Its first run found that `Tools/build-windows.ps1`, `Assets/Editor/WindowsBuild.cs`,
`Assets/Editor/ContentValidation.cs`, both localization folders, `Assets/Scenes/Agniya.unity`
and the whole of `Assets/Scripts/Combat/Abilities/` were **untracked**. That checkout
had no build script or temple. **Consequence:** the
clean-checkout build could not run until those files were committed. They are now
committed. On 9 October 2026, fresh clones of `994112b` and then `b663619` built and packaged both
Windows variants; the Development smoke and Release startup checks passed.

## TASK 043 — Shared combat and progression rules

Every skill does something, abilities share one contract, and attacks are readable.
These are what that leaves.

### The manual feel pass has not been done

This is half of TASK 043's gate and no test can stand in for it. Whether an unblockable
is spottable at speed, whether a parry feels like it landed, whether Warrior and Mythic
feel like different games rather than different numbers — all of it needs a person with
a controller. `TEST_PLAN.md` M0 has the steps. **Consequence:** the tuning is coherent
and unplayed. Every number in the difficulty table is an argument, not an observation.

### CLOSED — The unblockable was distinguished by colour and a pulse (closure pass, 28 September 2026)

The pulse existed but was the same size for both kinds of swing, so it separated "an
attack is coming" from "no attack" and said nothing about which one. Colour was doing
all the work on the read that decides whether to block or dodge.

Two things changed. An unblockable wind-up now swells to 1.38× against an ordinary
1.12×, which is a cue no colour vision is needed for, asserted by
`AnUnblockableWindUpSwellsFurtherThanAnOrdinaryOne`. And the palette is now checked
against deuteranopia, protanopia and tritanopia simulations, with the primary assertion
on Rec. 709 luminance — the one channel no dichromacy removes —
in `AnUnblockableTelegraphIsLegibleWithoutColourVision` and
`EnemyVariantsStayDistinguishableWithoutColourVision`. Those checks found the two
oranges recorded at the top of this file.

**Still true:** a distinct silhouette would be better than either, and there is no
animation budget for one while the art is primitives. That is polish, not an
accessibility hole.

### The ability contract has one real ability in it

Ember Step is the only divine ability that exists. The contract's reuse is proven by
`TestTideStepEffect`, a test double written the way a temple's ability will be written.
That is real evidence — it is the same interface and the same controller — but a test
double never surprises you. **Closing it:** TASK 045's Varuna ability is the first
honest test, and anything the contract is missing will show up there.

### CLOSED — Ember Step's memory cost did not generalise (TASK 040)

SPEC.md section 20's "repeated use temporarily removes minor memories" is written
against Ember Step specifically, and `MemoryManager` listened for `EmberStepUsedEvent`
rather than the shared `DivineAbilityUsedEvent`.

**Decided (TASK 040): only designated abilities cost memory, and each states its own
price.** `DivineAbilityDefinition` gained `costsMemory`, `memoryIntegrityCostPerUse` and
`usesPerForgottenMemory`; the definition rides on `DivineAbilityUsedEvent` so
`MemoryManager` reads the cost without knowing which abilities exist. Ember Step declares
the numbers `MemoryManager` used to hold as its own serialized fields, so its behaviour is
unchanged.

**Why not charge every ability the same way**, which was the tempting one-rule version: a
cost everything pays is a tax, and a tax is not a characterisation. Ember Step burning
memory is a statement about fire and about what the player is trading away, and it stops
meaning anything if the water temple's ability does it too.

The six `MemoryManager` tests that drove the old coupling were rewritten rather than
deleted — they were testing exactly the coupling this removed — and two new ones assert
that an ability declaring no cost is *exactly* free, even with cost numbers sitting on the
asset.

### The anti-grind claim is arithmetic, not play

`TheHardestEnemyDiesToAnUnskilledPlayerInABoundedNumberOfHits` divides health by an
unskilled light attack and checks the result is under 120. That proves no enemy is a
health sponge you must out-level. **It does not prove the fight is winnable** — it says
nothing about whether an unskilled player survives long enough to land those hits on
Mythic. **Closing it:** the manual pass, on each of the four modes.

### Difficulty does not change enemy behaviour, only its timings

The modifiers scale damage, telegraph, cooldown, group aggression and player windows.
What they do not do is change *what* an enemy does — a Mythic enemy uses the same
moveset as a Story one, faster. SPEC.md section 44 asks difficulty to move behaviour,
and this moves the timing of behaviour, which is not quite the same claim.
**Consequence:** a player who has learned a fight on Normal learns nothing new on
Mythic except to be quicker. **Closing it:** per-mode attack pattern variation, which is
content for TASK 045–050 rather than a rule change here.

### The unblockable cadence is fixed, not adaptive

Every Nth attack, counted from the start of the fight. A player who counts can predict
it exactly. **Why left:** predictability is a feature at this stage — a fixed cadence is
learnable, and learnable is what "readable" means. **Consequence:** a boss fight has a
rhythm rather than a threat. **Closing it:** a design call in TASK 044's boss pass, not
a defect to fix blind.

## TASK 042 — Content authoring and validation

The content is checked before a build, and the strings players read are out of the
code. These are what that leaves.

### PARTLY CLOSED — Content localization is incomplete (TASK 042)

The code string table and all shipped quest, memory, item and skill assets now
have English entries. `LocalizedContent` derives their field keys from one prefix
on each asset; the journal and quest tracker use localized objective text. The
repeatable import and authoring steps are in `CONTENT_PIPELINE.md`. What remains
is dialogue line text, scene-authored labels and a complete second language with
font and layout verification. English remains the only supported language.

### [RELEASE BLOCKER] Latest unsigned Release player blocked by Windows Application Control

The 10 October 2026 Release build compiled and passed developer-code gating,
but Windows refused to start its executable with "An Application Control policy
has blocked this file." Earlier unsigned Release builds launched on the same
machine. The wrapper now records the launch failure and still packages the
player, so the exact binary can be tested on another Windows account or PC.
Until it starts and passes the manual Release playthrough, this candidate is
not an approved release. No paid signing service is assumed.

### Text typed into a scene is not localizable at all (still open after TASK 040)

`promptVerb` and `displayName` on an `Interactable`, `locationName` on a
`LocationTrigger`, the Main Menu's button labels, the pause menu's. These are authored
per-object in the Inspector, so they are not in the code and not in a catalogue either.
**Consequence:** a player switching to Hindi would get translated HUD readouts and
English button labels. **Closing it:** the same mechanism as the assets above now exists
— a scene field would hold a key rather than a sentence — but applying it means touching
every prefab and scene object that carries a label, which is a separate mechanical pass
and was not part of TASK 040.

### PARTLY CLOSED — No second language exists (closure pass, 28 September 2026)

There is now a real second table: `Assets/Resources/Localization/Strings_qps.asset`, a
pseudo-locale generated from English by `Tools/make-pseudo-locale.py`. It accents every
letter, pads each string by about a third so a label that only ever held English is
caught before a translator finds it, and **deliberately omits three keys** so the
English fallback is a path something walks rather than a branch only a test double has
entered. `LocalizationTests` now loads it from Resources, checks the fallback produces
English rather than a raw key, checks placeholders survive translation in every entry,
and checks the accents survive the asset import as UTF-8.

**Still open, and unchanged by this:** a pseudo-locale is not a language. A font missing
Devanagari glyphs will not show up here, because the pseudo-locale is Latin by design —
it has to stay readable to a reviewer. TMP font atlases remain the likely first problem
of a real translation.

### The validator checks references, not meaning

It proves an objective id resolves to a real objective. It cannot tell you the objective
is reachable, that the quest is completable in the order the level is laid out, or that
a door the player needs is on the right side of a wall. **Why:** that needs a reachability
model of the level, which does not exist. **Consequence:** a quest can pass validation
and still be impossible to finish. **Closing it:** partly TASK 044's manual play pass;
a real solver is out of scope for 1.0.

### CLOSED — Two quests promised rewards they did not grant (closure pass, 28 September 2026)

Both now grant what they say. `Q001 The Queen's Charge` gives the Divine Mark and a
skill point; `Q002 The Ash at the Gate` gives two Ember Draughts and a skill point.
The `rewardsSummary` prose on both was placeholder text that named the absence of a
reward system that has existed since TASK 035, and is rewritten to describe what is
actually granted.

**Worth keeping in view:** TASK 044 re-authors both quests, and these rewards are a
first pass chosen to be plausible rather than balanced against a progression curve
nobody has drawn yet.

### No ending conditions are authored

Nothing sets any `ENDING_*` flag, so the rule that would check them has nothing to check
and reports one warning saying so. Expected until Act V exists. It is recorded here so
that "the validator passes" is not mistaken for "the endings work".

### CLOSED — The build gate's refusal was untested end to end (closure pass, 28 September 2026)

`Build_RefusesBrokenContentBeforeItTouchesThePreviousPlayer` breaks the content for
real. It writes a quest asset with no objectives — which the validator rates an error —
asks `WindowsBuild.Run` to build, and asserts it refuses. Nothing has to be committed
broken: the asset is created and deleted inside the test.

It also checks *where* the refusal happens, which the original entry did not ask for and
should have. `Run` empties the output directory before it builds, so a gate that fired
after that point would still refuse correctly and would have destroyed the player the
author was running five minutes ago in exchange for nothing. A marker file written into
the output directory before the attempt is the assertion.

## TASK 041 — Cross-scene progression and save durability

The temple is its own scene, a round trip out and back keeps everything, and the whole
journey is verified in a shipped Windows player. These are what that does **not** settle.

### Only two scenes have ever been travelled between

The mechanism is general — a `SceneExit` names a scene and a spawn point, and nothing in
it knows about Avarsha or Agniya — but exactly one pair of scenes has ever exercised it,
in one direction and back. **Why left:** the other six temples do not exist. **What could
still be wrong:** a three-scene chain (city → temple → deeper chamber) would make the
session's `SceneMemory` hold three records, and nothing has ever held three. The saved
file shape supports it and `SceneTravelTests` covers three names in a `SaveData`, but no
running game has done it. **Closing it:** TASK 045's second temple is the first honest
test of the general case.

### PARTLY CLOSED — A journey is not resumable (closure pass, 28 September 2026)

The crossing is no longer held only in RAM. `TravelJournal` writes the destination and
the captured progression to `travel.journal` beside the save files when a journey
starts, and deletes it on arrival. `SaveManager.TryCompleteArrival` falls back to it when
the statics are gone, so a domain reload or a replaced `SaveManager` no longer costs the
run everything since the last save — which matters because saving is deliberately
blocked for the whole crossing.

It is **not** a save slot and never appears as one. The Main Menu deletes it, along with
`SceneTravel` and `SceneMemory`, because reaching the menu is the one unambiguous signal
that the journey is over — and a stale journal would otherwise deposit a previous run's
progression into a New Game that happened to start in the same scene.

**Still open, and it is a decision rather than a defect:** nothing offers to resume an
interrupted journey at boot. After a crash the game starts at the Main Menu, which
abandons the journal by design. Whether "Continue" should notice one, and whether
entering a temple should simply autosave, is a design call — see the difficulty entry
below for the same shape of question.

### The temple's own quests and dialogue were not re-authored for the split

The split moved objects, not content. Quest objectives, dialogue nodes and memory
prerequisites that referred to the temple still refer to it by id and still work, because
all of them were already id-based. **What was not done:** nothing checks that a quest
whose objectives span both scenes reads sensibly — for instance, whether a journal
objective should say where to go now that "go to the temple" means changing scene.
**Closing it:** TASK 042's content validation pass, which is where cross-scene quest links
are meant to be checked anyway.

### PARTLY CLOSED — Recovery only knows about items and doors (closure pass, 28 September 2026)

A third rule was added: `RecoverStrandedObjectives`. An active quest whose objective the
world already records as done — the objective's own `CompletionFlag` is set and the
journal still lists it outstanding — is completed. This is the unfinishable-quest
softlock, and it needs no `essential` marking because the evidence is unambiguous on its
own: whatever would have reported the objective restored itself from that same flag and
will therefore never report again.

Like the other two rules it only ever grants. The opposite case, a journal ahead of the
world, is deliberately left alone: it is not a softlock, and un-completing an objective
is exactly the confiscation this class refuses to do.

**Still not covered:** a player saved inside geometry they cannot walk out of, a boss
recorded as alive in an arena whose entrance has re-sealed, an essential NPC who is
dead. Each needs its own rule, written when the content that can produce it exists.

### PARTLY CLOSED — Nothing is marked `essential` yet (closure pass, 28 September 2026)

`Gate_TempleFirePuzzle` in Avarsha and `Gate_AgniyaPuzzle` in Agniya are now marked
essential, so the sweep has something to sweep. Marking a gate is safe in a way that
marking an item is not: recovery only opens a gate whose puzzle the save already records
as solved, so a false positive cannot exist.

`Pickup_EmberDraught` is deliberately **not** marked. It is a consumable, losing one is
not a softlock, and an essential consumable would be re-granted every load.

**Still true:** no shipped *item* is essential, because the current content has none the
player can be permanently locked out by. That is a fact about the content, not a gap in
the machinery, and TASK 044's real key items are where it changes.

### PARTLY CLOSED — The migration had only been run on an empty file (closure pass, 28 September 2026)

Two tests now put content through it.
`MigratingAPopulatedVersionOneSaveCarriesEveryListAcrossIntact` builds a version 1 save
with something in all nine collections — including a quest with two objectives, which is
a list inside a list and is where a migration loses things — and checks every one
survives. `APopulatedVersionOneFileLoadsAndMigratesThroughTheOrdinaryReader` does the
same through the reader: envelope, checksum and a payload written with **no
`SceneStates` key at all**, because a migration that works only when the field happens
to be present as an empty array is not a migration of anything a version 1 build wrote.

**The distinction that matters, and the reason the old entry is not simply closed:**
these saves are *constructed*, so the shape they use is this build's idea of version 1.
`save-v1-windows-player.sav` remains the only evidence about what version 1 actually
looked like, and the two kinds of test answer different questions. **Closing it
properly:** still keep a fixture from each format version as it is retired, taken from a
real playthrough.

### CLOSED — Difficulty's ownership was settled in code and not in the UI (TASK 040)

The slot wins and `SettingsManager` is told (see ARCHITECTURE.md). **What is untested:**
changing difficulty from the in-game Settings panel mid-run writes to the global copy and
to the live `Difficulty`, but the loaded slot only learns about it at the next save. Quit
without saving and the change is lost, silently.

**Decided (TASK 040): changing it marks the run dirty.** `SettingsManager.SetDifficulty`
publishes `DifficultyChangedByPlayerEvent`, and `SaveManager` sets
`HasUnsavedRunChanges` with a reason a prompt can quote. Refusing the change while a game
is loaded was the alternative and is worse: the commonest reason to change difficulty is
that the fight in front of you is too hard, which is exactly when a game is loaded.

`AdoptDifficultyFromSave` deliberately does **not** mark anything — that is the game
discovering what a run is being played on, not the player changing it, and conflating the
two would make every load look like unsaved work.

**Still open:** nothing reads the flag yet. See the TASK 040 section at the top.

### Avarsha's NavMesh was briefly an in-memory object

Fixed during this task, recorded because the failure mode is invisible. A
`NavMeshSurface.BuildNavMesh()` produces a `NavMeshData` attached to nothing. The Editor
works perfectly for the rest of the session, the scene saves, and every test that asks
"is there a NavMesh" passes — the live object is still there. The player build has
nothing to serialize and ships a scene whose enemies cannot move.
`GameplayScene_BakedNavigationIsASavedAssetAndNotInMemoryOnly` now asserts the asset path
rather than the triangulation.

**CLOSED (closure pass, 28 September 2026):** staleness is now checked too.
`GameplayScene_BakedNavigationIsCurrentWithItsGeometry` asks whether the places
navigation actually has to work are on the mesh — every spawn point, every enemy, every
patrol waypoint — within two metres. A bake taken before the floor moved, a corridor
widened or a room was added fails there. It is not a proof that the bake is byte-identical
to a fresh one; it is a proof that nothing which has to navigate is standing off the
mesh, which is the form the bug takes.

## TASK 039 — Reproducible Windows x64 build

A Windows player now builds, launches and passes an automated smoke test outside the
Editor (`WINDOWS_BUILD.md`). These are what that first verified build does **not**
establish.

### Resolved: the gate's "clean checkout" clause

TASK 039 asks for a build from a clean checkout. Earlier builds were made in the
working tree, which is a different claim: a working tree carries an imported `Library`,
generated project files, and — the failure this is actually about — files that are on
disk and were never added to Git.

**Closure pass (28 September 2026):** `Tools/build-from-clean-checkout.ps1` now exists.
It clones the repository at a commit into a scratch directory, asserts no generated
directory came along and that everything a build needs is committed, then builds by
calling the *clone's* copy of `build-windows.ps1` so the committed build script is what
gets tested. It refuses to run against a dirty working tree, because a clean-checkout
build of a commit that does not contain your changes passes while telling you nothing.

That first attempt exposed the uncommitted source files mentioned above. After they
were committed, the wrapper itself exposed an argument-passing bug; commit `994112b`
fixed it. The next run cloned that commit without `Library` or other generated state,
built and zipped both variants, and passed the Development scene-travel and cold-start
smoke checks plus the Release code-gating and startup checks. A second clean clone of
`b663619` verified the final authored dialogue build. TASK 039 still requires
the M0 manual pass below.

### The manual pass on the Release player has not been done

`TEST_PLAN.md` section M0 exists and nobody has run it. The automated smoke test
covers the Development player only, and covers no input, no rendering and no legibility.
**Why left:** it needs a person at a keyboard; nothing in this task could do it.
**Closing it:** work through M0's six steps, ideally on a Windows account with no Unity
installed, and record the result in `WINDOWS_BUILD.md`. Until then, "a Windows player
exists" is true and "the Windows player is good" is unverified.

### CLOSED — The two-process save check has run

The wrapper now relaunches the Development player after its first smoke run, loads the
save from the previous process and checks progression. Both runs use the isolated
`SmokeTestSaves` directory. The check passed in the clean-checkout Windows builds
of 9 October 2026; see `WINDOWS_BUILD.md`.

### The scripting backend is Mono

The build uses Mono2x, the project default. The smoke-test harness and menu seam are
compiled out of Release by `#if`, verified by searching the shipped assembly, but Mono
does not strip every unreferenced developer type or obfuscate the assembly. The
`DebugMode` comment now states that accurately. **Closing it:** choose the 1.0 backend
in TASK 055 using performance measurements and verify the release gate again.

### CLOSED — `DEVELOPMENT_BUILD` is deprecated in Unity 6 (closure pass, 28 September 2026)

Replaced by `GAME_DEVELOPER_TOOLS`, a define this project owns. `WindowsBuild` adds it
to the Development player through `BuildPlayerOptions.extraScriptingDefines` and to
nothing else; the four gated files (`DebugMode`, `StandaloneSmokeTest`, `SaveStorage`,
`MainMenuController`) now read `#if UNITY_EDITOR || GAME_DEVELOPER_TOOLS`, and the
wrapper script's binary gating check searches for the same thing.

**Why not `DEBUG`, which the warning suggests:** `DEBUG` is a gate but it is Unity's
symbol, so what it means is decided by a compiler configuration a future Editor upgrade
is free to change underneath us. A runtime `Debug.isDebugBuild` check is worse — it is
not a gate at all, since the code stays in the Release binary and a flag can be flipped.
SPEC.md section 52 asks for absence, not for a disabled feature.

`BuildOptions.Development` is still passed, because that is what makes a development
player a development player. What it no longer does is decide what is compiled in.

The failure direction is unchanged and still safe: if the define were ever dropped, the
developer tools would vanish from the Development player rather than appear in the
Release one, and the wrapper's first check catches that immediately.

### A build dirties four URP settings files

Every player build rewrites `Assets/Settings/PC_RPAsset.asset`,
`DefaultVolumeProfile.asset`, `UniversalRenderPipelineGlobalSettings.asset` and
`ProjectSettings/GraphicsSettings.asset`. Almost all of it is `m_Prefiltering*` and
`m_Prefilter*` fields — URP's record of which shader keywords *this build* was able to
strip — plus one field Unity renames back to its historical misspelling
(`chromaticAbberationIntensity`). **Why left:** it is Unity writing build output into
source assets, and there is no supported switch to stop it. **Consequence:** `git status`
looks alarming after every build, and committing the churn would make graphics settings
flip back and forth between whoever built last. These four files were reverted after the
TASK 039 build for exactly that reason.

**CLOSED (closure pass, 28 September 2026):** the build now does it itself.
`WindowsBuild` reads those four files immediately before `BuildPipeline.BuildPlayer` and
writes them back immediately after, whether the build succeeded or not, logging which
ones it reverted. Restoring rather than committing the churn is the right way round: the
stripping record is derived from the build, so it is reproducible from the build and
belongs in the player, not in the repository — nothing at runtime reads it back out of
these files. The manual `git checkout --` step is no longer needed.

### No signing, installer or crash reporting

The output is a folder and a ZIP. Windows SmartScreen will warn on an unsigned
executable from an unknown publisher. **Why left:** signing costs money and TASK 057
owns the distribution decision. **Closing it:** TASK 057.

### Remote CI still cannot build this

GitHub Actions has no Unity Personal licence secrets configured, so the build is
reproducible locally and by hand only. Carried forward from TASK 030 unchanged.

## SPEC AUDIT (after TASK 019) — all seven gaps now CLOSED by TASK 020–026

A full pass over SPEC.md sections 1–87 after the vertical slice closed found
seven requirements that were neither built nor planned anywhere. All seven were
implemented in TASK 020–026; each is recorded below with what closing it
actually delivered and what it deliberately did **not**. The residual
limitations are the entries under "still open" — those are the ones that matter
going forward.

### CLOSED — Debug mode (section 52), TASK 021

`Assets/Scripts/Debug/` now holds `DebugMode` (the release gate), `DebugCommands`
(the twelve state-changing tools as headless statics), `DebugOverlay` (the five
read-only overlays) and `DebugConsole` (the IMGUI front end, backquote to open).
All seventeen section 52 tools exist. Gating is two-layer: a compile-time
`#if UNITY_EDITOR || DEVELOPMENT_BUILD` so a release player has no console in the
binary at all, plus a runtime switch that starts off inside builds that do have
it.

**Still open:** the console is IMGUI and unstyled — it is a developer tool and
was not given design attention. There is no command to *fail* a quest, because
`QuestManager.FailQuest` needs a reason string and no caller has ever needed one
from the console; add it when something does. `spawn` clones an enemy already in
the scene rather than building one from an archetype asset, so it cannot spawn a
type the current scene does not contain — building one from scratch would mean
this file knowing how a dozen components fit together, and drifting from the
scenes the first time that changed.

### CLOSED — Map screen (section 42), TASK 026

`MapUI` draws a top-down map from the scene at the moment it opens: markers for
the player (with facing), checkpoints, NPCs, quest targets, discovered memories
and bosses, each carrying a letter as well as a colour (section 43). Bound to
**M** and to the D-pad up, and reachable from a Close button. Extent comes from
`WorldBounds` when the scene has one, so the map does not rescale under the
player as they walk.

**Still open:** it is a marker map, not a floor plan — nothing draws walls,
buildings or district outlines, so it answers "where is the thing I want" but
not "how do I get there". No zoom and no pan; Avarsha fits on one screen and the
temples will not. Undiscovered memories are deliberately hidden so the map does
not become a collectible checklist. The optional HUD minimap (section 42) is
still not built.

### CLOSED — The three missing documents (sections 81, 82), TASK 020

`GAME_DESIGN.md`, `STORY_BIBLE.md` and `TEST_PLAN.md` now exist. The story bible
records the canon invented across TASK 003–018 — the ruin stone, Avarsha's
districts, every NPC line, the three memory fragments, Nirvaan's cinematic
lines — separated into "canon from SPEC.md" (authoritative) and "canon added
during implementation" (provisional), with the section 82 rule stated at the
top.

**Still open, and this needs the author, not a coding agent:** writing the bible
surfaced four story questions the implementation has already answered without
anyone deciding them. They are listed at the end of STORY_BIBLE.md. The sharpest
is that SPEC.md section 8.1 names Agniya's boss **The Flame Sovereign**, and the
shipped boss is **Agniya, the First Flame** — a direct conflict with the spec,
not a gap in it. The others: whether "the war that made them seven" (MEM_003) is
canon, whether Nirvaan knows his own name, and whether the Temple Guardian is a
construct or one of the Forgotten.

### CLOSED — Falling out of the world (section 50, edge cases 9 and 10), TASK 022

`WorldBounds` defines a kill plane and a horizontal footprint per scene.
`PlayerBoundsGuard` returns the player to the latest valid checkpoint with a
message; `EnemyBoundsGuard` returns an enemy to its authored home rather than
deleting it (section 55 forbids a required NPC disappearing). Both are wired
into Avarsha and Test, sized from the scenes' own geometry plus 40 m of padding.

**Still open:** the volume is a box and a plane, not per-region. A later scene
with legitimately separated areas — Vayu's floating islands especially — will
need either several volumes or a different shape. Recovery is position-only: it
does not undo anything that happened during the fall, though nothing currently
can. A scene with no `WorldBounds` is silently unguarded, which keeps every
existing test arena working but means a new scene opts in rather than being
protected by default.

### CLOSED — "[Dialogue unavailable]" (section 50), TASK 024

A missing graph, or one with no usable entry node, now shows the exact
specified string as a real one-line conversation — dismissed with the ordinary
advance key — and `DialogueRunner.Begin` still returns **false**, so the
caller's own consequences do not fire for a conversation that did not happen.
`NpcInteractable` no longer hides its prompt when it has no dialogue, because a
hidden prompt cannot display a fallback. A node with empty text gets the same
string.

**Still open:** a link to a node that does not exist *mid-conversation* still
ends the conversation cleanly rather than showing the fallback. That is
deliberate — the player has read real lines by then and ending is honest —
but it means the two "missing dialogue" cases behave differently, which is worth
knowing when debugging. There is also no per-line fallback for missing *voice*
audio, because no voice audio exists at all yet (section 41).

### CLOSED — Device and window edge cases (20–24), TASK 025

`DeviceWatcher` handles all three: `InputSystem.onDeviceChange` for controller
loss and return, and `OnApplicationFocus`/`OnApplicationPause` for focus. Losing
the last gamepad or the window pauses via an `AutoPauseRequestedEvent` that
`PauseMenu` turns into a real menu — never a frozen game with nothing on screen.
Nothing resumes automatically. Resolution, graphics quality and fullscreen now
exist in the Settings screen, and a change made while a scene is loading is
deferred until the load finishes (edge case 20).

**Still open:** focus-loss pausing is off in the Editor by default, because
clicking out of the Game view would freeze any PlayMode test that happened to be
running; the behaviour is covered by tests calling the handler directly, but the
Editor does not exercise the Unity callback itself. There is no control-scheme
swap — the game does not re-prompt keyboard glyphs when a pad is unplugged,
because no glyph system exists. `DeviceWatcher` is in the gameplay scenes only,
not the Main Menu, so unplugging a pad in the menu is silent. The Settings
screen is still reachable only from the Main Menu, not from Pause (carried over
from TASK 017).

### CLOSED — Boss arena escape (edge case 7, sections 55 and 56), TASK 023

`BossArena` watches the player's distance while an encounter is live. Leaving
for longer than a three-second grace ends the fight: the boss returns to full
health, phase 1 and its start position, and `BossEncounterResetEvent` takes the
HUD bar down without any victory treatment. Both Avarsha bosses have one, each
sized wider than the boss can see so an encounter cannot start outside its own
arena.

**Still open:** it is a radius, not an authored shape. A non-circular arena — a
long hall, an L — is either too generous at the corners or too tight at the
ends. The reset is not announced to the player: they get no message explaining
why the boss walked away and healed, which will read as a bug the first time it
happens to someone. Nothing prevents *ranged* attacks from just inside the
margin; there are no ranged player attacks yet, so this is latent.

---

## SPEC AUDIT (second pass, during TASK 020–026) — newly found, still open

### CLOSED — Motion blur policy (TASK 034)

The game renders no motion blur. SPEC.md section 43 now requires a toggle only if
the effect is introduced, so there is no inert control in Settings. Add the toggle
with the effect if a later visual pass uses motion blur.

### CLOSED — Section 54 edge case tests run in Unity (TASK 038)

Named tests for cases 1–4, 8, 12–14, 19 and 27–30 are now in the EditMode and PlayMode suites (TASK 038). They pass in the 180 EditMode and 130 PlayMode Unity test runs. Case 12 is guarded by an editor test asserting the project has no asset-bundle dependency.

### [DESIGN DECISION] Colour-blind indicators (section 43) are satisfied by convention, not by a mode

Section 43 asks for "colorblind-friendly indicators" and says important
information must never be communicated by colour alone. The project satisfies
the second sentence everywhere — every bar has a number, telegraphs pulse in
scale as well as colour, map markers carry letters — but there is no
colour-blind *mode* offering alternative palettes. Whether the convention is
enough to close the requirement is a judgement call that has not been made.

## TASK 018 — Placeholder audio, animation and VFX pass

### [PLACEHOLDER] Attack timings were not moved to animation events

This task's ROADMAP wording asks for "attack timings moved to animation
events." `WeaponController`/`EnemyCombatant`'s windup/active/recovery
durations are read from archetype data and rescaled by `Difficulty` at swing
time, not authored into a clip. Real `AnimationEvent`-driven timing needs
clips whose lengths already match that rescaled duration — impossible without
a rig to author them against, and no rig exists. Doing it anyway would mean
either duplicating the timing data into clips that immediately drift from
`Difficulty`, or making clip length authoritative and losing per-difficulty
rescaling entirely — a regression, not a placeholder. `PlaceholderAnimator`
reads the existing `IsSwinging` state to drive a cosmetic-only tilt instead;
timing authority stays exactly where it was. Closing this for real needs an
authored rig and clips first.

### [MISSING CONTENT] No environmental ambience or music

SPEC.md section 40 asks for temple ambience, wind, fire, water, insects,
distant creatures, stone movement, supernatural whispers, and music with a
unique identity per god. None of that exists — only five one-shot combat
tones (`CombatAudio`) and two content tones (`MemoryDiscovered`,
`PuzzleSolved`). A placeholder works for a one-shot cue (a beep marks the
moment); it does not work for a texture meant to loop continuously, which
would just read as a bug. Closing this needs either composed/recorded audio
or a much more elaborate procedural ambience generator, neither of which fits
this task's placeholder-first scope.

### [MISSING CONTENT] Several VFX categories have no content to trigger them yet

SPEC.md section 36 lists 14 effect categories; this task wires up 7 (fire,
sparks, dust, divine energy, memory fragments, glowing symbols, boss
transformations). Water caustics, underwater particles, wind trails,
lightning, time distortion and dream distortion are deferred — nothing in the
game yet has a water area, a dream sequence, or open sky worth a wind trail.
Smoke has no dedicated trigger either (it would need a source — a doused fire,
rubble, a temple brazier freshly extinguished — and none of the existing
triggers reads as "smoke" specifically rather than reusing the fire cue).

### [PLACEHOLDER] Enemies have no placeholder locomotion animation

Only the player got a `PlaceholderAnimator`. Enemies already have a tint
(`EnemyController.ApplyPhaseTint`) and a telegraph scale pulse
(`EnemyCombatant`, TASK 017) covering combat readability, and a second
placeholder-animation system for AI locomotion (idle/patrol/chase leans) was
judged lower priority than finishing the player's within this task's scope.

## TASK 017 — Accessibility, settings and remapping

### CLOSED — Motion blur is not gated (TASK 034)

This duplicates the motion blur decision above. The game currently renders no motion blur, so the accessibility control will be considered when an effect is added.

### [POLISH] Settings and remapping are only reachable from the Main Menu

`MainMenuController`'s Settings and Controls panels are the only place to
change any of these options, including rebinding controls — there is no
in-game or Pause-menu path to either once a game is running. A player who
wants to rebind a key or adjust text scale mid-playthrough has to quit to the
Main Menu first. Closing this means giving `PauseMenu` its own route to the
same panels (or a lightweight in-game equivalent), sharing `MainMenuController`'s
logic rather than duplicating it.

### [POLISH] Gamepad navigation onto dynamically built rows is still unwired

See "The journal has no gamepad navigation" below (TASK 010) — the fix this
task adds is a first-selection only. Main Menu panels now also select an active,
interactable control on open, so focus does not remain on a hidden panel. Rows built
at runtime (quest list, memory
list, remap rows) still have no explicit `Selectable.navigation` between them.

## TASK 016 — Inventory, progression and skill tree

### [ARCHITECTURAL GAP] Eight of the twelve skills have no effect yet

`SkillDefinition` assets exist for all twelve of SPEC.md section 30's named upgrades,
and all twelve can be unlocked, cost points correctly, respect their prerequisite, and
persist through a save — but only one per branch is actually read by a system
(`SkillTreeManager.GetBonus` is generic; nothing calls it for the other eight's
`SkillEffectType`). Specifically unwired: Warrior's combo extension and parry timing,
Guardian's block and general damage reduction, Divine's ability dash speed and
cooldown, Memory's detection range and Ember-Step-forget restore speed. Each needs a
small, safe hookup into its own system (`ComboTracker`'s window, `CombatController`'s
parry window/Ember Step cooldown, `GuardController`'s reduction, `MemoryPickup`'s
interaction range) the same shape as the four already wired — deferred so this task
stayed reviewable rather than touching seven more files' live behaviour in one pass.

### [POLISH] No UI feedback for "why can't I unlock this"

`ProgressionUI`'s Unlock button is simply disabled when `CanUnlock` is false, with no
distinction shown between "can't afford it" and "prerequisite not met" — both read as
a greyed-out button. Small, deferred as UI polish rather than a missing mechanic.

### [MISSING CONTENT] The inventory has three items, two of them symbolic

Weapons and Divine Marks currently exist to prove the category works, not because the
game has multiple weapons to carry or marks with their own effects — there is one
Astra Blade (granted at the start, never removable, since no second weapon exists to
swap to) and the Divine Mark is a stackable count with no gameplay significance beyond
its own tally. Quest Items and Lore have no content at all: nothing in the game yet
drops a quest-specific item or a piece of lore text. This matches section 28's own
"avoid item clutter" instruction more than it undersells the system — the categories
exist and work; only real per-temple/per-quest content is missing, the same shape as
`ObjectiveType`'s still-unimplemented values from TASK 003.

### [POLISH] Skill points have no visible source in the UI

A player looking at "Skill Points: 0" has no in-game hint that finishing quests and
defeating bosses is what grants them — that context currently lives only in this file
and the tooltip text on `SkillTreeManager`'s Inspector fields. Worth a HUD toast or
journal note once there is more than one quest and one boss to notice the pattern
from.

## TASK 015 — Agniya temple section

### [ARCHITECTURAL GAP] The temple is a walled-off area in Avarsha, not its own scene

SPEC.md section 57 names `SCN_Temple_Agniya`, implying a separate scene, the way a
real level structure for this kind of game normally works. It was built inside
`Avarsha.unity` instead, at a location far from every other district, because
`QuestManager`/`MemoryManager`/`DialogueRunner` are scene-scoped and nothing exists
yet to carry their state across an *ordinary* scene transition (only a menu
Continue/Load does, via `SaveManager.PendingLoad`) — a real second scene today would
reset quest and memory progress every time the player walked in. Closing this needs
an area-transition mechanism that saves and immediately reloads progress-only state
(world flags, quest/memory state) without also restoring player position or the
active checkpoint the way a full `Load` does; a good candidate is the first task that
actually needs a second temple, since a single temple never has to prove the seam
works.

### CLOSED — Fire VFX is warm point lights, no particles (TASK 018)

FireBrazierVfx now emits a fire particle burst whenever a brazier lights.

### CLOSED — Ember Step is now truly Agniya's reward (TASK 044)

SPEC.md section 8.1 places Ember Step behind this temple's boss, and it had been usable
from the first second of the game since TASK 011 — which made the temple's entire reward
something the player already had.

`requireAbilityUnlock` is now on in both scenes, and `BossController.flagsOnDefeat` has
the Flame Sovereign granting `ABILITY_UNLOCKED_EMBER_STEP`.

**This entry's stated cost was wrong, and worth correcting rather than quietly dropping.**
It said gating the ability would need "updating every existing test that calls
`CombatController.TryAbility()`". It needed none: the field's *code* default is already
false, and all 11 call sites spawn their own `CombatController`, so they never read the
scene value. The change was two scene fields and one authored flag. An estimate that
stopped work for two tasks was never checked against the default it depended on.

### [DESIGN DECISION] One enemy group before Agniya — no difficulty curve within the temple

The Agniya temple has one group (two Ash Creatures and one Divine Guardian) before its boss. A longer temple would ramp difficulty across several encounters. This vertical slice demonstrates enemy variants and the boss framework but does not prove pacing across a full dungeon.

## TASK 014 — Cinematic system and the first cinematic

### [PLACEHOLDER] No camera control, no actors, no audio

A beat is subtitle text and a duration — no camera cut, pan or focus target, no actor
animation, and no narration audio (`AudioNarrationId` is a placeholder string with no
clip behind it, same as `MemoryFragment.AudioNarrationId`). Nirvaan's voice plays as
letterboxed silence with text. `CinematicBeat` has room to grow a camera target and an
audio clip once a cinematic actually needs one; none of the beats built so far do.
Needs SPEC.md section 41's audio pass and the animation/VFX pass (TASK 018).

### [MISSING CONTENT] Only one cinematic exists, and it is one of the smallest on section 69's list

Section 69 names ten "major cinematics"; only "first Nirvaan voice" has one built.
It was chosen because it needs no actor, arena or transformation to read correctly —
a deliberate easy case to prove the skip/subtitle/freeze mechanics work before a
harder one (temple boss transformation, the identity reveal) asks more of them.

### [PLACEHOLDER] The mini-boss's phase 3 tint and the puzzle's gate opening are not cinematics

TASK 012 and TASK 013 each left a "scripted cinematic moment" as an empty `UnityEvent`
hook rather than building a second `CinematicPlayer` instance for it. This was a
deliberate sequencing choice — the framework needed to exist first — and closing it is
now straightforward: wire those hooks to a `CinematicPlayer.Play()` call once content
(camera work, dialogue) exists to justify one.

## TASK 013 — Boss framework and mini-boss

### [PLACEHOLDER] The arena, cinematic moment and victory sequence are placeholders

Section 18 asks for a unique arena, a scripted cinematic moment and a victory
sequence. The arena is a tinted floor and four cylinder pillars; the cinematic moment
and victory sequence are `UnityEvent` hooks (`onEncounterStarted`, `onDefeated`) with
nothing wired into them yet. Needs TASK 014's cinematic system and TASK 018's
animation/VFX pass; the hooks exist now specifically so wiring real content in later
is a matter of plugging into them, not restructuring `BossController`.

### [MISSING CONTENT] No unique music

Section 18 asks for unique music per boss. No music system exists yet at all (SPEC.md
section 36 is unaddressed project-wide) — this is not specific to the boss framework.

### [MISSING CONTENT] Two boss encounters exist; later bosses and transformations need content

Avarsha contains the Temple Guardian and Agniya encounters. The framework supports three phases; phase 3 currently uses a tint rather than a model, attack-set or arena transformation. Later bosses and a full transformation need authored art, animation, VFX and encounter content.

### [MISSING CONTENT] The arena has no road leading to it

`MiniBossArena` sits on Avarsha's single flat ground plane, reachable by walking
there directly, the same way `AncientRuins` is — there is no authored path, gate or
signpost connecting it to the rest of the city. Placeholder-first (SPEC.md section
78); a real approach is set dressing, not a system, and belongs with whichever later
pass gives Avarsha its roads.

## TASK 012 — Puzzle system and first fire puzzle

### [MISSING CONTENT] The first puzzle has no direct item reward

Solving the early fire puzzle opens a passage. Inventory and the Agniya temple now exist, but this first puzzle does not grant an item or skill. Any reward added should fit the route and be authored as scene or quest data rather than assumed from the puzzle framework.

### [MISSING CONTENT] One puzzle category exists of ten

SPEC.md section 26 lists ten categories (element matching, pressure plates, light
reflection, water movement, wind direction, time manipulation, memory reconstruction,
symbol sequences, environmental traversal, multi-stage temple puzzles). Only the first
concrete piece — braziers with a burn timer — exists. `IPuzzleElement` is deliberately
generic enough for the other nine to be added as new classes without touching
`PuzzleController`, but that is a claim the framework supports, not something this task
proves nine more times over. Each temple's own puzzle work will be the real test.

### [POLISH] No visual or audio cue for a brazier about to burn out

A brazier's timer is only visible as its own lit/unlit tint — there is no warning
flicker, no sound cue, before it goes out. The design rule (see the burn timer's own
purpose) is satisfied in principle — the state is directly observable, not hidden —
but a player watching from across the room has no early signal. Needs the VFX/audio
pass (ROADMAP TASK 018).

### [PLACEHOLDER] The gate is a plain box, not a door

Opening removes the collider and hides the whole `GameObject` — there is no open
animation, no hinge, nothing suggesting a mechanism. Placeholder per SPEC.md section 78.

## TASK 011 — Divine ability framework and Ember Step

### [DESIGN DECISION] Ember Step is always available

SPEC.md section 8.1 places Ember Step at the Agniya temple's reward; here it works
from the moment `DivineEnergyComponent` has energy to spend, with no unlock gate. This
matches the placeholder-first policy (section 78) — the framework had to exist and be
exercisable before any temple does — but the real gating (granted after Agniya's boss,
ROADMAP TASK 015) is not built. There is also only one ability: the "framework" part of
this task's name is the input/cost/combo plumbing in `CombatController`, not a
registry of interchangeable abilities. A second ability would currently mean copying
`TryAbility`/`AbilityRoutine` rather than plugging into a shared shape — acceptable for
one ability, worth generalizing once a second one exists to compare against.

### CLOSED — The dash has no visual identity (TASK 018)

Ember Step now emits divine-energy VFX and a sound through the existing presentation listeners.

### [DESIGN DECISION] Ember Step memory costs remain untuned

`emberStepIntegrityCost` and `emberStepUsesPerForget` are first-pass numbers. TASK 037 adds the remaining section 20 presentation paths, but the cost still needs playtesting against how often players use the ability and restore memories.

## TASK 010 — Quest log and memory archive

### [POLISH] No scrolling

The journal's rows are stacked at a fixed height with no `ScrollRect`. Avarsha has one
quest and one memory today, so nothing overflows the panel yet; a `ScrollRect` +
`Viewport` + `Mask` needs building before either list can grow past what fits on
screen. Placeholder per SPEC.md section 78, tracked rather than silently left.

### [DESIGN DECISION] Corruption's cost is a guess — partially addressed in TASK 011

`corruptionCost` (0.1) still has nothing to balance it against on the "spend" side, but
TASK 011 gave integrity a real drain the other way: Ember Step costs a slice of it per
use (`emberStepIntegrityCost`, 0.02). Neither number is tuned against the other yet —
there is still no economy where the player weighs corrupting a memory deliberately
against the drain from ordinary ability use.

### [DESIGN DECISION] Low memory integrity presentation needs more authored content

Ember Step can temporarily forget optional memories. TASK 037 adds low-integrity dialogue variation, an incomplete memory-discovery description, and a confused-recognition line for Dev. These paths now read Integrity, but most dialogue and memories still have no authored variants. Dialogue validation and the Unity test suites pass; these particular presentation paths still need an in-editor visual review.

### [POLISH] The journal has no gamepad navigation (partially fixed, TASK 017)

`JournalUI`/`ProgressionUI`/`PauseMenu` now select a first control through
`EventSystem.SetSelectedGameObject` when their panel opens, so a controller has
somewhere to start rather than nothing selected at all. What remains open:
nothing moves the gamepad's focus between the *dynamically built* rows below
that first selection (quest list, memory list, remap rows) — those still rely
on whatever `InputSystemUIInputModule`'s default navigation happens to reach,
with no explicit `Selectable.navigation` wiring between rows built at runtime.

### [POLISH] Corrupting from the archive has no confirmation or undo

Clicking Corrupt acts immediately — no "are you sure", and no way to reverse it beyond
`MemoryManager.SetState` from a debug context. A permanent-feeling choice with no
confirmation step is a rough edge for a real player, acceptable for a placeholder
screen exercising the mechanic but worth a confirm dialog before this ships.

## TASK 009 — World save identity

### [DEFERRED] Hazards have no per-object state, by design

`DamageVolume` is stateless — a hazard is always active, so there is nothing to
remember across a save. "Hazards restore with the player" from the ROADMAP line is
satisfied trivially: a fresh scene load rebuilds them exactly as authored. If a future
hazard gains state (a one-shot trap that expends itself, say), it needs a
`SaveIdentity` and its own `WorldObjectState` flag the same way `EnemyHealth` does.

### CLOSED — Avarsha has no checkpoint yet (TASK 013)

Avarsha gained checkpoints on both sides of the Temple Guardian encounter.

### [ARCHITECTURAL GAP] Quest-item and essential-door recovery need authored rules

Inventory and puzzle gates now exist. Critical memories are protected, but quest items are not marked or enforced as unloseable, and puzzle gates have no alternate recovery state if their puzzle becomes impossible. Define which shipped items and doors are essential, then test each recovery path before release.

### [ARCHITECTURAL GAP] A world flag is the only persistence primitive; there is no per-object blob

`WorldObjectState` can remember that something happened (dead, collected) but not
arbitrary per-object data (an enemy's remaining poise, a chest's exact loot roll). That
scale of state has `ISaveParticipant` already; `WorldObjectState` intentionally covers
only the common "did this happen yet" case cheaply, without a new save-file version.

## TASK 008 — Menus and save/load UI

### [POLISH] There is one manual slot, not several

The Save/Load screen lists the three `SaveSlot` values (Checkpoint, Manual, Chapter),
not several independently-numbered manual slots. SPEC.md section 31 lists "manual
save" as one save type, not a bank of slots, so this matches the spec as written; if
the intent was several manual slots, `SaveSlot` needs numbered `Manual1..N` entries and
`SaveBrowser`/`MainMenuController` extended to list them.

### ~~Continuing does not restore the active checkpoint's spawn behaviour~~ — closed in TASK 009

`CheckpointManager.RestoreActiveCheckpoint` now hands `SaveData.CheckpointId` back to
`CheckpointManager` during `SaveManager.Apply`.

### [POLISH] `Application.Quit()` does nothing in the Editor and is unverified in a build

The Quit button calls `EditorApplication.isPlaying = false` in the Editor and
`Application.Quit()` otherwise; only the Editor path has been exercised, since there is
no build pipeline yet (that is TASK 019's job).

### Master Volume has nothing to control — CLOSED (spec audit, TASK 019)

The Settings screen's volume slider wrote to `SettingsManager.Current.MasterVolume`
and persisted it, but nothing read that field: there was no audio system until
TASK 018. Once TASK 018 added real audio the slider became a genuinely broken
control rather than an honest placeholder, which the spec audit caught.
`SettingsManager.Apply` now pushes it to `AudioListener.volume`, the same
push-not-pull shape `Difficulty` already uses, and the slider applies live.

### [POLISH] Menu UI is built by an editor script, not laid out by hand

`MainMenu.unity`'s whole hierarchy, and the pause panel's new buttons, were generated
by a one-off editor script (the same approach TASK 007 used for the HUD) rather than
placed in the Scene view. Programmer art: flat colours, `LegacyRuntime.ttf`, no
transitions. A real UI pass replaces this wholesale rather than editing it in place.

### [DEFERRED] Fixed this task, noted for anyone touching scene-level singletons

`GameManager`, `GameSceneManager`, `SettingsManager`, `WorldState` and
`CheckpointManager` share a `GameSystems` GameObject with scene-scoped systems in
`Avarsha.unity`. Any of those five destroying `gameObject` instead of `this` on
finding a duplicate will again take the whole object — and everything on it — down
with it the next time a scene is revisited. See `CHANGELOG.md`'s TASK 008 entry for
the full story; the fix is `Destroy(this)`, never `Destroy(gameObject)`, in a duplicate
singleton's `Awake`. The same reasoning is why `SaveManager` now lives on its own
GameObject rather than sharing one with anything that calls `DontDestroyOnLoad`.

## TASK 007 — Combat completion and HUD

### ~~The divine ability is still missing~~ — closed in TASK 011

`CombatController.TryAbility` (Ember Step) spends `DivineEnergyComponent` and records
`ComboStep.Ability`. All nine SPEC.md section 13 actions now exist.

### [DESIGN DECISION] Combo multipliers and windows are untuned placeholders

The seven chain multipliers (1.2×–2×), the 0.7s window, the finisher threshold of 20%
and the parry windows (0.2s, perfect 0.08s) are first guesses. They cannot be tuned
honestly until attacks have animations, because the window a player perceives is the
animation, not the number. Section 78 applies: tune when the placeholder is replaced.

### [PLACEHOLDER] The finisher is a big swing, not an execution

A finisher is `AttackType.Finisher` on the same weapon path: unblockable, lethal,
slower. There is no animation, no camera move, no invulnerability for the player
during it and no lunge, so an enemy just outside `finisherRange` is not pulled in.
Section 69's cinematic direction for executions needs the animation and camera
systems first.

### [POLISH] Lock-on has no target cycling

One press acquires the best visible candidate in view; the only way to change target is to release and re-acquire. TASK 032 adds a shared solid-obstruction check to lock-on and interaction, with PlayMode coverage pending a Unity run. The camera still does not handle a target directly above or below the player. Cycling needs an input choice because the gamepad layout is already full.

### [POLISH] Blocking does not slow the player

While holding guard the player moves and turns at full speed. TASK 033 adds a directional guard cone, so attacks from behind now pass through guard. Its EditMode test compiles, and the PlayMode guard scenarios need a Unity run.

### CLOSED — Guard break has no visible reaction (TASK 018)

GuardBrokenEvent already drives dust VFX, guard-break audio, a HUD message and camera shake.

### [PLACEHOLDER] The HUD is placeholder text on flat bars

`HudUI` draws three bars and two labels with the built-in font. Partly overtaken by
later tasks: the boss health bar arrived with TASK 013 and a damage flash with
TASK 017's `ScreenEffectsUI`. Still open as of the TASK 019 spec audit: no
low-health warning, no equipped-ability indicator (Ember Step has existed since
TASK 011, so the system it was waiting for is now there), and no minimap. It is
hideable through `SetVisible`, but nothing binds that to an input or a setting, so
SPEC.md section 42's "HUD must be hideable" is still a method rather than a feature.

### [POLISH] The canvas renders in camera space, which can be occluded

`UI_Canvas` was switched from Screen Space – Overlay to Screen Space – Camera at a
plane distance of 0.5 so it appears in the Scene view next to the world. A camera-space
canvas is geometry: anything closer than 0.5 m to the camera would draw over it. The
camera's collision clamp keeps it at least 0.8 m from surfaces, so this does not
happen today, but a future camera change could. Switch back to Overlay if it does.

### [POLISH] Scene-view UI placement was not visually verified

The Game view was screenshotted with the HUD showing; the Scene view could not be,
because Unity draws canvases in the Scene view in its own pass that a manual camera
render does not include. The change is the documented fix for the symptom, not one
that was watched working.

### CLOSED — Difficulty scales the dodge window but nothing else on the player (TASK 007)

Difficulty scales both the dodge and parry timing windows.

### CLOSED — `Avarsha.unity` uses YAML (TASK 028)

`ProjectSettings/EditorSettings.asset` uses Force Text. The baked NavMesh was embedded in Avarsha, forcing the scene to binary. TASK 028 moved it to `AvarshaNavMesh.asset`, converted the scene to YAML and committed the one-time change separately as `8a96485`. All 8 scene integrity tests pass.

### [DEFERRED] Deprecated `FindObjectsSortMode` overloads were removed

Unity 6000.6 deprecates `FindObjectsByType<T>(FindObjectsSortMode)`. Two sites in
`SaveManager` and `EnemyAiPlayModeTests` predated this task and were fixed alongside
the two new ones so the project compiles with zero warnings again. Recorded so the
"0 warnings" claim in TASK 006's changelog is understood as true for the Editor it was
made on.

## TASK 006 — Save system

### [ARCHITECTURAL GAP] Four save fields remain reserved and empty

`Inventory` and `Abilities` are filled since TASK 016. `NpcStates`, `BossStates`,
`DialogueFlags` and `EndingFlags` remain reserved; boss defeat currently persists
through a WorldObjectState flag instead. These fields await systems that need them.

### CLOSED — The world is not saved, only the player's progress (TASK 009)

SaveIdentity and WorldObjectState now persist enemy deaths and collected pickups through world flags.

### CLOSED — Loading does not change scene (TASK 008)

SaveManager now loads the saved scene before applying scene state.

### CLOSED — Nothing in the game offers to save or load (TASK 008)

SaveBrowser now exposes manual save and load from the menu.

### CLOSED — The required corruption message is published but never shown (TASK 008)

RecoveryMessageUI shows the specified backup-recovery message to the player.

### [DEFERRED] The checksum detects corruption, not editing

`SaveSerializer` uses FNV-1a, which catches a truncated, half-written or bit-rotted
file — the failure SPEC.md section 32 is about. It is not a signature: anyone who
wants to edit their own save can recompute it. That is a deliberate scope decision for
a single-player game, not an oversight, but it means the checksum must never be relied
on as an anti-cheat measure.

### [DEFERRED] Migration has never migrated anything

There is one save version, so `SaveMigration` has no steps and its loop has never run.
The refusal path is tested; the upgrade path cannot be until there is a version 2. The
first real migration should arrive with a test that loads a genuine version 1 file
captured from this build, not a hand-written one.

### [ARCHITECTURAL GAP] Settings live in two places

`SettingsManager` persists the difficulty in `PlayerPrefs` and `SaveData.Difficulty`
persists it again in the save file. Loading a save applies the save's value, which can
then disagree with what the options screen would show. SPEC.md section 31 lists
`Settings` as save data and also names a separate settings save, so both are wanted —
but which one wins is currently decided by whichever ran last.

### [ARCHITECTURAL GAP] Saving is only blocked for two of the cases that should block it

`SaveManager` refuses to save during a conversation and while the player is dead.
SPEC.md section 31 says never to save during a critical state transition, and SPEC.md
section 33 names irreversible story decisions, boss transitions and temple completions.
None of those exist yet, and none of them call `BlockSaves`. The hook is public and
reason-keyed so they can when they do.

### [POLISH] Quarantined saves accumulate forever

A save that fails validation is renamed `*.corrupt-<timestamp>` and left on disk,
because SPEC.md section 32 forbids deleting user saves. Nothing ever removes them,
counts them or tells the player they are there, so a player hitting repeated corruption
slowly fills their save folder with files they do not know about.

### [DEFERRED] Two tests deliberately log console errors

`Load_ACorruptPrimary_...` and `Storage_ACorruptPrimary_...` feed the loader a shredded
file on purpose, and `SaveStorage` logs that at error level as SPEC.md section 32 step
4 requires. The tests suppress the failure with `LogAssert.ignoreFailingMessages`, but
the entries still land in the Unity console, so a clean run of the suite leaves errors
behind that are expected rather than real.

## TASK 005 — PlayMode tests

### [ARCHITECTURAL GAP] Whole systems still have no PlayMode coverage

`Assets/Tests/PlayMode/` covers combat, enemy AI, quests and memories — the systems
whose failures were expensive. Dialogue, the interaction prompt, every UI screen, the
pause menu, the third-person camera and player locomotion have none. Locomotion and
the camera are the two most obviously frame-dependent systems in the project and are
still checked only by eye.

### CLOSED — Shipped scene integrity tests run in Unity (TASK 029)

TASK 029 adds EditMode tests opening each shipped scene and checking missing scripts and key wiring, plus Avarsha's encounter guards and baked NavMesh. All 8 scene tests pass after Avarsha's YAML conversion.

### [POLISH] The suite is wall-clock and tolerance-based

PlayMode tests run in real seconds: the current 25 take about 42. Assertions about
movement are distance thresholds ("closed more than 2m in two seconds") and every wait
has a deadline, so a heavily loaded machine could fail a test that is not actually
broken. Nothing is time-scaled or deterministic. If flakes appear, the fix is to drive
the systems from a fixed clock rather than to widen the tolerances.

### CLOSED — SPEC.md section 53 lists tests for systems that do not exist (TASK 005)

The listed systems gained automated tests as their implementations were added.

### [ARCHITECTURAL GAP] GitHub CI has not been exercised

TASK 030 adds a GitHub Actions workflow for EditMode and PlayMode using GameCI's free runner. It needs Unity Personal credentials in GitHub secrets, a push, and a successful run before CI can be considered verified. The local editor is currently blocked by its licensing-client connection.

### [DEFERRED] `CombatController.Configure` exists only for the tests

`CombatController` disables itself in `Awake` when it has no `InputActionAsset`, so a
test-built player needs one before activation. `Configure` is the seam for that,
matching the pattern already used across the project — but it is a public method on a
gameplay component that shipping code never calls.

## TASK 004 — Enemy AI

### [MISSING CONTENT] Five enemy archetype assets exist; the full roster is still missing

SPEC.md section 16 lists ten enemy classes. There are now five archetype assets:
Forgotten Soldier, Stone Guardian, Divine Guardian, Temple Guardian and Agniya.
The later roster still needs authored assets and, for some classes, new behaviours
such as flight or phasing.

### [MISSING CONTENT] Enemies have one attack, and it is a serialized timer

`EnemyCombatant` plays one swing: telegraph, active window, recovery. SPEC.md section
16 asks for attack *patterns*. There is no second attack, no ranged option, no combo
and no choice between them, so every enemy of a class fights identically. The timings
are serialized numbers rather than animation events, the same limitation recorded for
`WeaponController` in TASK 002, and for the same reason: there are no animations yet.

### [PLACEHOLDER] The telegraph is a colour flash

SPEC.md section 16 requires readable telegraphs. What exists is the body tinting to
the archetype's telegraph colour during the wind-up. That is legible but it is not a
wind-up animation, a VFX or an audio cue — SPEC.md section 16 asks for audio cues and
death animations, and there is no audio system at all yet.

### [POLISH] `EnemyHealth` and `EnemyCombatant` tint through different mechanisms

`EnemyHealth` (TASK 002) writes `renderer.material.color`, which instantiates a
material. `EnemyCombatant` and `EnemyController` use a `MaterialPropertyBlock`. A
property block overrides the material, so a hit flash landing during a telegraph can
be masked, and clearing the block reveals whatever the material instance was left at.
Closing this means moving all three onto one tinting path — worth doing when real
materials replace the placeholder ones.

### [POLISH] Group coordination is attack slots and a shout

`EnemyGroup` limits how many members may swing at once and forwards an alert to
members within a radius. SPEC.md section 17's "group coordination" implies more:
flanking, surrounding, ranged members holding back, coordinated openings. None of
that exists. Grouping is also purely hierarchical — an enemy's group is whatever
`EnemyGroup` is on a parent — so enemies cannot join or leave a group at runtime.

### [DESIGN DECISION] Factions are a three-value enum, not a relationship model

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

### [MISSING CONTENT] Enemies cannot target anything but the player

`EnemyPerception` resolves its target by finding the one `PlayerDeath` in the scene.
There is no faction model, so an enemy cannot deliberately target an NPC and the
Avarsha civilians are in no danger. Same fix as the defect above.

### [POLISH] Retreat is the only self-preservation behaviour

An enemy backs off once when it drops below its archetype's health fraction, then
never again unless it heals. It does not call for help while retreating, does not
seek cover, and does not flee the encounter entirely. The one-shot rule exists to
stop a retreat/re-engage loop rather than because it is the right behaviour.

### [POLISH] Navigation failure returns to patrol but never retries

SPEC.md section 50's fallback is implemented in `EnemyNavigator.SetDestination`:
stop, recalculate, sample the nearest valid point, and failing that abandon the
destination so `EnemyController` sends the enemy back to patrol. What it does not do
is remember that a destination was unreachable, so an enemy can re-target the same
bad point on the next tick and log the same fallback repeatedly.

### [ARCHITECTURAL GAP] Without a baked NavMesh, enemies walk through walls

`EnemyNavigator` falls back to steering the transform straight at its destination
when there is no `NavMeshAgent` or the agent is not on a NavMesh. That keeps an
unbaked scene playable instead of filling it with frozen enemies, but the fallback
ignores obstacles entirely. It logs once per enemy when it engages. Avarsha's NavMesh
is baked, so this only affects scenes built later.

### [DEFERRED] The NavMesh is baked once and does not react to the world

`Navigation` carries a `NavMeshSurface` baked at edit time over the whole scene.
Nothing rebuilds it, and nothing in the scene is a `NavMeshObstacle`, so a door, a
collapsing bridge or the act-by-act changes to Avarsha would not change where enemies
can walk. SPEC.md section 17 points at the AI Navigation package's runtime baking and
links for exactly this; neither is used yet.

### CLOSED — Difficulty is implemented but cannot be changed in game (TASK 008)

Difficulty is selectable in the Settings screen.

### [MISSING CONTENT] `Q002 The Ash at the Gate` is a demonstration, not designed content

The quest exists so `ObjectiveType.DefeatEnemy` has a real driver end to end. Its
text is a placeholder, no NPC gives it, it is started by walking into a trigger, and
its reward is a sentence saying there is no reward system. It should be replaced when
Avarsha's Act I content is actually written.

### [POLISH] Encounter reset needs manual gameplay review

TASK 034 changes `EnemyHealth` to hide defeated ordinary enemies so a player death can restore their health, collider, hurtbox and visuals. `EnemyController` sends them home and forgets aggro on respawn; live bosses reset phase and health. Quest and memory progress remain. The PlayMode respawn test passes; a manual gameplay check remains.

### [POLISH] Knockback and the enemy tint are still unverified

`EnemyStagger` pushes an enemy along `DamageData.Direction` when poise breaks, and
`EnemyController` and `EnemyCombatant` tint the body. TASK 005 covers the stagger
itself — the swing is interrupted and the state machine enters `Stagger` — but not
that the enemy visibly moves, and not that either tint reaches the renderer. Both are
cosmetic and both will be rewritten when real art and animation arrive, which is why
they were left rather than tested.

## TASK 003 — Avarsha

### [MISSING CONTENT] Avarsha is one flat scene, not the hub the spec describes

SPEC.md section 24 lists eight districts and says the hub changes across five acts.
What exists is one `Avarsha.unity` with all eight districts blocked out as primitives
on a single ground plane, in the Act I state only. There is no streaming, no interiors,
and no mechanism for the act-by-act changes. The world-state flags the act changes
would key off do exist (`WorldState`), so the hook is there; the content is not.

### [PLACEHOLDER] The supernatural event is a scripted placeholder

`SupernaturalEvent` plays SPEC.md section 68 scenes 5 and 7 — everyone falls asleep,
then the statues bleed — as a rotation, a colour lerp and a light change. There is no
animation, no VFX, no audio and no camera work. It is one hard-coded sequence rather
than a reusable cutscene system; SPEC.md section 69 asks for cinematic direction that
does not exist yet. Scenes 1-4, 6, 8 and 10 of that sequence are not implemented at
all: no sunrise, no training scene, no empty-city exploration state, no dead soldier,
no Nirvaan.

### [MISSING CONTENT] Dialogue has no voice, no localization and no history

`DialogueNode` carries `VoiceAssetId` and `Subtitle`, and nothing reads the first —
there is no audio (SPEC.md section 41). Text is authored in English directly in the
assets rather than through string keys, so SPEC.md section 73 localization would need
the data reworked. There is also no backlog or replay of lines already shown, and no
typewriter reveal or skip.

### CLOSED — Dialogue graphs are authored in code, not an editor (TASK 003)

DialogueGraph assets are ScriptableObjects under Assets/Data/Dialogue and are editable in the Inspector.

### CLOSED — Relationship counters removed (TASK 034)

The user selected the resolution plan's removal option. `ChangeRelationship` consequences and their authored uses are removed; new saves omit legacy `REL_` counters and old saves ignore them. NPC behaviour can gain a designed relationship model later. Unity imported the authored dialogue assets and the save tests pass.

### [ARCHITECTURAL GAP] New quest objective adapters need scene authoring

TASK 035 adds CollectItem, ChooseDialogue, Survive and Escort adapters. DefeatEnemy and SolvePuzzle already had drivers (`QuestTarget` and `PuzzleController`), so all ten enum values now have a reporting path. Content authors must assign matching objective ids in each scene. Their PlayMode tests pass in Unity; shipped scenes still need objective ids authored where the story uses them.

### [ARCHITECTURAL GAP] Concrete quest rewards need authored use

TASK 035 adds `QuestReward` grants for items, ability unlocks, memory restoration, skill points and world flags. `RewardsSummary` remains display text. Existing quest assets still need rewards authored where the story calls for them; the grant tests pass in Unity.

### [ARCHITECTURAL GAP] Memory integrity consequences need broader content

Ember Step drains integrity and can temporarily forget optional memories. TASK 037 adds integrity-driven dialogue, incomplete memory-discovery text and a confused NPC-recognition line. The mechanisms compile and Unity tests pass; broader authored variants and in-editor presentation still need review.

### [PLACEHOLDER] Memory visualization is a text panel

SPEC.md section 70 asks for memory visualization. `MemoryDiscoveryUI` shows the title
and description on a flat panel with a coloured bar. There is no flashback, no visual
representation per memory beyond a tint colour, and no narration.

### CLOSED — No memory log or quest log screen (TASK 010)

JournalUI provides the quest log and memory archive screens.

### CLOSED — Nothing persists across a scene load or quit (TASK 006–009)

SaveManager restores player, quest, memory and world-object state across scene loads and process restarts.

### [DEFERRED] Entering Avarsha is a trigger, not a scene transition

Acceptance criterion 1 is satisfied by a `LocationTrigger` at the city gate inside
`Avarsha.unity`, which sets `ENTERED_AVARSHA`. There is no loading of Avarsha from
another scene and no door or transition system; `GameSceneManager` can load scenes but
nothing in the world calls it.

### [MISSING CONTENT] The NPCs do nothing but talk

Amara, Dev and Mira are capsules that turn to face the player. No routines, no
schedules, no idle animation, no walking (SPEC.md section 25 asks for NPC routines).
They also have no colliders tuned for anything but standing still.

### CLOSED — Interaction visibility and collider cap (TASK 032)

TASK 032 replaces the fixed sixteen-collider buffer with an unrestricted sphere overlap and adds a shared solid-obstruction check used by interaction and lock-on. Its PlayMode test covers a wall and twenty irrelevant colliders. The PlayMode test passes in Unity.

### [ARCHITECTURAL GAP] Conversation and interaction are still untested over frames

TASK 005 covers the quest and memory halves of this in PlayMode — trigger volumes
firing on entry, kills advancing an objective, a pickup granting once. What remains
uncovered is everything belonging to a conversation: the interaction prompt, the
dialogue UI, `PlayerDialogueLock` suspending and restoring control, and the
supernatural event's sequence over time. Those are still verified only by a scripted
play-mode run, which is a manual check rather than a regression test.

### [POLISH] Gamepad bindings are saturated (remapping now available, TASK 017)

Interact is still bound to `E` on keyboard and to **d-pad up** on gamepad by
default, because all four face buttons are already taken by Jump, Light Attack,
Heavy Attack and Dodge — d-pad up remains a poor default place for the
most-used contextual button. TASK 017's `RemapUI` (Main Menu → Controls) now
lets a player move any binding themselves, so this is no longer a hard
limitation, but the shipped default is unchanged and still forces a bad
binding on anyone who never opens the remap screen.

## TASK 002 — Combat Foundation

### Combat actions not yet implemented — closed in TASK 007

Block, parry, finisher and lock-on now exist; only the divine ability remains, tracked
under TASK 007.

### Combo system is a counter, not a chain — closed in TASK 007

`ComboTracker` matches recorded steps against the section 14 table; the finisher
exists. Tuning is tracked under TASK 007.

### [PLACEHOLDER] Attack timing is serialized numbers, not animation events

`WeaponController` opens and closes the hitbox on fixed delays because there are no
attack animations yet (SPEC.md section 78, placeholder-first). When animations land,
these should become animation events, or the damage window will drift out of sync with
what the player sees.

### CLOSED — Fast-hit sweep (TASK 031)

TASK 031 sweeps the active hitbox between sampled positions and retains attack-id deduplication. A PlayMode test moves the hitbox across a thin target in one frame and checks it damages once. The PlayMode test passes in Unity.

### CLOSED — No hit reaction, stagger or knockback (TASK 004)

EnemyStagger now handles poise, hit reaction and knockback.

### CLOSED — Enemy has no AI and cannot attack (TASK 004)

EnemyController, EnemyPerception and EnemyCombatant implement the enemy AI and attacks.

### [DEFERRED] `DamageVolume` is outside the TASK 002 component list

It was added because "player can die" and "checkpoint restores player" are acceptance
criteria and nothing else in this task could damage the player. It is a real system
rather than a test fixture, but it was not part of the specified scope.

### ~~`CheckpointManager` is not persisted~~ — closed in TASK 009

The active checkpoint now survives a save via `CheckpointManager.RestoreActiveCheckpoint`.
`CaptureFallback`'s player-start-position fallback still applies before any checkpoint
has ever been activated, in either a fresh game or an old save from before one was.

### [DESIGN DECISION] Respawn resets encounters while preserving progression

The user selected the resolution plan's recommended reset. TASK 034 restores ordinary enemies to home at full health, clears aggro and resets live boss phase/health on player death. Quest, memory and other earned progress remain. Defeated bosses remain defeated. This needs Unity PlayMode and manual verification.

### Difficulty scaling is absent — closed in TASK 004 and TASK 007

Enemy-side scaling arrived in TASK 004; the player's dodge and parry windows scale
since TASK 007.

### [POLISH] Dodge is bound to left Ctrl

A placeholder binding. Left Ctrl is conventionally crouch, and dodge is usually the same
button as sprint. It was not bound to shift or space because both are already taken by
sprint and jump, which is itself a sign the control scheme needs a pass.

## TASK 001 — Project Foundation

### Gamepad look speed is wrong — CLOSED (TASK 017)

`PlayerCamera` applied one sensitivity value to the Look action, but mouse delta is
per-frame pixels while a gamepad stick is a -1..1 rate, tuned for mouse, so stick look
was very slow. Fixed by splitting the two paths: mouse still uses `sensitivity`
directly, and a gamepad stick now multiplies by its own
`gamepadLookDegreesPerSecond * Time.deltaTime`.

### [POLISH] `Assets/Settings/SampleSceneProfile.asset` is unused

Carried over from the URP template and not referenced by `Test.unity`. Harmless; delete
it once a real volume profile per region exists (SPEC.md section 35).

### [PLACEHOLDER] Placeholder art only

Everything in `Test.unity` is a Unity primitive with the default URP material, per the
placeholder-first rule (SPEC.md section 78). The Astra Blade is a stretched cube.
