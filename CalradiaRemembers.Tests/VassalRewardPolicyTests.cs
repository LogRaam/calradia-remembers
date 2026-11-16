// Code written by Gabriel Mailhot, 16/11/2026.
// VassalRewardPolicy decides which of the game's oath rewards an oath sworn through CR's chat gives. Aporo (15/11/2026):
// sworn in the chat, he got none of what the game's own oath gives; Gabriel (16/11/2026): give them at the player's yes.
// The game (LordConversationsCampaignBehavior, 1.5.7) gives influence at every oath and the gift of arms and men with the
// ruler's goodwill once per campaign. Wrong one way, the chat oath stays poorer than the game's; the other, the gift is
// handed out again at every oath.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class VassalRewardPolicyTests
   {
      // STAKE: Aporo's oath: the first oath of the campaign gives all the game gives: influence, the gift of arms and men,
      // and the ruler's goodwill.
      [Test]
      public void GIVEN_the_first_oath_of_the_campaign_WHEN_sworn_THEN_everything_the_game_gives_is_given()
      {
         VassalRewardGrant g = VassalRewardPolicy.Decide(new VassalRewardFacts {AlreadyRewardedThisCampaign = false, RealmHasRuler = true});

         g.Influence.Should().BeTrue();
         g.GiftOfArmsAndMen.Should().BeTrue();
         g.RulersGoodwill.Should().BeTrue();
      }

      // STAKE: the game gives its gift once per campaign (its saved flag): a second oath, through the game or the chat, does
      // not hand out arms and men again; the influence of joining comes every time, as in the game.
      [Test]
      public void GIVEN_a_later_oath_WHEN_sworn_THEN_only_the_influence_is_given()
      {
         VassalRewardGrant g = VassalRewardPolicy.Decide(new VassalRewardFacts {AlreadyRewardedThisCampaign = true, RealmHasRuler = true});

         g.Influence.Should().BeTrue();
         g.GiftOfArmsAndMen.Should().BeFalse();
         g.RulersGoodwill.Should().BeFalse();
      }

      // STAKE: the goodwill is with the ruler: a realm without one gives none, the gift still comes.
      [Test]
      public void GIVEN_a_realm_without_a_ruler_WHEN_first_sworn_THEN_no_goodwill_is_given()
      {
         VassalRewardGrant g = VassalRewardPolicy.Decide(new VassalRewardFacts {AlreadyRewardedThisCampaign = false, RealmHasRuler = false});

         g.GiftOfArmsAndMen.Should().BeTrue();
         g.RulersGoodwill.Should().BeFalse();
      }
   }
}
