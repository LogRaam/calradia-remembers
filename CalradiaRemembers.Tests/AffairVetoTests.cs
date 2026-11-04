// Code written by Gabriel Mailhot, 03/11/2026.
// The same veto book that guards a conception guards a hidden affair between lords. tashmetu (Nexus, 03/11/2026), author of
// Romance & Seduction: "R&S now reads CR's list and never starts an affair for a lord who's already in one of yours. The
// reverse isn't covered yet." Gabriel (03/11/2026) wanted CR to depend on no mod, so the other mod registers a veto and CR
// asks it before any affair. If this is wrong, one lord carries an affair in each mod, or a broken veto stops every affair.

#region

using System;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AffairVetoTests
   {
      // Stands for another mod's own record: these lords already have an affair there.
      private static string AlreadyInOne(string married, string lover) => married == "lord_in_rs" || lover == "lover_in_rs" ? "already in an affair" : null;

      // STAKE: tashmetu's case: a lord already in an R&S affair, on either side, is never given a second one by CR.
      [Test]
      public void GIVEN_a_lord_already_in_another_mods_affair_WHEN_CR_would_begin_one_THEN_it_is_refused_on_either_side()
      {
         var book = new PairVetoBook<string>();
         book.Register("Romance & Seduction", AlreadyInOne);

         book.Ask("lord_in_rs", "free_lady").Refused.Should().BeTrue();
         book.Ask("married_lord", "lover_in_rs").Refused.Should().BeTrue();
         book.Ask("married_lord", "free_lady").Refused.Should().BeFalse();
      }

      // STAKE: Gabriel: CR depends on no mod. Without the other mod, or with it broken, CR's affairs go on as before.
      [Test]
      public void GIVEN_no_veto_or_a_broken_one_WHEN_CR_would_begin_an_affair_THEN_it_is_allowed()
      {
         new PairVetoBook<string>().Ask("married_lord", "free_lady").Refused.Should().BeFalse();

         var book = new PairVetoBook<string>();
         book.Register("Broken Mod", (m, l) => throw new InvalidOperationException("boom"));
         book.Ask("married_lord", "free_lady").Refused.Should().BeFalse();
      }
   }
}
