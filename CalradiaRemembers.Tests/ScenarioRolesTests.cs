// Code written by Gabriel Mailhot, 12/10/2026.
// The tale-scenario bridge (docs/design/tale-scenario-bridge; Gabriel, 12/10/2026): a scenario rule answering a tale
// names the tale's people by role ({husband}, {giver}, {player}), because they are chosen when the tale begins, never
// known when the pack is written. And a husband who does not rule "presses his king toward war" ("rancune et pousse son
// roi vers la guerre"), the king deciding as the leader always does. If these are wrong, a rule names a stranger, the
// realm's news reads "{husband}" to the player, or a king goes to war for any vassal's grievance, or never.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ScenarioRolesTests
   {
      // A role is written in braces, as in the tale itself; anything else stays an id or a name, read as before.
      [Test]
      public void GIVEN_a_reference_in_braces_WHEN_read_THEN_it_names_a_tale_role_and_anything_else_does_not()
      {
         ScenarioRoleRef.TryRole("{husband}", out string role).Should().BeTrue();
         role.Should().Be("husband");
         ScenarioRoleRef.TryRole(" {Giver} ", out string giver).Should().BeTrue();
         giver.Should().Be("giver");
         ScenarioRoleRef.TryRole("lord_1_14", out _).Should().BeFalse();
         ScenarioRoleRef.TryRole("{}", out _).Should().BeFalse();
      }

      // The news everyone hears names the people, never the braces (DESIGN.md: "everyone knows" why the war began).
      [Test]
      public void GIVEN_a_rule_message_naming_roles_WHEN_told_THEN_the_names_are_filled_in()
      {
         var names = new Dictionary<string, string> {["husband"] = "Ortysia's King", ["giver"] = "Queen Ilena", ["player"] = "Arwa"};

         ScenarioRoleText.Fill("{husband}'s realm goes to war over {giver}, taken by {player}.", names)
            .Should().Be("Ortysia's King's realm goes to war over Queen Ilena, taken by Arwa.");
         ScenarioRoleText.Fill("Nothing to fill.", names).Should().Be("Nothing to fill.");
         ScenarioRoleText.Fill("{nobody} stays as written.", names).Should().Be("{nobody} stays as written.");
      }

      // ── A husband presses his king toward war ──

      // A husband who rules decides himself: the insult is his and so is the realm.
      [Test]
      public void GIVEN_the_husband_rules_WHEN_he_presses_for_war_THEN_war_is_declared()
      {
         WarPressPolicy.KingDeclares(new WarPressFacts {PresserRules = true}).Should().BeTrue();
      }

      // Gabriel: the king still decides. A vassal he holds dear, at peace elsewhere, against a realm he can face: war.
      [Test]
      public void GIVEN_a_king_who_holds_the_husband_dear_and_can_fight_WHEN_pressed_THEN_he_declares()
      {
         WarPressPolicy.KingDeclares(new WarPressFacts {
            KingRegardForPresser = WarPressPolicy.RegardToHeed, KingOpenWars = 0, StrengthRatio = 1.0
         }).Should().BeTrue();
      }

      // A king who does not care for him, one already stretched on other fronts, or one far weaker, declines.
      [Test]
      public void GIVEN_what_a_leader_weighs_WHEN_it_goes_against_war_THEN_the_king_declines()
      {
         var fit = new WarPressFacts {KingRegardForPresser = 50, KingOpenWars = 0, StrengthRatio = 1.0};

         WarPressPolicy.KingDeclares(new WarPressFacts {KingRegardForPresser = WarPressPolicy.RegardToHeed - 1, StrengthRatio = 1.0}).Should().BeFalse();
         WarPressPolicy.KingDeclares(new WarPressFacts {KingRegardForPresser = 50, KingOpenWars = WarPressPolicy.MostOpenWars + 1, StrengthRatio = 1.0}).Should().BeFalse();
         WarPressPolicy.KingDeclares(new WarPressFacts {KingRegardForPresser = 50, StrengthRatio = WarPressPolicy.LeastStrengthRatio - 0.01}).Should().BeFalse();
         WarPressPolicy.KingDeclares(fit).Should().BeTrue();
      }
   }
}
