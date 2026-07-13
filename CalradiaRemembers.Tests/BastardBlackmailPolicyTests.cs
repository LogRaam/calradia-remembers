// Code written by Gabriel Mailhot, 26/06/2026.
// Documents when a bastard's mother can extort the player and how steep her price is: she has leverage only
// while the secret is hidden, and her demand tracks the player's wealth within a floor-and-ceiling band.
//
// WHY IT MATTERS: HoldsLeverage gates whether BastardBehavior even lets the mother raise a demand or send a
// blackmail letter, and DemandAmount is the exact gold figure she asks for and the player pays via Give Gold.
// A wrong HoldsLeverage either lets an exposed secret or the player's own spouse keep extorting forever, or
// silences a mother who should have real leverage. A wrong DemandAmount either bankrupts a poor lord in one
// demand or turns extortion into pocket change for a rich one, and CanRaiseDemand/DemandHasLapsed gate the
// recurring cycle (pay for GraceSeasons of peace, or an unmet demand past its deadline makes her leak the
// secret) that the whole blackmail thread runs on.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class BastardBlackmailPolicyTests
    {
        // The floor case the whole leverage gate stands on: a living mother of a still-concealed child, not
        // married to the player, has a genuine secret to sell. Without this the blackmail thread never fires.
        [Test]
        public void GIVEN_a_hidden_bastard_and_a_living_unmarried_mother_WHEN_checking_THEN_she_holds_leverage()
        {
            BastardBlackmailPolicy.HoldsLeverage(bastardConcealed: true, motherAlive: true, motherIsPlayerSpouse: false)
                .Should().BeTrue();
        }

        // Once a bastard is discovered, silence has no market: a mother who kept demanding gold for a secret
        // everyone already knows would be an obvious exploit, paid forever with nothing bought in return.
        [Test]
        public void GIVEN_the_secret_is_already_out_WHEN_checking_THEN_there_is_nothing_left_to_sell()
        {
            BastardBlackmailPolicy.HoldsLeverage(bastardConcealed: false, motherAlive: true, motherIsPlayerSpouse: false)
                .Should().BeFalse();
        }

        // A dead woman cannot write a letter or collect a payment: without this guard a stale blackmail state
        // could keep haunting the player past the mother's own death.
        [Test]
        public void GIVEN_a_dead_mother_WHEN_checking_THEN_she_holds_no_leverage()
        {
            BastardBlackmailPolicy.HoldsLeverage(bastardConcealed: true, motherAlive: false, motherIsPlayerSpouse: false)
                .Should().BeFalse();
        }

        // Per the policy's own doc comment: "a wife shares the house, not a secret to sell." Without this
        // carve-out a married mother would extort the very husband she lives with over their own child.
        [Test]
        public void GIVEN_the_mother_is_the_players_own_spouse_WHEN_checking_THEN_she_holds_no_leverage()
        {
            BastardBlackmailPolicy.HoldsLeverage(bastardConcealed: true, motherAlive: true, motherIsPlayerSpouse: true)
                .Should().BeFalse();
        }

        // MinDemand exists so even a poor lord pays something for silence: without a floor, a 10% cut of a
        // small purse would make the blackmail thread meaningless for anyone but the wealthy.
        [Test]
        public void GIVEN_a_poor_player_WHEN_demanding_THEN_the_price_is_floored()
        {
            //Arrange — 10 % of 1000 = 100, below the floor.
            BastardBlackmailPolicy.DemandAmount(1000).Should().Be(BastardBlackmailPolicy.MinDemand);
        }

        // Pins the core formula (DemandPercentOfWealth of playerGold) so the demand actually scales with the
        // player's means, the design intent stated in the policy's header ("she extracts what she can").
        [Test]
        public void GIVEN_a_comfortable_player_WHEN_demanding_THEN_she_takes_a_share_of_the_wealth()
        {
            BastardBlackmailPolicy.DemandAmount(50000)
                .Should().Be(50000 * BastardBlackmailPolicy.DemandPercentOfWealth / 100);
        }

        // MaxDemand exists so the demand "never becomes ruinous in one stroke" (policy's own comment): without
        // a ceiling, a wealthy player's 10% share could dwarf the GraceSeasons of peace it buys.
        [Test]
        public void GIVEN_a_fabulously_rich_player_WHEN_demanding_THEN_the_price_is_capped()
        {
            BastardBlackmailPolicy.DemandAmount(1_000_000).Should().Be(BastardBlackmailPolicy.MaxDemand);
        }

        // Defensive: a caller could pass a corrupted or unread gold figure. Negative wealth must never subtract
        // from the demand or produce a negative gold ask, it floors like any other poor-player case.
        [Test]
        public void GIVEN_no_or_negative_gold_WHEN_demanding_THEN_it_floors_at_the_minimum()
        {
            BastardBlackmailPolicy.DemandAmount(0).Should().Be(BastardBlackmailPolicy.MinDemand);
            BastardBlackmailPolicy.DemandAmount(-5000).Should().Be(BastardBlackmailPolicy.MinDemand);
        }

        // Blackmail is RECURRING, not a one-time toll: a payment buys exactly GraceSeasons of peace, then she
        // returns. Get the arithmetic wrong and the player either pays forever or is silenced permanently.
        [Test]
        public void GIVEN_a_payment_now_WHEN_setting_silence_THEN_it_lasts_the_grace_period()
        {
            BastardBlackmailPolicy.SilenceUntil(10)
                .Should().Be(10 + BastardBlackmailPolicy.GraceSeasons);
        }

        // The -1 sentinel (no demand raised) must never read as an ancient, long-overdue demand: without this
        // guard a mother who never demanded anything could still be read as "leaking" the secret.
        [Test]
        public void GIVEN_no_outstanding_demand_WHEN_checking_a_lapse_THEN_nothing_lapses()
        {
            BastardBlackmailPolicy.DemandHasLapsed(currentSeason: 12, demandSeason: -1).Should().BeFalse();
        }

        // Pins the lower edge of DemandDeadlineSeasons: the player still has time to pay, so the secret must
        // not leak early and cost him the scandal before the deadline he was given has actually passed.
        [Test]
        public void GIVEN_an_unmet_demand_within_the_deadline_WHEN_checking_THEN_she_waits()
        {
            //Arrange — deadline 2 seasons: demanded at 10, only one season has passed.
            BastardBlackmailPolicy.DemandHasLapsed(currentSeason: 11, demandSeason: 10).Should().BeFalse();
        }

        // The consequence side of the deadline: BastardBehavior reads this to actually carry out the threat
        // and leak the secret. Miss this boundary and an ignored demand never costs the player anything.
        [Test]
        public void GIVEN_an_unmet_demand_past_the_deadline_WHEN_checking_THEN_she_leaks()
        {
            BastardBlackmailPolicy.DemandHasLapsed(currentSeason: 12, demandSeason: 10).Should().BeTrue();
        }

        // Without this guard a mother could stack a second demand on top of an unresolved one, doubling the
        // gold she asks for and spamming the player with duplicate blackmail letters over the same secret.
        [Test]
        public void GIVEN_an_active_demand_WHEN_checking_THEN_she_does_not_raise_another()
        {
            BastardBlackmailPolicy.CanRaiseDemand(currentSeason: 20, silentUntilSeason: 0, hasActiveDemand: true)
                .Should().BeFalse();
        }

        // The other half of "recurring, not one-time": if she could re-demand while a paid GraceSeasons is
        // still running, paying for silence would buy nothing and the grace period would be a lie.
        [Test]
        public void GIVEN_paid_silence_still_in_force_WHEN_checking_THEN_she_stays_quiet()
        {
            BastardBlackmailPolicy.CanRaiseDemand(currentSeason: 12, silentUntilSeason: 14, hasActiveDemand: false)
                .Should().BeFalse();
        }

        // Boundary must be INCLUSIVE at the exact expiry season, or the recurring cycle stalls one season late
        // every time, quietly giving the player extra free seasons of silence he never paid for.
        [Test]
        public void GIVEN_the_silence_has_expired_and_no_demand_stands_WHEN_checking_THEN_she_may_demand_again()
        {
            BastardBlackmailPolicy.CanRaiseDemand(currentSeason: 14, silentUntilSeason: 14, hasActiveDemand: false)
                .Should().BeTrue();
        }
    }
}
