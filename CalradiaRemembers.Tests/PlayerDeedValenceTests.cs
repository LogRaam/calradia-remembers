// Code written by Gabriel Mailhot, 22/06/2026.
// Protects the influence-loop valence — how a deed the player did lands on an NPC who only HEARD of it
// (a wound to one's own side, a boon to one's champion), the colour behind "what is said of you".

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PlayerDeedValenceTests
   {
      [Test]
      public void GIVEN_a_deed_that_struck_the_observers_own_side_WHEN_classified_THEN_it_is_a_wound()
      {
         //Arrange/Act/Assert
         PlayerDeedValence.Classify(observerAggrieved: true, observerSharesPlayerSide: false)
            .Should().Be(DeedValence.Wound);
      }

      [Test]
      public void GIVEN_a_deed_by_the_observers_own_faction_WHEN_classified_THEN_it_is_a_boon()
      {
         //Arrange/Act/Assert
         PlayerDeedValence.Classify(observerAggrieved: false, observerSharesPlayerSide: true)
            .Should().Be(DeedValence.Boon);
      }

      [Test]
      public void GIVEN_a_distant_deed_touching_neither_side_WHEN_classified_THEN_it_is_neutral()
      {
         //Arrange/Act/Assert
         PlayerDeedValence.Classify(observerAggrieved: false, observerSharesPlayerSide: false)
            .Should().Be(DeedValence.Neutral);
      }

      [Test]
      public void GIVEN_a_deed_that_both_served_the_players_side_AND_aggrieved_the_observer_WHEN_classified_THEN_the_wound_wins()
      {
         //Arrange — a lord of the player's own kingdom whose kin the player nonetheless harmed.
         //Act/Assert — the blow is felt before the shared banner.
         PlayerDeedValence.Classify(observerAggrieved: true, observerSharesPlayerSide: true)
            .Should().Be(DeedValence.Wound);
      }
   }
}
