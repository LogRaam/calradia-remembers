// Code written by Gabriel Mailhot, 23/09/2026.
// Documents the "Reach:" line cr.feed_test and cr.world_event print after feeding news. cr.feed_test used to
// feed magnitude 30 untagged and tell the tester it "reaches fewer people"; it reached nobody, and a modder
// comparing their bridge against it would have concluded CR was broken. The line must state what the awareness
// model will actually do, per situation.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class FedEventReachTests
   {
      // The old cr.feed_test fact: the line must say plainly that nobody will ever hear it.
      [Test]
      public void GIVEN_an_untagged_magnitude_30_fact_WHEN_its_reach_is_described_THEN_it_says_nobody_hears_it()
      {
         //Arrange/Act
         string text = FedEventReach.Describe(30, factionTagged: false, placed: false);

         //Assert
         text.Should().Contain("anyone, anywhere never hear it");
      }

      // The realm tag is the lever that matters most for a bridge; its window is the one the tester plans on.
      [Test]
      public void GIVEN_a_realm_tagged_magnitude_60_fact_WHEN_its_reach_is_described_THEN_it_gives_the_realms_window()
      {
         //Arrange/Act
         string text = FedEventReach.Describe(60, factionTagged: true, placed: false);

         //Assert
         text.Should().Contain("people of the tagged realm(s) for 16 day(s)");
      }

      // A place is new for fed events in 2.5.21: magnitude 50 alone lasts one day, at the place about two weeks.
      [Test]
      public void GIVEN_a_placed_magnitude_50_fact_WHEN_its_reach_is_described_THEN_the_place_carries_it_longer_than_magnitude_alone()
      {
         //Arrange/Act
         string text = FedEventReach.Describe(50, factionTagged: false, placed: true);

         //Assert
         text.Should().Contain("anyone, anywhere only on the day itself");
         text.Should().Contain("anyone at the place for 13 day(s)");
         text.Should().NotContain("tagged realm");
      }
   }
}
