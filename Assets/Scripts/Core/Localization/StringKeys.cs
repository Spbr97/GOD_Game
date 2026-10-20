namespace Game.Core.Localization
{
    /// <summary>
    /// Every string key the code asks for (SPEC.md section 73, TASK 042).
    ///
    /// Constants rather than literals at the call sites, for one reason: a typo becomes a
    /// compile error instead of a label reading <c>ui.hud.helath</c> that nobody notices
    /// until a screenshot. It also means <c>EveryKeyHasEnglishText</c> can walk this class
    /// by reflection and prove the table covers all of them — a check that is impossible
    /// if the keys only exist as strings scattered through the UI.
    ///
    /// The naming is <c>area.thing</c>, lower case, matching the folder the string appears
    /// in. Keys are never shown to a player, so they are for whoever maintains the table.
    /// </summary>
    public static class StringKeys
    {
        // ------------------------------------------------------------------------- HUD

        public const string HudHealth = "hud.health";
        public const string HudStamina = "hud.stamina";
        public const string HudDivine = "hud.divine";
        public const string HudLockedOn = "hud.locked_on";
        public const string HudCombo = "hud.combo";
        public const string HudParry = "hud.parry";
        public const string HudPerfectParry = "hud.perfect_parry";
        public const string HudGuardBroken = "hud.guard_broken";

        // -------------------------------------------------------------------- journal

        public const string JournalQuestRow = "journal.quest_row";
        public const string JournalObjectiveCount = "journal.objective_count";
        public const string JournalMemoryIntegrity = "journal.memory_integrity";
        public const string JournalMemoryRow = "journal.memory_row";
        public const string JournalCorrupt = "journal.corrupt";

        // ---------------------------------------------------------------- progression

        public const string ProgressionSkillPoints = "progression.skill_points";
        public const string ProgressionItemCount = "progression.item_count";
        public const string ProgressionSkillRow = "progression.skill_row";
        public const string ProgressionUnlocked = "progression.unlocked";
        public const string ProgressionCost = "progression.cost";
        public const string ProgressionUnlockButton = "progression.unlock_button";

        // --------------------------------------------------------------- quest tracker

        public const string QuestObjectiveComplete = "quest.objective_complete";
        public const string QuestObjectiveFailed = "quest.objective_failed";
        public const string QuestObjectiveCount = "quest.objective_count";

        // ------------------------------------------------------------------ main menu

        public const string MenuSaveRow = "menu.save_row";
        public const string MenuSaveRowEmpty = "menu.save_row_empty";
        public const string MenuDifficulty = "menu.difficulty";

        /// <summary>The language row in Settings. {0} is the language's own name for itself.</summary>
        public const string MenuLanguage = "menu.language";

        /// <summary>The button that cycles language. No placeholder.</summary>
        public const string MenuChangeLanguage = "menu.change_language";
        public const string MenuResolution = "menu.resolution";

        // --------------------------------------------------------------------- remap

        public const string RemapListening = "remap.listening";
        public const string RemapButton = "remap.button";

        // ----------------------------------------------------------------------- map

        public const string MapCheckpointReached = "map.checkpoint_reached";
        public const string MapBossDefeated = "map.boss_defeated";

        // -------------------------------------------------------------------- memory

        public const string MemoryRecovered = "memory.recovered";

        // -------------------------------------------------------------- interaction

        public const string InteractionPrompt = "interaction.prompt";
        public const string InteractionPromptWithKey = "interaction.prompt_with_key";
        public const string InteractionTakeItem = "interaction.take_item";
        public const string InteractionTakeItemGeneric = "interaction.take_item_generic";
        public const string InteractionTollPay = "interaction.toll_pay";

        /// <summary>Speaking to an NPC. {0} is the character's name.</summary>
        public const string InteractionSpeakTo = "interaction.speak_to";

        /// <summary>Recalling a memory. {0} is the memory's title.</summary>
        public const string InteractionRecallMemory = "interaction.recall_memory";

        /// <summary>Recalling a memory whose fragment is missing. No placeholder.</summary>
        public const string InteractionRecallMemoryGeneric = "interaction.recall_memory_generic";

        /// <summary>Lighting a brazier. {0} is what the brazier is called.</summary>
        public const string InteractionLightBrazier = "interaction.light_brazier";

        /// <summary>A brazier that is already lit. {0} is what it is called.</summary>
        public const string InteractionBrazierAlreadyLit = "interaction.brazier_already_lit";
        public const string InteractionTollPaid = "interaction.toll_paid";

        // ---------------------------------------------------------------------- save

        public const string SaveBackupRestored = "save.backup_restored";
        public const string SaveRefused = "save.refused";
        public const string SaveFailed = "save.failed";
        public const string SaveLoadFailed = "save.load_failed";

        // --------------------------------------------------------------------- world

        public const string WorldFellOutOfWorld = "world.fell_out_of_world";
        public const string DeviceControllerLost = "device.controller_lost";
        public const string DeviceControllerFound = "device.controller_found";

        // ------------------------------------------------------------------ dialogue

        public const string DialogueUnavailable = "dialogue.unavailable";
    }
}
