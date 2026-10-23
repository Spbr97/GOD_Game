using Game.Core;
using Game.Core.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Makes a label that was typed into a scene read from the string table instead
    /// (STANDALONE_RELEASE_ROADMAP.md TASK 054).
    ///
    /// **The gap this closes was not visible from the code.** TASK 042 externalized every
    /// string a *script* shows a player, and every check written for it passed, because
    /// every check looked at scripts. Meanwhile the menus' own words — "New Game",
    /// "Continue", "Settings", every accessibility row's caption, every button on the
    /// pause panel — were authored directly into <c>MainMenu.unity</c> as
    /// <see cref="Text.text"/> values and belonged to no key at all. The language picker
    /// added in TASK 042 therefore switched the handful of labels a script writes and left
    /// the entire menu in English, which is the worst of the three possible outcomes: it
    /// looks supported, and it is not.
    ///
    /// So a scene label gets a key the same way a content asset does: authored on the
    /// object, resolved through the table, with the authored English kept in the table as
    /// the fallback rather than in the scene. <c>MenuTextTests</c> asserts that every label
    /// under a menu canvas either has one of these or is written by a script, which is what
    /// stops the gap reopening the next time someone drops a button into a panel.
    ///
    /// Redraws on <see cref="LanguageChangedEvent"/> as well as on enable, because the
    /// Settings panel is itself a menu: a player who changes language is looking straight
    /// at a screenful of these, and labels that keep their old words until reopened read as
    /// the setting having failed.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class LocalizedLabel : MonoBehaviour
    {
        [Tooltip("Key into the string table, e.g. menu.new_game. The English text lives in Strings_en, not here.")]
        [SerializeField] private string key;

        private Text label;

        public string Key => key;

        /// <summary>The label this drives. Exposed so an editor tool and a test can check it without guessing.</summary>
        public Text Label => label != null ? label : (label = GetComponent<Text>());

        private void Awake()
        {
            label = GetComponent<Text>();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);
            Apply();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        private void OnLanguageChanged(LanguageChangedEvent changed) => Apply();

        /// <summary>
        /// Writes the key's text into the label. Public so the editor tool that assigns keys
        /// can show the result immediately, and so a test can drive it without an enable
        /// cycle.
        /// </summary>
        public void Apply()
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            var target = Label;
            if (target != null)
            {
                target.text = Strings.Get(key);
            }
        }

        /// <summary>Editor-tool and test seam: assigns the key and redraws.</summary>
        public void Configure(string stringKey)
        {
            key = stringKey;
            Apply();
        }
    }
}
