// Code written by Gabriel Mailhot, 22/11/2026.
// Whether a character's "what matters most" is held as written or left to the world. Bhelogan (Discord 21/11/2026): a
// Muh Prompts user set Isilflead the Huntress's value to Kin, and CR moved it back to Glory at the next refresh because
// she has no living kin ("its ground fell"). If this policy is wrong, a value written for the story is lost at every
// refresh, or a rolled value never follows the world again.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Character;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class MattersMostLockPolicyTests
   {
      // STAKE: Bhelogan's huntress. Pinned by Muh Prompts, her Kin is kept though no kin of hers lives.
      [Test]
      public void GIVEN_a_pinned_value_WHEN_refreshed_THEN_it_is_held()
      {
         MattersMostLockPolicy.Held(new MattersMostLockFacts {Current = MattersMost.Kin, Pinned = true}).Should().Be(MattersMost.Kin);
      }

      // STAKE: a rolled value still follows the world, as before.
      [Test]
      public void GIVEN_a_rolled_value_WHEN_refreshed_THEN_nothing_is_held()
      {
         MattersMostLockPolicy.Held(new MattersMostLockFacts {Current = MattersMost.Kin}).Should().BeNull();
      }

      // STAKE: a pin on nothing chosen yet would freeze a character at None forever.
      [Test]
      public void GIVEN_a_pin_on_no_value_WHEN_refreshed_THEN_nothing_is_held()
      {
         MattersMostLockPolicy.Held(new MattersMostLockFacts {Current = MattersMost.None, Pinned = true}).Should().BeNull();
      }

      // STAKE: the author's file wins over whatever is on the profile, pinned or not, and the log says where it came from.
      [Test]
      public void GIVEN_a_value_written_in_the_overrides_WHEN_refreshed_THEN_it_is_held_over_the_profile()
      {
         var facts = new MattersMostLockFacts {Current = MattersMost.Glory, Authored = MattersMost.Kin, Pinned = true};
         MattersMostLockPolicy.Held(facts).Should().Be(MattersMost.Kin);
         MattersMostLockPolicy.Why(facts).Should().Be("written in character_overrides.json");
         MattersMostLockPolicy.Why(new MattersMostLockFacts {Current = MattersMost.Kin, Pinned = true}).Should().Be("pinned");
      }

      // STAKE: the field reads as authors write it, and a typo is reported rather than silently dropped.
      [Test]
      public void GIVEN_an_override_WHEN_its_matters_most_is_parsed_THEN_names_are_read_and_typos_reported()
      {
         new CharacterOverride {Hero = "Isilflead", MattersMost = "kin"}.ParsedMattersMost.Should().Be(MattersMost.Kin);
         new CharacterOverride {Hero = "Isilflead", MattersMost = "House Honor"}.ParsedMattersMost.Should().Be(MattersMost.HouseHonor);
         new CharacterOverride {Hero = "Isilflead", MattersMost = "None"}.ParsedMattersMost.Should().BeNull();

         var typo = new CharacterOverride {Hero = "Isilflead", MattersMost = "Kinn"};
         typo.ParsedMattersMost.Should().BeNull();
         typo.AuthoringProblems().Should().ContainSingle(p => p.Contains("mattersMost \"Kinn\" is not recognized"));
         typo.HasAnyAuthoredContent.Should().BeTrue();
      }
   }
}
