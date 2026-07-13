// Code written by Gabriel Mailhot, 12/07/2026.
// Documents the per-reason "does the letter's intention still hold at delivery?" rules. A letter must never
// outlive the intent that wrote it: a reconciled spouse's divorce letter, a lord's war letter after peace, a
// marriage proposal to a now-married player, and so on, are dropped. The live world is a stub here so each
// rule reads as a plain statement of the signal it depends on.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterIntentionPolicyTests
    {
        private const string SenderId = "npc_sender";

        private static bool Holds(LetterReason reason, StubWorld world)
            => LetterIntentionPolicy.StillHolds(reason, SenderId, world);

        // ── Divorce ─────────────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_divorce_demand_WHEN_the_spouse_no_longer_demands_it_THEN_the_intention_does_not_hold()
        {
            Holds(LetterReason.SpouseDivorceDemand, new StubWorld {DivorceDemand = true}).Should().BeTrue();
            Holds(LetterReason.SpouseDivorceDemand, new StubWorld {DivorceDemand = false}).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_divorce_escalation_WHEN_the_spouse_is_no_longer_estranging_THEN_the_intention_does_not_hold()
        {
            Holds(LetterReason.SpouseDivorceEscalation, new StubWorld {Estranging = true}).Should().BeTrue();
            Holds(LetterReason.SpouseDivorceEscalation, new StubWorld {Estranging = false}).Should().BeFalse();
        }

        // ── War / stance ────────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_reinforcement_request_WHEN_the_sender_made_peace_THEN_the_intention_does_not_hold()
        {
            Holds(LetterReason.ReinforcementRequest, new StubWorld {AtWar = true}).Should().BeTrue();
            Holds(LetterReason.ReinforcementRequest, new StubWorld {AtWar = false}).Should().BeFalse();
        }

        [Test]
        public void GIVEN_an_open_enmity_letter_WHEN_the_sender_is_no_longer_hostile_THEN_the_intention_does_not_hold()
        {
            Holds(LetterReason.StanceHostility, new StubWorld {StanceHostile = true}).Should().BeTrue();
            Holds(LetterReason.StanceHostility, new StubWorld {StanceHostile = false}).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_friendship_letter_WHEN_the_sender_soured_THEN_the_intention_does_not_hold()
        {
            Holds(LetterReason.StanceFavor, new StubWorld {StanceFriendly = true}).Should().BeTrue();
            Holds(LetterReason.StanceFavor, new StubWorld {StanceFriendly = false}).Should().BeFalse();
        }

        // ── Marriage / jealousy / corruption / blackmail (2nd lot) ──────────

        [Test]
        public void GIVEN_a_marriage_proposal_WHEN_the_player_is_now_married_THEN_the_intention_does_not_hold()
        {
            Holds(LetterReason.MarriageProposal, new StubWorld {PlayerIsMarried = false}).Should().BeTrue();
            Holds(LetterReason.MarriageProposal, new StubWorld {PlayerIsMarried = true}).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_jealous_threat_WHEN_no_live_jealousy_grudge_remains_THEN_the_intention_does_not_hold()
        {
            Holds(LetterReason.JealousThreat, new StubWorld {JealousyGrudge = true}).Should().BeTrue();
            Holds(LetterReason.JealousThreat, new StubWorld {JealousyGrudge = false}).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_corruption_attempt_WHEN_the_sender_is_no_longer_adverse_THEN_the_intention_does_not_hold()
        {
            Holds(LetterReason.CorruptionAttempt, new StubWorld {Adverse = true}).Should().BeTrue();
            Holds(LetterReason.CorruptionAttempt, new StubWorld {Adverse = false}).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_blackmail_WHEN_either_the_spouse_or_the_secret_lover_is_gone_THEN_the_intention_does_not_hold()
        {
            Holds(LetterReason.Blackmail, new StubWorld {PlayerIsMarried = true, PlayerHasSecretLover = true})
                .Should().BeTrue(); // both present: the leverage stands
            Holds(LetterReason.Blackmail, new StubWorld {PlayerIsMarried = false, PlayerHasSecretLover = true})
                .Should().BeFalse(); // no spouse to hide it from
            Holds(LetterReason.Blackmail, new StubWorld {PlayerIsMarried = true, PlayerHasSecretLover = false})
                .Should().BeFalse(); // no affair left to expose
        }

        // ── Safety ──────────────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_reason_that_carries_no_lapsing_intent_WHEN_asked_THEN_it_always_holds()
        {
            Holds(LetterReason.BirthAnnouncement, new StubWorld()).Should().BeTrue();
            Holds(LetterReason.TournamentVictory, new StubWorld()).Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_null_world_WHEN_asked_THEN_it_holds_so_nothing_is_swallowed()
        {
            LetterIntentionPolicy.StillHolds(LetterReason.MarriageProposal, SenderId, null).Should().BeTrue();
        }

        private sealed class StubWorld : ILetterIntentionWorld
        {
            public bool PlayerIsMarried { get; set; }
            public bool PlayerHasSecretLover { get; set; }
            public bool DivorceDemand { get; set; }
            public bool Estranging { get; set; }
            public bool AtWar { get; set; }
            public bool StanceHostile { get; set; }
            public bool StanceFriendly { get; set; }
            public bool JealousyGrudge { get; set; }
            public bool Adverse { get; set; }

            public bool SpouseStillDemandsDivorce(string senderId) => DivorceDemand;
            public bool SpouseStillEstranging(string senderId) => Estranging;
            public bool SenderStillAtWarWithPlayer(string senderId) => AtWar;
            public bool SenderStanceStillHostile(string senderId) => StanceHostile;
            public bool SenderStanceStillFriendly(string senderId) => StanceFriendly;
            public bool SenderHasLiveJealousyGrudge(string senderId) => JealousyGrudge;
            public bool SenderStillAdverseToPlayer(string senderId) => Adverse;
        }
    }
}
