// Code written by Gabriel Mailhot, 23/09/2026.
// Documents the account cr.heard prints for "has this character heard this event". A bridge author (tashmetu,
// 23/09/2026) asked for a way to check it "short of just going and talking to them"; the answer is read by a
// modder deciding whether their bridge is broken, so a wrong verdict or a wrong lever sends them debugging the
// wrong thing. The arithmetic itself is pinned in RumorAwarenessTests; this pins what is SAID about it.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class RumorAwarenessBreakdownTests
   {
      // A modder planning a test pass needs to know how long they have left, not only "yes": a realm-tagged
      // magnitude 60 fact is held until day 16, so on day 10 there are six days to go and talk to them.
      [Test]
      public void GIVEN_a_heard_event_WHEN_described_THEN_it_says_heard_and_how_many_days_remain()
      {
         //Arrange
         RumorAwarenessBreakdown heard = RumorAwareness.Explain(60, factionMatches: true, geoBonus: 0, concernedParty: false, ageDays: 10);

         //Act
         string text = heard.Describe();

         //Assert
         text.Should().StartWith("HEARD");
         text.Should().Contain("Still heard for 6 more day(s).");
      }

      // "Faded" and "never" call for opposite fixes: a faded fact means the bridge worked and the test came too
      // late, a never-heard one means the tags are wrong. Telling them apart is the point of the command.
      [Test]
      public void GIVEN_an_event_heard_once_but_now_too_old_WHEN_described_THEN_it_says_faded_and_when()
      {
         //Arrange
         RumorAwarenessBreakdown faded = RumorAwareness.Explain(60, true, 0, false, ageDays: 20);

         //Act
         string text = faded.Describe();

         //Assert
         text.Should().StartWith("NOT HEARD");
         text.Should().Contain("Faded: they had it until it was 16 day(s) old.");
      }

      // A never-heard fact lists the levers the MODDER controls and that are still unused, so the fix is read
      // off the console line rather than reverse-engineered from the formula.
      [Test]
      public void GIVEN_an_event_this_observer_could_never_hear_WHEN_described_THEN_it_names_every_unused_lever()
      {
         //Arrange
         RumorAwarenessBreakdown never = RumorAwareness.Explain(30, false, 0, false, ageDays: 0);

         //Act
         string text = never.Describe();

         //Assert
         text.Should().Contain("could not hear it even on the day it happened");
         text.Should().Contain("tag their realm (the kingdom id)");
         text.Should().Contain("place it near them");
         text.Should().Contain("name them in HeroIds");
      }

      // A lever already pulled is not advice: telling a modder to tag the realm when the realm IS tagged would
      // send them hunting a bug in an id that is correct.
      [Test]
      public void GIVEN_a_never_heard_event_already_tagged_with_their_realm_WHEN_described_THEN_it_does_not_advise_tagging_it()
      {
         //Arrange
         RumorAwarenessBreakdown never = RumorAwareness.Explain(20, factionMatches: true, geoBonus: 0, concernedParty: false, ageDays: 0);

         //Act
         string text = never.Describe();

         //Assert
         never.Knows.Should().BeFalse();
         text.Should().NotContain("tag their realm");
      }
   }
}
