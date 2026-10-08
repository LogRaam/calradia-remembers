// Code written by Gabriel Mailhot, 17/09/2026.
// Two player reports, a week apart, in almost the same words:
//
//   Lordfadooboo, 11/09/2026: "i enter a city and it says (companion) wants to talk to you, i say yes, and
//   nothing happens."
//   Zinced121212, 17/09/2026: "a companion may approach me to talk when entering a city or settlement but if
//   i accept the chat window doesn't actually open."
//
// Both are INSIDE A SETTLEMENT, and that is the whole of this file.
//
// When a staged meeting is asked for, one of two engine calls is made. On the map it is OpenMapConversation,
// which registers its conversation immediately and only then loads a scene, so "the call returned and no
// conversation exists" is a real failure within a few frames. Inside a settlement it is
// CreateAndOpenMissionController, which has an entire interior scene to load BEFORE any conversation can
// exist, so that same reading is the normal state for most of a second.
//
// The staging watchdog was reasoned about the first call (its own comment says so, in as many words: "This is
// NOT a timeout on the scene load") and then applied to both. A settlement staging is therefore judged dead in
// ten frames, a sixth of a second, and the recovery opens the chat over the town menu just in time for the
// arriving scene to carry it off. The player answers the popup and sees nothing.
//
// The bound is not the thing to delete. A staging that never resolves jams every later meeting, which is its
// own player report (Nexus, 2026-07-19: "an endless chain of them trying to talk to me"). What was wrong is
// that one clock was used to time two different things. So the rule is stated here, once, where it can be read
// and tested, instead of being a constant in a tick loop that nobody compares against the call it is watching.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   How long a staged meeting is given to show signs of life before the mod concludes nothing was staged
    ///   and opens the chat directly. Frames, because the caller is a per-frame tick.
    /// </summary>
    public static class StagingGracePolicy
    {
        /// <summary>
        ///   The map call registers its conversation before it loads anything, so a few frames is the SHORTEST
        ///   wait that lets a success be observed. Short on purpose: it is the difference between recovering in
        ///   a blink and leaving the player in the endless chain.
        /// </summary>
        public const int ConversationRegistrationFrames = 10;

        /// <summary>
        ///   The settlement call has an interior scene to load first. This one IS a bound on a scene load, and
        ///   is sized so a tired machine is never mistaken for a dead staging (half a minute at 60 fps) while
        ///   still being finite, because the jam it prevents is worse than the wait it costs.
        /// </summary>
        public const int MissionLoadFrames = 1800;

        /// <summary>
        ///   The grace owed to the call that was actually made. <paramref name="waitingOnAnInteriorScene" />
        ///   is true when the staging went through CreateAndOpenMissionController (inside a settlement) rather
        ///   than OpenMapConversation.
        /// </summary>
        public static int FramesFor(bool waitingOnAnInteriorScene)
            => waitingOnAnInteriorScene ? MissionLoadFrames : ConversationRegistrationFrames;

        /// <summary>
        ///   Whether the stager begins the game's conversation itself (Gabriel's play test, 08/10/2026: in the town square
        ///   the conversation with the staged person never began, the chat opened with none, and he walked off). Only for
        ///   an interior scene, once, with the person standing in it and no conversation live.
        /// </summary>
        public static bool ShouldStartTheConversation(StagedStartFacts f)
            => f != null && f.WaitingOnAnInteriorScene && !f.ConversationLive && f.PartnerInTheScene && !f.AlreadyTried;
    }

    /// <summary>What the stager knows when its grace runs out.</summary>
    public sealed class StagedStartFacts
    {
        /// <summary>The staging opened an interior scene (CreateAndOpenMissionController).</summary>
        public bool WaitingOnAnInteriorScene { get; init; }

        /// <summary>A conversation with someone is already live.</summary>
        public bool ConversationLive { get; init; }

        /// <summary>The person the conversation is for stands in the scene.</summary>
        public bool PartnerInTheScene { get; init; }

        /// <summary>The stager already began it once.</summary>
        public bool AlreadyTried { get; init; }
    }
}
