// Code written by Gabriel Mailhot, 21/11/2026.
// OwnCommandPolicy decides how many men a character may call their own in the prompt's YOUR OWN COMMAND section.
// Wrong, and a companion is told the player's men are hers (JackJack, Discord 21/11/2026: Isilflaed "commands a company
// of a few dozen soldiers" in the same prompt that says the player is her captain), or a lord loses his strength.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class OwnCommandPolicyTests
   {
      // STAKE: JackJack's report. A companion riding in the player's party was handed the player's men as her own,
      // contradicting the line that says she never commands them.
      [Test]
      public void GIVEN_a_companion_riding_in_the_players_party_WHEN_resolving_THEN_she_commands_nobody()
      {
         OwnCommandPolicy.Resolve(new OwnCommandFacts {LeadsTheParty = false, PartyTroops = 40}).Should().Be(0);
      }

      // STAKE: the same mistake inside a lord's party: his wife riding with him would claim his men, even his army.
      [Test]
      public void GIVEN_a_member_of_a_lords_party_that_leads_an_army_WHEN_resolving_THEN_she_commands_nobody()
      {
         OwnCommandPolicy.Resolve(new OwnCommandFacts {LeadsTheParty = false, PartyTroops = 120, LeadsArmy = true, LedArmyTroops = 900})
                         .Should().Be(0);
      }

      // STAKE: the original report behind the section: a lord asked how many men he leads must still know.
      [Test]
      public void GIVEN_a_lord_leading_his_own_party_WHEN_resolving_THEN_he_commands_its_men()
      {
         OwnCommandPolicy.Resolve(new OwnCommandFacts {LeadsTheParty = true, PartyTroops = 60}).Should().Be(60);
      }

      // STAKE: a lord at the head of an army answers for the whole army, not his own party alone.
      [Test]
      public void GIVEN_a_lord_leading_an_army_WHEN_resolving_THEN_he_commands_the_whole_army()
      {
         OwnCommandPolicy.Resolve(new OwnCommandFacts {LeadsTheParty = true, PartyTroops = 60, LeadsArmy = true, LedArmyTroops = 900})
                         .Should().Be(900);
      }

      // STAKE: a captured leader commands nobody from his cell, whatever his party still holds.
      [Test]
      public void GIVEN_a_captive_party_leader_WHEN_resolving_THEN_he_commands_nobody()
      {
         OwnCommandPolicy.Resolve(new OwnCommandFacts {IsCaptive = true, LeadsTheParty = true, PartyTroops = 60}).Should().Be(0);
      }
   }
}
