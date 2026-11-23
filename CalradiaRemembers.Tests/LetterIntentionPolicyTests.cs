// Code written by Gabriel Mailhot, 12/07/2026.
// Documents the per-reason "does the letter's intention still hold at delivery?" rules. A letter must never
// outlive the intent that wrote it: a reconciled spouse's divorce letter, a lord's war letter after peace, a
// marriage proposal to a now-married player, and so on, are dropped. The live world is a stub here so each
// rule reads as a plain statement of the signal it depends on.

#region

using CalradiaRemembers.Logic.Letters;
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

        // The reconciliation case (CHANGELOG v1.30.11): a spouse who mended things must have her stale demand
        // drop, or an already-resolved divorce would still be announced by courier days later.
        [Test]
        public void GIVEN_a_divorce_demand_WHEN_the_spouse_no_longer_demands_it_THEN_the_intention_does_not_hold()
        {
            Holds(LetterReason.SpouseDivorceDemand, new StubWorld {DivorceDemand = true}).Should().BeTrue();
            Holds(LetterReason.SpouseDivorceDemand, new StubWorld {DivorceDemand = false}).Should().BeFalse();
        }

        // The escalation letter carries the same reconciliation check as the initial demand: an ultimatum must
        // not still land once the estrangement itself has been resolved.
        [Test]
        public void GIVEN_a_divorce_escalation_WHEN_the_spouse_is_no_longer_estranging_THEN_the_intention_does_not_hold()
        {
            Holds(LetterReason.SpouseDivorceEscalation, new StubWorld {Estranging = true}).Should().BeTrue();
            Holds(LetterReason.SpouseDivorceEscalation, new StubWorld {Estranging = false}).Should().BeFalse();
        }

        // ── War / stance ────────────────────────────────────────────────────

        // A call for help's whole premise is the crisis (DepaHN, 27/10/2026): one that closed while the letter rode must
        // not arrive still begging. Nothing sent this reason before 01/11/2026; its old rule, "still at war with the
        // player", would have dropped every plea from a friend, and this test used to pin that.
        [Test]
        public void GIVEN_a_call_for_help_WHEN_its_crisis_closed_on_the_road_THEN_the_letter_does_not_arrive()
        {
            Holds(LetterReason.ReinforcementRequest, new StubWorld {PleaStands = true}).Should().BeTrue();
            Holds(LetterReason.ReinforcementRequest, new StubWorld {PleaStands = false}).Should().BeFalse();
        }

        // A letter's text is written at opening: a lord who broke his resolve, or a war that ended, on the road, would
        // otherwise promise the player what is no longer true, and NPCs never lie.
        [Test]
        public void GIVEN_a_promise_to_spare_villages_WHEN_it_broke_on_the_road_THEN_the_letter_does_not_arrive()
        {
            Holds(LetterReason.VillagesSpared, new StubWorld {RestraintStands = true}).Should().BeTrue();
            Holds(LetterReason.VillagesSpared, new StubWorld {RestraintStands = false}).Should().BeFalse();
        }

        // A hostility letter announces an open threat; if the sender's posture has already cooled, delivering it
        // anyway would read as a threat that was never withdrawn.
        [Test]
        public void GIVEN_an_open_enmity_letter_WHEN_the_sender_is_no_longer_hostile_THEN_the_intention_does_not_hold()
        {
            Holds(LetterReason.StanceHostility, new StubWorld {StanceHostile = true}).Should().BeTrue();
            Holds(LetterReason.StanceHostility, new StubWorld {StanceHostile = false}).Should().BeFalse();
        }

        // The mirror case: a warm-favor letter must not arrive once the sender has since soured on the player,
        // or a friendly overture would misrepresent a bond that no longer exists.
        [Test]
        public void GIVEN_a_friendship_letter_WHEN_the_sender_soured_THEN_the_intention_does_not_hold()
        {
            Holds(LetterReason.StanceFavor, new StubWorld {StanceFriendly = true}).Should().BeTrue();
            Holds(LetterReason.StanceFavor, new StubWorld {StanceFriendly = false}).Should().BeFalse();
        }

        // ── Marriage / jealousy / corruption / blackmail (2nd lot) ──────────
        // CHANGELOG v1.30.12: "letters 2nd-lot resolvers (marriage/jealousy/corruption/blackmail drop stale)".

        // Proposing to match a child to the player is moot the instant he marries someone else; an unanswered
        // proposal arriving after the fact would read as ignorant of news the player already knows.
        [Test]
        public void GIVEN_a_marriage_proposal_WHEN_the_player_is_now_married_THEN_the_intention_does_not_hold()
        {
            Holds(LetterReason.MarriageProposal, new StubWorld {PlayerIsMarried = false}).Should().BeTrue();
            Holds(LetterReason.MarriageProposal, new StubWorld {PlayerIsMarried = true}).Should().BeFalse();
        }

        // A jealous threat stands only while its grudge is still live; once resolved (JealousyBehavior clears
        // it), the threat has no basis left and must not still arrive as if the grievance were current.
        [Test]
        public void GIVEN_a_jealous_threat_WHEN_no_live_jealousy_grudge_remains_THEN_the_intention_does_not_hold()
        {
            Holds(LetterReason.JealousThreat, new StubWorld {JealousyGrudge = true}).Should().BeTrue();
            Holds(LetterReason.JealousThreat, new StubWorld {JealousyGrudge = false}).Should().BeFalse();
        }

        // A bribe or pressure letter only makes sense from someone still working against the player; once that
        // adversity is gone, the coercion attempt has lost its point and must not still land.
        [Test]
        public void GIVEN_a_corruption_attempt_WHEN_the_sender_is_no_longer_adverse_THEN_the_intention_does_not_hold()
        {
            Holds(LetterReason.CorruptionAttempt, new StubWorld {Adverse = true}).Should().BeTrue();
            Holds(LetterReason.CorruptionAttempt, new StubWorld {Adverse = false}).Should().BeFalse();
        }

        // Blackmail's leverage IS the hidden affair (production doc): it takes both a spouse to hide it from
        // AND a secret lover to expose, so losing either one alone must already drop the threat, covering both
        // ways the leverage can vanish rather than just the obvious one.
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

        // A factual reason has no per-reason case in the switch and must fall to the default true: it must
        // never be readable as stale, since it carries no intention to lapse in the first place.
        [Test]
        public void GIVEN_a_reason_that_carries_no_lapsing_intent_WHEN_asked_THEN_it_always_holds()
        {
            Holds(LetterReason.BirthAnnouncement, new StubWorld()).Should().BeTrue();
            Holds(LetterReason.TournamentVictory, new StubWorld()).Should().BeTrue();
        }

        // Fail safe, never silently lenient in the dangerous direction: if the host cannot yet supply live world
        // state, the letter must be delivered, never dropped, so a wiring gap can only ever over-deliver, not
        // erase a letter the player was owed.
        [Test]
        public void GIVEN_a_null_world_WHEN_asked_THEN_it_holds_so_nothing_is_swallowed()
        {
            LetterIntentionPolicy.StillHolds(LetterReason.MarriageProposal, SenderId, null).Should().BeTrue();
        }

        // STAKE: the vassal war duty (15/10/2026). A summons that reaches a vassal who already rode back to the war, or
        // after the peace, would call him to account for nothing.
        [Test]
        public void GIVEN_a_war_summons_in_transit_WHEN_the_reckoning_closed_before_it_arrived_THEN_it_is_stale()
        {
            LetterIntentionPolicy.StillHolds(LetterReason.WarSummons, SenderId, new StubWorld {WarDutyOpen = false}).Should().BeFalse();
            LetterIntentionPolicy.StillHolds(LetterReason.WarLevyDemand, SenderId, new StubWorld {WarDutyOpen = true}).Should().BeTrue();
        }

        // STAKE: a love letter whose feeling cooled, or whose romance began or ended on the road, is never delivered as
        // "I have never told you" (the love letter, 23/11/2026).
        [Test]
        public void GIVEN_a_love_letter_in_transit_WHEN_the_feeling_no_longer_stands_THEN_it_is_stale()
        {
            LetterIntentionPolicy.StillHolds(LetterReason.LoveConfession, SenderId, new StubWorld {Heartfelt = true}).Should().BeTrue();
            LetterIntentionPolicy.StillHolds(LetterReason.LoveConfession, SenderId, new StubWorld {Heartfelt = false}).Should().BeFalse();
        }

        private sealed class StubWorld : ILetterIntentionWorld
        {
            public bool PlayerIsMarried { get; set; }
            public bool PlayerHasSecretLover { get; set; }
            public bool DivorceDemand { get; set; }
            public bool Estranging { get; set; }
            public bool PleaStands { get; set; }

            public bool RestraintStands { get; set; }
            public bool StanceHostile { get; set; }
            public bool StanceFriendly { get; set; }
            public bool JealousyGrudge { get; set; }
            public bool Adverse { get; set; }

            public bool SpouseStillDemandsDivorce(string senderId) => DivorceDemand;
            public bool SpouseStillEstranging(string senderId) => Estranging;
            public bool PleaStillStands(string senderId) => PleaStands;

            public bool RestraintStillStands(string senderId) => RestraintStands;
            public bool SenderStanceStillHostile(string senderId) => StanceHostile;
            public bool SenderStanceStillFriendly(string senderId) => StanceFriendly;
            public bool SenderHasLiveJealousyGrudge(string senderId) => JealousyGrudge;
            public bool SenderStillAdverseToPlayer(string senderId) => Adverse;
            public bool WarDutyOpen { get; set; }
            public bool WarDutyStillOpen(string senderId) => WarDutyOpen;
            public bool Heartfelt { get; set; }
            public bool ConfessionStillHeartfelt(string senderId) => Heartfelt;
        }
    }
}
