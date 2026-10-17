// Code written by Gabriel Mailhot, 16/10/2026.
// Whether a lord who rode up to the player on the road asks first, by a popup, or opens the conversation at once.
// fkasad (Nexus, 16/10/2026): an enemy lord rode up as the player was about to attack him; the chat opened by force,
// and a local model reasoned for half a minute over an opener the player closed unread to start the battle. Gabriel
// the same day: a popup, for hostile lords only. Wrong one way, an enemy the player means to fight still forces a
// conversation and a call to the model; wrong the other way, a friend with a word owed can be waved away, which the
// forced meeting exists to prevent.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class EnemyMeetingOfferTests
   {
      // STAKE: the reported case; a lord of a realm at war is offered, so the player can attack instead of talking.
      [Test]
      public void GIVEN_a_lord_of_a_realm_at_war_with_the_player_WHEN_he_reaches_them_THEN_the_word_is_offered_first()
      {
         InterceptionPolicy.AsksBeforeMeeting(InterceptionTrigger.HeardOfDeed, true).Should().BeTrue();
         InterceptionPolicy.AsksBeforeMeeting(InterceptionTrigger.Grudge, true).Should().BeTrue();
      }

      // STAKE: a lord defending his realm's land from the player is an enemy whatever else is known.
      [Test]
      public void GIVEN_a_territorial_patrol_WHEN_he_reaches_the_player_THEN_the_word_is_offered_first()
      {
         InterceptionPolicy.AsksBeforeMeeting(InterceptionTrigger.TerritorialPatrol, false).Should().BeTrue();
      }

      // STAKE: a lord at peace with the player, even one holding a grudge, keeps the forced meeting; a popup would let
      // the player back out of a word owed.
      [Test]
      public void GIVEN_a_lord_at_peace_with_the_player_WHEN_he_reaches_them_THEN_the_meeting_opens_at_once()
      {
         InterceptionPolicy.AsksBeforeMeeting(InterceptionTrigger.Grudge, false).Should().BeFalse();
         InterceptionPolicy.AsksBeforeMeeting(InterceptionTrigger.AppreciationOwed, false).Should().BeFalse();
         InterceptionPolicy.AsksBeforeMeeting(InterceptionTrigger.NemesisBounty, false).Should().BeFalse();
      }
   }
}
