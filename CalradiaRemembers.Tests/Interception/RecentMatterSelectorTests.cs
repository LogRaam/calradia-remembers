// Code written by Gabriel Mailhot, 05/09/2026.
// RecentMatterSelector decides the ONE thing a lord raises when he approaches the player about a recent matter
// (design [[project_npc_recent_matter]], phase 1): the most recent one he knows of, within a recency window, that
// he has not already raised. If this picked the wrong one, a lord who just fought beside the player would still
// dredge up a stale distant rumor (fkasad's report) instead of the fresh shared battle; if it forgot the
// once-per-NPC guard, he would raise the same matter every time they met. These pin that selection.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Interception;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Interception
{
   [TestFixture]
   public sealed class RecentMatterSelectorTests
   {
      private const int Today = 100;
      private const int Window = 6;

      // The core of fkasad's report: a fresh LIVED memory (the battle fought together today) must beat an older
      // RUMOR (a distant skirmish a few days ago). Freshest wins, full stop.
      [Test]
      public void GIVEN_a_fresh_lived_memory_and_an_older_rumor_WHEN_picking_THEN_the_fresher_lived_memory_wins()
      {
         var matters = new[]
         {
            new RecentMatter("we:skirmish", 97, "a skirmish near Pale", RecentMatterRegister.RumorAboutPlayer),
            new RecentMatter("mem:battle", 100, "we broke the enemy together", RecentMatterRegister.LivedMemory)
         };

         RecentMatter picked = RecentMatterSelector.PickFreshest(matters, new HashSet<string>(), Today, Window);

         picked.Key.Should().Be("mem:battle");
         picked.Register.Should().Be(RecentMatterRegister.LivedMemory);
      }

      // A rumor can still win if it is the freshest thing he knows: the register is a tiebreak, not an override.
      // Here the rumor is more recent than the lived memory, so it is what he raises (as hearsay).
      [Test]
      public void GIVEN_a_fresher_rumor_than_the_lived_memory_WHEN_picking_THEN_the_fresher_rumor_wins()
      {
         var matters = new[]
         {
            new RecentMatter("mem:old", 96, "an old shared ride", RecentMatterRegister.LivedMemory),
            new RecentMatter("we:news", 100, "the king readies for war", RecentMatterRegister.RumorAboutPlayer)
         };

         RecentMatterSelector.PickFreshest(matters, new HashSet<string>(), Today, Window)
            .Key.Should().Be("we:news");
      }

      // The once-per-NPC guard: a matter he has already raised is skipped, so he moves on to the next-freshest
      // unraised one instead of harping on the same event every meeting.
      [Test]
      public void GIVEN_the_freshest_matter_already_raised_WHEN_picking_THEN_it_falls_through_to_the_next()
      {
         var matters = new[]
         {
            new RecentMatter("mem:battle", 100, "we broke the enemy together", RecentMatterRegister.LivedMemory),
            new RecentMatter("we:skirmish", 98, "a skirmish near Pale", RecentMatterRegister.RumorAboutPlayer)
         };
         var raised = new HashSet<string> {"mem:battle"};

         RecentMatterSelector.PickFreshest(matters, raised, Today, Window)
            .Key.Should().Be("we:skirmish");
      }

      // Nothing fresh left to raise (all outside the window or all raised) must return null, so the lord simply
      // does not approach on this pretext rather than reaching for something stale.
      [Test]
      public void GIVEN_only_stale_or_raised_matters_WHEN_picking_THEN_nothing_is_returned()
      {
         var matters = new[]
         {
            new RecentMatter("we:ancient", 80, "an old war", RecentMatterRegister.RumorAboutPlayer),  // outside the window
            new RecentMatter("mem:done", 100, "already spoken of", RecentMatterRegister.LivedMemory)
         };
         var raised = new HashSet<string> {"mem:done"};

         RecentMatterSelector.PickFreshest(matters, raised, Today, Window).Should().BeNull();
      }

      // A future-dated matter (a clock/data glitch) and a blank-key matter must never be picked, or a corrupt
      // candidate could hijack the opener.
      [Test]
      public void GIVEN_a_future_dated_or_blank_key_matter_WHEN_picking_THEN_it_is_ignored()
      {
         var matters = new[]
         {
            new RecentMatter("we:future", 105, "tomorrow's news", RecentMatterRegister.RumorAboutPlayer),
            new RecentMatter("", 100, "no key", RecentMatterRegister.LivedMemory),
            new RecentMatter("we:valid", 99, "a real recent matter", RecentMatterRegister.RumorAboutPlayer)
         };

         RecentMatterSelector.PickFreshest(matters, new HashSet<string>(), Today, Window)
            .Key.Should().Be("we:valid");
      }

      // Determinism on an exact tie (same day, same register): the smaller key wins regardless of order, so the
      // same world state always yields the same opener rather than flickering between two matters.
      [Test]
      public void GIVEN_two_matters_tied_on_day_and_register_WHEN_picking_THEN_the_result_is_deterministic()
      {
         var a = new RecentMatter("we:aaa", 100, "matter A", RecentMatterRegister.RumorAboutPlayer);
         var b = new RecentMatter("we:bbb", 100, "matter B", RecentMatterRegister.RumorAboutPlayer);

         RecentMatterSelector.PickFreshest(new[] {a, b}, new HashSet<string>(), Today, Window).Key.Should().Be("we:aaa");
         RecentMatterSelector.PickFreshest(new[] {b, a}, new HashSet<string>(), Today, Window).Key.Should().Be("we:aaa");
      }

      // An empty or null candidate set is a safe no-op.
      [Test]
      public void GIVEN_no_candidates_WHEN_picking_THEN_nothing_is_returned()
      {
         RecentMatterSelector.PickFreshest(null, new HashSet<string>(), Today, Window).Should().BeNull();
         RecentMatterSelector.PickFreshest(new List<RecentMatter>(), new HashSet<string>(), Today, Window).Should().BeNull();
      }
   }
}
