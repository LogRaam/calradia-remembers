// Code written by Gabriel Mailhot, 20/07/2026.
// Courier robustness batch (ROADMAP 2026-07-20, inspired by the Realistic Courier decompile): when a letter
// becomes undeliverable, the player used to get one generic line ("could not be reached") whatever happened.
// The world knows more: a dead recipient and a captured one are different stories, and telling the right one
// turns a mute failure into narrative. Pure so the wording rules are documented by tests.

namespace CalradiaRemembers.Logic
{
    /// <summary>Why a letter could not be delivered, in the order the courier would learn of it.</summary>
    public enum UndeliverableCause
    {
        /// <summary>The recipient died before the letter arrived.</summary>
        RecipientDead,

        /// <summary>The recipient was taken prisoner before the letter arrived.</summary>
        RecipientCaptured,

        /// <summary>The recipient cannot be located (vanished, no party, no settlement).</summary>
        RecipientVanished
    }

    /// <summary>Turns an undeliverable letter's cause into the specific line the player is told.</summary>
    public static class UndeliverableReasonDescriber
    {
        /// <summary>Dead outranks captured: a recipient who died in captivity is reported dead.</summary>
        public static UndeliverableCause Resolve(bool recipientIsDead, bool recipientIsPrisoner)
        {
            if (recipientIsDead) return UndeliverableCause.RecipientDead;

            return recipientIsPrisoner ? UndeliverableCause.RecipientCaptured : UndeliverableCause.RecipientVanished;
        }

        /// <summary>
        ///   The reason clause shown to the player inside "Your letter could not be delivered: {clause}.".
        ///   Each cause names what actually happened instead of a generic "could not be reached".
        /// </summary>
        public static string Describe(UndeliverableCause cause, string recipientName)
        {
            switch (cause)
            {
                case UndeliverableCause.RecipientDead:
                    return $"word comes back that {recipientName} is dead";
                case UndeliverableCause.RecipientCaptured:
                    return $"{recipientName} has been taken prisoner, and no letter reaches a captive";
                default:
                    return $"{recipientName} could not be reached";
            }
        }
    }
}
