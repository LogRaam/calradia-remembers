// Code written by Gabriel Mailhot, 07/10/2026.
// The house a character was born into. Maifailun (Nexus bug report, 07/10/2026, on 2.7.2): "Lidia was in Western Empire
// before, she recognizes her father's name but LLM cannot connect the dots." Asked what her clan was before her
// marriage, Lidia answered "I was born to Takhi; I did not marry into it. My father is Eustorgios, and whatever his own
// house, it did not change mine." Her family line named her father and nothing else: neither his house nor hers before
// the wedding. A character who married into another house now knows the one they were born into.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class BirthHouseTests
   {
      // Lidia: born into her father's imperial house, wed into the Takhi.
      [Test]
      public void GIVEN_a_lady_who_married_into_another_house_WHEN_her_family_is_told_THEN_she_knows_the_house_she_was_born_into()
      {
         BirthHouseText.Line(new BirthHouseFacts {CurrentClan = "Takhi", BirthClan = "Comnos", BirthRealm = "the Western Empire", ByMarriage = true})
                       .Should().Be("Born into the Comnos (of the Western Empire); joined the Takhi by marriage.");
      }

      // Born and still in the same house: nothing to say, the line stays as it was.
      [Test]
      public void GIVEN_a_character_still_in_the_house_they_were_born_into_WHEN_told_THEN_nothing_is_added()
      {
         BirthHouseText.Line(new BirthHouseFacts {CurrentClan = "Takhi", BirthClan = "Takhi"}).Should().BeNull();
         BirthHouseText.Line(new BirthHouseFacts {CurrentClan = "Takhi", BirthClan = null}).Should().BeNull();
      }

      // A realm of the same name as the house, or none known, is left out rather than repeated.
      [Test]
      public void GIVEN_no_realm_known_for_the_birth_house_WHEN_told_THEN_the_house_alone_is_named()
      {
         BirthHouseText.Line(new BirthHouseFacts {CurrentClan = "Takhi", BirthClan = "Comnos", ByMarriage = true})
                       .Should().Be("Born into the Comnos; joined the Takhi by marriage.");
      }

      // Someone who changed houses unwed is never told it was a marriage: that would be a past invented for them.
      [Test]
      public void GIVEN_a_character_who_changed_houses_unwed_WHEN_told_THEN_no_marriage_is_claimed()
      {
         BirthHouseText.Line(new BirthHouseFacts {CurrentClan = "Takhi", BirthClan = "Comnos", ByMarriage = false})
                       .Should().Be("Born into the Comnos; now of the Takhi.");
      }
   }
}
