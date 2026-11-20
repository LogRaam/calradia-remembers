// Code written by Gabriel Mailhot, 01/10/2026.
// fkasad (Nexus, 01/10/2026), on a governor's supply task: "Works for garrison reinforcement too." A governor or lord
// whose garrison is thin may ask the player to bring men; handed over in conversation, they go straight onto the
// walls. The strength bands are the ones the seat-standing pack already describes a garrison by, moved into one home
// so what a lord says of his walls and what he asks for them cannot disagree. If these rules are wrong, a lord with a
// full garrison begs for men, a skeleton garrison never asks, a lord asks for more men than his walls can hold, or a
// short delivery completes the task.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Knowledge;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class GarrisonReinforcementTests
    {
        // ── How strong the walls are (the bands the seat pack already speaks in) ──────────────────

        // The same shares the seat pack used before the move: 15% a skeleton, 50% under strength, 85% strong.
        [Test]
        public void GIVEN_healthy_men_against_the_limit_WHEN_banded_THEN_the_seat_packs_own_bands_hold()
        {
            GarrisonStrength.BandOf(healthy: 10, limit: 100).Should().Be(GarrisonBand.Skeleton);
            GarrisonStrength.BandOf(healthy: 30, limit: 100).Should().Be(GarrisonBand.UnderStrength);
            GarrisonStrength.BandOf(healthy: 60, limit: 100).Should().Be(GarrisonBand.Adequate);
            GarrisonStrength.BandOf(healthy: 90, limit: 100).Should().Be(GarrisonBand.Strong);
            GarrisonStrength.BandOf(healthy: 10, limit: 0).Should().Be(GarrisonBand.Unknown);
        }

        // ── When a lord asks ──────────────────────────────────────────────────────────────────────

        // Thin walls ask; walls already manned do not.
        [Test]
        public void GIVEN_the_garrison_band_WHEN_a_lord_considers_asking_THEN_only_a_thin_garrison_asks()
        {
            ReinforceGarrisonPolicy.MayAsk(GarrisonBand.Skeleton).Should().BeTrue();
            ReinforceGarrisonPolicy.MayAsk(GarrisonBand.UnderStrength).Should().BeTrue();
            ReinforceGarrisonPolicy.MayAsk(GarrisonBand.Adequate).Should().BeFalse();
            ReinforceGarrisonPolicy.MayAsk(GarrisonBand.Unknown).Should().BeFalse();
        }

        // A lord never asks for more men than his walls can take, nor a token two, nor a host.
        [Test]
        public void GIVEN_a_proposed_number_WHEN_the_ask_is_set_THEN_it_stays_within_the_band_and_the_room_left()
        {
            ReinforceGarrisonPolicy.TroopsToAsk(proposed: 2, roomLeft: 200).Should().Be(ReinforceGarrisonPolicy.MinTroops);
            ReinforceGarrisonPolicy.TroopsToAsk(proposed: 500, roomLeft: 200).Should().Be(ReinforceGarrisonPolicy.MaxTroops);
            ReinforceGarrisonPolicy.TroopsToAsk(proposed: 40, roomLeft: 25).Should().Be(25);
            ReinforceGarrisonPolicy.TroopsToAsk(proposed: 40, roomLeft: 3).Should().BeNull();
        }

        // ── The hand-over ─────────────────────────────────────────────────────────────────────────

        // All the men asked, or the task stands: the same rule as a supply of goods.
        [Test]
        public void GIVEN_the_players_healthy_men_WHEN_handing_over_THEN_only_the_full_number_counts()
        {
            ReinforceGarrisonPolicy.CanHandOver(playerHealthyRegulars: 30, required: 30).Should().BeTrue();
            ReinforceGarrisonPolicy.CanHandOver(playerHealthyRegulars: 29, required: 30).Should().BeFalse();
            ReinforceGarrisonPolicy.CanHandOver(playerHealthyRegulars: 30, required: 0).Should().BeFalse();
        }

        // ── A donation through the game's own garrison screen (Lordfadooboo, Nexus, 20/11/2026) ──────

        // STAKE: Lordfadooboo left 30 men on the walls of a castle that asked for 20, through the game's garrison
        // screen, and the task never moved; he had to force it with a debug command.
        [Test]
        public void GIVEN_a_task_for_20_men_WHEN_30_fit_men_are_donated_on_the_garrison_screen_THEN_the_task_is_done()
        {
            GarrisonDonationCredit credit = ReinforceGarrisonPolicy.Credit(new GarrisonDonationFacts {Required = 20, FitMenDonated = 30});

            credit.Done.Should().BeTrue();
            credit.StillOwed.Should().Be(0);
        }

        // STAKE: a player who brings the men in two trips (12 now, 8 later) gets the task done, not stuck at the
        // second trip because neither trip alone was enough.
        [Test]
        public void GIVEN_12_men_already_left_WHEN_8_more_are_donated_THEN_the_two_donations_add_up_and_the_task_is_done()
        {
            GarrisonDonationCredit first = ReinforceGarrisonPolicy.Credit(new GarrisonDonationFacts {Required = 20, FitMenDonated = 12});
            first.Done.Should().BeFalse();
            first.StillOwed.Should().Be(8);

            GarrisonDonationCredit second = ReinforceGarrisonPolicy.Credit(new GarrisonDonationFacts {Required = 20, AlreadyDelivered = first.Delivered, FitMenDonated = 8});
            second.Done.Should().BeTrue();
        }

        // STAKE: wounded men and companions are counted out before the facts are made; a donation of only those leaves
        // the task where it was, as the conversation hand-over refuses them too.
        [Test]
        public void GIVEN_no_fit_men_in_the_donation_WHEN_credited_THEN_nothing_is_counted()
        {
            GarrisonDonationCredit credit = ReinforceGarrisonPolicy.Credit(new GarrisonDonationFacts {Required = 20, AlreadyDelivered = 5, FitMenDonated = 0});

            credit.Counted.Should().BeFalse();
            credit.Done.Should().BeFalse();
            credit.StillOwed.Should().Be(15);
        }

        // STAKE: after a donation on the screen, the giver in conversation asks only for the men still owed, never the
        // whole number again.
        [Test]
        public void GIVEN_men_already_delivered_WHEN_the_men_still_owed_are_read_THEN_only_the_rest_is_asked()
        {
            ReinforceGarrisonPolicy.StillOwed(required: 20, delivered: 12).Should().Be(8);
            ReinforceGarrisonPolicy.StillOwed(required: 20, delivered: 0).Should().Be(20);
            ReinforceGarrisonPolicy.StillOwed(required: 20, delivered: 25).Should().Be(0);
        }
    }
}
