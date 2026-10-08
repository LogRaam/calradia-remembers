// Code written by Gabriel Mailhot, 08/10/2026.
// Two things a character could not know, from Windmolen (Nexus, 08/10/2026), ratified by Gabriel:
// - "I was captured after a battle with an enemy lord. Both of us commanded a party of over 100 men. During the
//   conversations my captor insisted that I was caught while crossing his lands alone and wanted to know who I was
//   spying for. He did not remember the battle. This has happened before." A lord met for the first time on the field
//   kept no memory of it, and nothing told him how he took the player: the model filled the gap with a spy.
// - "he took a second wife, who happened to be his first spouse's mother. The first spouse does not know": no partner
//   was told who the player's other partners are, and a family line said "Mother: X" without saying X was one of them.
//   Gabriel (08/10/2026): such a match stays possible, but everyone knows.
// If these break, a captor invents how he took the player, or a wife writes of her mother as if she were not her rival.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CaptureAndPartnersTests
   {
      // Windmolen's captor: the battle, where it was, the men the player led.
      [Test]
      public void GIVEN_a_player_taken_after_a_battle_WHEN_the_captor_remembers_THEN_he_knows_the_battle_the_place_and_the_men()
      {
         string m = CaptureMemoryText.Compose(new CaptureFacts {PlayerName = "Can", Place = "Pravend", AfterBattle = true, PlayerMen = 112});

         m.Should().Contain("Can").And.Contain("Pravend").And.Contain("112").And.Contain("prisoner");
         m.Should().Contain("battle");
      }

      // Taken without a fight (alone, or a surrender): no battle is claimed.
      [Test]
      public void GIVEN_a_player_taken_without_a_battle_WHEN_remembered_THEN_no_battle_is_claimed()
      {
         string m = CaptureMemoryText.Compose(new CaptureFacts {PlayerName = "Can", Place = "Pravend", AfterBattle = false});

         m.Should().Contain("prisoner").And.NotContain("battle");
      }

      // A lone rider beaten is not said to have led an army.
      [Test]
      public void GIVEN_a_player_who_led_almost_no_one_WHEN_remembered_THEN_no_host_is_claimed()
      {
         CaptureMemoryText.Compose(new CaptureFacts {PlayerName = "Can", AfterBattle = true, PlayerMen = 1}).Should().NotContain("men");
      }

      // Windmolen's first wife: her mother is the player's consort, and the family line says so.
      [Test]
      public void GIVEN_a_relative_who_is_also_the_players_partner_WHEN_the_family_line_is_written_THEN_it_says_so()
      {
         PlayerPartnersText.KinNote(isSpouse: false).Should().Contain("also the player's consort");
         PlayerPartnersText.KinNote(isSpouse: true).Should().Contain("also the player's spouse");
      }

      // And she knows the player's other partners by name, with the tie that binds them to her.
      [Test]
      public void GIVEN_a_partner_of_the_player_WHEN_told_THEN_they_know_the_others_by_name_and_tie()
      {
         string line = PlayerPartnersText.OtherPartners(new List<PartnerFacts> {
            new() {Name = "Rhagaea", IsSpouse = false, KinToSubject = "your mother"},
            new() {Name = "Ira", IsSpouse = true}
         });

         line.Should().Contain("Rhagaea").And.Contain("your mother").And.Contain("consort");
         line.Should().Contain("Ira").And.Contain("spouse");
      }

      // The only partner has no others to know of.
      [Test]
      public void GIVEN_no_other_partner_WHEN_told_THEN_nothing_is_said()
      {
         PlayerPartnersText.OtherPartners(new List<PartnerFacts>()).Should().BeNull();
      }
   }
}
