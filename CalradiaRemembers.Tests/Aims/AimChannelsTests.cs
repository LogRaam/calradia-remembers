// Code written by Gabriel Mailhot, 28/10/2026.
// The audience and letter channels of a lord's aim (CrAims). tashmetu (14/10/2026): "nobody takes the first step with the
// player"; Gabriel (28/10/2026): "fait 2 et 1", a lord seeks the player out, then writes, about an aim another mod gave
// him. If this is wrong, a lord who sought the player out is then forbidden to say why, an aim crowds out a grudge or the
// war duty, a missed meeting spends a recent matter it never meant, or the letter invents what the mod never said.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Aims;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Aims
{
   [TestFixture]
   public sealed class AimChannelsTests
   {
      private static (AimBook book, StandingAim aim) LordWithAim(AimChannels channels)
      {
         var book = new AimBook();
         book.Register(new StandingAim {Id = "a", NpcId = "lord_herra", SourceMod = "BCBridge", Want = "Lakonon.", PlayerPart = "Your men.", Channels = channels}, 1);
         return (book, book.ForNpc("lord_herra"));
      }

      // STAKE: a lord who sought the player out for his aim, under an aim open to audiences only, must be free to say it
      // in the conversation that follows, and the ledger must remember it was an audience.
      [Test]
      public void GIVEN_an_audience_sought_for_an_aim_WHEN_the_conversation_opens_THEN_the_lord_may_raise_it_and_it_is_recorded_as_an_audience()
      {
         var (book, aim) = LordWithAim(AimChannels.Audience);

         new AimConversationLeave().Open(new AimOpening {SessionId = "s", Aim = aim, Today = 10, Channel = AimChannels.Audience}, book)
                                   .Should().Be(AimRaiseVerdict.MayRaise);
         aim.LastRaisedChannel.Should().Be(AimChannels.Audience);
      }

      // STAKE: the same aim, closed to conversation, is not raised in a conversation the player opened.
      [Test]
      public void GIVEN_an_aim_open_to_audiences_only_WHEN_the_player_opens_a_conversation_THEN_the_lord_does_not_raise_it()
      {
         var (book, aim) = LordWithAim(AimChannels.Audience);

         new AimConversationLeave().Open(new AimOpening {SessionId = "s", Aim = aim, Today = 10}, book).Should().Be(AimRaiseVerdict.ChannelClosed);
      }

      // STAKE: an aim is the least urgent reason to seek the player: a grudge, the war duty or fresh news come first.
      [Test]
      public void GIVEN_an_aim_and_another_reason_WHEN_the_lord_chooses_why_to_seek_the_player_THEN_the_other_reason_wins()
      {
         InterceptionPolicy.Choose(new InterceptionFacts {HoldsAimToRaise = true, HoldsLiveGrudgeAgainstPlayer = true}).Should().Be(InterceptionTrigger.Grudge);
         InterceptionPolicy.Choose(new InterceptionFacts {HoldsAimToRaise = true, HeardRecentPlayerDeed = true}).Should().Be(InterceptionTrigger.HeardOfDeed);
         InterceptionPolicy.Choose(new InterceptionFacts {HoldsAimToRaise = true}).Should().Be(InterceptionTrigger.StandingAim);
      }

      // STAKE: a lord may ask for a word about his aim where he stands as well as ride out for it.
      [Test]
      public void GIVEN_an_aim_WHEN_the_player_walks_into_his_town_or_passes_on_the_road_THEN_both_may_carry_it()
      {
         FiefAudiencePolicy.IsFiefValid(InterceptionTrigger.StandingAim).Should().BeTrue();
         InterceptionPolicy.IsRoadValid(InterceptionTrigger.StandingAim).Should().BeTrue();
      }

      // STAKE: a missed meeting over an aim never writes the generic letter (it would spend a recent matter the lord never
      // meant); the aim's own letter channel writes about it, paced there.
      [Test]
      public void GIVEN_a_chase_over_an_aim_given_up_WHEN_asked_for_a_missed_meeting_letter_THEN_none()
      {
         InterceptionPolicy.ShouldWriteMissedMeetingLetter(AbandonReason.GaveUp, true, InterceptionTrigger.StandingAim).Should().BeFalse();
      }

      // STAKE: the letter is written from what the mod gave, and from nothing else: the want, the player's part, the manner.
      [Test]
      public void GIVEN_an_aim_WHEN_its_letter_context_is_built_THEN_it_carries_the_want_the_players_part_and_the_manner()
      {
         var (_, aim) = LordWithAim(AimChannels.All);

         AimLetterText.Context(aim, "Ask openly.").Should().Be("What you want, in your own terms: Lakonon. What the player could do in it: Your men. How you put it: Ask openly.");
         AimLetterText.Context(new StandingAim {Want = " "}, "x").Should().BeNull();
      }
   }
}
