using Game.Core;
using Game.Core.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The build's version and a line saying this is not the finished game, in the Main
    /// Menu's corner (STANDALONE_RELEASE_ROADMAP.md TASK 054).
    ///
    /// `OWNER_RELEASE_CHECKLIST.md` asks a tester to report which build they played, and
    /// until now the only place that said so was a text file inside the ZIP — which is not
    /// where someone looks while the game is in front of them.
    ///
    /// Written at runtime rather than typed into the scene for the reason
    /// <see cref="LocalizedLabel"/> exists: a string baked into a scene belongs to no key
    /// and no language. It cannot be a plain <see cref="LocalizedLabel"/> either, because
    /// the version is not a translatable string — it is a number the table formats.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class VersionLabel : MonoBehaviour
    {
        private Text label;

        private void Awake() => label = GetComponent<Text>();

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

        /// <summary>Writes the version line. Public so a tool can show it without a play session.</summary>
        public void Apply()
        {
            var target = label != null ? label : (label = GetComponent<Text>());
            if (target == null)
            {
                return;
            }

            target.text = Strings.Format(StringKeys.MenuVersion, Application.version)
                          + "\n" + Strings.Get(StringKeys.MenuBuildNotice);
        }
    }
}
