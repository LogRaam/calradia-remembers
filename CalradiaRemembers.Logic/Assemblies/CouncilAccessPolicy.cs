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
        ///   What stands between the player and opening the council. Three booleans read in PRIORITY order,
        ///   and the order is the rule: each names a different block, and a swap reports the wrong one to a
        ///   player who then cannot tell what to do about it.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class CouncilAccessFacts
        {
           /// <summary>A chat is already open. The most direct hijack risk, so checked first.</summary>
           public bool ChatAlreadyOpen { get; set; }

           /// <summary>The player is held captive and holds no council.</summary>
           public bool PlayerIsPrisoner { get; set; }

           /// <summary>A mission scene owns the screen.</summary>
           public bool InMissionScene { get; set; }

           /// <summary>
           ///   The sitting is a TRIAL, held for a captive player by definition (arrests, 26/09/2026): the one
           ///   council captivity does not block.
           /// </summary>
           public bool IsTrial { get; set; }
        }

        /// <summary>
        ///   The first condition that blocks opening, in priority order: an already-open chat first (the most
        ///   direct hijack risk), then captivity, then an active scene. <see cref="CouncilAccessBlock.None" />
        ///   when none holds, so the caller may open.
        /// </summary>
        public static CouncilAccessBlock Evaluate(CouncilAccessFacts facts)
        {
           if (facts == null) return CouncilAccessBlock.None;

           bool chatAlreadyOpen = facts.ChatAlreadyOpen;
           bool playerIsPrisoner = facts.PlayerIsPrisoner;
           bool inMissionScene = facts.InMissionScene;

            if (chatAlreadyOpen) return CouncilAccessBlock.ChatAlreadyOpen;
            if (playerIsPrisoner && !facts.IsTrial) return CouncilAccessBlock.PlayerCaptive;
            if (inMissionScene) return CouncilAccessBlock.InScene;

            return CouncilAccessBlock.None;
        }
    }
}
