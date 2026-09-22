using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Core
{
    /// <summary>
    /// Raised when a controller comes or goes (SPEC.md section 54's edge cases 21
    /// and 22). Carries the sentence to show, so the UI does not have to know what
    /// kind of device change deserves what wording.
    /// </summary>
    public readonly struct InputDeviceChangedEvent
    {
        public readonly string DeviceName;
        public readonly bool Connected;
        public readonly string PlayerMessage;

        public InputDeviceChangedEvent(string deviceName, bool connected, string playerMessage)
        {
            DeviceName = deviceName;
            Connected = connected;
            PlayerMessage = playerMessage;
        }
    }

    /// <summary>
    /// A request to pause that did not come from the player pressing Pause (SPEC.md
    /// section 54's edge cases 21 and 23).
    ///
    /// Published rather than calling <see cref="GameManager.Pause"/> directly,
    /// because pausing is two things: the state, which GameManager owns, and the
    /// panel, which <see cref="Game.UI.PauseMenu"/> owns. Setting the state alone
    /// leaves a frozen game with no menu on it, and the next Pause press would then
    /// resume without ever having shown one. Routing the request through the bus lets
    /// the menu do what it already does, and keeps Core from referencing UI.
    /// </summary>
    public readonly struct AutoPauseRequestedEvent
    {
        public readonly string Reason;

        public AutoPauseRequestedEvent(string reason)
        {
            Reason = reason;
        }
    }

    /// <summary>
    /// Watches the things outside the game that can change underneath it (SPEC.md
    /// section 54's edge cases 21, 22 and 23): a controller being unplugged, one
    /// being plugged back in, and the window losing focus.
    ///
    /// All three pause rather than carry on. A player who has just lost their
    /// controller, or alt-tabbed away, is not playing, and combat that continues
    /// without them is combat they lose for a reason that is not theirs.
    ///
    /// Nothing here resumes automatically. Coming back to a game that un-paused
    /// itself the instant the window regained focus — mid-swing, mid-boss — is worse
    /// than one more keypress.
    /// </summary>
    [DisallowMultipleComponent]
    public class DeviceWatcher : MonoBehaviour
    {
        [Tooltip("Pause when the last gamepad disconnects (SPEC.md section 54, edge case 21).")]
        [SerializeField] private bool pauseOnControllerLoss = true;

        [Tooltip("Pause when the window loses focus (SPEC.md section 54, edge case 23).")]
        [SerializeField] private bool pauseOnFocusLoss = true;

        [Tooltip("Also do it in the Editor. Off by default: clicking out of the Game view would otherwise freeze every PlayMode test that happens to be running.")]
        [SerializeField] private bool pauseOnFocusLossInEditor;

        [SerializeField] private string controllerLostMessage = "Controller disconnected. The game is paused.";
        [SerializeField] private string controllerFoundMessage = "Controller reconnected.";

        /// <summary>Whether a gamepad was present the last time this looked. Read by tests and the debug overlay.</summary>
        public bool HasGamepad { get; private set; }

        /// <summary>Counts auto-pauses, so a test can tell "paused for this reason" from "was already paused".</summary>
        public int AutoPauseCount { get; private set; }

        private void OnEnable()
        {
            HasGamepad = Gamepad.all.Count > 0;
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is not Gamepad)
            {
                // Keyboard and mouse are not worth a notice: they are how the player
                // would dismiss it, and on a desktop they do not meaningfully vanish.
                return;
            }

            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                case InputDeviceChange.Enabled:
                    HandleControllerChanged(device.displayName, connected: true);
                    break;

                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                case InputDeviceChange.Disabled:
                    HandleControllerChanged(device.displayName, connected: false);
                    break;
            }
        }

        /// <summary>
        /// Reacts to a controller arriving or leaving. Public so a test can exercise
        /// it without physically unplugging hardware, which is the only other way the
        /// Input System raises this.
        /// </summary>
        public void HandleControllerChanged(string deviceName, bool connected)
        {
            // Gamepad.all is the truth, not this one event: a player with two pads who
            // unplugs one has not lost their controller, and pausing on them would be
            // wrong. The event only tells us to go and look again.
            HasGamepad = Gamepad.all.Count > 0;

            var message = connected ? controllerFoundMessage : controllerLostMessage;
            var lostTheLastOne = !connected && !HasGamepad;

            if (!connected && !lostTheLastOne)
            {
                // One of several pads went away. Nothing the player needs telling.
                return;
            }

            GameLogger.Log(LogCategory.Game, $"Controller '{deviceName}' {(connected ? "connected" : "disconnected")}.", this);
            EventBus.Publish(new InputDeviceChangedEvent(deviceName, connected, message));

            if (lostTheLastOne && pauseOnControllerLoss)
            {
                RequestAutoPause($"the controller '{deviceName}' was disconnected");
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (Application.isEditor && !pauseOnFocusLossInEditor)
            {
                return;
            }

            HandleFocusChanged(hasFocus);
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (Application.isEditor && !pauseOnFocusLossInEditor)
            {
                return;
            }

            HandleFocusChanged(!isPaused);
        }

        /// <summary>
        /// Reacts to the window gaining or losing focus. Public for the same reason as
        /// <see cref="HandleControllerChanged"/>: the Unity callback cannot be raised
        /// from a test.
        /// </summary>
        public void HandleFocusChanged(bool hasFocus)
        {
            if (hasFocus || !pauseOnFocusLoss)
            {
                return;
            }

            RequestAutoPause("the window lost focus");
        }

        private void RequestAutoPause(string reason)
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing)
            {
                // Already paused, in a cutscene, or still booting. Nothing to do, and
                // forcing a pause out of a cutscene would strand it half-played.
                return;
            }

            AutoPauseCount++;
            GameLogger.Log(LogCategory.Game, $"Auto-pausing: {reason}.", this);
            EventBus.Publish(new AutoPauseRequestedEvent(reason));
        }
    }
}
