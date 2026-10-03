// Code written by Gabriel Mailhot, 03/10/2026.
// Found answering tashmetu (02/10/2026), fixed at Gabriel's word ("corrige aussi les traits a +-2"): the persona
// catalog matched a trait only at exactly -1 or +1, so a hero whose trait sat at -2 or +2, the game's strongest, met no
// archetype at all and got the generic "figure of contradictions" line. The most marked characters were the least
// described. The catalog now reads a trait's DIRECTION, and what is extreme is said in a sentence of its own. If these
// rules are wrong, the strongest natures fall back to a shrug again, or a mere leaning is described as an extreme.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PersonaTraitIntensityTests
   {
      // A -2 trait leans the same way as a -1: it must meet the same archetype rather than none.
      [TestCase(-2, -1)]
      [TestCase(-1, -1)]
      [TestCase(0, 0)]
      [TestCase(1, 1)]
      [TestCase(2, 1)]
      public void GIVEN_a_trait_level_WHEN_the_archetype_is_looked_up_THEN_it_is_read_by_its_direction(int level, int direction)
      {
         PersonaTraitIntensity.Direction(level).Should().Be(direction);
      }

      // Leanings only: nothing is called extreme, so the archetype speaks alone.
      [Test]
      public void GIVEN_only_leanings_WHEN_described_THEN_nothing_is_called_extreme()
      {
         PersonaTraitIntensity.Clause(new PersonaTraitLevels {Honor = 1, Mercy = -1, Valor = 1}).Should().BeEmpty();
      }

      // A trait at the game's strongest is said to be so, and in the right direction.
      [Test]
      public void GIVEN_mercy_at_its_lowest_WHEN_described_THEN_the_want_of_mercy_is_called_extreme()
      {
         PersonaTraitIntensity.Clause(new PersonaTraitLevels {Mercy = -2}).Should().Contain("mercy").And.Contain("extreme");
      }

      // Extreme is still never relish: the strongest trait says what they do, not that they enjoy it (the 02/10 report).
      [Test]
      public void GIVEN_every_trait_at_its_lowest_WHEN_described_THEN_nothing_speaks_of_pleasure_in_suffering()
      {
         PersonaTraitIntensity.Clause(new PersonaTraitLevels {Honor = -2, Mercy = -2, Valor = -2, Generosity = -2, Calculating = -2})
                              .Should().NotContainAny("pleasure", "enjoy", "relish", "sadis");
      }

      // Two extremes, both named: neither hides the other.
      [Test]
      public void GIVEN_two_extremes_WHEN_described_THEN_both_are_named()
      {
         string clause = PersonaTraitIntensity.Clause(new PersonaTraitLevels {Honor = 2, Valor = 2});

         clause.Should().Contain("word").And.Contain("courage");
      }
   }
}
