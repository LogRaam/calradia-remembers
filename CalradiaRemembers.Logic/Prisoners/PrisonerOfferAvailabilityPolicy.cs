// Code written by Gabriel Mailhot, 09/09/2026.
// Player report (Nexus, 2026-09-08): "Lords are writing me to offer some good sums of coin for the release of
// prisoners, and my only option is to decline the letter. I wanted to accept an offer, but was forced to decline."
//
// The Accept button was already conditional and the condition is right: a trade cannot be settled for a captive
// who is no longer in your prison. What was wrong is that the mod knew exactly why and said nothing. The player
// read a generous offer, found one button that turned it down, and could not tell a lapsed offer from a broken
// mod. He even opened with "this isn't necessarily a bug" because he had no way to judge.
//
// So the reason is a value now, not a hidden boolean: the window says it, and the delivery log records it, which
// is also how we will learn whether these offers usually arrive too late to take (Gabriel and I do not know yet,
// and the fix if they do is to speed those letters, NOT to block the vanilla ransom offer, which the player
// answers themselves through a map notification that auto-declines after two days).

namespace CalradiaRemembers.Logic.Prisoners
{
    /// <summary>Why a lord's prisoner offer cannot be accepted, or <see cref="None" /> when it can.</summary>
    public enum PrisonerOfferBlock
    {
        /// <summary>Nothing stands in the way: the trade can be settled exactly as the letter named it.</summary>
        None,

        /// <summary>The letter carries no captive or no sum, so there is no trade to settle.</summary>
        TermsMissing,

        /// <summary>The letter names a captive the world no longer knows at all.</summary>
        CaptiveUnknown,

        /// <summary>The captive is real but no longer yours to trade: freed, ransomed, escaped, or lost with a fief.</summary>
        NoLongerHeld
    }

    /// <summary>Whether a lord's written offer for a prisoner can still be honoured, and if not, why not.</summary>
    public static class PrisonerOfferAvailabilityPolicy
    {
        /// <summary>
        ///   What the host reads off the live campaign about one prisoner-buy offer. Four booleans in a row,
        ///   and each produces a DIFFERENT sentence to the player and a different line in the log, which is
        ///   the whole reason they are named apart rather than collapsed into one "unavailable".
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class PrisonerOfferFacts
        {
           /// <summary>The offer names whom it is about.</summary>
           public bool HasTargetId { get; set; }

           /// <summary>The offer names a sum. Without terms there is no offer to speak of.</summary>
           public bool HasAmount { get; set; }

           /// <summary>The captive can be found in the world at all.</summary>
           public bool CaptiveResolved { get; set; }

           /// <summary>
           ///   The player still holds them. You cannot sell a captive who has been ransomed, escaped, or was
           ///   lost with a fallen fief.
           /// </summary>
           public bool StillHeld { get; set; }
        }

        /// <summary>
        ///   Judges the offer from plain facts the host reads off the live campaign. The order matters: terms
        ///   first, because without them there is no offer to speak of; then whether the captive exists at all;
        ///   then whether they are still held. Each answer is a different sentence to the player and a different
        ///   line in the log, which is the whole point of naming them apart.
        /// </summary>
        public static PrisonerOfferBlock Evaluate(PrisonerOfferFacts facts)
        {
           if (facts == null) return PrisonerOfferBlock.TermsMissing;

           bool hasTargetId = facts.HasTargetId;
           bool hasAmount = facts.HasAmount;
           bool captiveResolved = facts.CaptiveResolved;
           bool stillHeld = facts.StillHeld;

            if (!hasTargetId || !hasAmount) return PrisonerOfferBlock.TermsMissing;
            if (!captiveResolved) return PrisonerOfferBlock.CaptiveUnknown;

            return stillHeld ? PrisonerOfferBlock.None : PrisonerOfferBlock.NoLongerHeld;
        }

        /// <summary>
        ///   What to tell the player, in their own reading window, when the offer cannot be taken. Empty when it
        ///   can, so the note simply does not appear. <paramref name="captiveName" /> may be null or unknown, in
        ///   which case the sentence still has to read like a sentence.
        /// </summary>
        public static string Describe(PrisonerOfferBlock block, string captiveName)
        {
            string who = string.IsNullOrWhiteSpace(captiveName) ? "The captive named here" : captiveName.Trim();

            switch (block)
            {
                case PrisonerOfferBlock.NoLongerHeld:
                    return $"{who} is no longer your prisoner, so this trade can no longer be made. You can only set the letter aside.";
                case PrisonerOfferBlock.CaptiveUnknown:
                    return "The prisoner this letter speaks of can no longer be found in the world, so the trade cannot be settled.";
                case PrisonerOfferBlock.TermsMissing:
                    return "This letter names no terms the ledger can settle, so it can only be set aside.";
                default:
                    return "";
            }
        }
    }
}
