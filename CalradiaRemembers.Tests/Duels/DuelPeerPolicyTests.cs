// Code written by Gabriel Mailhot, 28/10/2026.
// DuelPeerPolicy decides who is of the standing a duel of honour asks; its answer gates both the challenge the prompt
// teaches and the one the bridge stages. avalanche24 (Nexus, 28/10/2026) was called out by a landowner with no skill at
// arms and by a companion hired hours before. Gabriel (28/10/2026): the duel is noble, the street brawl a settling of
// scores, and a companion only brawls. If this is wrong, commoners and hirelings demand satisfaction of their lord again.

#region

using CalradiaRemembers.Logic.Duels;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Duels
{
   [TestFixture]
   public sealed class DuelPeerPolicyTests
   {
      // STAKE: a lord who is insulted still demands satisfaction: the duel stays the nobles' affair.
      [Test]
      public void GIVEN_a_noble_WHEN_judged_THEN_they_may_duel_the_player()
      {
         DuelPeerPolicy.IsHonourPeer(new DuelPeerFacts {IsNoble = true}).Should().BeTrue();
      }

      // STAKE: avalanche24's companion, hired hours before, challenged him for being told to follow orders; a companion
      // only brawls.
      [Test]
      public void GIVEN_a_hired_companion_WHEN_judged_THEN_no_duel_of_honour()
      {
         DuelPeerPolicy.IsHonourPeer(new DuelPeerFacts {IsPlayerCompanion = true}).Should().BeFalse();
      }

      // STAKE: avalanche24's landowner challenged a master of arms for saying no; a notable has no duel to call.
      [Test]
      public void GIVEN_a_notable_WHEN_judged_THEN_no_duel_of_honour()
      {
         DuelPeerPolicy.IsHonourPeer(new DuelPeerFacts {IsNotable = true}).Should().BeFalse();
      }
   }
}
