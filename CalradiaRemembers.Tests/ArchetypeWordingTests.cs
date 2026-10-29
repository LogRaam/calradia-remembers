// Code written by Gabriel Mailhot, 28/10/2026.
// ArchetypeWording words a character archetype for its bearer's station. The archetypes were written for lords and served
// to companions and notables unchanged; avalanche24 (Nexus, 28/10/2026) found companions talking "way above their
// station". If this is wrong, a hireling is told "A formidable lord." again, or a lord loses the words of his own rank.

#region

using CalradiaRemembers.Logic.Character;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ArchetypeWordingTests
   {
      private const string Strategic = "The Strategic Warrior. Brave, principled, and calculating. A formidable lord.";

      // STAKE: a companion keeps the nature of the archetype, without the lordship she does not have.
      [Test]
      public void GIVEN_a_commoner_WHEN_the_archetype_is_worded_THEN_it_names_no_lordship()
      {
         string worded = ArchetypeWording.ForStation(Strategic, false);

         worded.Should().Be("The Strategic Warrior. Brave, principled, and calculating. A formidable fighter.");
         worded.Should().NotContainEquivalentOf("lord");
      }

      // STAKE: a lord's archetype is his own and stays word for word.
      [Test]
      public void GIVEN_a_noble_WHEN_the_archetype_is_worded_THEN_it_is_unchanged()
      {
         ArchetypeWording.ForStation(Strategic, true).Should().Be(Strategic);
      }

      // STAKE: a save made before this wording is found and worded again, so an existing companion stops being a lord.
      [Test]
      public void GIVEN_a_commoner_archetype_stored_before_WHEN_checked_THEN_it_is_found_lordly_and_once_worded_it_is_not()
      {
         ArchetypeWording.HasLordlyWords(Strategic).Should().BeTrue();
         ArchetypeWording.HasLordlyWords(ArchetypeWording.ForStation(Strategic, false)).Should().BeFalse();
      }
   }
}
