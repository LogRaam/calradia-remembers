// Code written by Gabriel Mailhot, 10/07/2026.
// Turns a resolved FirstImpressionDisposition into a ready-to-inject prompt line. Kept pure and separate from the
// policy so the wording is testable and easy to tune. The line instructs OPENING TONE only: it is explicit that
// nothing has actually wronged the NPC and that only deeds move regard, so this never becomes an automatic malus.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Renders a <see cref="FirstImpressionDisposition" /> as the prompt's "first impression" guidance.</summary>
    public static class FirstImpressionDescriber
    {
        // Appended to every prejudiced or reserved opening so the model never reads it as a real grievance.
        private const string ToneCaveat =
            " This colours your TONE and how you first receive them, nothing more: they have done nothing to wrong " +
            "you, and only their deeds, not this first impression, will move your regard.";

        /// <summary>
        ///   The guidance line for this disposition, or null when there is nothing to inject (an already-known
        ///   NPC keeps their existing stance, and a plain Neutral opening is the ordinary default).
        /// </summary>
        public static string Describe(FirstImpressionDisposition disposition)
        {
            switch (disposition)
            {
                case FirstImpressionDisposition.Loyal:
                    return "This person has joined YOUR OWN CLAN and cast their lot with you. Open with the warmth " +
                           "of a sworn member: gratitude, loyalty, or plain faith in the cause you share. Never cold, " +
                           "superior, or suspicious toward one of your own.";

                case FirstImpressionDisposition.Cordial:
                    return "You do not know this newcomer well, yet you are inclined to receive them warmly and give " +
                           "them a fair, open hearing." + ToneCaveat;

                case FirstImpressionDisposition.Cool:
                    return "You do not know this person and see little reason to warm to them. Open reserved and " +
                           "transactional, keeping them at arm's length until they prove worth your time." + ToneCaveat;

                case FirstImpressionDisposition.Arrogant:
                    return "A new clan risen in your own faction: one more mouth at the table when the spoils are " +
                           "shared. You do not know them and you resent the crowding. Open superior and a little " +
                           "territorial, as to a rival who has not earned their place." + ToneCaveat;

                case FirstImpressionDisposition.Suspicious:
                    return "You do not know this person and are not minded to trust a stranger on sight. Open guarded, " +
                           "reading for hidden motives, slow to take their word." + ToneCaveat;

                case FirstImpressionDisposition.Demeaning:
                    return "An unproven upstart with no name and no lands worth speaking of. You do not know them and " +
                           "you are not impressed. Open dismissive, even faintly contemptuous, as to a lesser." + ToneCaveat;

                default: // None, Neutral
                    return null;
            }
        }
    }
}
