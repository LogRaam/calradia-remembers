// Code written by Gabriel Mailhot, 03/09/2026.
// Guards SettlementAudienceSafetyPolicy.IsSafeForPrivateWord, the entry gate CompanionAudienceTopicBehavior
// applies before offering a "private word" popup (and, through the same entry point, the fief-audience pass).
// Player report: the popup fired while entering a hostile/raided settlement, and answering it inside that
// hostile flow closed the conversation and dumped the player into the location's encounter. These tests pin
// that ANY hostility (siege, raid, war) blocks the offer, and that only a genuinely peaceful visit lets it
// through.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class SettlementAudienceSafetyPolicyTests
    {
        // A peaceful visit (neutral or friendly streets, nothing contested) is the whole point of the
        // feature: the offer must pass.
        [Test]
        public void GIVEN_a_peaceful_entry_WHEN_gating_the_audience_THEN_the_offer_may_fire()
        {
            SettlementAudienceSafetyPolicy.IsSafeForPrivateWord(new SettlementAudienceSafetyPolicy.SettlementSafetyFacts {UnderSiege = false, RaidInProgress = false, AtWarWithPlayer = false})
                                          .Should().BeTrue("a town visited in peace is exactly where a private word belongs");
        }

        // A settlement under siege is a war zone: no quiet word while the ladders are up.
        [Test]
        public void GIVEN_a_settlement_under_siege_WHEN_gating_the_audience_THEN_the_offer_is_blocked()
        {
            SettlementAudienceSafetyPolicy.IsSafeForPrivateWord(new SettlementAudienceSafetyPolicy.SettlementSafetyFacts {UnderSiege = true, RaidInProgress = false, AtWarWithPlayer = false})
                                          .Should().BeFalse();
        }

        // The reported case itself: entering a village being raided is a hostile action, not a visit.
        [Test]
        public void GIVEN_a_raid_in_progress_WHEN_gating_the_audience_THEN_the_offer_is_blocked()
        {
            SettlementAudienceSafetyPolicy.IsSafeForPrivateWord(new SettlementAudienceSafetyPolicy.SettlementSafetyFacts {UnderSiege = false, RaidInProgress = true, AtWarWithPlayer = false})
                                          .Should().BeFalse();
        }

        // Enemy territory at war (the hostile village / enemy castle of the report): even walked into
        // peacefully (a sneak-in, a tolerated entry), it is no place for a companion to pull the player aside.
        [Test]
        public void GIVEN_enemy_territory_at_war_WHEN_gating_the_audience_THEN_the_offer_is_blocked()
        {
            SettlementAudienceSafetyPolicy.IsSafeForPrivateWord(new SettlementAudienceSafetyPolicy.SettlementSafetyFacts {UnderSiege = false, RaidInProgress = false, AtWarWithPlayer = true})
                                          .Should().BeFalse();
        }

        // Every combination carrying at least one hostility is blocked — no AND-shaped hole where two
        // hostilities would cancel out.
        [Test]
        public void GIVEN_any_combination_of_hostilities_WHEN_gating_the_audience_THEN_the_offer_is_blocked()
        {
            SettlementAudienceSafetyPolicy.IsSafeForPrivateWord(new SettlementAudienceSafetyPolicy.SettlementSafetyFacts {UnderSiege = true, RaidInProgress = true, AtWarWithPlayer = false}).Should().BeFalse();
            SettlementAudienceSafetyPolicy.IsSafeForPrivateWord(new SettlementAudienceSafetyPolicy.SettlementSafetyFacts {UnderSiege = true, RaidInProgress = false, AtWarWithPlayer = true}).Should().BeFalse();
            SettlementAudienceSafetyPolicy.IsSafeForPrivateWord(new SettlementAudienceSafetyPolicy.SettlementSafetyFacts {UnderSiege = false, RaidInProgress = true, AtWarWithPlayer = true}).Should().BeFalse();
            SettlementAudienceSafetyPolicy.IsSafeForPrivateWord(new SettlementAudienceSafetyPolicy.SettlementSafetyFacts {UnderSiege = true, RaidInProgress = true, AtWarWithPlayer = true}).Should().BeFalse();
        }
    }
}
