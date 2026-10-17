// Code written by Gabriel Mailhot, 17/10/2026.
// RecentMatterSelector.GossipAlreadyRaisedByAnyone decides whether a character may still come to ask the player about a
// piece of distant news. froggyluv (Nexus, 17/10/2026): "just about everyone wants to stop me ... in every village or
// town but basically ask me the same question ... what do you know of South Empire War with Western Empire? ... Next
// was what did i know about Dertherts capture? by also 2-3 people. So it seems kind of stuck in a loop." The ledger
// was per character, so the next notable asked again. If this is wrong, the loop comes back; if it is too broad, a
// character who offered the news finds nothing to say when the player accepts.

#region

using CalradiaRemembers.Logic.Interception;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class GossipOncePerNewsTests
   {
      private const string War = "we:war_south_west";

      // STAKE: froggyluv's loop: one notable asked about the war, the next must not ask again.
      [Test]
      public void GIVEN_one_character_already_asked_about_a_war_WHEN_another_would_ask_THEN_he_does_not()
      {
         RecentMatterSelector.GossipAlreadyRaisedByAnyone(new[] {"notable_1|" + War}, War, "notable_2").Should().BeTrue();
      }

      // STAKE: declining the invitation that names the news is an answer too: the next notable does not offer it again.
      [Test]
      public void GIVEN_a_character_offered_the_news_and_the_player_declined_WHEN_another_would_ask_THEN_he_does_not()
      {
         RecentMatterSelector.GossipAlreadyRaisedByAnyone(new[] {RecentMatterSelector.OfferedPrefix + "notable_1|" + War}, War, "notable_2")
                             .Should().BeTrue();
      }

      // STAKE: the one who offered the news must still find it when the player says "speak with them", or he opens on
      // nothing.
      [Test]
      public void GIVEN_a_character_offered_the_news_WHEN_the_player_accepts_THEN_he_can_still_raise_it()
      {
         RecentMatterSelector.GossipAlreadyRaisedByAnyone(new[] {RecentMatterSelector.OfferedPrefix + "notable_1|" + War}, War, "notable_1")
                             .Should().BeFalse();
      }

      // STAKE: other news stays open: a capture can still be asked about after the war was.
      [Test]
      public void GIVEN_the_war_was_asked_about_WHEN_a_character_would_ask_about_a_capture_THEN_he_may()
      {
         RecentMatterSelector.GossipAlreadyRaisedByAnyone(new[] {"notable_1|" + War}, "we:capture_derthert", "notable_2").Should().BeFalse();
      }

      // STAKE: a key that merely ends the same way is not the same news ("we:war_1" against "we:war_11").
      [Test]
      public void GIVEN_a_different_event_whose_id_ends_alike_WHEN_compared_THEN_it_is_not_the_same_news()
      {
         RecentMatterSelector.GossipAlreadyRaisedByAnyone(new[] {"notable_1|we:war_11"}, "we:war_1", "notable_2").Should().BeFalse();
      }
   }
}
