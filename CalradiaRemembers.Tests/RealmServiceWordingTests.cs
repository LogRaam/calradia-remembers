// Code written by Gabriel Mailhot, 25/09/2026.
// Documents how the world records the player's clan entering a realm's service. tashmetu, 25/09/2026: hiring on as a
// mercenary was recorded as an oath of fealty, the chronicle said the player "bent the knee", and a king told the
// hired sellsword "I've heard word you swore fealty". What characters hear here is what they will say back, so a
// wrong word is a wrong thing said to the player's face.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class RealmServiceWordingTests
   {
      // The report itself: a hired company swears nothing, so neither the news nor the chronicle may say it did.
      [Test]
      public void GIVEN_the_player_hires_on_as_a_mercenary_WHEN_the_world_records_it_THEN_no_oath_is_spoken_of()
      {
         RealmServiceLine line = RealmServiceWording.Joined(asMercenary: true, "Brennoc", "Regnum Valandorum", "Derthert");

         line.Summary.Should().Be("Brennoc took service with Regnum Valandorum as a mercenary captain.");
         (line.Summary + " " + line.ChronicleLine).Should().NotContainAny("fealty", "bent the knee", "swore");
         line.ChronicleLine.Should().Contain("Derthert").And.Contain("Regnum Valandorum");
      }

      // The oath is still an oath: joining as a vassal keeps the words it always had.
      [Test]
      public void GIVEN_the_player_swears_to_a_realm_WHEN_the_world_records_it_THEN_it_is_fealty()
      {
         RealmServiceLine line = RealmServiceWording.Joined(asMercenary: false, "Brennoc", "Regnum Valandorum", "Derthert");

         line.Summary.Should().Be("Brennoc swore fealty to Regnum Valandorum.");
         line.ChronicleLine.Should().Be("Brennoc bent the knee and swore fealty to Derthert of Regnum Valandorum");
      }

      // A contract is a smaller matter than an oath, and its start carries at least as far as its end (30), or the
      // world would hear a contract end that it never heard begin.
      [Test]
      public void GIVEN_a_mercenary_contract_WHEN_its_reach_is_set_THEN_it_carries_less_than_an_oath_and_no_less_than_its_end()
      {
         RealmServiceWording.MercenaryMagnitude.Should().BeLessThan(RealmServiceWording.VassalageMagnitude);
         RealmServiceWording.MercenaryMagnitude.Should().BeGreaterThanOrEqualTo(30);
      }
   }
}
