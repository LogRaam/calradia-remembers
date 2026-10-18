// Code written by Gabriel Mailhot, 18/10/2026.
// press_for_war (Gabriel, 18/10/2026): the player urges any lord, in conversation, to push his realm into war; the
// lord's house files the game's own war proposal, voted by the clans and decided by the ruler. This policy says
// whether a lord can press at all (told before he speaks), how his house leans by the game's own support, and when
// he reaches court. If it is wrong, a lord promises a proposal the game then refuses, a house proposes a war the game
// itself forbids (allies, a fresh peace, a second proposal), or a lord's words contradict how his house will vote.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PressForWarPolicyTests
   {
      private static PressForWarFacts Can(int? daysSincePeace = null, bool pending = false, bool allied = false, bool atWar = false,
                                          float influence = 300f, bool playersHouse = false, bool pressing = false)
         => new() {
            HouseInARealm = true, IsPlayersHouse = playersHouse, TargetIsAnotherRealm = true, AlreadyAtWar = atWar, Allied = allied,
            DaysSincePeace = daysSincePeace, WarProposalPending = pending, HouseAlreadyPressing = pressing, HouseInfluence = influence, ProposalCost = 200
         };

      // STAKE: the plain case: a lord of a realm at peace with the target, his house rich enough, may press.
      [Test]
      public void GIVEN_a_lord_whose_house_can_propose_WHEN_asked_THEN_he_may_press()
      {
         PressForWarPolicy.WhyNot(Can()).Should().BeNull();
         PressForWarPolicy.WhyNot(Can(daysSincePeace: PressForWarPolicy.MinDaysSincePeace)).Should().BeNull();
      }

      // STAKE: the game itself forbids these proposals; a lord who promised one would be refuted by the world after.
      [Test]
      public void GIVEN_what_the_game_forbids_WHEN_asked_THEN_he_cannot_press()
      {
         PressForWarPolicy.WhyNot(Can(daysSincePeace: 5)).Should().Be("the two realms made peace too recently");
         PressForWarPolicy.WhyNot(Can(pending: true)).Should().Be("a war proposal already waits in his realm");
         PressForWarPolicy.WhyNot(Can(atWar: true)).Should().Be("the two realms are already at war");
         PressForWarPolicy.WhyNot(Can(allied: true)).Should().Be("the two realms are allied");
      }

      // STAKE: a proposal costs the house influence, as the game's own; a poor house cannot carry it.
      [Test]
      public void GIVEN_a_house_short_of_influence_WHEN_asked_THEN_he_cannot_press()
      {
         PressForWarPolicy.WhyNot(Can(influence: 150f)).Should().Be("his house lacks the influence to propose it");
      }

      // STAKE: the player's own house proposes through the game's own screen, never through a companion's chat; and one
      // house carries one such matter at a time.
      [Test]
      public void GIVEN_the_players_house_or_a_house_already_pressing_WHEN_asked_THEN_he_cannot_press()
      {
         PressForWarPolicy.WhyNot(Can(playersHouse: true)).Should().Be("his house is the player's own");
         PressForWarPolicy.WhyNot(Can(pressing: true)).Should().Be("his house is already carrying such a matter to court");
      }

      // STAKE: the lord speaks as his house will vote: the game's own threshold for backing a war marks "for".
      [Test]
      public void GIVEN_the_games_support_WHEN_read_as_a_leaning_THEN_it_follows_the_games_threshold()
      {
         PressForWarPolicy.Leaning(80f).Should().Be(HouseLeaning.For);
         PressForWarPolicy.Leaning(50f).Should().Be(HouseLeaning.Torn);
         PressForWarPolicy.Leaning(-120f).Should().Be(HouseLeaning.Against);
      }

      // STAKE: a case for war names the realm; "the Vlandians" or its king will do, a word shared by every empire will not.
      [Test]
      public void GIVEN_the_players_words_WHEN_checked_for_the_realm_THEN_a_distinctive_name_counts_and_a_generic_word_does_not()
      {
         string[] vlandia = {"Vlandia", "Vlandia", "Derthert"};
         string[] west = {"Western Empire", "Empire", "Garios"};

         PressForWarPolicy.PlayerSpokeOfRealm("The Vlandians grow bold on our border.", vlandia).Should().BeTrue();
         PressForWarPolicy.PlayerSpokeOfRealm("Derthert will never keep his word.", vlandia).Should().BeTrue();
         PressForWarPolicy.PlayerSpokeOfRealm("The Northern Empire is weak this year.", west).Should().BeFalse();
         PressForWarPolicy.PlayerSpokeOfRealm("A fine day, my lord.", vlandia).Should().BeFalse();
      }

      // STAKE: the player sees nothing at once; the proposal comes when the lord has had time to reach court.
      [Test]
      public void GIVEN_a_convinced_lord_WHEN_dated_THEN_his_house_proposes_days_later()
      {
         PressForWarPolicy.DueDay(100).Should().Be(100 + PressForWarPolicy.DaysToReachCourt);
      }
   }
}
