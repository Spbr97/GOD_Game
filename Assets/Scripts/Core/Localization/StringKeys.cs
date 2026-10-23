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

        /// <summary>Shown where the quest list would be when the player has no quests.</summary>
        public const string JournalNoQuests = "journal.no_quests";

        /// <summary>Shown where the memory list would be when nothing has been discovered.</summary>
        public const string JournalNoMemories = "journal.no_memories";

        // ---------------------------------------------------------------- progression

        public const string ProgressionSkillPoints = "progression.skill_points";
        public const string ProgressionItemCount = "progression.item_count";
        public const string ProgressionSkillRow = "progression.skill_row";
        public const string ProgressionUnlocked = "progression.unlocked";
        public const string ProgressionCost = "progression.cost";
        public const string ProgressionUnlockButton = "progression.unlock_button";

        /// <summary>Shown in place of the item list when nothing is carried.</summary>
        public const string ProgressionNoItems = "progression.no_items";

        /// <summary>Shown under a skill branch that has no skills in it.</summary>
        public const string ProgressionBranchEmpty = "progression.branch_empty";

        /// <summary>Where the skill points came from, so the number is not unexplained. No placeholder.</summary>
        public const string ProgressionPointsSource = "progression.points_source";

        /// <summary>Why a skill cannot be unlocked yet: not enough points. {0} is the shortfall.</summary>
        public const string ProgressionNeedPoints = "progression.need_points";

        /// <summary>Why a skill cannot be unlocked yet: its prerequisite. {0} is the prerequisite's name.</summary>
        public const string ProgressionNeedPrerequisite = "progression.need_prerequisite";

        // --------------------------------------------------------------- quest tracker

        public const string QuestObjectiveComplete = "quest.objective_complete";
        public const string QuestObjectiveFailed = "quest.objective_failed";
        public const string QuestObjectiveCount = "quest.objective_count";

        // ------------------------------------------------------------------ main menu

        public const string MenuSaveRow = "menu.save_row";
        public const string MenuSaveRowEmpty = "menu.save_row_empty";
        public const string MenuResolution = "menu.resolution";

        // The menus' own words. Before TASK 054 these were typed into MainMenu.unity and
        // belonged to no key at all, so the language picker switched the handful of labels
        // a script writes and left every button, caption and heading in English.
        //
        // Named constants rather than literals for the usual reason — a typo becomes a
        // compile error — and, here, for a second one: LocalizationTests walks this class
        // by reflection, so a key that only exists as a literal in a builder call is a key
        // nothing can prove the table covers.

        public const string MenuSectionAudio = "menu.section.audio";
        public const string MenuSectionGameplay = "menu.section.gameplay";
        public const string MenuSectionAccessibility = "menu.section.accessibility";
        public const string MenuSectionDisplay = "menu.section.display";

        public const string MenuLabelMasterVolume = "menu.label.master_volume";
        public const string MenuLabelLookSensitivity = "menu.label.look_sensitivity";
        public const string MenuLabelInvertY = "menu.label.invert_y";
        public const string MenuLabelDifficulty = "menu.label.difficulty";
        public const string MenuLabelLanguage = "menu.label.language";
        public const string MenuLabelTextSize = "menu.label.text_size";
        public const string MenuLabelSubtitleBackground = "menu.label.subtitle_background";
        public const string MenuLabelCameraShake = "menu.label.camera_shake";
        public const string MenuLabelScreenEffects = "menu.label.screen_effects";
        public const string MenuLabelAimAssist = "menu.label.aim_assist";
        public const string MenuLabelResolution = "menu.label.resolution";
        public const string MenuLabelQuality = "menu.label.quality";
        public const string MenuLabelFullscreen = "menu.label.fullscreen";

        /// <summary>The caption on a step-through button. No placeholder.</summary>
        public const string MenuLabelChange = "menu.label.change";

        public const string MenuLabelOn = "menu.label.on";
        public const string MenuLabelOff = "menu.label.off";
        public const string MenuLabelBack = "menu.label.back";
        public const string MenuLabelSettings = "menu.label.settings";
        public const string MenuLabelControls = "menu.label.controls";
        public const string MenuLabelHelp = "menu.label.help";
        public const string MenuLabelResume = "menu.label.resume";
        public const string MenuLabelSave = "menu.label.save";
        public const string MenuLabelMainMenu = "menu.label.main_menu";
        public const string MenuLabelPaused = "menu.label.paused";
        public const string MenuLabelSettingsTitle = "menu.label.settings_title";
        public const string MenuLabelControlsTitle = "menu.label.controls_title";
        public const string MenuLabelHelpTitle = "menu.label.help_title";
        public const string MenuLabelTitle = "menu.label.title";
        public const string MenuLabelNewGame = "menu.label.new_game";
        public const string MenuLabelNewGameTitle = "menu.label.new_game_title";
        public const string MenuLabelContinue = "menu.label.continue";
        public const string MenuLabelLoad = "menu.label.load";
        public const string MenuLabelLoadTitle = "menu.label.load_title";
        public const string MenuLabelCredits = "menu.label.credits";
        public const string MenuLabelCreditsTitle = "menu.label.credits_title";
        public const string MenuLabelQuit = "menu.label.quit";
        public const string MenuLabelResetBindings = "menu.label.reset_bindings";

        /// <summary>The build's version in the menu's corner. {0} is the version string.</summary>
        public const string MenuVersion = "menu.version";

        /// <summary>Tells the player this build is unfinished, where they will actually read it.</summary>
        public const string MenuBuildNotice = "menu.build_notice";

        // --------------------------------------------------------------------- credits

        public const string CreditsMade = "credits.made";
        public const string CreditsEngine = "credits.engine";
        public const string CreditsAssets = "credits.assets";
        public const string CreditsThanks = "credits.thanks";

        /// <summary>
        /// A difficulty's name and its one-line description. Built as
        /// <c>menu.difficulty.&lt;mode&gt;</c> and <c>menu.difficulty.&lt;mode&gt;.description</c>
        /// from <see cref="Game.Core.DifficultyMode"/>, so adding a mode cannot leave the
        /// menu showing a bare enum name.
        /// </summary>
        public const string MenuDifficultyPrefix = "menu.difficulty.";

        /// <summary>What the player is about to lose by leaving without saving. No placeholder.</summary>
        public const string MenuUnsavedWarning = "menu.unsaved_warning";

        // ----------------------------------------------------------------- how to play

        /// <summary>A control's line on the Help screen. {0} is what it does, {1} the key or button.</summary>
        public const string HelpBindingRow = "help.binding_row";

        /// <summary>Said once at the top of the Help screen, before the control list.</summary>
        public const string HelpIntro = "help.intro";

        /// <summary>Says that the list above is the live bindings, not a printed sheet.</summary>
        public const string HelpBindingNote = "help.binding_note";

        public const string HelpSectionMoving = "help.section.moving";
        public const string HelpSectionFighting = "help.section.fighting";
        public const string HelpSectionScreens = "help.section.screens";

        /// <summary>
        /// What one control does, in words, as <c>help.action.&lt;action&gt;</c> lower-cased
        /// from the Input System action's own name. Derived rather than switched so a new
        /// action shows a missing key instead of its C# name.
        /// </summary>
        public const string HelpActionPrefix = "help.action.";

        /// <summary>A line of advice a first-time player needs and cannot work out from the controls.</summary>
        public const string HelpTipStamina = "help.tip.stamina";
        public const string HelpTipMemory = "help.tip.memory";
        public const string HelpTipSaving = "help.tip.saving";

        /// <summary>Toggling the HUD off and on (SPEC.md section 42). {0} is the bound key.</summary>
        public const string HudToggleHint = "hud.toggle_hint";

        // --------------------------------------------------------------------- remap

        public const string RemapListening = "remap.listening";
        public const string RemapButton = "remap.button";

        // ----------------------------------------------------------------------- map

        public const string MapCheckpointReached = "map.checkpoint_reached";
        public const string MapBossDefeated = "map.boss_defeated";

        /// <summary>Shown on the map when it has nothing to mark. No placeholder.</summary>
        public const string MapEmpty = "map.empty";

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

        // ------------------------------------------------------------------------ UI

        /// <summary>
        /// Shown in place of a screen's contents when the system behind it is missing from
        /// the scene. A player should never see this — it means the scene is misconfigured
        /// — so the precise diagnostic goes to <c>GameLogger</c> and the player gets one
        /// short sentence rather than a developer's sentence about scene wiring.
        /// </summary>
        public const string UiUnavailable = "ui.unavailable";
    }
}
