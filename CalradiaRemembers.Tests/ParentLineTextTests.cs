// Code written by Gabriel Mailhot, 19/11/2026.
// ParentLineText: a living parent in a character's family line says so. Eisendrache2 (Nexus, 19/11/2026, Gemma 3 12B):
// lords said their fathers fell at Pendraic while the fathers were alive and well. If this is wrong, a small model reads
// "Father: Name (age 61)" without seeing that he lives, and buries him.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ParentLineTextTests
   {
      // STAKE: a lord whose father lives is told so in plain words, and does not speak of his death.
      [Test]
      public void GIVEN_a_living_father_WHEN_his_line_is_written_THEN_it_says_he_is_alive_with_his_age()
      {
         ParentLineText.Living("Derthert", 61).Should().Be("Derthert (alive, age 61)");
      }
   }
}
