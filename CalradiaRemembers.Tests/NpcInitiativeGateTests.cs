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

      // The refusal a log names is the truest one: a dead child is dead, a captive child is a child.
      [Test]
      public void GIVEN_several_reasons_at_once_WHEN_checked_THEN_the_most_absolute_is_named()
      {
         NpcInitiativeGate.Check(new InitiatorFacts {IsAlive = false, IsChild = true}).Should().Be(InitiativeRefusal.Dead);
         NpcInitiativeGate.Check(new InitiatorFacts {IsChild = true, IsPrisoner = true}).Should().Be(InitiativeRefusal.Child);
      }
   }
}
