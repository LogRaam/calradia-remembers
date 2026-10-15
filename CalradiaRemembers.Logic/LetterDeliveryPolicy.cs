// Code written by Gabriel Mailhot, 11/07/2026.
// Two distinct delivery concerns for letters: divorce letters must ALWAYS reach the player (they must never
// be silently held back, whatever their sender's proximity), while loneliness/longing letters read wrong
// when the sender is standing right beside the player and should instead wait until they part again.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure gate deciding whether a letter should be delivered now, or held back because its sender is
    ///   currently near the player. Kept separate from cadence/distance policies: this is purely about
    ///   the moment of delivery, not whether a letter is written at all.
    /// </summary>
    public static class LetterDeliveryPolicy
    {
        /// <summary>
        ///   True for a letter reason that must ALWAYS reach the player, even while its sender stands right
        ///   beside them: the player must always be told a divorce is unfolding, so these are never deferred
        ///   for proximity.
        /// </summary>
        public static bool IsGuaranteedDelivery(LetterReason reason)
        {
            return reason == LetterReason.SpouseDivorceDemand || reason == LetterReason.SpouseDivorceEscalation;
        }

        /// <summary>
        ///   True for a loneliness or longing letter reason that reads wrong when the sender is standing
        ///   beside the player, and so should defer delivery until they are apart again.
        /// </summary>
        public static bool ShouldDeferWhenNear(LetterReason reason)
        {
            return reason == LetterReason.RomanticCorrespondence
                || reason == LetterReason.SpouseCorrespondence
                || reason == LetterReason.AwaitingReply;
        }

        /// <summary>
        ///   The sea gate: true when every delivery must be held back because the PLAYER is currently at sea.
        ///   No courier can meet a ship, so neither the physical courier nor the date-based fallback may
        ///   deliver while the player sails (player report: letters "delivered" in the middle of a crossing).
        ///   The letter stays a candidate and lands on the first daily tick the player is ashore past its
        ///   delivery day.
        /// </summary>
        public static bool ShouldHoldDeliveryAtSea(bool playerAtSea)
        {
            return playerAtSea;
        }

        /// <summary>
        ///   The delivery gate: true when the letter should be held back rather than delivered right now,
        ///   that is, the sender is currently near the player AND this reason is one that reads wrong up
        ///   close (<see cref="ShouldDeferWhenNear" />). Guaranteed-delivery reasons
        ///   (<see cref="IsGuaranteedDelivery" />) are never deferred by proximity even if a future reason
        ///   set were to overlap both lists.
        /// </summary>
        public static bool ShouldDeferDelivery(LetterReason reason, bool senderNearPlayer)
        {
            return senderNearPlayer && ShouldDeferWhenNear(reason);
        }

        /// <summary>
        ///   True for a letter reason whose motivating intention can LAPSE before the letter is delivered:
        ///   the sender may change their mind between writing and delivery (a spouse reconciles and no longer
        ///   wants a divorce; a lord makes peace and no longer threatens war). Such a letter must be
        ///   re-validated against the sender's CURRENT intention at delivery time, because a letter must never
        ///   outlive the intent that wrote it (an autonomous letter that no longer reflects its sender only
        ///   confuses the player). Factual announcements (a birth, a victory, a farewell, a reply) carry no
        ///   lapsing intention and are never dropped on this basis.
        /// </summary>
        public static bool IsIntentionBearing(LetterReason reason)
        {
            switch (reason)
            {
                case LetterReason.SpouseDivorceDemand:
                case LetterReason.SpouseDivorceEscalation:
                case LetterReason.StanceHostility:
                case LetterReason.ReinforcementRequest:
                case LetterReason.MarriageProposal:
                case LetterReason.StanceFavor:
                case LetterReason.JealousThreat:
                case LetterReason.CorruptionAttempt:
                case LetterReason.Blackmail:
                case LetterReason.WarSummons:
                case LetterReason.WarLevyDemand:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        ///   The staleness gate: true when a letter should be DROPPED (destroyed, never delivered) because its
        ///   sender's motivating intention no longer holds. Only an intention-bearing reason
        ///   (<see cref="IsIntentionBearing" />) can go stale; a reason that carries no lapsing intention is
        ///   never dropped here. <paramref name="intentionStillHolds" /> is resolved by the host from live
        ///   game state (is the divorce still wanted, is the war still on, is the match still viable) and
        ///   defaults to true for a reason the host cannot yet assess, so an unknown case is delivered, never
        ///   silently swallowed.
        /// </summary>
        public static bool ShouldDropAsStale(LetterReason reason, bool intentionStillHolds)
        {
            return IsIntentionBearing(reason) && !intentionStillHolds;
        }
    }
}
