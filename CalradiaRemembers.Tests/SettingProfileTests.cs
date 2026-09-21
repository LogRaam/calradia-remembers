// Code written by Gabriel Mailhot, 23/06/2026.
// Protects the setting-override resolution — each field falls back to its Calradia default when the
// override leaves it blank, so an unmodified install is unchanged and an overhaul pack only sets what it needs.
//
// The resolved (name, adjective, cultures) feeds PromptLore, which PromptBuilder interpolates into dozens of
// prompt sentences (world flavor, marriage law, the patriarchal-culture opening-tone gate); SettingProfileLoader
// applies it at session launch and on cr.reload_lore. A wrong fallback here either leaks "Calradia" into an
// overhaul pack's prompt, or leaks a blank/whitespace value into those same interpolated sentences.

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
      // The floor the whole feature stands on: a stock install (no setting.json, or the file absent) must
      // behave exactly like Calradia always did, or every existing playthrough's prompt text changes silently.
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

      // An overhaul pack should be able to set just the world name without also being forced to redeclare the
      // adjective and culture list, or partial overrides become impractical to author.
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

      // Confirms a complete overhaul (e.g. "The Old Realms") actually replaces every field PromptBuilder
      // interpolates, not just the world name: the adjective and the patriarchal-culture gate too.
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

      // A setting.json with present-but-empty/whitespace fields (an easy authoring mistake) must not leak
      // blank text into the sentences that interpolate WorldName/PatriarchalCultures throughout the prompt.
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
   
      #region the calendar a total conversion may bring with it (21/09/2026)

      // A conversion could already rename the world; its YEAR was still 84 days long, so every "a season ago" and
      // "about 2 year(s) ago" the mod wrote was wrong for it. Absent fields keep Calradia, which is what every
      // existing install has.
      [Test]
      public void GIVEN_no_calendar_override_WHEN_resolved_THEN_it_is_Calradia()
      {
         SettingProfileDefaults.ResolveCalendar(null).Should().Be((21, 4));
         SettingProfileDefaults.ResolveCalendar(new SettingProfile()).Should().Be((21, 4));
      }

      [Test]
      public void GIVEN_a_world_with_its_own_calendar_WHEN_resolved_THEN_the_override_is_taken()
      {
         SettingProfile profile = new() {DaysPerSeason = 30, SeasonsPerYear = 12};

         SettingProfileDefaults.ResolveCalendar(profile).Should().Be((30, 12));
      }

      // Half an override is honest and must work: a world with longer seasons but still four of them.
      [Test]
      public void GIVEN_only_one_of_the_two_WHEN_resolved_THEN_the_other_keeps_its_default()
      {
         SettingProfileDefaults.ResolveCalendar(new SettingProfile {DaysPerSeason = 40}).Should().Be((40, 4));
         SettingProfileDefaults.ResolveCalendar(new SettingProfile {SeasonsPerYear = 2}).Should().Be((21, 2));
      }

      // A TYPO MUST NOT STOP TIME: every recency window in the mod divides by these.
      [Test]
      public void GIVEN_a_nonsense_calendar_WHEN_resolved_THEN_Calradia_is_kept()
      {
         SettingProfileDefaults.ResolveCalendar(new SettingProfile {DaysPerSeason = 0, SeasonsPerYear = -1})
                               .Should().Be((21, 4));
      }

      #endregion
}
}
