// Code written by Gabriel Mailhot, 30/08/2026.
// PrisonerCustodyPolicy decides which prisons are the player's and merges the captives they hold, deduped. It
// backs BuyPrisonerVerb.PlayerHeldHeroPrisoners, which gates three surfaces at once: the in-conversation
// buy_prisoner offer, the ransom-letter trigger, and that letter's Accept button. Player report: a ransom offer
// arrived with only a Close button for a lord who had NOT escaped (still behind bars in the player's own city),
// because the old enumeration saw only the travelling party. These tests pin the fix: a settlement the player's
// clan owns counts as the player's custody, a prison it does not own never does, and a captive is counted once.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Prisoners;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Prisoners
{
    [TestFixture]
    public sealed class PrisonerCustodyPolicyTests
    {
        private static PrisonHolding MainParty(params string[] ids)
            => new PrisonHolding(isPlayerMainParty: true, isSettlementOwnedByPlayerClan: false, ids);

        private static PrisonHolding OwnedSettlement(params string[] ids)
            => new PrisonHolding(isPlayerMainParty: false, isSettlementOwnedByPlayerClan: true, ids);

        private static PrisonHolding ForeignSettlement(params string[] ids)
            => new PrisonHolding(isPlayerMainParty: false, isSettlementOwnedByPlayerClan: false, ids);

        // STAKE: the rule itself. If a player-owned settlement dungeon does not read as the player's custody, the
        // ransom-letter Accept button stays hidden for a captive the player plainly holds (the exact report).
        [Test]
        public void GIVEN_a_settlement_owned_by_the_players_clan_WHEN_checked_THEN_it_is_the_players_custody()
        {
            PrisonerCustodyPolicy.IsPlayerCustody(isPlayerMainParty: false, isSettlementOwnedByPlayerClan: true)
                .Should().BeTrue();
        }

        // STAKE: the main party has always counted and must keep counting; regressing this would drop every
        // travelling captive out of every sale/ransom path.
        [Test]
        public void GIVEN_the_main_party_WHEN_checked_THEN_it_is_the_players_custody()
        {
            PrisonerCustodyPolicy.IsPlayerCustody(isPlayerMainParty: true, isSettlementOwnedByPlayerClan: false)
                .Should().BeTrue();
        }

        // STAKE: the boundary. A prison the player does not own (a captive is not theirs to sell from another
        // lord's dungeon) must never count, or the offer would surface prisoners the player cannot deliver.
        [Test]
        public void GIVEN_a_prison_that_is_neither_the_main_party_nor_an_owned_settlement_WHEN_checked_THEN_it_is_not_custody()
        {
            PrisonerCustodyPolicy.IsPlayerCustody(isPlayerMainParty: false, isSettlementOwnedByPlayerClan: false)
                .Should().BeFalse();
        }

        // STAKE: the headline case, end to end. A captive sitting in the player's own town dungeon must appear in
        // the held roster alongside the party's captives.
        [Test]
        public void GIVEN_captives_in_the_party_and_an_owned_town_WHEN_merged_THEN_both_are_held()
        {
            var prisons = new List<PrisonHolding> {MainParty("lord_a"), OwnedSettlement("lord_b")};

            PrisonerCustodyPolicy.HeldByPlayer(prisons).Should().Equal("lord_a", "lord_b");
        }

        // STAKE: a captive left in a settlement NOT owned by the player must be excluded from the merged roster,
        // so an offer is never made for someone the player cannot hand over.
        [Test]
        public void GIVEN_a_captive_in_a_foreign_settlement_WHEN_merged_THEN_it_is_excluded()
        {
            var prisons = new List<PrisonHolding> {MainParty("lord_a"), ForeignSettlement("lord_x")};

            PrisonerCustodyPolicy.HeldByPlayer(prisons).Should().Equal("lord_a");
        }

        // STAKE: the same captive can momentarily appear in two rosters during an engine hand-off; counting them
        // twice would double a captive in the sale menu and could double-charge or double-transfer on accept.
        [Test]
        public void GIVEN_the_same_captive_id_in_two_owned_prisons_WHEN_merged_THEN_it_appears_once_in_first_seen_order()
        {
            var prisons = new List<PrisonHolding> {MainParty("lord_a", "lord_b"), OwnedSettlement("lord_b", "lord_c")};

            PrisonerCustodyPolicy.HeldByPlayer(prisons).Should().Equal("lord_a", "lord_b", "lord_c");
        }

        // STAKE: a roster can hand back a blank/absent id for a stripped element; a blank must be dropped rather
        // than pollute the held roster with an empty entry that resolves to no hero.
        [Test]
        public void GIVEN_blank_and_null_ids_WHEN_merged_THEN_they_are_dropped()
        {
            var prisons = new List<PrisonHolding> {MainParty("lord_a", "", null, "   ", "lord_b")};

            PrisonerCustodyPolicy.HeldByPlayer(prisons).Should().Equal("lord_a", "lord_b");
        }

        // STAKE: the method is called on every conversation turn and every letter open; a null or empty input must
        // degrade to an empty list, never throw.
        [Test]
        public void GIVEN_null_or_empty_input_WHEN_merged_THEN_it_is_empty_and_does_not_throw()
        {
            PrisonerCustodyPolicy.HeldByPlayer(null).Should().BeEmpty();
            PrisonerCustodyPolicy.HeldByPlayer(new List<PrisonHolding>()).Should().BeEmpty();
            PrisonerCustodyPolicy.HeldByPlayer(new List<PrisonHolding> {OwnedSettlement((string[]) null)}).Should().BeEmpty();
        }
    }
}
