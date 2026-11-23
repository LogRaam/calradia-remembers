// Code written by Gabriel Mailhot, 12/07/2026.
// A letter must never outlive the intent that wrote it. LetterDeliveryPolicy decides WHICH reasons can go
// stale; this decides, per reason, WHETHER the intention still holds, reading the live world only through the
// injected ILetterIntentionWorld seam so the per-reason keep/drop rules stay pure and unit-tested (the mod
// supplies the real campaign reads; a test supplies a stub).

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Letters
{
    /// <summary>
    ///   The live-world signals the per-reason staleness decision needs, keyed by the letter's sender id.
    ///   Implemented in the mod against the campaign; stubbed in tests. Each member is read lazily, so only
    ///   the one signal a given reason cares about is ever computed.
    /// </summary>
    public interface ILetterIntentionWorld
    {
        /// <summary>The player currently has a spouse.</summary>
        bool PlayerIsMarried { get; }

        /// <summary>The player currently keeps a secret lover.</summary>
        bool PlayerHasSecretLover { get; }

        bool SpouseStillDemandsDivorce(string senderId);
        bool SpouseStillEstranging(string senderId);
        bool PleaStillStands(string senderId);

        /// <summary>The sender still spares the player's villages: not broken, and the war not over.</summary>
        bool RestraintStillStands(string senderId);

        bool SenderStanceStillHostile(string senderId);
        bool SenderStanceStillFriendly(string senderId);
        bool SenderHasLiveJealousyGrudge(string senderId);
        bool SenderStillAdverseToPlayer(string senderId);

        /// <summary>The sender's war-duty reckoning with the player is still open (not settled, dropped or closed).</summary>
        bool WarDutyStillOpen(string senderId);

        /// <summary>The sender's love still stands as it did when they wrote (LoveConfessionLetterPolicy.IsStillHeartfelt).</summary>
        bool ConfessionStillHeartfelt(string senderId);
    }

    /// <summary>
    ///   Pure per-reason decision: does the intention that motivated this letter still hold at delivery time?
    ///   False means the letter is now stale and should be dropped (LetterDeliveryPolicy.ShouldDropAsStale
    ///   gates that on the reason being intention-bearing in the first place). A reason with no lapsing intent,
    ///   or a null world, holds by default, so nothing is ever swallowed by accident.
    /// </summary>
    public static class LetterIntentionPolicy
    {
        public static bool StillHolds(LetterReason reason, string senderId, ILetterIntentionWorld world)
        {
            if (world == null) return true;

            switch (reason)
            {
                // A spouse who reconciled no longer wants the divorce their letter announced.
                case LetterReason.SpouseDivorceDemand:     return world.SpouseStillDemandsDivorce(senderId);
                case LetterReason.SpouseDivorceEscalation: return world.SpouseStillEstranging(senderId);

                // A call for help is stale once its crisis closed on the road (DepaHN, 27/10/2026). Nothing sent this
                // reason before 01/11/2026, when its rule read "still at war with the player": the opposite of a friend.
                case LetterReason.ReinforcementRequest:    return world.PleaStillStands(senderId);

                // The text is written at opening: a resolve that broke on the road must not be promised after the fact.
                case LetterReason.VillagesSpared:          return world.RestraintStillStands(senderId);

                // An open-enmity / friendship letter is stale once the sender's posture flipped the other way.
                case LetterReason.StanceHostility:         return world.SenderStanceStillHostile(senderId);
                case LetterReason.StanceFavor:             return world.SenderStanceStillFriendly(senderId);

                // A proposal to match a child to the player is moot once the player marries.
                case LetterReason.MarriageProposal:        return !world.PlayerIsMarried;

                // A jealous threat stands only while its sender still holds a live jealousy grudge.
                case LetterReason.JealousThreat:           return world.SenderHasLiveJealousyGrudge(senderId);

                // An enemy's bribe or pressure loses its point once they are no longer adverse to the player.
                case LetterReason.CorruptionAttempt:       return world.SenderStillAdverseToPlayer(senderId);

                // Blackmail's leverage is the hidden affair: gone once there is no spouse to hide it from, or
                // no secret lover left to expose.
                case LetterReason.Blackmail:               return world.PlayerIsMarried && world.PlayerHasSecretLover;

                // A summons or a levy demand is stale once the reckoning closed in transit: he served, paid, or
                // the war ended (the vassal war duty, 15/10/2026).
                case LetterReason.WarSummons:
                case LetterReason.WarLevyDemand:           return world.WarDutyStillOpen(senderId);

                // A love letter is stale once the feeling fell below what may be said, or something began or ended
                // between them on the road: "I have never told you" must not arrive after they did.
                case LetterReason.LoveConfession:          return world.ConfessionStillHeartfelt(senderId);
                default:                                   return true;
            }
        }
    }
}
