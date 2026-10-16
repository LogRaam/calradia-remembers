// Code written by Gabriel Mailhot, 16/10/2026.
// Whether a character may open with a greeting that offers the player service. tashmetu (Nexus, 16/10/2026): a princess
// greeted a landless player with no troops with "I am at your disposal", then sized him up as a landless stranger.
// Wrong one way, a lord fawns over a nobody; wrong the other way, a spouse, the player's own people, a dear friend, or
// anyone speaking to a ruler loses the warm lines that fit them.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Standing;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class GreetingDeferencePolicyTests
   {
      private static CorrespondenceRankFacts Facts(Stature sender, Stature player, bool bond = false, int regard = 0)
         => new() {Sender = sender, Player = player, PersonalBond = bond, Regard = regard};

      // STAKE: the reported case; a princess of a great house does not put herself at a landless stranger's disposal.
      [Test]
      public void GIVEN_a_great_house_speaker_and_a_landless_player_WHEN_the_greeting_is_chosen_THEN_no_line_offers_service()
      {
         bool mayDefer = GreetingDeferencePolicy.MayBeDeferential(Facts(new Stature(4, false), new Stature(0, false)));

         mayDefer.Should().BeFalse();
         GreetingDeferencePolicy.Filter(new[] {GreetingDeferencePolicy.AtYourDisposal, "You have my ear. Speak."}, mayDefer)
                                .Should().Equal("You have my ear. Speak.");
      }

      // STAKE: a ruler does not offer service to a vassal or a stranger who does not rule.
      [Test]
      public void GIVEN_a_ruler_speaking_to_a_player_who_does_not_rule_WHEN_asked_THEN_the_ruler_does_not_defer()
      {
         GreetingDeferencePolicy.MayBeDeferential(Facts(new Stature(6, true), new Stature(6, false))).Should().BeFalse();
      }

      // STAKE: an equal or a lesser house may speak courteously; only rank above the player removes the lines.
      [Test]
      public void GIVEN_a_speaker_of_the_players_rank_or_below_WHEN_asked_THEN_they_may_defer()
      {
         GreetingDeferencePolicy.MayBeDeferential(Facts(new Stature(3, false), new Stature(3, false))).Should().BeTrue();
         GreetingDeferencePolicy.MayBeDeferential(Facts(new Stature(0, false), new Stature(2, false))).Should().BeTrue();
      }

      // STAKE: a spouse, the player's own house or close blood, and a dear friend keep their warm lines whatever the rank.
      [Test]
      public void GIVEN_a_personal_bond_or_a_dear_friend_WHEN_asked_THEN_rank_does_not_take_the_warm_lines()
      {
         GreetingDeferencePolicy.MayBeDeferential(Facts(new Stature(5, false), new Stature(0, false), bond: true)).Should().BeTrue();
         GreetingDeferencePolicy.MayBeDeferential(Facts(new Stature(5, false), new Stature(0, false), regard: Regard.ConfidingTier)).Should().BeTrue();
      }

      // STAKE: anyone may offer a ruling player service.
      [Test]
      public void GIVEN_a_player_who_rules_a_realm_WHEN_a_ruler_greets_them_THEN_the_ruler_may_defer()
      {
         GreetingDeferencePolicy.MayBeDeferential(Facts(new Stature(6, true), new Stature(4, true))).Should().BeTrue();
      }

      // STAKE: the four lines tashmetu named are the ones held back; leaving one out would let the report recur.
      [Test]
      public void GIVEN_the_lines_named_in_the_report_WHEN_listed_THEN_each_is_deferential()
      {
         GreetingDeferencePolicy.DeferentialLines.Should().Contain(new[] {
            GreetingDeferencePolicy.AtYourDisposal, GreetingDeferencePolicy.WhatCanIDoForYou,
            GreetingDeferencePolicy.HonorMeWithYourCompany, GreetingDeferencePolicy.WhateverICanSpare
         });
      }
   }
}
