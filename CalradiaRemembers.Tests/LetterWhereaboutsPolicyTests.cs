// Code written by Gabriel Mailhot, 30/09/2026.
// Where a letter says its writer and the player are. Lordfadooboo (Nexus, 27/09/2026, on 2.6.5): a wanderer he had
// beaten in a tournament wrote of him "riding in the field with THEIR retinue", though he rode alone and a wanderer has
// no soldiers. The writer's phrase is addressed to the writer ("with your own retinue") and was reused for the player,
// so the letter told the writer that the player marched with the writer's own retinue. If the player's phrase ever
// borrows that "your" again, the same letter comes back; if it invents a company, a lone player is given one.

#region

using CalradiaRemembers.Logic.Letters;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class LetterWhereaboutsPolicyTests
   {
      // Lordfadooboo's case: the player alone in the field.
      [Test]
      public void GIVEN_the_player_riding_alone_WHEN_the_letter_places_them_THEN_they_ride_alone_and_nobody_elses_retinue_is_named()
      {
         string where = LetterWhereaboutsPolicy.ForPlayer(new WhereaboutsFacts {InParty = true, MenInParty = 1});

         where.Should().Contain("riding alone");
         where.Should().NotContain("your");
         where.Should().NotContain("retinue");
      }

      // A player with men of their own leads their own company, still never "your" (the writer's).
      [Test]
      public void GIVEN_the_player_with_a_company_WHEN_placed_THEN_it_is_their_own_company()
      {
         string where = LetterWhereaboutsPolicy.ForPlayer(new WhereaboutsFacts {InParty = true, MenInParty = 40});

         where.Should().Contain("their own company");
         where.Should().NotContain("your");
      }

      // In a town, or with an army, both read the same for writer and player.
      [Test]
      public void GIVEN_someone_in_a_town_WHEN_placed_THEN_the_town_is_named_for_either()
      {
         LetterWhereaboutsPolicy.ForPlayer(new WhereaboutsFacts {SettlementName = "Pravend", InParty = true}).Should().Be("within Pravend");
         LetterWhereaboutsPolicy.ForWriter(new WhereaboutsFacts {SettlementName = "Pravend"}).Should().Be("within Pravend");
      }

      // The writer's own phrase is unchanged: it is addressed to the writer, so "your own retinue" is theirs.
      [Test]
      public void GIVEN_the_writer_on_the_march_WHEN_placed_THEN_it_is_the_writers_own_retinue()
      {
         LetterWhereaboutsPolicy.ForWriter(new WhereaboutsFacts {InParty = true, MenInParty = 30}).Should().Be("in the field, on the march with your own retinue");
      }
   }
}
