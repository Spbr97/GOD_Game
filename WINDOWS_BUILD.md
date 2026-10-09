# Building the Windows Player

How a Windows x64 player of *The God Who Was Forgotten* is produced, what the two
variants contain, and how each one is verified outside the Editor.
`STANDALONE_RELEASE_ROADMAP.md` TASK 039 owns this document; TASK 057 will extend it
into a publishing procedure.

## The one command

With nothing open in Unity, from a clean checkout:

```powershell
.\Tools\build-windows.ps1                 # Development player: built, smoke tested, zipped
.\Tools\build-windows.ps1 -Variant Release
.\Tools\build-windows.ps1 -Variant Both
```

Useful switches: `-SkipSmokeTest`, `-NoZip`, `-UnityPath <Unity.exe>`,
`-SmokeTestTimeoutSeconds <n>`.

Everything lands under `Build/Windows/`, which `.gitignore` excludes:

```
Build/Windows/Development/TheGodWhoWasForgotten.exe   the player
Build/Windows/Development/build-info.txt              commit, Unity version, scene list
Build/Windows/_logs/build-development.log             Unity's build log
Build/Windows/_logs/smoke-test-development.txt        the smoke-test record
Build/Windows/_logs/smoke-test-resume.txt             cold-start save-load record
Build/Windows/TheGodWhoWasForgotten-<version>-windows-x64-development.zip
Build/Windows/…zip.sha256
```

The Editor version is read from `ProjectSettings/ProjectVersion.txt`, never written
into the script. Building with a different Unity than the project is pinned to is the
single easiest way to make a "reproducible" build not reproducible, so the script
refuses rather than falling back to whatever is installed.

## The two variants

| | Development | Release |
|---|---|---|
| `BuildOptions` | `Development` | `None` |
| `DEVELOPMENT_BUILD` | defined | absent |
| Debug console, overlay, commands | present and usable | compiled out |
| `StandaloneSmokeTest` harness | present | not in the assembly |
| `Assets/Scenes/Test.unity` (developer arena) | shipped | excluded |
| Profiler / deep logging | available | off |

`Assets/Editor/WindowsBuild.cs` is the only place a player is produced from. It takes
its scene list from Build Settings rather than from an array of its own, so adding a
region the ordinary way ships it and there is no second list to forget. The one
subtraction is the developer arena, by path, for Release only.

It refuses to build at all if `MainMenu` or `Avarsha` is missing from the list. Those
two are loaded **by name** at runtime — `MainMenuController.startingScene` and
`PauseMenu.mainMenuScene` — so a player without them reaches a dead end that produces
one log line and no visible error. That is worth failing a build over.

`build-info.txt` is written next to each player, after a success, recording the commit
and whether tracked content or untracked source files differed from it. TASK 057 has
to be able to say which commit a downloaded binary came from, and build time is the
only moment that is known for certain.

## What the automated smoke test does

`Assets/Scripts/Debug/StandaloneSmokeTest.cs`, activated by `-smokeTest` and then
`-smokeTestResume` on the command line, walks the sequence TASK 039 asks for inside
the built player:

1. reach the Main Menu
2. clear the isolated `SmokeTestSaves` directory, so this is a fresh-save run, and
   confirm the menu would not offer Continue; normal saves in `Saves` are untouched
3. New Game → the starting scene loads
4. `Avarsha` is active with a player in it, and `SaveManager` and `WorldState` are
   present
5. a manual save is written, and the file exists on disk under
   `Application.persistentDataPath`
6. **the temple door leads to `Agniya`**, the player lands on that scene's own arrival
   point, world flags and a scene-scoped manager's state both survived the journey, and
   saving is allowed again once it is over (TASK 041)
7. **the way out leads back to `Avarsha`**, landing on Avarsha's arrival point — and
   *not* at the position the save was written at, which is the defect TASK 041 exists to
   prevent
8. a save can still be written after the round trip
9. return to the Main Menu
10. Continue → the saved scene loads
11. the saved game is playable again
12. quit, launch a second player process, Continue the save written by the first,
    and verify the player, progression state and save system are intact

It drives `MainMenuController`'s own New Game and Continue methods through a
development-only seam rather than reimplementing them, so a bug in difficulty
application, world-state reset or save precedence fails the test instead of hiding
behind a parallel copy of the logic. The result is written to
`Build/Windows/_logs/smoke-test-development.txt` and returned as the process exit code.
The wrapper then relaunches the Development player with `-smokeTestResume` and records
the second process in `smoke-test-resume.txt`. Both launches use `SmokeTestSaves`,
separate from the normal player's `Saves` directory.

**Why an automated harness rather than only a checklist.** The failures this is meant
to catch are the ones that by definition cannot appear in the EditMode or PlayMode
suites: a scene absent from the player, an asset that only resolved because the Editor
had it imported, a save path that differs under `persistentDataPath`. A person clicking
through once per release finds those late and inconsistently.

**What it does not do.** It presses no buttons and reads no pixels. It cannot tell you
a label is unreadable, a control feels wrong, or a frame rate is bad. It answers one
narrow question honestly — does the shipped binary get a new player from launch to a
reloaded save — and the manual pass in `TEST_PLAN.md` covers the rest. A passing smoke
test is not a passing release.

## How the Release variant is checked

The Release player has no harness in it, which is the point, so the script does two
things instead:

- **A binary check of the gating.** It searches `Game.Runtime.dll` in the player's
  `Managed` folder for `StandaloneSmokeTest` and `BeginNewGameForSmokeTest`. Both are
  wholly inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, so in a Release player neither
  name should exist in the assembly's metadata at all. This is how SPEC.md section 52's
  "debug mode must be disabled in release builds" is confirmed rather than assumed.
  Searching a DLL for a type name is crude, and crude in the safe direction: a false
  positive reports "still present" and fails the check, which gets looked at. It cannot
  report absent something that is there.
- **A start-up check.** The player is launched for twenty seconds and its log is
  checked for a fatal error or an early exit.

Everything past start-up in a Release player is the manual pass.

## Recorded runs

### 9 October 2026 — final authored slice, clean checkout of `b663619`

`.\Tools\build-from-clean-checkout.ps1 -Variant Both -Keep` exited 0 from a
fresh clone of `b663619` using Unity Personal 6000.6.2f1. The clone contained no
generated Unity state. Both `build-info.txt` files name that commit without a dirty
marker. Development passed the Avarsha → Agniya → Avarsha smoke test, Q003 arrival
checks, and a second-process Continue. Release passed developer-code gating and its
twenty-second startup check. Both players were packaged; computed SHA-256 hashes
match the `.sha256` files:

```text
development  782BC17ADA587D0B3C6AB23C974AE5B1B51FB1BBE22312F67F03F27DC8588C5F
release      310FB8867FF22211598034E0C637636AD7BC9A6B83D81661A66CB1D3CBF7C270
```

The packages are in `Build/clean-checkout-20261009c/repo/Build/Windows/` on this
machine. `Build/` is ignored by Git. The post-build wrapper printed a URP settings
warning because Git status saw line ending changes; `git diff HEAD` found no source
content change. The diagnostic now compares content too. The Release player still
needs the M0 manual playtest before this gate can be closed.

### 9 October 2026 — clean checkout of `994112b`

`.\Tools\build-from-clean-checkout.ps1 -Variant Both -Keep` cloned only committed
files into a new directory with no `Library`, `Build`, `Temp` or user settings. The
Development and Release players built with Unity Personal 6000.6.2f1 and were zipped.
The Development player passed the full Avarsha → Agniya → Avarsha smoke test, including
Q003 starting on arrival, and loaded the saved progression in a second process. The
Release player passed developer-code gating and its twenty-second startup check.
Package SHA-256 hashes:

```text
development  30F944D36434D7518464E38CBEB7A23993496072102B6C2DB6305630E71F16A6
release      709FBDB910D07A7E60DF90D288CDC746167759E446E19A5CE85B0DFD9850F108
```

Unity's first import changed file line endings in three tracked settings files. Git
reported them as modified, although `git diff HEAD` found no content change. The
players' `build-info.txt` therefore incorrectly says the clone had uncommitted changes.
The manifest check now compares normalized content and untracked files. The initial
clone was verified clean before Unity opened it, so this label does not affect the
clean-checkout result. M0 still needs a person to play the Release build.

### 9 October 2026 — task 044 scene-arrival regression and repair

On commit `44ad794`, the clean working-tree build produced both Windows variants,
but the Development smoke test failed: entering Agniya did not start `Q003` or close
`REACH_AGNIYA_TEMPLE`. The player arrives at spawn `FromAvarsha` at z=46 while the
physical entrance trigger is at z=-22. The smoke test exposed that gap before a
release. The Release variant compiled, passed its developer-code gate and startup
check, and packaged, but the overall build command correctly returned failure.

`LocationTrigger` now responds to an authored scene-arrival spawn as well as a
physical collider entry. Agniya names `FromAvarsha` on its entrance trigger, and
content validation rejects a missing arrival spawn. With this working-tree fix,
`.\Tools\build-windows.ps1 -Variant Development -NoZip` exited 0: the full
Avarsha → Agniya → Avarsha smoke run passed, followed by a second-process Continue
that restored the save and progression. The player was built from a dirty tree, so
the clean-checkout gate remains open until this fix is committed and rebuilt.

### 28 September 2026 — the closure pass build

Development variant, `.\Tools\build-windows.ps1 -Variant Development -NoZip`, exit 0.
176 MB, 37 build warnings, Unity 6000.6.2f1, Mono2x.

Three things were verified here for the first time:

- **A save written by one player process was loaded by a second.** The wrapper relaunches
  the player after its first smoke run and loads the previous process's save from the
  isolated `SmokeTestSaves` directory. The harness had been written and had never run,
  because an open Unity Editor blocked batchmode on 27 September. Both reports passed.
- **`GAME_DEVELOPER_TOOLS` replaces `DEVELOPMENT_BUILD`.** `build-info.txt` names it, and
  the binary gating check found `StandaloneSmokeTest` and `BeginNewGameForSmokeTest`
  present in the Development assembly, as intended.
- **No URP churn.** The build log reads "Reverted shader-stripping churn in 4 render
  settings file(s)" and `git status` was clean of `Assets/Settings` and
  `ProjectSettings/GraphicsSettings.asset` afterwards. No manual revert was needed.

Content validation ran ahead of the build and reported 0 errors, 1 warning — the
`ending-condition` warning, which is expected until Act V exists.

`build-info.txt` still records the commit as `b935117` with uncommitted changes, because
the work is not committed. The clean-checkout clause remains open; see below.

### 27 September 2026 — the Avarsha ↔ Agniya round trip (TASK 041)

Machine and Editor as below. Built from `main` at `b935117` with the TASK 039 and TASK
041 working trees on top, which `build-info.txt` records as dirty.
`.\Tools\build-windows.ps1 -Variant Both`, run twice with a full rebuild in between.

| | Development | Release |
|---|---|---|
| Scenes in the player | 4 | 3 — no `Test.unity` |
| Player on disk | 176 MB | 100 MB |
| Packaged ZIP | 70.3 MB | 37.6 MB |
| Developer code in `Game.Runtime.dll` | present, as intended | **none found** |
| Automated verification | smoke test, **29/29** | gating check + start-up check |

The smoke test now crosses a scene boundary twice. Verbatim, the part that is new:

```text
PASS the player contains 'Agniya'
   left Avarsha from (0.00, 1.08, -32.00)
PASS 5. the temple door leads to 'Agniya'
PASS 6. 'Agniya' is active with a player in it
PASS the arrival landed on the temple's own spawn point
PASS world flags survived the journey
PASS a scene-scoped manager's state survived the journey
PASS SaveManager is present in the temple
PASS saving is allowed again once the journey is over
PASS 7. the way out leads back to 'Avarsha'
PASS 8. 'Avarsha' is active again with a player in it
PASS the return landed on Avarsha's arrival point
PASS the return did not restore the position the save was written at
PASS progression is intact after the round trip
PASS 9. a save can be written after the round trip
```

The save file that run left behind was read afterwards and holds two `SceneStates`
entries — Avarsha at z 39, Agniya at z 46 — which is the version 2 shape doing exactly
what it is for.

One defect was found by this run and fixed: the scene split had left Avarsha's
`NavMeshSurface` pointing at an **unsaved in-memory** `NavMeshData`. It behaved
perfectly in the Editor and passed every existing test, and would have shipped a hub
scene whose enemies could not move. `GameplayScene_BakedNavigationIsASavedAssetAndNotInMemoryOnly`
now checks the asset path rather than the triangulation.

### 26 September 2026 — first verified Windows player

Machine: Windows 11 Home Single Language 10.0.26200. Unity 6000.6.2f1, Mono2x. Built
from `main` at `b935117` with the TASK 039 working tree on top, which `build-info.txt`
records as dirty. `.\Tools\build-windows.ps1 -Variant Both`.

| | Development | Release |
|---|---|---|
| Scenes in the player | 3 | 2 — no `Test.unity` |
| `DEVELOPMENT_BUILD` | defined | absent |
| Player on disk | 176 MB | 100 MB |
| Packaged ZIP | 70.3 MB | 37.6 MB |
| Developer code in `Game.Runtime.dll` | present, as intended | **none found** |
| Automated verification | smoke test, **15/15** | gating check + start-up check |

The 76 MB difference between the two players is itself evidence: that is the developer
arena and the development scripting overhead not being shipped.

Smoke-test record, verbatim, from the Development player:

```text
PASS developer tools are present in a Development player
PASS the player contains 3 scene(s) including 'Avarsha'
PASS 1. reach the Main Menu
   save root: C:/Users/sibap/AppData/LocalLow/DefaultCompany/The God Who Was Forgotten\Saves
   cleared 1 pre-existing save file(s) so this is a fresh-save run
PASS no save exists before New Game
PASS 2. New Game loads the starting scene
PASS 3. 'Avarsha' is active with a player in it
PASS SaveManager is present in the gameplay scene
PASS WorldState is present in the gameplay scene
PASS 4. a manual save is written
PASS the save file exists on disk
PASS the menu would now offer Continue
PASS 5. return to the Main Menu
PASS 6. the Main Menu is active again
PASS 7. Continue loads the saved scene
PASS 8. the saved game is playable again

RESULT: PASS - launch to reloaded save completed in the Windows player.
```

Run twice, identically, with a full rebuild in between.

**The manual M0 pass in `TEST_PLAN.md` has not been done.** Nothing above involves a
keypress, a rendered frame or a legible label, and the Release player has been launched
but not played. Until M0 is worked through, "a Windows player exists" is established and
"the Windows player is good" is not.

Two defects in the wrapper script were found by the first real run and fixed: the
player's `.exe` was still locked by Windows when packaging began, and a packaging
failure aborted the whole run and discarded an already-verified build. Both are the kind
of thing only a real end-to-end run surfaces, which is the argument for running one.

`Build/` is not committed, so this section is the durable record; re-run the script to
regenerate `Build/Windows/_logs/`.

## Known limits of this setup

These are recorded in `KNOWN_ISSUES.md` as well, with the reasoning:

- The scripting backend is **Mono**, not IL2CPP. `DebugMode`'s documentation talks
  about the IL2CPP stripper removing dead developer branches; under Mono the branches
  are still compiled out by `#if`, but unreferenced types are not stripped from the
  assembly. The gating is real; the stripping claim is aspirational until a backend
  decision is made.
- The two variants are separated by `GAME_DEVELOPER_TOOLS`, a define this project owns
  and `WindowsBuild` adds to the Development player only. It replaced the deprecated
  `DEVELOPMENT_BUILD` (`UAC0009`) in the closure pass of 28 September 2026; see
  `WindowsBuild.DeveloperToolsDefine` for why neither `DEBUG` nor a runtime check was
  the right substitute.
- The cross-process save check now has a recorded passing run (28 September 2026).
- A **clean-checkout** build passed on 9 October 2026. The wrapper cloned committed
  source, built both variants, ran their automated checks and packaged the players.
- A build no longer dirties the four URP settings files; `WindowsBuild` captures and
  restores them around `BuildPipeline.BuildPlayer`. The old manual
  `git checkout -- Assets/Settings ProjectSettings/GraphicsSettings.asset` step is gone.
- There is no signing, no installer and no crash reporter. TASK 057 decides whether
  1.0 needs them.
- Remote CI still cannot build this: GitHub Actions has no Unity Personal licence
  secrets configured. The build is reproducible locally and by hand only.
