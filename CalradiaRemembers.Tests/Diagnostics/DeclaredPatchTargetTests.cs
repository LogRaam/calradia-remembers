// Code written by Gabriel Mailhot, 25/10/2026.
// DeclaredPatchTarget finds the method a game model runs under a name, taken from the type that declares it, for the
// edict patches (loyalty, security, prosperity, militia, food, tax) and the courier speed floor. tashmetu (Nexus,
// 25/10/2026): Harvest and Production's tax model inherits CalculateTownTax without overriding it, the method was taken
// as seen from that subclass, Harmony refused it, "So edicts never move taxes." If this picks wrong, an edict or the
// courier floor silently stops working under any mod whose model inherits the method.

#region

using System.Reflection;
using CalradiaRemembers.Logic.Diagnostics;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Diagnostics
{
   [TestFixture]
   public sealed class DeclaredPatchTargetTests
   {
      private class DefaultModel
      {
         public virtual int CalculateTownTax(int town) => town;
      }

      // Harvest and Production's shape: replaces the model, keeps the default tax.
      private sealed class InheritingModel : DefaultModel
      {
      }

      private sealed class OverridingModel : DefaultModel
      {
         public override int CalculateTownTax(int town) => town * 2;
      }

      private sealed class OverloadedModel
      {
         public int CalculateTownTax(int town) => town;

         public int CalculateTownTax(int town, bool explain) => town;
      }

      // STAKE: the reported bug. The tax edict must reach the method the inheriting model really runs, and Harmony
      // only patches it when taken from the type that declares it.
      [Test]
      public void GIVEN_a_model_that_inherits_the_method_WHEN_finding_it_THEN_the_base_declaration_is_patched()
      {
         MethodInfo found = DeclaredPatchTarget.Find(typeof(InheritingModel), "CalculateTownTax");

         found.DeclaringType.Should().Be(typeof(DefaultModel));
         found.ReflectedType.Should().Be(typeof(DefaultModel));
      }

      // STAKE: a model with its own override (War Sails' speed model) must keep its own method patched, or the
      // edict line or the courier floor sits on a method that model never runs.
      [Test]
      public void GIVEN_a_model_that_overrides_the_method_WHEN_finding_it_THEN_its_own_override_is_patched()
      {
         DeclaredPatchTarget.Find(typeof(OverridingModel), "CalculateTownTax").DeclaringType.Should().Be(typeof(OverridingModel));
      }

      // STAKE: a model with no such method must report it (the log warns the channel will not move), never guess.
      [Test]
      public void GIVEN_no_such_method_WHEN_finding_it_THEN_nothing_is_patched()
      {
         DeclaredPatchTarget.Find(typeof(InheritingModel), "CalculateFinalSpeed").Should().BeNull();
      }

      // STAKE: two overloads of one name cannot be told apart from the name alone; patching either could put the
      // edict on a method the game never asks for its number.
      [Test]
      public void GIVEN_two_overloads_of_the_name_WHEN_finding_it_THEN_nothing_is_patched()
      {
         DeclaredPatchTarget.Find(typeof(OverloadedModel), "CalculateTownTax").Should().BeNull();
      }
   }
}
