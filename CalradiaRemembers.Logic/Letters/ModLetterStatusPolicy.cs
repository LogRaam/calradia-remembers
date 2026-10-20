// Code written by Gabriel Mailhot, 20/10/2026.
// tashmetu (20/10/2026): "Deliver returns only true or false, so R&S finds its letter on the sender's profile by the
// sender, the day it was sent and its text. If Deliver returned an id [...], that letter could be asked about by its id:
// delivered yet, lost on the road, answered." And: "today closing the window leaves nothing behind, so a mod cannot tell
// 'read and set aside' from 'not read yet'." Gabriel approved (20/10/2026). What another mod is told of its letter, as one
// word it can compare without naming any of CR's types (CrLetters.GetStatus).

namespace CalradiaRemembers.Logic.Letters
{
    /// <summary>What a mod's letter has become, as far as the player's side of it goes.</summary>
    public sealed class ModLetterFacts
    {
        /// <summary>The courier put it in the player's hands.</summary>
        public bool IsDelivered { get; init; }

        /// <summary>The courier was lost on the road; CR sends it again until it arrives.</summary>
        public bool IsLost { get; init; }

        /// <summary>The player sealed a reply to it.</summary>
        public bool HasReply { get; init; }

        /// <summary>The player read it and set it aside without replying.</summary>
        public bool SetAside { get; init; }
    }

    /// <summary>The status words CrLetters.GetStatus answers with.</summary>
    public static class ModLetterStatusPolicy
    {
        /// <summary>No letter has this id (never delivered through CR, or its sender's memory is gone).</summary>
        public const string Unknown = "unknown";

        /// <summary>A courier carries it; the player has not had it yet.</summary>
        public const string OnTheRoad = "on_the_road";

        /// <summary>Its courier was lost; it has not reached the player.</summary>
        public const string Lost = "lost";

        /// <summary>The player has it, and has neither answered nor set it aside.</summary>
        public const string Delivered = "delivered";

        /// <summary>The player read it and set it aside without replying.</summary>
        public const string SetAsideStatus = "set_aside";

        /// <summary>The player sealed a reply (CrLetters.GetReply gives its text).</summary>
        public const string Answered = "answered";

        /// <summary>The furthest this letter has come: an answer outranks setting it aside, which outranks having it.</summary>
        public static string Of(ModLetterFacts f)
        {
            if (f == null) return Unknown;
            if (f.HasReply) return Answered;
            if (f.SetAside) return SetAsideStatus;
            if (f.IsDelivered) return Delivered;

            return f.IsLost ? Lost : OnTheRoad;
        }
    }
}
