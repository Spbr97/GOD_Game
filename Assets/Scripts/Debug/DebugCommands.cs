using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Game.AI;
using Game.Combat;
using Game.Core;
using Game.Memory;
using Game.Progression;
using Game.Quests;
using Game.World;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>
    /// The developer tools SPEC.md section 52 asks for, as plain static methods.
    ///
    /// No UI, no MonoBehaviour, no scene requirement: <see cref="DebugConsole"/>
    /// parses text and calls these, and a test calls them directly. Splitting it that
    /// way is the whole point — a tool that only exists inside an IMGUI window can
    /// never be automated, and these are exactly the operations an automated test of
    /// a late-game state needs.
    ///
    /// Every method returns the sentence to show the developer instead of logging and
    /// returning void, so a failure is answered in the console where the command was
    /// typed rather than somewhere in a log they then have to go and find.
    ///
    /// These drive the real systems — real events, real flags, real consequences —
    /// rather than writing state directly. A debug teleport that skipped the
    /// checkpoint system would be testing something the game does not do.
    /// </summary>
    public static class DebugCommands
    {
        /// <summary>Refused when debug mode is off, so nothing here can fire in a release build.</summary>
        private const string Unavailable = "Debug mode is not enabled in this build.";

        // ------------------------------------------------------------------ 1. teleport

        public static string Teleport(Vector3 position)
        {
            if (!DebugMode.IsEnabled)
            {
                return Unavailable;
            }

            if (!TryGetPlayer(out var player))
            {
                return "No player in this scene.";
            }

            // The CharacterController caches its own position and drags the player
            // back otherwise — the same dance PlayerDeath.Teleport does.
            var controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            player.transform.position = position;

            if (controller != null)
            {
                controller.enabled = true;
            }

            player.GetComponent<Game.Player.PlayerController>()?.CancelVerticalVelocity();

            return $"Teleported the player to {position}.";
        }

        /// <summary>Teleports to a named checkpoint, which is how a developer actually thinks about places.</summary>
        public static string TeleportToCheckpoint(string checkpointId)
        {
            if (!DebugMode.IsEnabled)
            {
                return Unavailable;
            }

            foreach (var checkpoint in Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!string.Equals(checkpoint.CheckpointId, checkpointId, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                checkpoint.Activate();
                return Teleport(checkpoint.RespawnPosition);
            }

            return $"No checkpoint called '{checkpointId}'. Known: {string.Join(", ", CheckpointIds())}";
        }

        // ------------------------------------------------------------- 2. unlock ability

        public static string UnlockAbility(string skillId)
        {
            if (!DebugMode.IsEnabled)
            {
                return Unavailable;
            }

            var skills = SkillTreeManager.Instance;
            if (skills == null)
            {
                return "No SkillTreeManager in this scene.";
            }

            var skill = skills.Find(skillId);
            if (skill == null)
            {
                return $"No skill called '{skillId}'. Known: {string.Join(", ", skills.CatalogueIds)}";
            }

            if (skills.IsUnlocked(skillId))
            {
                return $"'{skillId}' is already unlocked.";
            }

            // Unlock the prerequisite chain first, and pay for each of them. A
            // developer asking for a late skill means "put me in the state where I
            // have it", not "tell me I am missing three earlier ones".
            var chain = new List<SkillDefinition>();
            var walk = skill;
            while (walk != null && !skills.IsUnlocked(walk.SkillId))
            {
                chain.Insert(0, walk);
                walk = string.IsNullOrEmpty(walk.PrerequisiteId) ? null : skills.Find(walk.PrerequisiteId);
            }

            var unlocked = new List<string>();
            foreach (var step in chain)
            {
                // Grant exactly the cost rather than a large number, so the points
                // total afterwards is still the player's own.
                WorldState.Instance?.AddToCounter(SkillTreeManager.SkillPointsFlag, step.Cost);

                if (!skills.Unlock(step.SkillId))
                {
                    return $"Unlocked {string.Join(", ", unlocked)} but '{step.SkillId}' was refused.";
                }

                unlocked.Add(step.SkillId);
            }

            return $"Unlocked {string.Join(", ", unlocked)}.";
        }

        // ------------------------------------------------- 3 and 4. complete / reset quest

        public static string CompleteQuest(string questId)
        {
            if (!DebugMode.IsEnabled)
            {
                return Unavailable;
            }

            var quests = QuestManager.Instance;
            if (quests == null)
            {
                return "No QuestManager in this scene.";
            }

            if (quests.GetStatus(questId) != QuestStatus.Active && !quests.StartQuest(questId))
            {
                return $"Could not start or find quest '{questId}'.";
            }

            var progress = quests.GetProgress(questId);
            if (progress == null)
            {
                return $"Quest '{questId}' has no progress to complete.";
            }

            // Report each objective through the real path rather than marking the
            // quest complete behind its back: the completion flags, the rewards and
            // the follow-on quest are all things that only happen on that path, and
            // they are usually what the developer is trying to reach.
            var objectives = progress.Definition.Objectives;
            for (var i = 0; objectives != null && i < objectives.Count; i++)
            {
                var objective = objectives[i];
                var required = Mathf.Max(1, objective.RequiredCount);

                for (var step = progress.GetCount(objective.ObjectiveId); step < required; step++)
                {
                    quests.ReportObjective(objective.ObjectiveId);
                }
            }

            return $"Quest '{questId}' is now {quests.GetStatus(questId)}.";
        }

        public static string ResetQuest(string questId)
        {
            if (!DebugMode.IsEnabled)
            {
                return Unavailable;
            }

            var quests = QuestManager.Instance;
            if (quests == null)
            {
                return "No QuestManager in this scene.";
            }

            // Restore with no completed objectives: the same path a save load takes,
            // which is silent by design — no started event, no completion flags — so
            // resetting does not re-fire anything the player already lived through.
            var empty = new string[0];
            if (!quests.RestoreQuest(questId, QuestStatus.Active, empty, new int[0], new bool[0]))
            {
                return $"No quest called '{questId}' in the catalogue.";
            }

            return $"Quest '{questId}' reset to Active with no objectives complete.";
        }

        // ------------------------------------------------------------- 5. spawn enemy

        /// <summary>
        /// Clones an enemy already in the scene. Building one from scratch would mean
        /// this file knowing how a dozen components fit together, and it would drift
        /// from how the scenes actually author them the first time one changes.
        /// </summary>
        public static string SpawnEnemy(string archetypeHint = null)
        {
            if (!DebugMode.IsEnabled)
            {
                return Unavailable;
            }

            if (!TryGetPlayer(out var player))
            {
                return "No player in this scene to spawn next to.";
            }

            EnemyController template = null;
            var known = new List<string>();

            foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var id = enemy.Archetype != null ? enemy.Archetype.ArchetypeId : enemy.name;
                known.Add(id);

                if (string.IsNullOrEmpty(archetypeHint)
                    || id.IndexOf(archetypeHint, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    template = enemy;
                    break;
                }
            }

            if (template == null)
            {
                return known.Count == 0
                    ? "No enemy in this scene to clone."
                    : $"No enemy matching '{archetypeHint}'. Known: {string.Join(", ", known)}";
            }

            var position = player.transform.position + player.transform.forward * 4f;
            var spawned = Object.Instantiate(template.gameObject, position, player.transform.rotation);
            spawned.name = $"{template.name} (debug spawn)";
            spawned.SetActive(true);

            return $"Spawned '{spawned.name}' at {position}.";
        }

        // --------------------------------------------------------- 6. kill all enemies

        public static string KillAllEnemies()
        {
            if (!DebugMode.IsEnabled)
            {
                return Unavailable;
            }

            var killed = 0;

            foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                var health = enemy.GetComponent<HealthComponent>();
                if (health == null || health.IsDead)
                {
                    continue;
                }

                // Through Kill, not by zeroing health: death events, loot, quest
                // targets and save flags all hang off that path.
                health.IsInvulnerable = false;
                health.Kill(new DamageData
                {
                    Amount = health.MaxHealth,
                    Type = DamageType.Environmental,
                    Source = null,
                    HitPoint = enemy.transform.position,
                    Direction = Vector3.up,
                    AttackId = DamageData.NextAttackId(),
                    Unblockable = true
                });

                killed++;
            }

            return $"Killed {killed} enem{(killed == 1 ? "y" : "ies")}.";
        }

        // ------------------------------------------- 7 and 8. restore / corrupt memory

        public static string RestoreMemory(string memoryId)
        {
            if (!DebugMode.IsEnabled)
            {
                return Unavailable;
            }

            var memories = MemoryManager.Instance;
            if (memories == null)
            {
                return "No MemoryManager in this scene.";
            }

            var fragment = memories.Find(memoryId);
            if (fragment == null)
            {
                return $"No memory called '{memoryId}'.";
            }

            if (!memories.IsDiscovered(memoryId))
            {
                memories.Discover(fragment);
            }

            memories.SetState(fragment, MemoryState.Restored);
            return $"Memory '{memoryId}' is now {memories.GetState(memoryId)}.";
        }

        public static string CorruptMemory(string memoryId, float integrityCost = 0.1f)
        {
            if (!DebugMode.IsEnabled)
            {
                return Unavailable;
            }

            var memories = MemoryManager.Instance;
            if (memories == null)
            {
                return "No MemoryManager in this scene.";
            }

            var fragment = memories.Find(memoryId);
            if (fragment == null)
            {
                return $"No memory called '{memoryId}'.";
            }

            if (!memories.IsDiscovered(memoryId))
            {
                memories.Discover(fragment);
            }

            // Protected memories refuse on purpose (SPEC.md section 21's critical
            // memory protection, edge case 18). The debug tool does not get to break
            // that rule — it is one of the rules most worth exercising.
            if (!memories.Corrupt(fragment, integrityCost))
            {
                return fragment.IsProtected
                    ? $"'{memoryId}' is protected and cannot be corrupted. That is the rule working."
                    : $"'{memoryId}' refused corruption.";
            }

            return $"Memory '{memoryId}' is now {memories.GetState(memoryId)}; integrity {memories.Integrity:P0}.";
        }

        // ------------------------------------------------------------ 9. set world state

        public static string SetWorldFlag(string flag, bool value)
        {
            if (!DebugMode.IsEnabled)
            {
                return Unavailable;
            }

            var state = WorldState.Instance;
            if (state == null)
            {
                return "No WorldState in this scene.";
            }

            state.SetFlag(flag, value);
            return $"Flag '{flag}' = {value}.";
        }

        public static string SetWorldCounter(string key, int value)
        {
            if (!DebugMode.IsEnabled)
            {
                return Unavailable;
            }

            var state = WorldState.Instance;
            if (state == null)
            {
                return "No WorldState in this scene.";
            }

            state.SetCounter(key, value);
            return $"Counter '{key}' = {value}.";
        }

        // -------------------------------------------------------------- 10. trigger boss

        public static string TriggerBoss(string bossId = null)
        {
            if (!DebugMode.IsEnabled)
            {
                return Unavailable;
            }

            if (!TryGetPlayer(out var player))
            {
                return "No player in this scene for the boss to notice.";
            }

            var known = new List<string>();

            foreach (var boss in Object.FindObjectsByType<BossController>(FindObjectsSortMode.None))
            {
                known.Add(boss.BossId);

                if (!string.IsNullOrEmpty(bossId)
                    && boss.BossId.IndexOf(bossId, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (boss.Defeated)
                {
                    return $"Boss '{boss.BossId}' is already defeated.";
                }

                // The same event the boss's own perception raises, so the encounter
                // starts exactly as it does in play — music, HUD bar and hooks all.
                EventBus.Publish(new EnemyAlertedEvent(boss.gameObject, player));
                return $"Started the encounter with '{boss.DisplayName}'.";
            }

            return known.Count == 0
                ? "No boss in this scene."
                : $"No boss matching '{bossId}'. Known: {string.Join(", ", known)}";
        }

        // ------------------------------------------------- 11 and 12. refill health/stamina

        public static string RefillHealth()
        {
            if (!DebugMode.IsEnabled)
            {
                return Unavailable;
            }

            if (!TryGetPlayer(out var player))
            {
                return "No player in this scene.";
            }

            var health = player.GetComponent<HealthComponent>();
            if (health == null)
            {
                return "The player has no HealthComponent.";
            }

            health.ResetHealth();
            return $"Health refilled to {health.CurrentHealth:0}/{health.MaxHealth:0}.";
        }

        public static string RefillStamina()
        {
            if (!DebugMode.IsEnabled)
            {
                return Unavailable;
            }

            if (!TryGetPlayer(out var player))
            {
                return "No player in this scene.";
            }

            var stamina = player.GetComponent<StaminaComponent>();
            var divine = player.GetComponent<DivineEnergyComponent>();

            stamina?.ResetStamina();

            // Divine energy rides along. Section 52 does not list it, but it is the
            // third bar on the same HUD and a developer refilling two of three and
            // then hunting for the third is the kind of friction these tools exist to
            // remove.
            if (divine != null)
            {
                divine.RestoreTo(divine.MaxEnergy);
            }

            return stamina == null
                ? "The player has no StaminaComponent."
                : $"Stamina refilled to {stamina.CurrentStamina:0}/{stamina.MaxStamina:0}.";
        }

        // ---------------------------------------------------------------- out of world

        /// <summary>
        /// Forces the out-of-world recovery (SPEC.md section 50). Not in section 52's
        /// list, but the thing it tests cannot otherwise be reached without finding a
        /// hole in the floor, which is the bug you are looking for.
        /// </summary>
        public static string ForceRecovery()
        {
            if (!DebugMode.IsEnabled)
            {
                return Unavailable;
            }

            if (!TryGetPlayer(out var player))
            {
                return "No player in this scene.";
            }

            var guard = player.GetComponent<PlayerBoundsGuard>();
            if (guard == null)
            {
                return "The player has no PlayerBoundsGuard.";
            }

            guard.Recover();
            return $"Recovered the player to {player.transform.position}.";
        }

        // --------------------------------------------------------------------- helpers

        private static bool TryGetPlayer(out GameObject player)
        {
            // PlayerDeath marks the player, the same lookup HudUI and SaveManager use.
            var found = Object.FindAnyObjectByType<PlayerDeath>();
            player = found != null ? found.gameObject : null;
            return player != null;
        }

        private static IEnumerable<string> CheckpointIds()
        {
            foreach (var checkpoint in Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                yield return checkpoint.CheckpointId;
            }
        }

        /// <summary>
        /// Parses one console line and runs it. Split from the methods above so the
        /// tools stay usable without going through text at all.
        /// </summary>
        public static string Run(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return string.Empty;
            }

            var parts = line.Trim().Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].ToLowerInvariant();
            var argument = parts.Length > 1 ? parts[1] : null;

            switch (command)
            {
                case "help":
                    return Help();

                case "teleport" or "tp":
                    if (parts.Length == 4
                        && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                        && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
                        && float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                    {
                        return Teleport(new Vector3(x, y, z));
                    }

                    return argument != null
                        ? TeleportToCheckpoint(argument)
                        : "Usage: teleport <x> <y> <z>  |  teleport <checkpointId>";

                case "unlock":
                    return argument != null ? UnlockAbility(argument) : "Usage: unlock <skillId>";

                case "quest":
                    if (parts.Length < 3)
                    {
                        return "Usage: quest complete <id>  |  quest reset <id>";
                    }

                    return parts[1].ToLowerInvariant() switch
                    {
                        "complete" => CompleteQuest(parts[2]),
                        "reset" => ResetQuest(parts[2]),
                        _ => "Usage: quest complete <id>  |  quest reset <id>"
                    };

                case "spawn":
                    return SpawnEnemy(argument);

                case "killall":
                    return KillAllEnemies();

                case "memory":
                    if (parts.Length < 3)
                    {
                        return "Usage: memory restore <id>  |  memory corrupt <id>";
                    }

                    return parts[1].ToLowerInvariant() switch
                    {
                        "restore" => RestoreMemory(parts[2]),
                        "corrupt" => CorruptMemory(parts[2]),
                        _ => "Usage: memory restore <id>  |  memory corrupt <id>"
                    };

                case "flag":
                    if (parts.Length < 2)
                    {
                        return "Usage: flag <name> [true|false]";
                    }

                    return SetWorldFlag(parts[1], parts.Length < 3 || !bool.TryParse(parts[2], out var flagValue) || flagValue);

                case "counter":
                    if (parts.Length < 3 || !int.TryParse(parts[2], out var counterValue))
                    {
                        return "Usage: counter <name> <value>";
                    }

                    return SetWorldCounter(parts[1], counterValue);

                case "boss":
                    return TriggerBoss(argument);

                case "heal":
                    return RefillHealth();

                case "stamina":
                    return RefillStamina();

                case "recover":
                    return ForceRecovery();

                default:
                    return $"Unknown command '{command}'. Type 'help'.";
            }
        }

        public static string Help()
        {
            var text = new StringBuilder();
            text.AppendLine("SPEC.md section 52 developer tools:");
            text.AppendLine("  teleport <x> <y> <z> | teleport <checkpointId>");
            text.AppendLine("  unlock <skillId>            complete a prerequisite chain and unlock");
            text.AppendLine("  quest complete <id>         drive every objective through the real path");
            text.AppendLine("  quest reset <id>            back to Active with nothing complete");
            text.AppendLine("  spawn [archetype]           clone an enemy in front of the player");
            text.AppendLine("  killall                     kill every living enemy");
            text.AppendLine("  memory restore <id>");
            text.AppendLine("  memory corrupt <id>         refused for protected memories, by design");
            text.AppendLine("  flag <name> [true|false]");
            text.AppendLine("  counter <name> <value>");
            text.AppendLine("  boss [bossId]               start a boss encounter");
            text.AppendLine("  heal                        refill health");
            text.AppendLine("  stamina                     refill stamina and divine energy");
            text.AppendLine("  recover                     force the out-of-world recovery");
            text.AppendLine("  show fps|ai|nav|flags|save   toggle an overlay");
            return text.ToString();
        }
    }
}
