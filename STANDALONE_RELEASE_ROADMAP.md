# Windows Standalone 1.0 Roadmap

This is the path from the current playable Unity vertical slice to a finished,
downloadable Windows game. It extends [ROADMAP.md](ROADMAP.md) after TASK 038.
`SPEC.md` remains the design authority, especially sections 60, 63, 64, 72, 73
and 77. The scope assumed here is the **full story in SPEC.md**: seven temples,
Acts I-V, and the three main endings plus the hidden ending. The first release
target is **Windows x64**, using Unity Personal and free tools and services.

## Baseline and definition of finished

As of 24 September 2026, the project has the Avarsha/Agniya vertical slice,
three enabled scenes (`MainMenu`, `Test`, `Avarsha`), two boss encounters, and
310 locally passing EditMode/PlayMode tests. The newer scene tests pass after
Avarsha's YAML conversion. There is **no verified Windows player build** and
no full manual playthrough of TASK 027-038's changes. GitHub Actions cannot run
Unity yet because its Unity Personal secrets have not been configured.

Version 1.0 is finished only when all of these are true:

- A person can download a Windows package, unzip it, launch the game without
  Unity installed, finish all five acts and seven temples, and reach every
  authored ending from a fresh save.
- Every critical quest, memory and ability is obtainable; backtracking, death,
  saving, loading and changing scenes cannot permanently block progression.
- Combat, dialogue, puzzles, bosses, endings and settings have their final
  player-facing feedback. No placeholder blocks a critical story or gameplay
  moment. Any remaining optional placeholder is explicitly accepted in the
  release notes.
- Keyboard/mouse and controller are usable from first launch through credits.
  Required information is never conveyed by colour alone. The documented
  accessibility controls work in the standalone player.
- Fresh, current and older supported saves pass repeated load tests. Corrupt
  primary saves recover from backup. A release candidate has no known
  progression blocker or unhandled crash.
- The SPEC.md section 45 target is measured in a **non-development Windows
  build**: 1080p/60 FPS on the agreed reference PC, with a 1080p/30 FPS fallback
  profile. Record the actual hardware and measurements before claiming this.
- All shipped non-code assets have source and usage rights recorded in
  `ASSET_LICENSES.md`; the download includes credits, version, controls,
  requirements and a way to report bugs.

Passing automated tests or opening a scene in the Editor alone does not satisfy
these gates. Each implementation task also follows SPEC.md section 64's
definition of done: edge cases, save/load, UI feedback, performance, manual
verification and useful automated tests where practical.

## Milestones and tasks

Tasks are ordered by dependency. Art, audio, writing and usability work run
throughout production; their final acceptance gates appear near the end. Do
not wait for every temple before testing a standalone build.

### Gate A - a real Windows player

- [ ] **TASK 039 - Reproducible Windows x64 build.** Add a checked-in Unity
  Windows build profile or Editor build method, with Development and Release
  variants and a documented one-command local build. Build from a clean checkout
  with the pinned Editor version. Launch the output from a separate folder and
  test Main Menu -> New Game -> Avarsha -> save -> quit -> Continue without the
  Editor. Confirm the Release player excludes developer-only controls. Keep
  generated players out of Git. **Gate:** a zipped, locally reproducible
  Windows build and a written smoke-test record. Unity supports scripted
  [Windows player builds](https://docs.unity3d.com/6000.0/Documentation/Manual/build-command-line.html).
- [ ] **TASK 040 - Lock the production brief and story decisions.** Confirm the
  full-game scope, campaign order, ending paths, reference PC, English-first
  release language and target download channel. Resolve the four author
  questions at the end of `STORY_BIBLE.md`, including Agniya's boss name,
  before writing dependent content. Create a chapter/quest/memory dependency
  map and a content budget per region. **Gate:** approved canon and an
  achievable content list with IDs, owners and acceptance tests. Technical
  work in TASK 039 can proceed while these decisions are made.
- [ ] **TASK 041 - Cross-scene progression and save durability.** Move the
  Agniya temple into its own scene, then prove a return trip to Avarsha keeps
  quests, memories, inventory, abilities, world flags and checkpoints without
  restoring the wrong player position. Set save ownership and precedence for
  global settings versus slot data. Implement version migration using a real
  older save fixture; block saves during irreversible transitions and add
  recovery rules for essential doors/items. **Gate:** automated scene-travel
  and save tests plus a standalone round trip across both scenes.
- [ ] **TASK 042 - Content authoring and validation.** Extend existing dialogue
  and scene validators to cover unique IDs, quest/objective links, rewards,
  memory prerequisites, flags, boss rewards, scene exits and ending conditions.
  Provide a repeatable template/checklist for a temple. Externalize all
  player-facing strings for future localization (SPEC.md section 73).
  **Gate:** invalid content fails validation before a build; one complete
  Agniya chapter is authored through the same pipeline later temples will use.

### Gate B - a production-quality first chapter

- [ ] **TASK 043 - Finish shared combat and progression rules.** Define the
  reusable unlock, cost, cooldown, feedback and save contract for divine
  abilities; implement each new ability with its temple in TASK 045-050.
  Replace inert skill-tree choices with real effects or remove them from the
  1.0 tree. Make boss attacks and enemy variants mechanically readable, and
  tune all four difficulty modes without a grind requirement. **Gate:** every
  currently selectable skill and ability changes play and survives save/load;
  combat has a manual feel pass in a Windows player.
- [ ] **TASK 044 - Finish Avarsha and Agniya.** Replace the demonstration quest
  and critical-path placeholder presentation with authored Act I content.
  Finish Agniya as a distinct, enterable scene with its fire mechanic, enemies,
  canonical boss, reward and return route. Resolve the current Ember Step
  unlock contradiction with the story bible. **Gate:** a new player can finish
  Act I and Agniya in the Windows build without debug commands, dead ends or
  missing story beats; save/load works before, during and after the temple.

### Gate C - the remaining six temples

Each temple task delivers a separate region/scene, entrance and exit, one
distinct traversal or puzzle mechanic, its named ability and boss, authored
quests/memories, accessible cues, saved state, and a complete manual run in the
Windows player. Its ability must make at least one earlier area worth revisiting
without making main progression missable. Ship one temple at a time; do not
start the next until this gate passes.

- [ ] **TASK 045 - Varuna.** Drowned Palace; water levels and underwater
  traversal; Tide Veil; Leviathan of the Deep.
- [ ] **TASK 046 - Vayu.** Sky Kingdom; wind currents and vertical routes;
  Wind Leap; Storm Serpent.
- [ ] **TASK 047 - Dhara.** Buried Kingdom; movable stone and marked barriers;
  Earth Break; Stone Colossus.
- [ ] **TASK 048 - Surya.** Endless Desert; light, mirrors and hidden routes;
  Solar Sight; Sun-Eater.
- [ ] **TASK 049 - Chandra.** Dreaming Forest; reality shifts and memory echoes;
  Moonwalk; Dream Queen.
- [ ] **TASK 050 - Kaal.** Temple Outside Time; reversible and looped events;
  Moment Break; Clockless King.

**Gate:** all seven temples, including Agniya, are completable in order and
after allowed backtracking. Every god has a distinct fight and the save can
resume at every chapter boundary. Avoid building a new global system for each
temple when the existing puzzle, quest and boss frameworks can express it.

### Gate D - complete story and endings

- [ ] **TASK 051 - Acts II-IV and the memory consequences.** Author the
  Archivist, Forgotten, reveals, optional lore and cross-region quests using
  the approved story bible. Let lost/restored/corrupted memories change
  dialogue and non-critical world or gameplay states while protecting essential
  progression. **Gate:** the complete chapter graph is traversable; an
  automated dependency audit and manual playthrough find no missing line,
  quest objective or critical memory.
- [ ] **TASK 052 - Act V and endings.** Build the celestial realm, seven-god
  confrontation, First Memory sequence, final boss and three main endings plus
  the hidden ending. Encode the hidden ending prerequisites explicitly and
  expose understandable hints. **Gate:** four reproducible save fixtures reach
  the four endings, with credits and a stable post-ending save policy.

### Gate E - production finish

- [ ] **TASK 053 - Final presentation and asset rights.** Establish a visual
  and audio style guide, then replace critical-path primitive models, flat
  arena/gate presentation, missing character/attack animation, placeholder
  combat cues, absent ambience/music and incomplete cinematics. Use original,
  Unity-built-in, CC0 or other suitably licensed free assets; record every
  external asset and any generated-asset provenance in `ASSET_LICENSES.md`.
  **Gate:** coherent art/audio across all regions, readable combat cues and a
  complete rights ledger. Content production begins earlier; this task is its
  final audit, not a late start.
- [ ] **TASK 054 - UI, accessibility and text readiness.** Finish map,
  journal, inventory, progression, settings, pause, save browser, credits and
  onboarding for both input methods. Verify remapping, text scaling, subtitles,
  non-colour cues, camera/screen-effect options and all four difficulty modes
  in the built player. Keep English text externalized so Hindi and other
  translations can follow; do not claim a language is supported until its
  complete text and layout are tested. **Gate:** a first-time tester can play
  without instructions and accessibility options visibly work in Release.
- [ ] **TASK 055 - Windows performance and stability.** Profile the actual
  player on the agreed reference PC and fallback hardware. Apply scene
  loading/unloading, LOD, pooling, texture and particle budgets, AI update
  frequency and static/baked optimizations only where measurements justify
  them. **Gate:** record 1080p frame-time and memory results for hub, temples,
  bosses and ending; achieve SPEC.md's 60 FPS target and usable 30 FPS
  fallback without long stalls or growth across repeated scene travel.

### Gate F - release candidate and distribution

- [ ] **TASK 056 - Full QA and release candidate.** Make an RC from a clean
  commit. Run EditMode, PlayMode, validators, a fresh-save full playthrough,
  all endings, save migration/corruption/backup, controller disconnect,
  window-focus, graphics changes, repeated deaths and scene transitions.
  Test the packaged build on a Windows account/machine without Unity. Triage
  every known issue; fix all progression blockers and crashes, and record
  intentional minor limitations. **Gate:** signed-off RC test report and no
  known blocker. The existing local suite is a baseline, not a substitute
  for this pass.
- [ ] **TASK 057 - Publish and support Windows 1.0.** Package the Release
  build as a versioned ZIP with the `.exe` and its data folder, instructions,
  controls, requirements, credits, license notices, change notes and a
  checksum. Publish the binary separately from the source archive, retain the
  exact commit/build record, and provide a bug-report path and patch process.
  A free first channel can be [GitHub Releases](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases)
  (each asset must remain under its current 2 GiB limit); a free
  [itch.io page](https://itch.io/docs/creators/faq) is an option if desired.
  Recheck host limits and terms at launch. **Gate:** another person can
  download, unpack, launch and complete the published build.

## Ongoing release rules

1. Keep a Windows Development build available from TASK 039 onward and smoke
   test it after scene, save, input or build-setting changes. Test performance
   and final sign-off with a non-development build.
2. Every new temple follows the same content contract: unique ID, owner,
   dependencies, asset references, save behavior, test and completion gate
   (SPEC.md section 57). Add only one complete vertical piece at a time.
3. Keep the roadmap checkboxes truthful. A task closes after its player-build
   acceptance gate, not when C# compiles. Update `CHANGELOG.md`,
   `KNOWN_ISSUES.md`, `ARCHITECTURE.md` and `TEST_PLAN.md` where relevant.
4. Stay within the free path for now: Unity Personal, existing GitHub Actions,
   original/procedural or rights-cleared free assets, and free distribution.
   Remote Unity CI remains blocked until the owner securely configures the
   repository's Personal license secrets; never put credentials in the repo.

**Next action:** TASK 039. It establishes what actually runs outside the
Editor and exposes packaging or player-only failures while the game is still
small enough to fix them cheaply.
