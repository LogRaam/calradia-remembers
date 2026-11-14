// Code written by Gabriel Mailhot, 14/11/2026.
// What CharacterOverrideSet.Overlay and CampaignFolderName decide: a campaign's own character entries, read from
// Custom/campaigns/<unique game id>/character_overrides.json, laid over the shared file. Bhelogan (Discord, 14/11/2026):
// "your overrides are global, but runtime ids (CharacterObject_N) are per save, so characters from one playthrough leaked
// into the next". If this breaks, a character written for one campaign is handed to whoever holds that id in the next, or
// a campaign's entry fails to replace the shared one it was written to replace.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CampaignOverrideOverlayTests
   {
      // THE REPORT: a character spawned in this campaign (a runtime id) is written for this campaign alone.
      [Test]
      public void GIVEN_a_campaign_entry_for_a_runtime_id_WHEN_laid_over_the_shared_file_THEN_that_hero_resolves_to_it()
      {
         var shared = new CharacterOverrideSet(new[] {new CharacterOverride {Id = "lord_1_1", Backstory = "shared"}});

         CharacterOverrideSet set = shared.Overlay(new[] {new CharacterOverride {Id = "CharacterObject_2526", Backstory = "this campaign"}});

         set.Resolve("CharacterObject_2526", "Yahya")?.Backstory.Should().Be("this campaign");
         set.Resolve("lord_1_1", "Derthert")?.Backstory.Should().Be("shared");
      }

      // STAKE: a campaign entry for a hero the shared file also writes wins, without leaving two entries behind.
      [Test]
      public void GIVEN_a_campaign_entry_with_the_same_id_WHEN_laid_over_THEN_it_replaces_the_shared_one()
      {
         var shared = new CharacterOverrideSet(new[] {new CharacterOverride {Id = "lord_1_1", Backstory = "shared"}});

         CharacterOverrideSet set = shared.Overlay(new[] {new CharacterOverride {Id = "lord_1_1", Backstory = "this campaign"}});

         set.Count.Should().Be(1);
         set.Resolve("lord_1_1", "Derthert")?.Backstory.Should().Be("this campaign");
      }

      // STAKE: a campaign with no file of its own keeps the shared characters exactly.
      [Test]
      public void GIVEN_no_campaign_entries_WHEN_laid_over_THEN_the_shared_set_is_unchanged()
      {
         var shared = new CharacterOverrideSet(new[] {new CharacterOverride {Id = "lord_1_1", Backstory = "shared"}});

         shared.Overlay(null).Count.Should().Be(1);
         shared.Overlay(new List<CharacterOverride>()).Resolve("lord_1_1", "Derthert")?.Backstory.Should().Be("shared");
      }

      // STAKE: the campaign's folder is a plain folder name whatever the id holds.
      [Test]
      public void GIVEN_a_unique_game_id_WHEN_its_folder_is_named_THEN_only_safe_characters_remain()
      {
         CharacterOverrideSet.CampaignFolderName("aB3-x_9/..:z").Should().Be("aB3-x_9____z");
      }

      // STAKE: outside a campaign there is no campaign folder to read.
      [Test]
      public void GIVEN_no_game_id_WHEN_its_folder_is_named_THEN_there_is_none()
      {
         CharacterOverrideSet.CampaignFolderName(null).Should().BeNull();
         CharacterOverrideSet.CampaignFolderName("  ").Should().BeNull();
      }
   }
}
