# THE GOD WHO WAS FORGOTTEN

Third-person mythological action-adventure prototype. Unity 6 + C#.

`SPEC.md` is the single source of truth for design and architecture decisions. Read it before implementing anything.

## Status

The vertical slice (SPEC.md section 61) is complete and playable: Avarsha, the
Agniya temple, combat, quests, dialogue, memory, puzzles, saving and two bosses.
281 automated tests pass. Content for the remaining six temples and Acts III–V
is the next phase.

See `CHANGELOG.md` for progress and `KNOWN_ISSUES.md` for open problems.

## Documentation

| File | What it is for |
|---|---|
| `SPEC.md` | The requirements. Authoritative; read it before implementing anything. |
| `ARCHITECTURE.md` | How the code is arranged, and why each system is shaped as it is. |
| `GAME_DESIGN.md` | What the game currently *is* — the loop, the tuning, and the reasoning behind the numbers. |
| `STORY_BIBLE.md` | The fiction. **Read before writing any character line, place name or inscription** (SPEC.md section 82). |
| `TEST_PLAN.md` | How to run the tests, what they cover, and the manual pass. |
| `ROADMAP.md` | The ordered task list. |
| `RESOLUTION_PLAN.md` | Triage of every open issue — which are real bugs, which are placeholders, and what order to close them in. |
| `CHANGELOG.md` | What changed, per task. |
| `KNOWN_ISSUES.md` | Every open limitation, with why it was left. |
| `ASSET_LICENSES.md` | Provenance for anything not written here. |

## Running the tests

```bash
unity command run_tests --mode editor     # 170 tests, ~3 s
unity command run_tests --mode playmode   # 111 tests, ~60 s
```

`TEST_PLAN.md` covers the two failure modes that are not your change, and the
manual pass that the automated suite cannot replace.

## Opening the project

1. Install Unity 6000.x (Unity 6 LTS) via Unity Hub.
2. Add this folder as an existing project in Unity Hub, or open it directly with the Unity Editor.
3. Unity will regenerate `Library/`, the rest of `ProjectSettings/`, and resolve packages from `Packages/manifest.json` on first open.
4. Required packages (already declared in `manifest.json`): Input System, AI Navigation, Universal Render Pipeline.

## Repository layout

See `ARCHITECTURE.md` for the full folder structure and system map.
