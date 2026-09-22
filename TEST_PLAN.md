# TEST PLAN — THE GOD WHO WAS FORGOTTEN

Required by SPEC.md sections 81 and 53. Section 53 asks that **every major
system have automated and manual tests**; this document says which tests exist,
what each is for, what is deliberately not automated, and how to run any of it.

Current state: **170 EditMode + 111 PlayMode = 281 automated tests, all
passing.**

---

## 1. How to run the tests

Through the Unity CLI, against a running editor:

```bash
unity command run_tests --mode editor     # EditMode  (~3 s)
unity command run_tests --mode playmode   # PlayMode  (~60 s)

# Long runs: start it async and poll instead of blocking.
unity command run_tests --mode playmode --async_tests
unity command test_status
```

Or through the editor's Test Runner window (Window → General → Test Runner).

### Two failure modes that are not your change

1. **The runner reports `"total": 0`.** A stuck test-runner state, not a
   compile error and not an empty suite. Close and reopen the editor, then
   rerun:
   ```bash
   unity close --force "<project path>"
   unity open "<project path>"
   ```
2. **`SavePlayModeTests.Checkpoint_ActiveOneIsRestoredByALoad` fails alone.**
   A known flaky test — it has never failed twice in a row and has never failed
   alongside a genuine regression. If it is the *only* failure, rerun the suite.
   If anything else failed too, treat them all as real.

Anything else that fails is real until proven otherwise.

## 2. The split: EditMode versus PlayMode

**EditMode** covers logic that can be decided without a running game: pure
functions, data validation, component state after a direct method call. It runs
in about three seconds, so it is the suite to run constantly.

**PlayMode** covers what only goes wrong after several frames of a real game
loop — coroutines, physics triggers, navigation, timing windows, frame-order
hazards. This split is not stylistic. Every one of the three defects found at
the end of TASK 004 was of that kind, and none of the EditMode tests then
existing could have caught any of them.

**Write the PlayMode test when the thing under test involves time, physics,
navigation or component lifecycle. Write the EditMode test otherwise.** A
PlayMode test for arithmetic is a slow EditMode test.

## 3. Coverage against SPEC.md section 53

Section 53 names five systems and what each must test.

### Combat — damage, death, parry, dodge, combos, boss phases

| Requirement | Automated by |
|---|---|
| damage | `CombatTests` — reduction, death at zero, self-damage ignored, invulnerability, attack-id deduplication, two hurtboxes sharing one health pool |
| death | `CombatTests.TakeDamage_AtOrBelowZero_Kills`, `ResetHealth_RevivesAtFull`, `CombatPlayModeTests` respawn |
| parry | `CombatActionTests` — inside the window, too late, perfect timing restoring divine energy, against unblockables, one press deflecting one attack |
| dodge | `CombatActionTests`, `CombatActionPlayModeTests` — i-frame window against a live hitbox |
| combos | `CombatActionTests` — every section 14 chain reachable, longest match wins, late input starts fresh, linger past the swing |
| boss phases | `BossTests` (the pure phase table), `BossPlayModeTests` (transitions from real damage, speed and cooldown multipliers, encounter and defeat events, reward reveal) |

### Memory — acquisition, loss, restoration, corrupted state, critical protection

| Requirement | Automated by |
|---|---|
| acquisition | `AvarshaTests.Memory_Discover_*`, discovered-twice-granted-once |
| loss | `AvarshaTests.Memory_Integrity_ClampsBetweenZeroAndOne`, `MemoryTollTests` |
| restoration | `SavePlayModeTests` state round-trip |
| corrupted state | `AvarshaTests.Memory_Corrupt_SetsStateAndSpendsIntegrity` |
| critical memory protection | `AvarshaTests.Memory_CriticalMemories_CannotBeCorruptedOrForgotten`, `Memory_Corrupt_OnACriticalMemory_SpendsNothing` |

`MemoryTollTests.Toll_NeverBlocksPassageEvenAtZeroIntegrity` is the
anti-softlock guarantee for the memory economy (section 55).

### Quest — start, progress, completion, failure, reload, duplicate completion

| Requirement | Automated by |
|---|---|
| start | `AvarshaTests.Quest_StartingTheSameQuestTwice_DoesNotResetProgress` |
| progress | `Quest_ObjectiveNeedingSeveralReports_CompletesOnTheLastOne`, `Quest_CurrentObjective_IsTheFirstIncompleteOne` |
| completion | `Quest_CompletingEveryObjective_CompletesTheQuestAndSetsItsFlags` |
| failure | `QuestManager.FailQuest` via `AvarshaTests` |
| reload | `SavePlayModeTests` quest restore |
| duplicate completion | `Quest_ReportingTheSameObjectiveTwice_CountsOnce`, `EnemyAiTests.QuestTarget_Death_ReportsItsObjectiveExactlyOnce` |

### Save — save, load, backup, corrupted data, version migration

| Requirement | Automated by |
|---|---|
| save / load | `SaveTests.Storage_WriteThenRead_ReturnsTheSameSave`, `Serializer_RoundTripsASave`, `SavePlayModeTests` full round trips |
| backup | `Storage_ASecondWrite_KeepsTheFirstAsTheBackup`, `Storage_ACorruptPrimary_IsReplacedByTheBackupAndKeptOnDisk` |
| corrupted data | `Serializer_ATamperedPayload_FailsItsChecksum`, `Serializer_EmptyOrGarbage_IsRejectedRatherThanThrowing`, `Serializer_ImplausibleStats_AreCaughtEvenWithAValidChecksum` |
| version migration | `Migration_ASaveAtTheCurrentVersion_NeedsNoWork`, `Migration_AVersionWithNoStep_IsRefusedRatherThanGuessedAt`, `Serializer_ASaveFromANewerBuild_IsRefusedNotGuessedAt` |

`SavePlayModeTests.Load_ACorruptPrimary_RecoversFromBackupAndSaysSoInTheSpecifiedWords`
asserts the exact sentence section 32 requires. It deliberately writes a corrupt
save, so **one `[SAVE] ... failed validation` error in the console during a
PlayMode run is expected** and is that test working.

### AI — patrol, chase, attack, death, navigation failure

| Requirement | Automated by |
|---|---|
| patrol | `EnemyAiTests.Patrol_*` (looping, reversing, nearest waypoint) |
| chase | `Decide_SeeingTheTargetOutOfReach_Chases`, `Decide_TargetJustOutOfSight_IsStillPursued...` |
| attack | `Decide_InReachWithAnAttackSlot_Attacks`, group attack-slot limits |
| death | `Decide_Death_IsTerminal`, `QuestTarget_Death_ReportsItsObjectiveExactlyOnce` |
| navigation failure | `Navigator_WithNoNavMeshAgent_SteersDirectlyInsteadOfFailing`, `EnemyAiPlayModeTests` off-mesh behaviour |

The whole AI state table is a pure static `EnemyController.Decide`, so all 37
`EnemyAiTests` run without a scene. That is why the AI is the most densely
tested system in the project — it was made testable first.

## 4. Coverage of SPEC.md section 54's edge cases

| # | Edge case | Covered by |
|---|---|---|
| 5 | Quest completed twice | `Quest_ReportingTheSameObjectiveTwice_CountsOnce` |
| 6 | NPC interaction during dialogue | `Dialogue_SecondConversation_IsRefusedWhileOneIsRunning` |
| 7 | Player leaves a boss arena | `SpecAuditPlayModeTests.BossArena_ResetsTheEncounter...`, `...LeavesTheFightAlone...` |
| 9 | Enemy falls out of the world | `SpecAuditPlayModeTests.EnemyBoundsGuard_ReturnsAFallenEnemyToItsHome` |
| 10 | Player falls out of the world | `SpecAuditPlayModeTests.PlayerBoundsGuard_PutsThePlayerBackOnTheLatestCheckpoint`, `...LeavesThePlayerAloneInsideTheWorld` |
| 11 | NavMesh unavailable | `Navigator_WithNoNavMeshAgent_SteersDirectlyInsteadOfFailing` |
| 15 | Corrupted save | `Storage_ACorruptPrimary_*`, `Load_ACorruptPrimary_*` |
| 16 | Save version mismatch | `Serializer_ASaveFromANewerBuild_IsRefusedNotGuessedAt` |
| 17, 18 | Losing a normal / a protected memory | `Memory_NonCriticalMemories_CanBeCorrupted`, `Memory_CriticalMemories_CannotBe...` |
| 20 | Graphics settings changed during loading | `SettingsManager_ADisplayChangeDuringALoad_IsDeferredUntilItFinishes` |
| 21, 22 | Controller disconnect / reconnect | `DeviceWatcher_*` |
| 23 | Window loses focus | `DeviceWatcher_LosingWindowFocus_AsksForAPause`, `...DoesNotResumeByItself` |
| 25 | Rapid interaction presses | `Checkpoint_Activate_IsIdempotent`, dialogue advance cooldown |
| 26 | Repeated checkpoint triggering | `Checkpoint_Activate_IsIdempotent` |

Edge cases **1–4, 8, 12–14, 19, 24, 27–30** are handled in code but not yet
covered by a test. They are listed in KNOWN_ISSUES.md rather than claimed here.

## 5. Manual test plan

Section 53 asks for manual tests as well, because some things can only be judged
by a person: whether the combat feels good, whether the story lands, whether the
frame rate holds. The automated suite cannot answer any of those.

Run the manual pass **before any commit that changes combat, the scenes, or the
UI**, and in full before any release.

### M1 — The vertical slice, start to finish (~20 minutes)
Main Menu → New Game → Normal → Avarsha. Then:

1. Speak to Dev, Mira and Queen Amara. Every line should render, advance on
   Interact, and never show `[Dialogue unavailable]`.
2. Accept The Queen's Charge and confirm the quest tracker updates.
3. Walk to the ruins. The location trigger should fire once.
4. Take MEM_001. The memory panel should appear and the journal should list it.
5. Survive the ambush. Confirm the group surrounds rather than dogpiles.
6. Return to Amara. The quest should complete and her cut-off line should play.
7. Solve the temple district's three-brazier puzzle; the gate should open.
8. Beat the Temple Guardian. The boss bar should appear and phases should
   visibly change its pace. MEM_002 should be granted.
9. Enter the Agniya temple, solve the four-brazier puzzle, pay the memory toll.
10. Beat Agniya. MEM_003 should be granted.

**Pass:** completable without reloading, and the console has no errors other
than the expected duplicate-`GameManager` warning.

### M2 — Combat feel (~10 minutes)
Against a Forgotten Soldier, on each of the four difficulties:

- Does the parry window feel learnable rather than lucky?
- Does a perfect parry read as different from an ordinary one?
- Does stamina run out often enough to matter, and refill soon enough not to
  bore?
- Does Story mode feel easier without feeling inert? Does Mythic feel unfair?

**Pass:** a judgement, recorded in the commit message or KNOWN_ISSUES.md.

### M3 — Save and load (~5 minutes)
1. Save manually mid-fight. Load it. Health, position, quest and memory state
   should all match.
2. Cross a checkpoint and confirm the auto-save.
3. Corrupt the primary save file by hand. Load. The backup should be used and
   the exact section 32 sentence should appear on screen.
4. Quit from the pause menu and Continue from the Main Menu.

### M4 — Edge cases a test cannot reach (~10 minutes)
1. Unplug a controller mid-fight. The game should pause with the menu up and a
   message. Plug it back in.
2. Alt-tab away. The game should pause. It should **not** resume by itself.
3. Change the resolution from the Settings screen. Change it again during a
   scene load — the change should land after the load, not during it.
4. Walk out of a boss arena and wait. The fight should end and the boss should
   be back at full health when you return.
5. Use the debug console's `recover` to force the out-of-world path.

### M5 — Accessibility (~5 minutes)
Turn each accessibility option on and off and confirm it changes something
visible: text scale, subtitle background, camera shake, screen effects, aim
assist, master volume. Rebind a key and a pad button, then reset to defaults.

**Any control that changes nothing is a bug**, not a placeholder — that is how
the dead Master Volume slider survived for eighteen tasks.

### M6 — Performance (~5 minutes)
With `show fps` on in the debug console, walk the whole of Avarsha and fight the
Agniya encounter. Section 45's target is 60 FPS.

**Pass:** no sustained drop below target and no single frame long enough to
read as a hitch.

## 6. Developer tools for testing

The debug console (backquote, development builds and the editor only, see
SPEC.md section 52) exists so that a manual test does not require playing to the
state under test. `help` lists everything. The commands most useful in a manual
pass:

```text
teleport <checkpointId>    jump to a checkpoint
quest complete <id>        drive a quest to done through the real path
boss                       start a boss encounter from anywhere
unlock <skillId>           unlock a skill and its prerequisites
memory corrupt <id>        exercise the corruption path
show fps|ai|nav|flags|save turn on a readout
```

These drive the real systems rather than writing state behind their backs, so a
state reached with them is a state the game can actually be in.

## 7. Adding a test

1. Name it as a sentence: `Thing_Situation_ExpectedResult`. The failure output
   should say what broke without anyone opening the file.
2. Put it in the existing file for that system, not a new one per task.
3. Use `TestArena` for PlayMode — it builds and tears down a throwaway scene, so
   tests do not depend on Avarsha. A test that breaks when someone moves a
   building teaches the team to ignore it.
4. Every PlayMode wait needs a deadline (`TestArena.Until`). A PlayMode test
   that hangs takes the whole suite with it.
5. If the code under test logs an error on purpose, declare it with
   `LogAssert.Expect` — Unity fails a test on any unexpected error log, which is
   a feature.
6. Assert on the requirement, not on a copy of it. Where a spec string matters,
   compare against the constant the shipping code uses
   (`DialogueRunner.UnavailableText`, `SaveManager.BackupRestoredMessage`).
