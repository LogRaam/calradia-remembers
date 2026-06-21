// Code written by Gabriel Mailhot, 20/06/2026.
// Protects the power-differential note: how a lord reads the fear/respect axis. The thresholds and
// the precedence of factors (captivity over territory, a fresh defeat layered on) steer how an NPC
// speaks and break in silence — a regression would make lords blindly defiant or needlessly servile.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PowerBalanceTests
    {
        [Test]
        public void GIVEN_the_npc_is_not_a_lord_WHEN_describing_THEN_no_note_is_issued()
        {
            //Arrange — a wanderer or notable has no kingdom standing to weigh, even amid a rout.
            PowerBalanceFacts f = Facts(isLord: false, playerInKingdom: true, playerFiefs: 20, npcFiefs: 1, atWar: true);

            //Act/Assert
            PowerBalance.DescribeFor(f).Should().BeNull();
        }

        [Test]
        public void GIVEN_neither_side_is_in_a_kingdom_and_nothing_else_weighs_WHEN_describing_THEN_no_note()
        {
            //Arrange
            PowerBalanceFacts f = Facts();

            //Act/Assert
            PowerBalance.DescribeFor(f).Should().BeNull();
        }

        [Test]
        public void GIVEN_the_npc_is_the_players_prisoner_WHEN_describing_THEN_captivity_is_named()
        {
            //Arrange
            PowerBalanceFacts f = Facts(prisoner: true);

            //Act/Assert
            PowerBalance.DescribeFor(f).Should().Contain("prisoner");
        }

        [Test]
        public void GIVEN_a_captive_lord_of_a_strong_kingdom_WHEN_describing_THEN_captivity_overrides_territory()
        {
            //Arrange — even a mighty realm's lord, while captive, speaks as a captive, not a power.
            PowerBalanceFacts f = Facts(prisoner: true, npcInKingdom: true, playerInKingdom: true,
                                        npcFiefs: 20, playerFiefs: 1);

            //Act
            string note = PowerBalance.DescribeFor(f);

            //Assert
            note.Should().Contain("prisoner");
            note.Should().NotContain("fiefs");
        }

        [Test]
        public void GIVEN_a_captive_who_also_lost_recently_WHEN_describing_THEN_only_captivity_is_spoken()
        {
            //Arrange — captivity is the whole truth of the moment; the earlier defeat is subsumed.
            PowerBalanceFacts f = Facts(prisoner: true, recentlyLost: true);

            //Act/Assert
            PowerBalance.DescribeFor(f).Should().NotContain("bested");
        }

        [Test]
        public void GIVEN_at_war_and_the_player_holds_twice_the_territory_WHEN_describing_THEN_the_position_is_precarious()
        {
            //Arrange
            PowerBalanceFacts f = Facts(playerInKingdom: true, npcInKingdom: true, playerFiefs: 12, npcFiefs: 6, atWar: true);

            //Act/Assert
            PowerBalance.DescribeFor(f).Should().Contain("precarious");
        }

        [Test]
        public void GIVEN_at_war_and_the_npc_holds_twice_the_territory_WHEN_describing_THEN_they_hold_a_clear_advantage()
        {
            //Arrange
            PowerBalanceFacts f = Facts(playerInKingdom: true, npcInKingdom: true, playerFiefs: 4, npcFiefs: 12, atWar: true);

            //Act/Assert
            PowerBalance.DescribeFor(f).Should().Contain("clear advantage");
        }

        [Test]
        public void GIVEN_at_war_with_evenly_matched_realms_WHEN_describing_THEN_the_outcome_is_uncertain()
        {
            //Arrange
            PowerBalanceFacts f = Facts(playerInKingdom: true, npcInKingdom: true, playerFiefs: 8, npcFiefs: 8, atWar: true);

            //Act/Assert
            PowerBalance.DescribeFor(f).Should().Contain("uncertain");
        }

        [Test]
        public void GIVEN_at_war_against_a_fiefless_kingdom_WHEN_describing_THEN_the_rout_reads_as_precarious()
        {
            //Arrange — npcFiefs 0 must not divide by zero; a fief-holding player facing none is a rout.
            PowerBalanceFacts f = Facts(playerInKingdom: true, npcInKingdom: true, playerFiefs: 5, npcFiefs: 0, atWar: true);

            //Act/Assert
            PowerBalance.DescribeFor(f).Should().Contain("precarious");
        }

        [Test]
        public void GIVEN_at_peace_and_the_player_is_markedly_stronger_WHEN_describing_THEN_the_fief_disparity_is_named()
        {
            //Arrange
            PowerBalanceFacts f = Facts(playerInKingdom: true, npcInKingdom: true, playerFiefs: 10, npcFiefs: 4);

            //Act
            string note = PowerBalance.DescribeFor(f);

            //Assert — at peace the note states the actual counts, not just a verdict.
            note.Should().Contain("10");
            note.Should().Contain("4");
            note.Should().Contain("more powerful");
        }

        [Test]
        public void GIVEN_at_peace_but_below_the_fief_floor_WHEN_describing_THEN_the_thin_asymmetry_is_left_unspoken()
        {
            //Arrange — twice as many fiefs, but only 2 vs 1: too slight to remark on, and no clan gap.
            PowerBalanceFacts f = Facts(playerInKingdom: true, npcInKingdom: true, playerFiefs: 2, npcFiefs: 1,
                                        npcClanHasFiefs: true, playerClanHasFiefs: true);

            //Act/Assert
            PowerBalance.DescribeFor(f).Should().BeNull();
        }

        [Test]
        public void GIVEN_at_peace_and_the_npc_is_markedly_stronger_WHEN_describing_THEN_they_hold_the_stronger_position()
        {
            //Arrange
            PowerBalanceFacts f = Facts(playerInKingdom: true, npcInKingdom: true, playerFiefs: 4, npcFiefs: 12);

            //Act/Assert
            PowerBalance.DescribeFor(f).Should().Contain("stronger position");
        }

        [Test]
        public void GIVEN_evenly_matched_realms_but_the_npc_clan_holds_no_fiefs_WHEN_describing_THEN_the_clan_gap_is_noted()
        {
            //Arrange — kingdoms roughly even, but this lord personally holds nothing while the player does.
            PowerBalanceFacts f = Facts(playerInKingdom: true, npcInKingdom: true, playerFiefs: 8, npcFiefs: 8,
                                        npcClanHasFiefs: false, playerClanHasFiefs: true);

            //Act/Assert
            PowerBalance.DescribeFor(f).Should().Contain("no fiefs");
        }

        [Test]
        public void GIVEN_evenly_matched_realms_and_both_clans_hold_land_WHEN_describing_THEN_nothing_is_remarked()
        {
            //Arrange
            PowerBalanceFacts f = Facts(playerInKingdom: true, npcInKingdom: true, playerFiefs: 8, npcFiefs: 8,
                                        npcClanHasFiefs: true, playerClanHasFiefs: true);

            //Act/Assert
            PowerBalance.DescribeFor(f).Should().BeNull();
        }

        [Test]
        public void GIVEN_a_lord_with_no_realm_standing_who_lost_to_the_player_recently_WHEN_describing_THEN_the_defeat_is_spoken()
        {
            //Arrange — no kingdom comparison applies, but the fresh personal defeat still tempers them.
            PowerBalanceFacts f = Facts(recentlyLost: true);

            //Act/Assert
            PowerBalance.DescribeFor(f).Should().Contain("bested you on the field");
        }

        [Test]
        public void GIVEN_a_precarious_position_compounded_by_a_recent_defeat_WHEN_describing_THEN_both_are_voiced()
        {
            //Arrange
            PowerBalanceFacts f = Facts(playerInKingdom: true, npcInKingdom: true, playerFiefs: 12, npcFiefs: 6,
                                        atWar: true, recentlyLost: true);

            //Act
            string note = PowerBalance.DescribeFor(f);

            //Assert — the territorial verdict and the personal defeat both land.
            note.Should().Contain("precarious");
            note.Should().Contain("bested you on the field");
        }

        #region private

        private static PowerBalanceFacts Facts(
            bool isLord = true,
            bool playerInKingdom = false,
            bool npcInKingdom = false,
            int playerFiefs = 0,
            int npcFiefs = 0,
            bool atWar = false,
            bool npcClanHasFiefs = false,
            bool playerClanHasFiefs = false,
            bool prisoner = false,
            bool recentlyLost = false)
            => new PowerBalanceFacts
            {
                NpcIsLord                     = isLord,
                PlayerInKingdom               = playerInKingdom,
                NpcInKingdom                  = npcInKingdom,
                PlayerKingdomFiefs            = playerFiefs,
                NpcKingdomFiefs               = npcFiefs,
                AtWar                         = atWar,
                NpcClanHasFiefs               = npcClanHasFiefs,
                PlayerClanHasFiefs            = playerClanHasFiefs,
                NpcIsPlayerPrisoner           = prisoner,
                NpcRecentlyLostBattleToPlayer = recentlyLost
            };

        #endregion
    }
}
