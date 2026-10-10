# Authoring a Temple

The repeatable procedure for building one of the seven temples, and the checklist that
says when it is done. `STANDALONE_RELEASE_ROADMAP.md` TASK 042 owns this document;
TASK 045–050 each follow it once.

Agniya is the worked example. It was built before this pipeline existed and was moved
into its own scene by TASK 041, so it is the only temple that did not follow these
steps in order — read it as the shape to copy, not as proof the steps work.

---

## The rule this exists to enforce

**Almost everything in this project is joined by a string.** A quest objective is
completed by whatever reports its id. A dialogue node starts a quest by naming it. A
door names a scene and an arrival point. A gate names a puzzle.

That is the right design — it is what lets content be authored without recompiling, and
what lets two scenes refer to each other at all. But the compiler checks none of it. A
typo is not a build error; it is a quest that can never complete, found by a player.

So: **author ids first, run the validator often, and never hand-write an id twice.**

```
God Game → Validate Content
```

Errors stop a build. Warnings do not. Run it before you commit, not after.

---

## Step 1 — Name everything, before building anything

Fill this table in before opening Unity. Every id in it is a string some other piece of
content will have to spell identically.

| What | Convention | Agniya's |
|---|---|---|
| Scene | `<Name>.unity` in `Assets/Scenes/` | `Agniya.unity` |
| Region flag | `ENTERED_<NAME>` | `ENTERED_AGNIYA_TEMPLE` |
| Quest | `Q0NN` | `Q002` |
| Objective | `<VERB>_<NOUN>`, unique **across every quest** | `REACH_RUINS` |
| Puzzle | `<NAME>_PUZZLE` | `AGNIYA_PUZZLE` |
| Puzzle solved flag | `<NAME>_PUZZLE_SOLVED` | `AGNIYA_PUZZLE_SOLVED` |
| Boss | `BOSS_<NAME>` | `BOSS_AGNIYA` |
| Memory | `MEM_<NAME>` | `MEM_AGNIYA_REWARD` |
| Item | `ITEM_<NAME>` | `ITEM_EMBER_DRAUGHT` |
| Enemy archetype | `Enemy_<Name>` asset, `<CLASS>` id | `Enemy_DivineGuardian` |
| Arrival point | `From<OtherScene>` | `FromAvarsha` |
| Save id | `OBJ_<KIND>_<NAME>` via `SaveIdentity` | `PICKUP_TEMPLE_KEY` |

An objective id is unique **across all quests**, not within one. Objectives are reported
by id alone with no quest named, so two quests sharing one means a single report
advances a quest the player may not have started. The validator treats this as an error.

---

## Step 2 — The scene

Copy the nearest existing temple scene rather than starting empty. A gameplay scene
needs every one of these, and a copy brings them:

- `GameSystems` — `GameManager`, `GameSceneManager`, `SettingsManager`, `WorldState`,
  `QuestManager`, `MemoryManager`, `InventoryManager`, `SkillTreeManager`,
  `CheckpointManager`, `DialogueRunner`, `DeviceWatcher`, `CombatVfx`, `CombatAudio`
- `SaveManager` — a **standalone** GameObject, never on `GameSystems`, because nothing
  on `GameSystems` may call `DontDestroyOnLoad` and share an object with it
- `Player`, `Main Camera`, `UI_Canvas`, `EventSystem`, `DevTools`, `Directional Light`
- `WorldBounds`, sized to this temple and not to the scene it was copied from
- `Navigation` — a `NavMeshSurface`

Then add the scene to Build Settings, and to `WindowsBuild.RequiredScenes` if anything
loads it by name.

**Bake the NavMesh into a saved asset.** `BuildNavMesh()` alone produces a `NavMeshData`
attached to nothing: the Editor works perfectly for the rest of the session, the scene
saves, every test that asks "is there a NavMesh" passes — and the player build has
nothing to serialize and ships a temple whose enemies cannot move. This happened during
TASK 041. `GameplayScene_BakedNavigationIsASavedAssetAndNotInMemoryOnly` now catches it.

---

## Step 3 — The way in and the way out

Both halves, or the temple is a room the player can enter and not leave.

**In the previous scene:** a `SceneExit` naming this temple and its arrival point, and a
`SceneSpawnPoint` for the return trip to land on. Leave the doorway geometry behind so
the exit is standing in front of something.

**In the temple:** a `SceneSpawnPoint` matching the id the exit names, and a `SceneExit`
back, naming the arrival point in the previous scene.

`GameplayScene_HasTheExitAndArrivalPointTheRoundTripNeeds` and the validator's
`scene-exit` rule check all four ids resolve.

---

## Step 4 — The content

In this order, because each step's ids are the next step's references:

1. **Quest** (`Assets/Data/Quests/`) — objectives with ids, descriptions and counts.
   At least one objective must not be `Optional`, or nothing the player does completes
   the quest.
2. **Memories** (`Assets/Data/Memories/`) — the boss reward memory at minimum. Set
   `AssociatedQuestId` and `ObjectiveIdOnDiscovery` only to ids that now exist.
3. **Dialogue** (`Assets/Data/Dialogue/`) — every node needs an id; every link must
   resolve; every entry node must exist. Unreachable nodes are a warning, dangling
   links an error.
4. **Items** (`Assets/Data/Items/`) — anything the player picks up.
5. **Scene wiring** — `LocationTrigger`, `QuestTarget`, `ItemPickup`,
   `PuzzleController`/`PuzzleGate`, `MemoryToll`, `BossController`, `Checkpoint`s
   before and after the boss, `SaveIdentity` on everything whose state must survive a
   save.

**Tick `essential`** on any item or door the player cannot finish the game without.
`ProgressionRecovery` repairs only what is marked, and nothing in the game is marked
today — see `KNOWN_ISSUES.md`.

**The boss's reward must be inactive in the scene.** `BossController` reveals it on
death; one left active is a reward the player takes without fighting, which the
validator treats as an error.

---

## Step 5 — Player-facing text

**Never hard-code text into a script** (SPEC.md section 73). Anything a player reads
that comes from code goes in `Assets/Resources/Localization/Strings_en.asset`, with a
key in `StringKeys`, read through `Strings.Get` or `Strings.Format`.

Placeholders are positional — `{0}`, `{1}` — never `{name}` and never C# interpolation.
A translator must be able to reorder them; Hindi puts the verb last.

Text authored **as data** — a quest's title, a memory's description, an item's name —
stays in its ScriptableObject, and is localized by **a key on the asset pointing into
the same string table** (TASK 040's decision; see `LocalizedContent`). Fill in
`localizationKey` with the asset's id in lower case, prefixed by its kind:

| Asset | Key | Fields derived |
|---|---|---|
| `Quest_TheQueensCharge` | `quest.q001` | `.title`, `.description`, `.rewards`, `.objective.<objective_id>` |
| `Memory_AgniyasEmber` | `memory.mem_003` | `.title`, `.description` |
| `Item_EMBER_DRAUGHT` | `item.ember_draught` | `.name`, `.description` |
| `Skill_WARRIOR_DAMAGE` | `skill.warrior_damage` | `.name`, `.description` |
| `Dialogue_Amara` | `dialogue.dlg_amara` | `.node.<node_id>.speaker`, `.text`, `.subtitle`, `.low_integrity`, `.choice.<index>` |

All 21 shipped quest, memory, item and skill assets now have keys. The three
shipped dialogue graphs derive their prefix from `GraphId` and externalize their
lines and choices through it. To seed keys and English entries for a new asset
or graph, run `Tools/key-authored-content.py` from anywhere.
It adds missing entries without overwriting existing English copy; if you change
authored text later, update its English table entry deliberately. Quest objective
ids become lower-case field suffixes, for example
`quest.q001.objective.reach_ruins`. Empty descriptions stay in the asset and do not
create blank table entries.

**Leaving a new asset's key blank is valid:** the asset
shows the text typed into it. Adding a key before a translation exists is also valid —
the author's text still shows, and the validator raises a warning naming the key a
translator needs to fill. What is *not* valid is two assets sharing a key; that is an
error, because both would render the same title and the symptom looks like a content
mistake rather than a localization one.

**After adding or changing a key, regenerate the pseudo-locale:**

```
python Tools/make-pseudo-locale.py
```

`Strings_qps` is a machine-generated second language. It accents every letter and pads
each string by about a third, which is how a label sized for English gets caught before
a translator finds it, and it deliberately omits three keys so the English fallback is a
path something walks. Never edit it by hand — it is regenerated wholesale, and
`EveryTranslatedStringKeepsEnglishsPlaceholders` fails if it has drifted from English.

To see it, call `Strings.SetLanguage("qps")`. Anything still rendering unaccented Latin
is text that never went through `Strings`.

---

## Step 6 — Tests

A temple is not done because it plays once in the Editor.

- **Scene integrity** — add the scene to `SceneIntegrityTests`' `[TestCase]` lists.
- **Content validation** — `God Game → Validate Content` must report zero errors. Any
  new warning must be either fixed or added, deliberately, to
  `TheShippedContentsWarningsAreTheOnesWeKnowAbout`. A build refuses on errors, and
  refuses before it deletes the previous player.
- **Navigation** — re-bake the `NavMeshSurface` and save its asset after moving any
  geometry. `GameplayScene_BakedNavigationIsCurrentWithItsGeometry` fails when a spawn
  point, an enemy or a patrol waypoint has drifted off the baked mesh.
- **Anti-softlock** — tick `essential` on any item or door the player can be permanently
  locked out by. An `essential` consumable is a mistake: it would be re-granted on every
  load.
- **A PlayMode test per new mechanic.** If the temple introduces a mechanic no other
  temple has, it needs a test that a person could not have run by accident.
- **The standalone smoke test** — extend it if the temple is on the critical path.

---

## The checklist

Copy this into the task's pull request.

```
IDS
[ ] every id in step 1's table chosen and written down before building
[ ] no objective id is used by any other quest

SCENE
[ ] scene created, all managers present, SaveManager standalone
[ ] added to Build Settings (and to WindowsBuild.RequiredScenes if loaded by name)
[ ] WorldBounds sized to this temple
[ ] NavMesh baked INTO A SAVED ASSET, not left in memory

TRAVEL
[ ] SceneExit in the previous scene, naming this temple + its arrival point
[ ] SceneSpawnPoint in this temple, matching that id
[ ] SceneExit back out, naming the previous scene + its arrival point
[ ] SceneSpawnPoint in the previous scene for the return trip
[ ] walked both ways in a built player, not just in the Editor

CONTENT
[ ] quest authored; at least one objective is not Optional
[ ] memories authored; prerequisites point at ids that exist
[ ] dialogue authored; no dangling links, no unreachable nodes
[ ] rewards are real QuestRewards, not prose in rewardsSummary
[ ] boss reward assigned and INACTIVE in the scene
[ ] checkpoints before and after the boss
[ ] SaveIdentity on everything whose state must survive a save
[ ] `essential` ticked on anything the player can be locked out by

TEXT
[ ] no player-facing string literal added to any script
[ ] new keys in StringKeys and in Strings_en.asset, with translator notes
[ ] placeholders positional and numbered from zero

VERIFY
[ ] God Game > Validate Content: zero errors
[ ] scene added to SceneIntegrityTests
[ ] EditMode and PlayMode suites green
[ ] .\Tools\build-windows.ps1 -Variant Both passes
[ ] TEST_PLAN.md M0 walked by a person
[ ] CHANGELOG.md, KNOWN_ISSUES.md, ARCHITECTURE.md, TEST_PLAN.md updated
```
