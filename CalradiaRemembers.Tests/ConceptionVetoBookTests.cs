// Code written by Gabriel Mailhot, 28/10/2026.
// ConceptionVetoBook holds the vetoes other mods register against a conception CR would create, and answers whether one
// refuses. tashmetu (Nexus, 28/10/2026): Traits of Calradia's Sterile character is told "you will have none", yet CR's
// own conception never asked. If this is wrong, a Sterile woman falls pregnant through CR against her own words, or one
// broken mod's veto stops every child in the campaign, or a mod reloading its veto ends up asked twice.

#region

using System;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ConceptionVetoBookTests
   {
      private static string Sterile(string mother, string father) => mother == "sterile_lady" || father == "sterile_lord" ? "Sterile" : null;

      // STAKE: with no mod refusing, CR conceives as it always did.
      [Test]
      public void GIVEN_no_veto_WHEN_asked_THEN_the_conception_is_allowed()
      {
         new ConceptionVetoBook<string>().Ask("lady", "player").Refused.Should().BeFalse();
      }

      // STAKE: tashmetu's Sterile woman: the mod that owns the trait refuses, and the log can say who and why.
      [Test]
      public void GIVEN_a_sterile_mother_WHEN_asked_THEN_the_veto_refuses_and_names_itself()
      {
         var book = new ConceptionVetoBook<string>();
         book.Register("Traits of Calradia", Sterile);

         ConceptionVetoVerdict v = book.Ask("sterile_lady", "player");

         v.Refused.Should().BeTrue();
         v.Source.Should().Be("Traits of Calradia");
         v.Reason.Should().Be("Sterile");
      }

      // STAKE: Sterile is said of men too: a sterile father fathers no child, and a fertile pair is never refused.
      [Test]
      public void GIVEN_the_father_sterile_or_neither_WHEN_asked_THEN_only_the_sterile_father_is_refused()
      {
         var book = new ConceptionVetoBook<string>();
         book.Register("Traits of Calradia", Sterile);

         book.Ask("lady", "sterile_lord").Refused.Should().BeTrue();
         book.Ask("lady", "player").Refused.Should().BeFalse();
      }

      // STAKE: a broken veto must not stop every child in the campaign; it is noted, and the next veto is still asked.
      [Test]
      public void GIVEN_a_veto_that_throws_WHEN_asked_THEN_it_never_refuses_and_the_next_is_asked()
      {
         var book = new ConceptionVetoBook<string>();
         book.Register("Broken Mod", (m, f) => throw new InvalidOperationException("boom"));
         book.Register("Traits of Calradia", Sterile);

         book.Ask("lady", "player").Refused.Should().BeFalse();
         ConceptionVetoVerdict v = book.Ask("sterile_lady", "player");
         v.Source.Should().Be("Traits of Calradia");
         v.Faults.Should().ContainSingle().Which.Should().StartWith("Broken Mod: InvalidOperationException");
      }

      // STAKE: a mod that registers again (a reload) replaces its veto rather than being asked twice; it can withdraw it.
      [Test]
      public void GIVEN_the_same_source_registered_twice_WHEN_counted_THEN_one_veto_and_it_can_be_withdrawn()
      {
         var book = new ConceptionVetoBook<string>();
         book.Register("Traits of Calradia", Sterile);
         book.Register("Traits of Calradia", (m, f) => null);

         book.Count.Should().Be(1);
         book.Ask("sterile_lady", "player").Refused.Should().BeFalse();
         book.Unregister("Traits of Calradia").Should().BeTrue();
         book.Count.Should().Be(0);
      }

      // STAKE: an answer of blank words is no refusal, or a mod returning "" would stop every child.
      [Test]
      public void GIVEN_a_veto_answering_blank_WHEN_asked_THEN_the_conception_is_allowed()
      {
         var book = new ConceptionVetoBook<string>();
         book.Register("Quiet Mod", (m, f) => "  ");

         book.Ask("lady", "player").Refused.Should().BeFalse();
      }
   }
}
