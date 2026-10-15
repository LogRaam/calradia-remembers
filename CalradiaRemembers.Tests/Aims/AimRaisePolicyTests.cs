// Code written by Gabriel Mailhot, 14/10/2026.
// Whether a lord may bring up his own aim, unbidden (tashmetu, Nexus 14/10/2026). The answer feeds every channel the
// aim can reach the player by: a conversation, a letter, an audience. If it is wrong one way, the lord brings up
// Lakonon in every conversation, which is the risk tashmetu named himself; the other way, the lord never takes the
// first step, which is the complaint of his two testers ("nobody takes the first step with the player"). The rule
// (once, then one last time after a long while) was Claude's call on Gabriel's delegation, 14/10/2026, to review.

#region

using CalradiaRemembers.Logic.Aims;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Aims
{
   [TestFixture]
   public sealed class AimRaisePolicyTests
   {
      // The whole point: a lord with a fresh aim takes the first step.
      [Test]
      public void GIVEN_an_aim_never_raised_WHEN_deciding_THEN_the_lord_may_bring_it_up()
      {
         AimRaisePolicy.Decide(new AimRaiseFacts { TimesRaised = 0 }).Should().Be(AimRaiseVerdict.MayRaise);
      }

      // tashmetu's risk: the lord who raised Lakonon last week raises it again today.
      [Test]
      public void GIVEN_an_aim_raised_recently_WHEN_deciding_THEN_the_lord_does_not_bring_it_up_again()
      {
         AimRaisePolicy.Decide(new AimRaiseFacts { TimesRaised = 1, DaysSinceLastRaised = 5 })
            .Should().Be(AimRaiseVerdict.TooSoon);
      }

      // A want that still stands after a season is worth one more word; a lord who never returns to it reads as
      // having forgotten it.
      [Test]
      public void GIVEN_an_aim_raised_once_long_ago_and_still_standing_WHEN_deciding_THEN_the_lord_may_raise_it_one_last_time()
      {
         AimRaisePolicy.Decide(new AimRaiseFacts { TimesRaised = 1, DaysSinceLastRaised = AimRaisePolicy.ReRaiseAfterDays })
            .Should().Be(AimRaiseVerdict.MayRaise);
      }

      // After the second time the lord has made his case; pressing it a third time is nagging. He still wants it if
      // the player asks.
      [Test]
      public void GIVEN_an_aim_raised_as_often_as_allowed_WHEN_deciding_THEN_the_lord_never_raises_it_again_himself()
      {
         AimRaisePolicy.Decide(new AimRaiseFacts { TimesRaised = AimRaisePolicy.MaxRaises, DaysSinceLastRaised = 999 })
            .Should().Be(AimRaiseVerdict.Spent);
      }

      // tashmetu's own line: "never if he is your enemy".
      [Test]
      public void GIVEN_a_lord_who_holds_the_player_as_an_enemy_WHEN_deciding_THEN_he_does_not_court_the_player_with_his_aim()
      {
         AimRaisePolicy.Decide(new AimRaiseFacts { IsEnemyOfPlayer = true }).Should().Be(AimRaiseVerdict.Hostile);
      }

      // A captivity has its own scenes and its own bargaining (counter_offer, release_player): a lord's fief ambition
      // has no place in a cell.
      [Test]
      public void GIVEN_a_captivity_between_them_WHEN_deciding_THEN_the_aim_stays_unsaid()
      {
         AimRaisePolicy.Decide(new AimRaiseFacts { IsCaptivity = true }).Should().Be(AimRaiseVerdict.Captivity);
      }

      // A source may keep an aim off a channel (an admirer who would never write); CR must honour it.
      [Test]
      public void GIVEN_a_channel_the_source_closed_WHEN_deciding_THEN_the_aim_does_not_surface_there()
      {
         AimRaisePolicy.Decide(new AimRaiseFacts { ChannelOpen = false }).Should().Be(AimRaiseVerdict.ChannelClosed);
      }

      // A feast already held is not something to invite the player to.
      [Test]
      public void GIVEN_an_aim_past_its_last_day_WHEN_deciding_THEN_it_is_not_raised()
      {
         AimRaisePolicy.Decide(new AimRaiseFacts { IsExpired = true }).Should().Be(AimRaiseVerdict.Expired);
      }
   }
}
