// Code written by Gabriel Mailhot, 10/10/2026.
// A mod takes back a memory it added (philip0105, Vivid World, 10/10/2026: a rumor forgotten or denied in his mod
// lingered in CR, so the two mods told different stories). If this rule is wrong, a forgotten rumor stays in the
// character's mouth, or one mod's removal erases another mod's memories.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class InjectedMemoryRemovalPolicyTests
   {
      // The ask itself: Vivid World takes back the rumor it added.
      [Test]
      public void GIVEN_a_memory_a_mod_added_WHEN_that_mod_removes_its_text_THEN_it_is_taken()
      {
         InjectedMemoryRemovalPolicy.Removes(new MemoryRemovalFacts {
            StoredSummary = "Word is the miller's son ran off.", StoredSourceMod = "VividWorld",
            RequestedSummary = "  Word is the miller's son ran off.  ", RequestedSourceMod = "vividworld"
         }).Should().BeTrue("the text is matched as AddMemory stored it, and the mod by name in any case");
      }

      // One mod never erases another's: the same words added by a different mod stay.
      [Test]
      public void GIVEN_a_memory_another_mod_added_WHEN_a_mod_removes_the_same_text_THEN_it_stays()
      {
         InjectedMemoryRemovalPolicy.Removes(new MemoryRemovalFacts {
            StoredSummary = "Word is the miller's son ran off.", StoredSourceMod = "OtherMod",
            RequestedSummary = "Word is the miller's son ran off.", RequestedSourceMod = "VividWorld"
         }).Should().BeFalse();
      }

      // Memories Vivid World added before mods were recorded carry no mod: philip0105 asked to clean those up, so the text alone decides.
      [Test]
      public void GIVEN_a_memory_stored_before_mods_were_recorded_WHEN_its_exact_text_is_removed_THEN_it_is_taken()
      {
         InjectedMemoryRemovalPolicy.Removes(new MemoryRemovalFacts {
            StoredSummary = "Word is the miller's son ran off.", RequestedSummary = "Word is the miller's son ran off.", RequestedSourceMod = "VividWorld"
         }).Should().BeTrue();
      }

      // Only an exact text: a near match is a different memory and stays.
      [Test]
      public void GIVEN_a_different_text_WHEN_removed_THEN_nothing_is_taken()
      {
         InjectedMemoryRemovalPolicy.Removes(new MemoryRemovalFacts {
            StoredSummary = "Word is the miller's son ran off.", RequestedSummary = "Word is the miller's daughter ran off.", RequestedSourceMod = "VividWorld"
         }).Should().BeFalse();
         InjectedMemoryRemovalPolicy.Removes(new MemoryRemovalFacts {StoredSummary = "x", RequestedSummary = "", RequestedSourceMod = "VividWorld"}).Should().BeFalse();
      }

      // STAKE: since 2.7.19 a secret added with no source is recorded as "(another mod)" so the Memories panel keeps it out
      // (tashmetu, 20/10/2026); the mod that added it named no one, so it must still be able to take it back, as before.
      [Test]
      public void GIVEN_a_secret_added_without_a_source_WHEN_a_mod_takes_it_back_THEN_it_is_taken()
      {
         InjectedMemoryRemovalPolicy.Removes(new MemoryRemovalFacts {
            StoredSummary = "I am fond of her.", StoredSourceMod = InjectedMemoryRemovalPolicy.UnnamedSource, RequestedSummary = "I am fond of her."
         }).Should().BeTrue();
      }
   }
}
