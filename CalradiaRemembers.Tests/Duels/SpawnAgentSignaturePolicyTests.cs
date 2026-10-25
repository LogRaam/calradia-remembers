// Code written by Gabriel Mailhot, 25/10/2026.
// SpawnAgentSignaturePolicy picks the Mission.SpawnAgent overload that spawns a duel or escape fighter from an
// AgentBuildData alone, on 1.4.8 (AgentBuildData, bool) and on the 1.5.5 beta (AgentBuildData, bool, Equipment,
// ItemObject), as tashmetu reported (Nexus, 25/10/2026). If it picks wrong, a duel or a captive's escape throws the
// moment its first fighter is spawned, on one version or the other.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Duels;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Duels
{
   [TestFixture]
   public sealed class SpawnAgentSignaturePolicyTests
   {
      private static SpawnMethodShape Spawn(params (string Type, bool Optional)[] parameters)
      {
         var types = new List<string>();
         var optional = new List<bool>();
         foreach ((string type, bool opt) in parameters)
         {
            types.Add(type);
            optional.Add(opt);
         }

         return new SpawnMethodShape {Name = "SpawnAgent", IsPublic = true, IsStatic = false, ParameterTypes = types, ParameterOptional = optional};
      }

      private static readonly SpawnMethodShape Game148 = Spawn(("AgentBuildData", false), ("Boolean", true));

      private static readonly SpawnMethodShape Game155 =
         Spawn(("AgentBuildData", false), ("Boolean", true), ("Equipment", true), ("ItemObject", true));

      // STAKE: on today's game (1.4.8) the duel must keep calling the very method it always called.
      [Test]
      public void GIVEN_the_1_4_8_overload_WHEN_picking_THEN_it_is_chosen()
      {
         SpawnAgentSignaturePolicy.Pick(new[] {Game148}).Should().Be(0);
      }

      // STAKE (tashmetu): on the 1.5.5 beta the two-parameter method is gone; the four-parameter one must be found,
      // or the first fighter of a duel throws MissingMethodException.
      [Test]
      public void GIVEN_the_1_5_5_overload_with_two_more_optional_parameters_WHEN_picking_THEN_it_is_chosen()
      {
         SpawnAgentSignaturePolicy.Pick(new[] {Game155}).Should().Be(0);
      }

      // STAKE: an overload that needs something CR cannot give (a required second parameter, another first parameter,
      // a static or internal one) must never be called with blanks.
      [Test]
      public void GIVEN_overloads_CR_cannot_fill_WHEN_picking_THEN_none_is_chosen()
      {
         SpawnMethodShape required = Spawn(("AgentBuildData", false), ("Equipment", false));
         SpawnMethodShape otherFirst = Spawn(("Agent", false), ("Boolean", true));
         SpawnMethodShape internalOne = Spawn(("AgentBuildData", false));
         internalOne = new SpawnMethodShape {Name = "SpawnAgent", IsPublic = false, ParameterTypes = internalOne.ParameterTypes, ParameterOptional = internalOne.ParameterOptional};
         SpawnMethodShape staticOne = new SpawnMethodShape {Name = "SpawnAgent", IsPublic = true, IsStatic = true, ParameterTypes = Game148.ParameterTypes, ParameterOptional = Game148.ParameterOptional};
         SpawnMethodShape otherName = new SpawnMethodShape {Name = "SpawnMonster", IsPublic = true, ParameterTypes = Game148.ParameterTypes, ParameterOptional = Game148.ParameterOptional};

         SpawnAgentSignaturePolicy.Pick(new[] {required, otherFirst, internalOne, staticOne, otherName}).Should().Be(-1);
         SpawnAgentSignaturePolicy.Pick(null).Should().Be(-1);
      }

      // STAKE: reflection lists methods in no promised order; with two that fit, the call must not change from one
      // run to the next, so the shortest wins wherever it stands.
      [Test]
      public void GIVEN_two_overloads_that_fit_in_either_order_WHEN_picking_THEN_the_one_with_fewer_parameters_wins()
      {
         SpawnAgentSignaturePolicy.Pick(new[] {Game155, Game148}).Should().Be(1);
         SpawnAgentSignaturePolicy.Pick(new[] {Game148, Game155}).Should().Be(0);
      }
   }
}
