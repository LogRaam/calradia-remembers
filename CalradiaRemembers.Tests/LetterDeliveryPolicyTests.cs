// Code written by Gabriel Mailhot, 11/07/2026.
// Documents the two delivery concerns kept separate: divorce letters always reach the player regardless of
// proximity, while loneliness/longing letters defer only while their sender stands beside the player.
//
// A third concern lives here too: IsIntentionBearing/ShouldDropAsStale mark which letter reasons carry a
// motivating intention that can lapse before delivery (a spouse's divorce demand, a lord's war threat) versus
// a plain factual announcement (a birth, a victory) that can never go stale. Get IsIntentionBearing wrong
// either way and the staleness gate misfires: a factual letter gets silently swallowed, or a genuinely stale
// demand or threat still reaches the player after its sender changed course, the "reconciled spouse's divorce
// letter still arrives days later" bug this whole gate exists to prevent.

#region

using CalradiaRemembers.Logic.Letters;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterDeliveryPolicyTests
    {
        // The player must never be blindsided by an unfolding divorce (CHANGELOG v1.30.8): this reason must
        // bypass proximity deferral entirely, reaching him even while its sender stands right beside him.
        [Test]
        public void GIVEN_the_spouse_divorce_demand_reason_WHEN_checking_guaranteed_delivery_THEN_it_is_true()
        {
            LetterDeliveryPolicy.IsGuaranteedDelivery(LetterReason.SpouseDivorceDemand).Should().BeTrue();
        }

        // The escalation carries the same forced-delivery guarantee as the initial demand: an unanswered
        // ultimatum must still land on schedule, or standing near the sender would become a loophole that
        // silently pauses the divorce clock.
        [Test]
        public void GIVEN_the_spouse_divorce_escalation_reason_WHEN_checking_guaranteed_delivery_THEN_it_is_true()
        {
            LetterDeliveryPolicy.IsGuaranteedDelivery(LetterReason.SpouseDivorceEscalation).Should().BeTrue();
        }

        // Only the two divorce reasons are exempt from proximity; a longing letter has no such urgency and must
        // still fall through to the defer-when-near check, never skip it via this gate.
        [Test]
        public void GIVEN_a_romantic_correspondence_reason_WHEN_checking_guaranteed_delivery_THEN_it_is_false()
        {
            LetterDeliveryPolicy.IsGuaranteedDelivery(LetterReason.RomanticCorrespondence).Should().BeFalse();
        }

        // Pins the boundary of IsGuaranteedDelivery to exactly the divorce pair: an everyday spouse letter must
        // not silently join the guaranteed set, or it would force-deliver into someone's own tent.
        [Test]
        public void GIVEN_a_spouse_correspondence_reason_WHEN_checking_guaranteed_delivery_THEN_it_is_false()
        {
            LetterDeliveryPolicy.IsGuaranteedDelivery(LetterReason.SpouseCorrespondence).Should().BeFalse();
        }

        // A birth carries no urgency that would justify forcing it through beside its own mother; guaranteed
        // delivery stays reserved for the divorce pair alone.
        [Test]
        public void GIVEN_a_birth_announcement_reason_WHEN_checking_guaranteed_delivery_THEN_it_is_false()
        {
            LetterDeliveryPolicy.IsGuaranteedDelivery(LetterReason.BirthAnnouncement).Should().BeFalse();
        }

        // Player report (CHANGELOG v1.30.0): a lady invited into the player's own army kept sending "I miss
        // you" letters. This reason is exactly the kind that must wait until sender and player are truly apart.
        [Test]
        public void GIVEN_a_romantic_correspondence_reason_WHEN_checking_defer_when_near_THEN_it_is_true()
        {
            LetterDeliveryPolicy.ShouldDeferWhenNear(LetterReason.RomanticCorrespondence).Should().BeTrue();
        }

        // Same family as romantic correspondence: a spouse's own everyday letter reads just as wrong arriving
        // while she rides at the player's side, so it must defer too.
        [Test]
        public void GIVEN_a_spouse_correspondence_reason_WHEN_checking_defer_when_near_THEN_it_is_true()
        {
            LetterDeliveryPolicy.ShouldDeferWhenNear(LetterReason.SpouseCorrespondence).Should().BeTrue();
        }

        // A pending reply reads just as wrong delivered face to face as a longing letter would, so it belongs
        // in the same deferred set rather than forcing through on its own separate rule.
        [Test]
        public void GIVEN_an_awaiting_reply_reason_WHEN_checking_defer_when_near_THEN_it_is_true()
        {
            LetterDeliveryPolicy.ShouldDeferWhenNear(LetterReason.AwaitingReply).Should().BeTrue();
        }

        // Pairs with the guaranteed-delivery tests above: a divorce demand could otherwise look like it belongs
        // on the personal-letter deferred list, but it must be exempt so proximity can never mask an unfolding
        // divorce from the player.
        [Test]
        public void GIVEN_a_spouse_divorce_demand_reason_WHEN_checking_defer_when_near_THEN_it_is_false()
        {
            LetterDeliveryPolicy.ShouldDeferWhenNear(LetterReason.SpouseDivorceDemand).Should().BeFalse();
        }

        // A factual announcement has no proximity awkwardness to avoid; only the loneliness/longing family
        // (ShouldDeferWhenNear) waits for distance, everything else must deliver on its normal schedule.
        [Test]
        public void GIVEN_a_tournament_victory_reason_WHEN_checking_defer_when_near_THEN_it_is_false()
        {
            LetterDeliveryPolicy.ShouldDeferWhenNear(LetterReason.TournamentVictory).Should().BeFalse();
        }

        // ── The sea gate: never deliver while the player sails ───────────────────────────────────────

        // Courier audit (island report): no courier can meet a ship, so while the player is at sea every
        // delivery — courier or date fallback alike — must be held back. Missing this guard is what let
        // letters "deliver" in the middle of a crossing.
        [Test]
        public void GIVEN_the_player_is_at_sea_WHEN_checking_the_sea_gate_THEN_delivery_is_held_back()
        {
            LetterDeliveryPolicy.ShouldHoldDeliveryAtSea(true).Should().BeTrue();
        }

        // The other half of the rule: once the player is ashore the gate must release immediately, or a due
        // letter would stay stuck after landfall instead of landing on the first daily tick.
        [Test]
        public void GIVEN_the_player_is_ashore_WHEN_checking_the_sea_gate_THEN_delivery_is_not_held_back()
        {
            LetterDeliveryPolicy.ShouldHoldDeliveryAtSea(false).Should().BeFalse();
        }

        // A birth is likewise a factual announcement, not a longing note, so it must never be held back for
        // proximity even though BirthLetterPolicy separately decides not to send it at all when she is present.
        [Test]
        public void GIVEN_a_birth_announcement_reason_WHEN_checking_defer_when_near_THEN_it_is_false()
        {
            LetterDeliveryPolicy.ShouldDeferWhenNear(LetterReason.BirthAnnouncement).Should().BeFalse();
        }

        // The end-to-end combination: nearness alone or reason alone is not enough, ShouldDeferDelivery must
        // require BOTH before holding a letter back.
        [Test]
        public void GIVEN_the_sender_is_near_WHEN_the_reason_is_romantic_correspondence_THEN_delivery_is_deferred()
        {
            LetterDeliveryPolicy.ShouldDeferDelivery(LetterReason.RomanticCorrespondence, true).Should().BeTrue();
        }

        // The case that must never regress: near a spouse mid-divorce, the demand still goes through, because
        // IsGuaranteedDelivery overrides ShouldDeferWhenNear even when both lists would otherwise apply.
        [Test]
        public void GIVEN_the_sender_is_near_WHEN_the_reason_is_spouse_divorce_demand_THEN_delivery_is_not_deferred()
        {
            LetterDeliveryPolicy.ShouldDeferDelivery(LetterReason.SpouseDivorceDemand, true).Should().BeFalse();
        }

        // Once truly apart, the very same reason must release immediately, or a queued longing letter would
        // wait forever the first time it happened to be written while the sender was still nearby.
        [Test]
        public void GIVEN_the_sender_is_far_WHEN_the_reason_is_romantic_correspondence_THEN_delivery_is_not_deferred()
        {
            LetterDeliveryPolicy.ShouldDeferDelivery(LetterReason.RomanticCorrespondence, false).Should().BeFalse();
        }

        // Guards the third bucket: a reason that is neither guaranteed nor deferrable must fall through
        // ShouldDeferDelivery untouched, not get caught by either rule by accident.
        [Test]
        public void GIVEN_the_sender_is_near_WHEN_the_reason_is_birth_announcement_THEN_delivery_is_not_deferred()
        {
            LetterDeliveryPolicy.ShouldDeferDelivery(LetterReason.BirthAnnouncement, true).Should().BeFalse();
        }

        // ── Intention re-validation: a letter must never outlive the intent that wrote it ────────────

        // Pins the full intention-bearing set (the 2nd-lot letters: marriage/jealousy/corruption/blackmail,
        // CHANGELOG v1.30.12 "letters 2nd-lot resolvers"). Missing one of these from the switch means
        // ShouldDropAsStale can never re-validate it, so a stale letter of that reason would always deliver.
        [TestCase(LetterReason.SpouseDivorceDemand)]
        [TestCase(LetterReason.SpouseDivorceEscalation)]
        [TestCase(LetterReason.StanceHostility)]
        [TestCase(LetterReason.ReinforcementRequest)]
        [TestCase(LetterReason.VillagesSpared)]
        [TestCase(LetterReason.MarriageProposal)]
        [TestCase(LetterReason.StanceFavor)]
        [TestCase(LetterReason.JealousThreat)]
        [TestCase(LetterReason.CorruptionAttempt)]
        [TestCase(LetterReason.Blackmail)]
        public void GIVEN_a_reason_whose_intention_can_lapse_WHEN_checking_intention_bearing_THEN_it_is_true(LetterReason reason)
        {
            LetterDeliveryPolicy.IsIntentionBearing(reason).Should().BeTrue();
        }

        // The other side of that same boundary: a factual reason wrongly added here would make ShouldDropAsStale
        // eligible to drop it, and a mis-reported world signal could then silently swallow a real event (a birth
        // that truly happened) instead of always delivering it.
        [TestCase(LetterReason.BirthAnnouncement)]
        [TestCase(LetterReason.TournamentVictory)]
        [TestCase(LetterReason.CompanionFarewell)]
        [TestCase(LetterReason.PlayerLetterReply)]
        [TestCase(LetterReason.SpouseCorrespondence)]
        [TestCase(LetterReason.QuestUpdate)]
        public void GIVEN_a_factual_or_proximity_only_reason_WHEN_checking_intention_bearing_THEN_it_is_false(LetterReason reason)
        {
            LetterDeliveryPolicy.IsIntentionBearing(reason).Should().BeFalse();
        }

        // The core bug this gate exists to close (CHANGELOG v1.30.11 "Letters no longer outlive the intent that
        // wrote them"): without this drop, a reconciled spouse's divorce letter still arrives days later.
        [Test]
        public void GIVEN_a_divorce_demand_whose_intention_has_lapsed_WHEN_checking_stale_THEN_it_is_dropped()
        {
            // The spouse reconciled and no longer wants a divorce: the queued demand must never be delivered.
            LetterDeliveryPolicy.ShouldDropAsStale(LetterReason.SpouseDivorceDemand, intentionStillHolds: false)
                .Should().BeTrue();
        }

        // Same staleness principle applied outside marriage: a war-reinforcement letter that arrives after its
        // sender has already made peace would misrepresent the current relationship to the player.
        [Test]
        public void GIVEN_a_war_threat_whose_sender_made_peace_WHEN_checking_stale_THEN_it_is_dropped()
        {
            LetterDeliveryPolicy.ShouldDropAsStale(LetterReason.StanceHostility, intentionStillHolds: false)
                .Should().BeTrue();
        }

        // Guards against a gate that drops everything intention-bearing indiscriminately: a demand the spouse
        // still holds to must be delivered, or a genuine divorce could be silently erased instead of announced.
        [Test]
        public void GIVEN_a_divorce_demand_whose_intention_still_holds_WHEN_checking_stale_THEN_it_is_kept()
        {
            LetterDeliveryPolicy.ShouldDropAsStale(LetterReason.SpouseDivorceDemand, intentionStillHolds: true)
                .Should().BeFalse();
        }

        // A second guard on top of the IsIntentionBearing set: even if the host mis-reports a stale signal for
        // a factual reason, the class-level exemption keeps a real event from ever being silently dropped.
        [Test]
        public void GIVEN_a_factual_announcement_WHEN_its_intention_is_reported_gone_THEN_it_is_never_dropped_as_stale()
        {
            // A birth happened; there is no intention to lapse, so this gate must never swallow it.
            LetterDeliveryPolicy.ShouldDropAsStale(LetterReason.BirthAnnouncement, intentionStillHolds: false)
                .Should().BeFalse();
        }
    }
}
