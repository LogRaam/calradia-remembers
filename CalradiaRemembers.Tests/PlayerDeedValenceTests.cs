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
      // This feeds the "(this cut against you or your own)" prefix EncounterContextBuilder attaches to a
      // rumoured deed; misreading a faction-harming deed as neutral or a boon would have a lord speak
      // warmly of an act that actually struck his own side.
      [Test]
      public void GIVEN_a_deed_that_struck_the_observers_own_faction_WHEN_classified_THEN_it_is_a_wound()
      {
         //Arrange/Act/Assert
         PlayerDeedValence.Classify(new PlayerDeedValence.ObserverStakeFacts {FactionAggrieved = true, KinHarmed = false, SharesPlayerSide = false})
            .Should().Be(DeedValence.Wound);
      }

      // Kinship must count even when the faction tag does not match (a kinsman married into another
      // banner); without this, a deed that hurt someone's own blood would wrongly read as neutral.
      [Test]
      public void GIVEN_a_deed_that_struck_the_observers_kin_but_not_their_faction_WHEN_classified_THEN_it_is_a_wound()
      {
         //Arrange — a kinsman under another banner was harmed; the faction tag does not match, but blood does.
         //Act/Assert
         PlayerDeedValence.Classify(new PlayerDeedValence.ObserverStakeFacts {FactionAggrieved = false, KinHarmed = true, SharesPlayerSide = false})
            .Should().Be(DeedValence.Wound);
      }

      // This feeds the "(this served your side)" prefix; if it fails to fire, an NPC never credits the
      // player for deeds that actually helped their own faction.
      [Test]
      public void GIVEN_a_deed_by_the_observers_own_faction_WHEN_classified_THEN_it_is_a_boon()
      {
         //Arrange/Act/Assert
         PlayerDeedValence.Classify(new PlayerDeedValence.ObserverStakeFacts {FactionAggrieved = false, KinHarmed = false, SharesPlayerSide = true})
            .Should().Be(DeedValence.Boon);
      }

      // The no-op default: guards against over-eager classification tagging a wound/boon prefix onto a
      // deed that has nothing to do with the observer at all.
      [Test]
      public void GIVEN_a_distant_deed_touching_neither_side_WHEN_classified_THEN_it_is_neutral()
      {
         //Arrange/Act/Assert
         PlayerDeedValence.Classify(new PlayerDeedValence.ObserverStakeFacts {FactionAggrieved = false, KinHarmed = false, SharesPlayerSide = false})
            .Should().Be(DeedValence.Neutral);
      }

      // Pins the priority order stated in the production doc, "a blow to one's own side is felt before any
      // shared allegiance is enjoyed": Wound must always outrank Boon, so a lord whose kin was hurt never
      // sounds grateful just because the player nominally fights on his side.
      [Test]
      public void GIVEN_a_deed_that_both_served_the_players_side_AND_struck_the_observers_kin_WHEN_classified_THEN_the_wound_wins()
      {
         //Arrange — a lord of the player's own kingdom whose kin the player nonetheless harmed.
         //Act/Assert — the blow is felt before the shared banner.
         PlayerDeedValence.Classify(new PlayerDeedValence.ObserverStakeFacts {FactionAggrieved = false, KinHarmed = true, SharesPlayerSide = true})
            .Should().Be(DeedValence.Wound);
      }
   }
}
