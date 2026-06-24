// Code written by Gabriel Mailhot, 23/06/2026.
// Protects the setting-override resolution — each field falls back to its Calradia default when the
// override leaves it blank, so an unmodified install is unchanged and an overhaul pack only sets what it needs.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SettingProfileTests
   {
      [Test]
      public void GIVEN_no_override_WHEN_resolved_THEN_the_full_calradia_defaults_apply()
      {
         //Arrange/Act
         var (name, adj, cultures) = SettingProfileDefaults.Resolve(null);

         //Assert
         name.Should().Be("Calradia");
         adj.Should().Be("Calradian");
         cultures.Should().BeEquivalentTo(SettingProfileDefaults.PatriarchalCultures);
      }

      [Test]
      public void GIVEN_only_the_world_name_overridden_WHEN_resolved_THEN_the_rest_stays_default()
      {
         //Arrange
         var profile = new SettingProfile { WorldName = "the Old World" };

         //Act
         var (name, adj, cultures) = SettingProfileDefaults.Resolve(profile);

         //Assert
         name.Should().Be("the Old World");
         adj.Should().Be("Calradian", "an unspecified adjective keeps the default");
         cultures.Should().BeEquivalentTo(SettingProfileDefaults.PatriarchalCultures);
      }

      [Test]
      public void GIVEN_a_full_overhaul_override_WHEN_resolved_THEN_every_field_is_taken()
      {
         //Arrange
         var profile = new SettingProfile
         {
            WorldName = "the Old World",
            WorldAdjective = "Imperial",
            PatriarchalCultures = new List<string> { "Bretonnia", "Empire" }
         };

         //Act
         var (name, adj, cultures) = SettingProfileDefaults.Resolve(profile);

         //Assert
         name.Should().Be("the Old World");
         adj.Should().Be("Imperial");
         cultures.Should().BeEquivalentTo("Bretonnia", "Empire");
      }

      [Test]
      public void GIVEN_blank_or_empty_fields_WHEN_resolved_THEN_they_fall_back_to_defaults()
      {
         //Arrange — whitespace name, an empty culture list, blank entries.
         var profile = new SettingProfile
         {
            WorldName = "   ",
            PatriarchalCultures = new List<string> { "  ", "" }
         };

         //Act
         var (name, _, cultures) = SettingProfileDefaults.Resolve(profile);

         //Assert
         name.Should().Be("Calradia");
         cultures.Should().BeEquivalentTo(SettingProfileDefaults.PatriarchalCultures);
      }
   }
}
