# THE GOD WHO WAS FORGOTTEN

Third-person mythological action-adventure prototype. Unity 6 + C#.

`SPEC.md` is the single source of truth for design and architecture decisions. Read it before implementing anything.

## Status

The vertical slice (SPEC.md section 61) is playable in Unity: Avarsha, the
Agniya temple, combat, quests, dialogue, memory, puzzles, saving and two bosses.
488 automated tests passed locally. A **Windows x64 player builds, launches and
passes an automated smoke test outside the Editor** (TASK 039), and the temple is
now **its own scene** — the smoke test walks Avarsha → Agniya → back in the shipped
binary and proves quests, memories, inventory, abilities, flags and checkpoints all
survive the trip (TASK 041). The manual pass has still not been done. The
remaining six temples and Acts II–V are planned for later releases. Version 1.0
targets a polished Act I vertical slice; see `PRODUCTION_BRIEF.md` for that scope,
`STANDALONE_RELEASE_ROADMAP.md` for the remaining work, and `WINDOWS_BUILD.md` for how
to build the player today.

See `CHANGELOG.md` for progress and `KNOWN_ISSUES.md` for open problems.

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
| `OWNER_RELEASE_CHECKLIST.md` | Hands-on playtest and release approvals needed from the project owner. |

## Running the tests

```bash
unity command run_tests --mode editor     # 292 tests at the last local run
unity command run_tests --mode playmode   # 196 tests at the last local run
```

`TEST_PLAN.md` covers the two failure modes that are not your change, and the
manual pass that the automated suite cannot replace.

## Building a Windows player

```powershell
.\Tools\build-windows.ps1 -Variant Both   # build, smoke test, package, checksum
```

Nothing may be open in Unity. The ZIP includes player instructions, controls,
release notes, credits and the asset ledger. `WINDOWS_BUILD.md` explains the
two variants, what is verified automatically, and what still needs a person.

## Opening the project

1. Install Unity 6000.x (Unity 6 LTS) via Unity Hub.
2. Add this folder as an existing project in Unity Hub, or open it directly with the Unity Editor.
3. Unity will regenerate `Library/`, the rest of `ProjectSettings/`, and resolve packages from `Packages/manifest.json` on first open.
4. Required packages (already declared in `manifest.json`): Input System, AI Navigation, Universal Render Pipeline.

## Repository layout

See `ARCHITECTURE.md` for the full folder structure and system map.
