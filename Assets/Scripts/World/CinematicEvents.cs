namespace Game.World
{
    /// <summary>
    /// Cinematic payloads published on <see cref="Game.Core.EventBus"/> (SPEC.md
    /// section 69, TASK 014), so <c>CinematicUI</c> and later systems (audio, a real
    /// camera rig) can react without <see cref="CinematicPlayer"/> referencing them.
    /// </summary>
    public readonly struct CinematicStartedEvent
    {
        public readonly string CinematicId;

        /// <summary>
        /// The player driving this cinematic, so <c>CinematicUI</c> has something to
        /// call <see cref="CinematicPlayer.Skip"/> on. Every other cinematic/UI event
        /// in the project carries only a payload, not a component reference, but the
        /// skip button is inherently "tell that specific sequence to stop" and an id
        /// string alone would need a scene-wide lookup to act on.
        /// </summary>
        public readonly CinematicPlayer Player;

        public CinematicStartedEvent(string cinematicId, CinematicPlayer player)
        {
            CinematicId = cinematicId;
            Player = player;
        }
    }

    /// <summary>Raised for every beat, including the first, so a subtitle UI has no state of its own to keep in sync.</summary>
    public readonly struct CinematicBeatShownEvent
    {
        public readonly string CinematicId;
        public readonly int BeatIndex;
        public readonly string Subtitle;

        public CinematicBeatShownEvent(string cinematicId, int beatIndex, string subtitle)
        {
            CinematicId = cinematicId;
            BeatIndex = beatIndex;
            Subtitle = subtitle;
        }
    }

    /// <summary>Raised when the player skips instead of letting it play out. Always immediately followed by <see cref="CinematicCompletedEvent"/>.</summary>
    public readonly struct CinematicSkippedEvent
    {
        public readonly string CinematicId;

        public CinematicSkippedEvent(string cinematicId)
        {
            CinematicId = cinematicId;
        }
    }

    /// <summary>Raised once, whether the cinematic played out or was skipped.</summary>
    public readonly struct CinematicCompletedEvent
    {
        public readonly string CinematicId;

        public CinematicCompletedEvent(string cinematicId)
        {
            CinematicId = cinematicId;
        }
    }
}
