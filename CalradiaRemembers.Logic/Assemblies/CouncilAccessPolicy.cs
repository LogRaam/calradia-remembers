// Code written by Gabriel Mailhot, 27/07/2026.
// Council audit M1/M2: the pure gate that decides whether a council or assembly may be OPENED right now. Kept
// out of ChatManager so the refusal rules are documented by tests. The mod maps live engine state (is a chat
// already open, is the player a prisoner, is a mission scene running) to the three flags; this decides the
// verdict, and the mod turns the verdict into a player-facing message.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Why a council/assembly may not be opened, or <see cref="None" /> when it is safe to open.</summary>
    public enum CouncilAccessBlock
    {
        /// <summary>Nothing blocks opening.</summary>
        None,

        /// <summary>Another conversation is already open; opening now would hijack that live window (M2).</summary>
        ChatAlreadyOpen,

        /// <summary>The player is held captive and cannot convene their own house (M1).</summary>
        PlayerCaptive,

        /// <summary>A mission scene (battle, town walkabout) is running; a map gathering has no place there (M1).</summary>
        InScene
    }

    /// <summary>Decides whether convening is allowed, given the live campaign state as three flags.</summary>
    public static class CouncilAccessPolicy
    {
        /// <summary>
        ///   The first condition that blocks opening, in priority order: an already-open chat first (the most
        ///   direct hijack risk), then captivity, then an active scene. <see cref="CouncilAccessBlock.None" />
        ///   when none holds, so the caller may open.
        /// </summary>
        public static CouncilAccessBlock Evaluate(bool chatAlreadyOpen, bool playerIsPrisoner, bool inMissionScene)
        {
            if (chatAlreadyOpen) return CouncilAccessBlock.ChatAlreadyOpen;
            if (playerIsPrisoner) return CouncilAccessBlock.PlayerCaptive;
            if (inMissionScene) return CouncilAccessBlock.InScene;

            return CouncilAccessBlock.None;
        }
    }
}
