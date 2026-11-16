// Code written by Gabriel Mailhot, 20/06/2026.
// PowerBalance.DescribeFor turns the raw military/political facts (fief counts, war state, captivity, a
// recent battlefield defeat) into the POWER BALANCE prompt note that colours a lord's candor - how
// deferential, wary, or defiant they sound. Per the CHANGELOG this is meant to read as "calculated, wary
// deference rather than blind defiance or empty submission": too strong a note and every lord becomes a
// doormat regardless of standing; too weak (or silent when it shouldn't be) and a captive, or a lord
// losing a war, keeps talking as if nothing were at stake. Captivity always overrides territory (rank
// stops mattering the moment you are the player's prisoner); the thresholds below pin exactly how big an
// asymmetry has to be before a lord finds it worth remarking on at all.

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
      // Captivity dominance is the load-bearing rule of the whole policy (see DescribeFor's own comment):
      // without it, a mighty lord held prisoner would still get to boast about their kingdom's fiefs,
      // undercutting the entire point of "a captive measures every word".
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

      // DescribeFor returns on the captivity branch before the defeat clause is even built: piling "you
      // lost to me AND you're my prisoner" on top of each other would be redundant kicking of someone
      // whose situation is already fully told by captivity alone.
      [Test]
      public void GIVEN_a_captive_who_also_lost_recently_WHEN_describing_THEN_only_captivity_is_spoken()
      {
         //Arrange — captivity is the whole truth of the moment; the earlier defeat is subsumed.
         PowerBalanceFacts f = Facts(prisoner: true, recentlyLost: true);

         //Act/Assert
         PowerBalance.DescribeFor(f).Should().NotContain("bested");
      }

      // The defeat clause must stand on its own when no kingdom comparison applies (a landless or
      // kingdomless lord): otherwise a lord freshly humbled on the field would get NO note at all just
      // because they have no fiefs to compare, losing the "that defeat is still fresh" tempering entirely.
      [Test]
      public void GIVEN_a_lord_with_no_realm_standing_who_lost_to_the_player_recently_WHEN_describing_THEN_the_defeat_is_spoken()
      {
         //Arrange — no kingdom comparison applies, but the fresh personal defeat still tempers them.
         PowerBalanceFacts f = Facts(recentlyLost: true);

         //Act/Assert
         PowerBalance.DescribeFor(f).Should().Contain("bested you on the field");
      }

      // Pins Combine(): unlike the captivity case, a territorial verdict and a recent defeat are
      // INDEPENDENT stresses and both should land in the note - dropping either one would understate
      // just how cornered this lord actually is.
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

      // Peacetime asymmetry, NPC side: pins that being the stronger realm at peace still colours a lord's
      // confidence, even with no war to make the imbalance urgent.
      [Test]
      public void GIVEN_at_peace_and_the_npc_is_markedly_stronger_WHEN_describing_THEN_they_hold_the_stronger_position()
      {
         //Arrange
         PowerBalanceFacts f = Facts(playerInKingdom: true, npcInKingdom: true, playerFiefs: 4, npcFiefs: 12);

         //Act/Assert
         PowerBalance.DescribeFor(f).Should().Contain("stronger position");
      }

      // At peace the note cites the actual fief counts, not just a qualitative verdict (unlike the
      // wartime notes below): pins that specificity so a future refactor cannot quietly drop the
      // real numbers a wary lord would surely have counted for themselves.
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

      // The playerFiefs >= 3 floor guards against a lord solemnly remarking on the balance of power over
      // 2 fiefs vs 1: a ratio of "twice as many" is statistically loud but substantively trivial, and
      // narrating it as a real disparity would read as absurd, self-important dialogue.
      [Test]
      public void GIVEN_at_peace_but_below_the_fief_floor_WHEN_describing_THEN_the_thin_asymmetry_is_left_unspoken()
      {
         //Arrange — twice as many fiefs, but only 2 vs 1: too slight to remark on, and no clan gap.
         PowerBalanceFacts f = Facts(playerInKingdom: true, npcInKingdom: true, playerFiefs: 2, npcFiefs: 1,
            npcClanHasFiefs: true, playerClanHasFiefs: true);

         //Act/Assert
         PowerBalance.DescribeFor(f).Should().BeNull();
      }

      // Guards the ratio's divide-by-zero special case (DescribeStanding's own comment): npcFiefs == 0
      // must not throw or silently misclassify a rout as "uncertain" - a fief-holding player against a
      // fief-less kingdom is the most lopsided case the policy has to describe correctly.
      [Test]
      public void GIVEN_at_war_against_a_fiefless_kingdom_WHEN_describing_THEN_the_rout_reads_as_precarious()
      {
         //Arrange — npcFiefs 0 must not divide by zero; a fief-holding player facing none is a rout.
         PowerBalanceFacts f = Facts(playerInKingdom: true, npcInKingdom: true, playerFiefs: 5, npcFiefs: 0, atWar: true);

         //Act/Assert
         PowerBalance.DescribeFor(f).Should().Contain("precarious");
      }

      // Pins the wartime ratio boundary (0.5, i.e. the NPC holds >= 2x the player's fiefs): at war the
      // note must read as confident, not merely "stronger", since defiance in wartime carries real
      // military backing behind it.
      [Test]
      public void GIVEN_at_war_and_the_npc_holds_twice_the_territory_WHEN_describing_THEN_they_hold_a_clear_advantage()
      {
         //Arrange
         PowerBalanceFacts f = Facts(playerInKingdom: true, npcInKingdom: true, playerFiefs: 4, npcFiefs: 12, atWar: true);

         //Act/Assert
         PowerBalance.DescribeFor(f).Should().Contain("clear advantage");
      }

      // The reciprocal of the same 2x boundary, player side: this is the case the CHANGELOG names
      // directly ("when a lord's realm is far weaker than yours... it now colours how they speak") -
      // wary deference rather than blind defiance when the odds are visibly against them.
      [Test]
      public void GIVEN_at_war_and_the_player_holds_twice_the_territory_WHEN_describing_THEN_the_position_is_precarious()
      {
         //Arrange
         PowerBalanceFacts f = Facts(playerInKingdom: true, npcInKingdom: true, playerFiefs: 12, npcFiefs: 6, atWar: true);

         //Act/Assert
         PowerBalance.DescribeFor(f).Should().Contain("precarious");
      }

      // Between the two 2x/0.5x boundaries a lord at war should sound neither confident nor cowed: an
      // evenly matched war is genuinely uncertain, and the note must not manufacture false confidence
      // or false despair out of a roughly balanced fief count.
      [Test]
      public void GIVEN_at_war_with_evenly_matched_realms_WHEN_describing_THEN_the_outcome_is_uncertain()
      {
         //Arrange
         PowerBalanceFacts f = Facts(playerInKingdom: true, npcInKingdom: true, playerFiefs: 8, npcFiefs: 8, atWar: true);

         //Act/Assert
         PowerBalance.DescribeFor(f).Should().Contain("uncertain");
      }

      // The quiet baseline: when kingdoms are even AND both clans personally hold land, there is
      // genuinely nothing worth a lord remarking on - a note here would be noise, colouring dialogue
      // that should read as ordinary and unremarkable.
      [Test]
      public void GIVEN_evenly_matched_realms_and_both_clans_hold_land_WHEN_describing_THEN_nothing_is_remarked()
      {
         //Arrange
         PowerBalanceFacts f = Facts(playerInKingdom: true, npcInKingdom: true, playerFiefs: 8, npcFiefs: 8,
            npcClanHasFiefs: true, playerClanHasFiefs: true);

         //Act/Assert
         PowerBalance.DescribeFor(f).Should().BeNull();
      }

      // The clan-level fallback layer: even when the KINGDOMS are roughly even, this lord's own clan
      // personally holding nothing is a real, personal standing gap that the kingdom-level ratio alone
      // would miss - without this layer a landless lord would sound as secure as a landed one.
      [Test]
      public void GIVEN_evenly_matched_realms_but_the_npc_clan_holds_no_fiefs_WHEN_describing_THEN_the_clan_gap_is_noted()
      {
         //Arrange — kingdoms roughly even, but this lord personally holds nothing while the player does.
         PowerBalanceFacts f = Facts(playerInKingdom: true, npcInKingdom: true, playerFiefs: 8, npcFiefs: 8,
            npcClanHasFiefs: false, playerClanHasFiefs: true);

         //Act/Assert
         PowerBalance.DescribeFor(f).Should().Contain("no fiefs");
      }

      // Guards DescribeStanding's own entry check: "a territorial comparison means nothing unless at
      // least one side belongs to a kingdom" - two landless wanderer-adjacent lords have no realm
      // standing to compare at all, so forcing a note here would be fabricating stakes that do not exist.
      [Test]
      public void GIVEN_neither_side_is_in_a_kingdom_and_nothing_else_weighs_WHEN_describing_THEN_no_note()
      {
         //Arrange
         PowerBalanceFacts f = Facts();

         //Act/Assert
         PowerBalance.DescribeFor(f).Should().BeNull();
      }

      // "Only lords carry kingdom/clan standing worth weighing" (PowerBalanceFacts.NpcIsLord doc): a
      // wanderer or notable has no realm stake, so even amid what would otherwise be a rout, they get
      // no power-balance note at all - putting one in their mouth would misattribute standing they lack.
      [Test]
      public void GIVEN_the_npc_is_not_a_lord_WHEN_describing_THEN_no_note_is_issued()
      {
         //Arrange — a wanderer or notable has no kingdom standing to weigh, even amid a rout.
         PowerBalanceFacts f = Facts(false, true, playerFiefs: 20, npcFiefs: 1, atWar: true);

         //Act/Assert
         PowerBalance.DescribeFor(f).Should().BeNull();
      }

      // The baseline positive case for the dominance rule pinned above: captivity alone, with no other
      // facts set, must still produce the "you are currently a prisoner" note.
      [Test]
      public void GIVEN_the_npc_is_the_players_prisoner_WHEN_describing_THEN_captivity_is_named()
      {
         //Arrange
         PowerBalanceFacts f = Facts(prisoner: true);

         //Act/Assert
         PowerBalance.DescribeFor(f).Should().Contain("prisoner");
      }

      // VonLemberg (Discord, 16/11/2026): an independent player was told "Your kingdom outmatches the player's realm, 5 fiefs
      // to their 0": a missing kingdom was counted as a realm of nothing. A lord of a realm facing a player of none makes
      // no realm-against-realm claim.
      [Test]
      public void GIVEN_a_player_of_no_realm_facing_a_lord_of_a_strong_one_WHEN_describing_THEN_no_realm_comparison_is_made()
      {
         PowerBalanceFacts f = Facts(npcInKingdom: true, playerInKingdom: false, npcFiefs: 5, playerFiefs: 0, npcClanHasFiefs: true);

         string note = PowerBalance.DescribeFor(f);

         (note ?? "").Should().NotContain("realm").And.NotContain("their 0");
      }

      // The mirror case of the same audit: a lord of no realm was told the player's realm held "N fiefs to your kingdom's 0".
      [Test]
      public void GIVEN_a_lord_of_no_realm_facing_a_player_of_a_strong_one_WHEN_describing_THEN_no_realm_comparison_is_made()
      {
         PowerBalanceFacts f = Facts(npcInKingdom: false, playerInKingdom: true, npcFiefs: 0, playerFiefs: 6, npcClanHasFiefs: true);

         (PowerBalance.DescribeFor(f) ?? "").Should().NotContain("your kingdom's 0");
      }

      // An independent player whose own clan holds a town still weighs on a landless lord: the clan-level gap still speaks
      // when the realm comparison does not apply.
      [Test]
      public void GIVEN_an_independent_player_whose_clan_holds_a_fief_WHEN_a_landless_lord_of_a_realm_speaks_THEN_the_clan_gap_is_spoken()
      {
         PowerBalanceFacts f = Facts(npcInKingdom: true, playerInKingdom: false, npcFiefs: 5, npcClanHasFiefs: false, playerClanHasFiefs: true);

         PowerBalance.DescribeFor(f).Should().Contain("Your clan currently holds no fiefs");
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
         => new() {
            NpcIsLord = isLord,
            PlayerInKingdom = playerInKingdom,
            NpcInKingdom = npcInKingdom,
            PlayerKingdomFiefs = playerFiefs,
            NpcKingdomFiefs = npcFiefs,
            AtWar = atWar,
            NpcClanHasFiefs = npcClanHasFiefs,
            PlayerClanHasFiefs = playerClanHasFiefs,
            NpcIsPlayerPrisoner = prisoner,
            NpcRecentlyLostBattleToPlayer = recentlyLost
         };

      #endregion
   }
}