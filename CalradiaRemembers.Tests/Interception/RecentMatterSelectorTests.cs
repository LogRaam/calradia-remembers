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

      // Phase 2: a matter BETWEEN the two of them always beats impersonal world gossip, even when the gossip is
      // FRESHER, so a lord raises the shared battle before he reaches for hearsay about the king's war. Preserves
      // the fkasad fix against the new gossip source.
      [Test]
      public void GIVEN_a_fresher_world_gossip_and_an_older_personal_matter_WHEN_picking_THEN_the_personal_matter_wins()
      {
         var matters = new[]
         {
            new RecentMatter("mem:battle", 97, "we broke the enemy together", RecentMatterRegister.LivedMemory),
            new RecentMatter("we:war", 100, "the Sturgian king readies for war", RecentMatterRegister.WorldGossip)
         };

         RecentMatterSelector.PickFreshest(matters, new HashSet<string>(), Today, Window)
            .Key.Should().Be("mem:battle");
      }

      // But world gossip IS raised when nothing personal is fresh (all personal matters raised or aged out): the
      // lord, with nothing between them to bring up, probes the player about the noteworthy happening instead.
      [Test]
      public void GIVEN_only_world_gossip_left_WHEN_picking_THEN_the_gossip_is_raised()
      {
         var matters = new[]
         {
            new RecentMatter("mem:done", 100, "already spoken of", RecentMatterRegister.LivedMemory),
            new RecentMatter("we:scandal", 99, "a court scandal at Rhagaea's palace", RecentMatterRegister.WorldGossip)
         };
         var raised = new HashSet<string> {"mem:done"};

         RecentMatterSelector.PickFreshest(matters, raised, Today, Window)
            .Register.Should().Be(RecentMatterRegister.WorldGossip);
      }

      // An empty or null candidate set is a safe no-op.
      [Test]
      public void GIVEN_no_candidates_WHEN_picking_THEN_nothing_is_returned()
      {
         RecentMatterSelector.PickFreshest(null, new HashSet<string>(), Today, Window).Should().BeNull();
         RecentMatterSelector.PickFreshest(new List<RecentMatter>(), new HashSet<string>(), Today, Window).Should().BeNull();
      }

      #region what touches him, before what is merely newest

      // fkasad (Nexus, 19/09/2026): "Less than an hour ago, I arrived at a village that was being looted by an
      // enemy army, intending to defend it. The notable waiting at the gate called out to me to talk about a new
      // war taking place much further south, a conflict we aren't even involved in. Damn it! His village was
      // burning. He could have asked for help, or insulted me and called me incompetent for arriving too late
      // (again: a living world)."
      //
      // The notable knew both. The choice between them was made on the DAY ALONE, so a war declared this
      // morning outranked the raid on his own village that began yesterday. Nearness and size were sitting on
      // the world event and were being dropped on the way in.

      // THE REPORTED CASE, and it is a case the old ordering got exactly backwards.
      [Test]
      public void GIVEN_his_own_village_burning_and_a_fresher_distant_war_WHEN_picking_THEN_he_raises_the_village()
      {
         var matters = new[]
         {
            new RecentMatter("we:war", Today, "a war is declared far to the south", RecentMatterRegister.WorldGossip,
               touchesTheSpeaker: false, weight: 80),
            new RecentMatter("we:raid", Today - 1, "the village is being looted", RecentMatterRegister.WorldGossip,
               touchesTheSpeaker: true, weight: 30)
         };

         RecentMatterSelector.PickFreshest(matters, new HashSet<string>(), Today, Window)
                             .Key.Should().Be("we:raid", "it is his village, and it is burning now");
      }

      // Size decides between two that touch him equally, which is the half that makes the order an order rather
      // than a single rule.
      [Test]
      public void GIVEN_two_matters_that_both_touch_him_WHEN_picking_THEN_the_greater_one_wins()
      {
         var matters = new[]
         {
            new RecentMatter("we:small", Today, "a purse was cut in the market", RecentMatterRegister.WorldGossip,
               touchesTheSpeaker: true, weight: 10),
            new RecentMatter("we:siege", Today - 2, "the town is under siege", RecentMatterRegister.WorldGossip,
               touchesTheSpeaker: true, weight: 90)
         };

         RecentMatterSelector.PickFreshest(matters, new HashSet<string>(), Today, Window).Key.Should().Be("we:siege");
      }

      // And the clock still decides when nothing else does, so nothing that used to work stopped working.
      [Test]
      public void GIVEN_two_matters_alike_in_every_other_way_WHEN_picking_THEN_the_fresher_one_wins()
      {
         var matters = new[]
         {
            new RecentMatter("we:older", Today - 3, "an older happening", RecentMatterRegister.WorldGossip,
               touchesTheSpeaker: true, weight: 50),
            new RecentMatter("we:newer", Today, "a newer happening", RecentMatterRegister.WorldGossip,
               touchesTheSpeaker: true, weight: 50)
         };

         RecentMatterSelector.PickFreshest(matters, new HashSet<string>(), Today, Window).Key.Should().Be("we:newer");
      }

      // THE SCOPE, and it is the guard on the whole change: a matter between him and the PLAYER is never weighed
      // by size. A quiet conversation memory carries no magnitude at all, and ranking it against a battle would
      // bury the most personal material under the loudest, which is the opposite of what any of this is for.
      [Test]
      public void GIVEN_a_personal_memory_of_no_size_and_a_great_public_event_WHEN_picking_THEN_the_personal_one_wins()
      {
         var matters = new[]
         {
            new RecentMatter("we:battle", Today, "a great battle was fought", RecentMatterRegister.WorldGossip,
               touchesTheSpeaker: true, weight: 100),
            new RecentMatter("mem:word", Today - 2, "we spoke of his daughter", RecentMatterRegister.LivedMemory)
         };

         RecentMatterSelector.PickFreshest(matters, new HashSet<string>(), Today, Window)
                             .Key.Should().Be("mem:word", "personal still outranks the world, whatever the world was doing");
      }

      // Two PERSONAL matters keep the ordering they always had: freshest, untouched by nearness or size.
      [Test]
      public void GIVEN_two_personal_matters_WHEN_picking_THEN_the_fresher_one_still_wins()
      {
         var matters = new[]
         {
            new RecentMatter("mem:old", Today - 2, "an older word between us", RecentMatterRegister.LivedMemory),
            new RecentMatter("mem:new", Today, "what we said today", RecentMatterRegister.LivedMemory)
         };

         RecentMatterSelector.PickFreshest(matters, new HashSet<string>(), Today, Window).Key.Should().Be("mem:new");
      }

      // Nothing is FILTERED by any of this. It is an order, so a matter that touches nobody is still raised when
      // it is the only thing he knows: silence has its own gate and this is not it.
      [Test]
      public void GIVEN_only_a_distant_matter_WHEN_picking_THEN_it_is_still_raised()
      {
         var matters = new[]
         {
            new RecentMatter("we:far", Today, "a war far away", RecentMatterRegister.WorldGossip,
               touchesTheSpeaker: false, weight: 5)
         };

         RecentMatterSelector.PickFreshest(matters, new HashSet<string>(), Today, Window).Key.Should().Be("we:far");
      }

      #endregion

   }
}
