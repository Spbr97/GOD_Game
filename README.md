# THE GOD WHO WAS FORGOTTEN

Third-person mythological action-adventure prototype. Unity 6 + C#.

`SPEC.md` is the single source of truth for design and architecture decisions. Read it before implementing anything.

## Play it

**[Download v0.1.0-dev for Windows x64](https://github.com/Spbr97/GOD_Game/releases/tag/v0.1.0-dev)**
— 37.7 MB. Unzip, run `TheGodWhoWasForgotten.exe`.

It is unsigned, so SmartScreen will say "Windows protected your PC": choose
More info → Run anyway. A machine with Smart App Control enforcing may refuse it
outright — that is a reputation check on unsigned binaries, not a verdict about
this file.

## Status

**Runs, and has never been played.** Those are two different claims and only the
first is verified.

The Act I vertical slice (SPEC.md section 61) is playable: Avarsha, the Agniya
temple, combat with guard/parry/dodge/combos, quests, dialogue, the memory system,
a skill tree, saving, and two bosses. Agniya is its own scene, and the round trip
carries quest, memory, inventory, ability and checkpoint state with it.

The UI is complete as plumbing and unstyled as presentation: every screen exists,
reads from the string table, scales with the text-size setting and can be driven
with a gamepad — and all of it is flat colour, because the art direction has not
been decided (TASK 053). `UI_BASELINE_REVIEW.md` is a findings pass over it.

| Verified | How |
|---|---|
| 516 automated tests | 320 EditMode, 196 PlayMode |
| The built player runs the game | An automated walkthrough inside the `.exe`: Main Menu → New Game → Avarsha → the temple → quest and objective → autosave → back out → Continue, then a cold restart that reloads the save the previous process wrote |
| Every menu screen | Rendered and inspected, at the largest text size and in a pseudo-locale |

**Not verified: how it plays.** No human has played this build end to end. That
manual pass is the open gate on five roadmap tasks at once, and nothing here
substitutes for it.

The six remaining temples and Acts II–V are after 1.0. See `PRODUCTION_BRIEF.md`
for the 1.0 scope, `STANDALONE_RELEASE_ROADMAP.md` for the remaining work,
`CHANGELOG.md` for progress and `KNOWN_ISSUES.md` for open problems.

## Documentation

| File | What it is for |
|---|---|
| `SPEC.md` | The requirements. Authoritative; read it before implementing anything. |
| `ARCHITECTURE.md` | How the code is arranged, and why each system is shaped as it is. |
| `GAME_DESIGN.md` | What the game currently *is* — the loop, the tuning, and the reasoning behind the numbers. |
| `STORY_BIBLE.md` | The fiction. **Read before writing any character line, place name or inscription** (SPEC.md section 82). Its DECIDED CANON section is binding. |
| `PRODUCTION_BRIEF.md` | What 1.0 is: scope, reference PC, distribution and the Act I content list. |
| `TEST_PLAN.md` | How to run the tests, what they cover, and the manual pass. |
| `WINDOWS_BUILD.md` | How to build, verify and package a Windows player, and what that does not prove. |
| `CONTENT_PIPELINE.md` | How to author a temple: id conventions, the order to author in, and the checklist. |
| `ROADMAP.md` | The ordered task list. |
| `STANDALONE_RELEASE_ROADMAP.md` | Tasks 039–057 and release gates for a finished Windows game. |
| `RESOLUTION_PLAN.md` | Triage of every open issue — which are real bugs, which are placeholders, and what order to close them in. |
| `CHANGELOG.md` | What changed, per task. |
| `KNOWN_ISSUES.md` | Every open limitation, with why it was left. |
| `ASSET_LICENSES.md` | Provenance for anything not written here. |
| `UI_BASELINE_REVIEW.md` | Findings from putting the UI through maximum text size, a pseudo-locale and a gamepad — and the six questions that pass could not answer. |
| `OWNER_RELEASE_CHECKLIST.md` | Hands-on playtest and release approvals needed from the project owner. |

## Running the tests

```bash
unity command run_tests --mode editor     # 320 tests at the last local run
unity command run_tests --mode playmode   # 196 tests at the last local run
```

`TEST_PLAN.md` covers the two failure modes that are not your change, and the
manual pass that the automated suite cannot replace.

## Building a Windows player

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\build-windows.ps1 -Variant Both
```

The bypass is needed because Windows refuses unsigned `.ps1` files under the
default policy; it applies to that one command and changes nothing on the machine.

Nothing may be open in Unity, and the built player is **not** in this repository —
`Build/` is ignored, because a 100 MB binary committed on every build would
outgrow the source within a week. Releases carry the player instead. The ZIP includes player instructions, controls,
release notes, credits and the asset ledger. `WINDOWS_BUILD.md` explains the
two variants, what is verified automatically, and what still needs a person.

## Opening the project

1. Install Unity 6000.x (Unity 6 LTS) via Unity Hub.
2. Add this folder as an existing project in Unity Hub, or open it directly with the Unity Editor.
3. Unity will regenerate `Library/`, the rest of `ProjectSettings/`, and resolve packages from `Packages/manifest.json` on first open.
4. Required packages (already declared in `manifest.json`): Input System, AI Navigation, Universal Render Pipeline.

## Repository layout

See `ARCHITECTURE.md` for the full folder structure and system map.
