// Code written by Gabriel Mailhot, 30/09/2026.
// The one door every character's initiative toward the player goes through (Gabriel, 30/09/2026, after Nexusrrr's baby
// son asked to discuss warfare). An inventory found about a dozen paths, each with its own filter, two of them checking
// age, several trusting Hero.IsLord (an occupation, not an age). If this gate lets a child through, a baby writes
// threats or sends killers; if it lets a prisoner through, a captive mails the player from his cell; if it lets the dead
// or the player through, the world acts in the name of someone who cannot act.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class NpcInitiativeGateTests
   {
      // A free, living adult who is not the player: the ordinary case, and it must stay open.
      [Test]
      public void GIVEN_a_free_living_adult_WHEN_they_would_take_the_initiative_THEN_they_may()
      {
         NpcInitiativeGate.Allows(new InitiatorFacts()).Should().BeTrue();
      }

      // Nexusrrr's report, whatever the path: children answer, they do not take the initiative. A child of a lord's
      // house passes Hero.IsLord, so rank is never read as age here.
      [Test]
      public void GIVEN_a_child_WHEN_they_would_take_the_initiative_THEN_it_does_not_happen()
      {
         NpcInitiativeGate.Check(new InitiatorFacts {IsChild = true}).Should().Be(InitiativeRefusal.Child);
      }

      // A captive sends no courier and rides out to no one (the letters' own M15 rule, now everyone's).
      [Test]
      public void GIVEN_a_prisoner_WHEN_they_would_take_the_initiative_THEN_it_does_not_happen()
      {
         NpcInitiativeGate.Check(new InitiatorFacts {IsPrisoner = true}).Should().Be(InitiativeRefusal.Prisoner);
      }

      // The dead, the player, and no one at all never act toward the player.
      [Test]
      public void GIVEN_the_dead_the_player_or_no_one_WHEN_checked_THEN_none_takes_the_initiative()
      {
         NpcInitiativeGate.Check(new InitiatorFacts {IsAlive = false}).Should().Be(InitiativeRefusal.Dead);
         NpcInitiativeGate.Check(new InitiatorFacts {IsPlayer = true}).Should().Be(InitiativeRefusal.IsThePlayer);
         NpcInitiativeGate.Check(new InitiatorFacts {Exists = false}).Should().Be(InitiativeRefusal.NoOne);
         NpcInitiativeGate.Check(null).Should().Be(InitiativeRefusal.NoOne);
      }

      // A hero the game has taken out of the world (disabled: not yet in play, or set aside by a mod, such as a custom
      // family's late parent) has no whereabouts and does nothing, like the dead (Maifailun, 30/09/2026).
      [Test]
      public void GIVEN_a_hero_taken_out_of_the_world_WHEN_they_would_act_THEN_it_does_not_happen()
      {
         NpcInitiativeGate.Check(new InitiatorFacts {IsDisabled = true}, InitiativeKind.Letter).Should().Be(InitiativeRefusal.OutOfTheWorld);
      }

      // Maifailun (Nexus, 30/09/2026): "my characters dead father is also partaking in the meeting". Gabriel the same
      // day: sitting at the table is an action, so it asks the same gate as every other.
      [Test]
      public void GIVEN_a_dead_lord_of_the_house_WHEN_the_player_convenes_a_council_THEN_he_does_not_take_a_seat()
      {
         NpcInitiativeGate.Check(new InitiatorFacts {IsAlive = false, Age = 60}, InitiativeKind.CouncilSeat).Should().Be(InitiativeRefusal.Dead);
      }

      // The council's own age (CouncilRosterPolicy.MinSeatAge, 13, Gabriel's rule of 25/08/2026): a teen of the house
      // may sit and speak true to their years, though the game does not yet count them of age.
      [Test]
      public void GIVEN_a_teen_of_the_house_below_the_games_majority_WHEN_a_council_is_convened_THEN_they_may_sit()
      {
         NpcInitiativeGate.Check(new InitiatorFacts {IsChild = true, Age = 14}, InitiativeKind.CouncilSeat).Should().Be(InitiativeRefusal.None);
      }

      // ... but a younger child is never a candidate for the table (toddlers were once seated at a war council).
      [Test]
      public void GIVEN_a_young_child_WHEN_a_council_is_convened_THEN_they_do_not_sit()
      {
         NpcInitiativeGate.Check(new InitiatorFacts {IsChild = true, Age = 8}, InitiativeKind.CouncilSeat).Should().Be(InitiativeRefusal.Child);
      }

      // Every other kind keeps the game's majority: a fourteen-year-old still writes no threats and asks no audience.
      [Test]
      public void GIVEN_a_teen_below_the_games_majority_WHEN_they_would_write_first_THEN_it_does_not_happen()
      {
         NpcInitiativeGate.Check(new InitiatorFacts {IsChild = true, Age = 14}, InitiativeKind.Letter).Should().Be(InitiativeRefusal.Child);
      }

      // The refusal a log names is the truest one: a dead child is dead, a captive child is a child.
      [Test]
      public void GIVEN_several_reasons_at_once_WHEN_checked_THEN_the_most_absolute_is_named()
      {
         NpcInitiativeGate.Check(new InitiatorFacts {IsAlive = false, IsChild = true}).Should().Be(InitiativeRefusal.Dead);
         NpcInitiativeGate.Check(new InitiatorFacts {IsChild = true, IsPrisoner = true}).Should().Be(InitiativeRefusal.Child);
      }
   }
}
