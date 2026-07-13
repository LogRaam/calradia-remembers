// Code written by Gabriel Mailhot, 20/06/2026.
// Keeps the persisted world-event log bounded: WorldEventStore serializes the whole list to a save-file
// byte[] on every save, so an unbounded log is a growth-without-limit risk to the save (the same class
// of failure the mod already guards against for the NPC profile store). Two lifetimes, on purpose:
// third-party rumours age out on a window plus a hard cap; the player's own deeds are the chronicle and
// only get a very high safety cap; externally-fed "kept" deeds (Extension Surface) get their own smaller
// cap so a flooding third-party mod cannot crowd out either the player's record or ordinary gossip.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class WorldEventPrunerTests
   {
      private const int Today = 1000;

      // Prune runs from both the daily tick and every Record() call; a null list crashing here (e.g.
      // before the store finishes loading) would take down the daily tick or the recording path itself.
      [Test]
      public void GIVEN_a_null_list_WHEN_pruned_THEN_it_does_not_throw()
      {
         //Arrange
         List<FakeRumor>? events = null;

         //Act
         Action act = () => WorldEventPruner.Prune(events!, Today);

         //Assert
         act.Should().NotThrow();
      }

      // The player's own deeds ARE the chronicle (InvolvesPlayer=true); aging them out on the rumour
      // window would erase real history from the player's biography, the opposite of the pillar's point.
      [Test]
      public void GIVEN_a_player_deed_far_older_than_the_window_WHEN_pruned_THEN_it_is_kept()
      {
         //Arrange
         var events = new List<FakeRumor> {Deed(500)};

         //Act
         WorldEventPruner.Prune(events, Today);

         //Assert
         events.Should().HaveCount(1, "the player's deeds are the chronicle and never age out");
      }

      // Pins RumorWindowDays as an INCLUSIVE boundary; an off-by-one here silently shortens or lengthens
      // how long gossip is remembered by exactly one day.
      [Test]
      public void GIVEN_a_third_party_rumour_at_the_window_edge_WHEN_pruned_THEN_it_is_kept()
      {
         //Arrange
         var events = new List<FakeRumor> {Rumour(WorldEventPruner.RumorWindowDays)};

         //Act
         WorldEventPruner.Prune(events, Today);

         //Assert
         events.Should().HaveCount(1, "the window is inclusive at its edge");
      }

      // The other side of that same boundary: without it, the rumour window would never actually prune
      // anything and stale third-party gossip would grow the save blob forever.
      [Test]
      public void GIVEN_a_third_party_rumour_older_than_the_window_WHEN_pruned_THEN_it_is_dropped()
      {
         //Arrange
         var events = new List<FakeRumor> {Rumour(WorldEventPruner.RumorWindowDays + 1)};

         //Act
         WorldEventPruner.Prune(events, Today);

         //Assert
         events.Should().BeEmpty();
      }

      // The window alone is not enough: a flood of FRESH rumours (all inside the window) could still
      // grow unbounded. This hard cap is the last line against that save-blob growth.
      [Test]
      public void GIVEN_more_third_party_rumours_than_the_cap_WHEN_pruned_THEN_only_the_cap_remains()
      {
         //Arrange
         List<FakeRumor> events = Enumerable.Range(0, WorldEventPruner.MaxRumorEvents + 5)
                                            .Select(_ => Rumour(0))
                                            .ToList();

         //Act
         WorldEventPruner.Prune(events, Today);

         //Assert
         events.Should().HaveCount(WorldEventPruner.MaxRumorEvents);
      }

      // ── External "kept" deeds (Extension Surface, ExternalEventLifetime.Deed) ───────────────

      // A third-party mod's fed Lifetime.Deed fact must NOT be silently window-pruned like gossip; that
      // would break the Extension Surface contract that a "kept" deed persists rather than fades.
      [Test]
      public void GIVEN_an_external_deed_far_older_than_the_rumour_window_WHEN_pruned_THEN_it_is_kept()
      {
         //Arrange
         var events = new List<FakeRumor> {ExternalDeed(WorldEventPruner.RumorWindowDays + 500)};

         //Act
         WorldEventPruner.Prune(events, Today);

         //Assert
         events.Should().HaveCount(1, "a fed Deed is kept like a chronicle entry, not aged out like a rumour");
      }

      // Guards the exact failure the Extension Surface's own discipline calls out: a misbehaving external
      // mod flooding Record() toward the save-blob limit. MaxExternalDeedEvents is deliberately smaller
      // than the player's own chronicle cap so no third party's flood can crowd out the player's record.
      [Test]
      public void GIVEN_more_external_deeds_than_their_cap_WHEN_pruned_THEN_only_the_cap_remains_and_the_oldest_drop_first()
      {
         //Arrange: one fresh deed plus a flood of older ones past the cap.
         var fresh = ExternalDeed(0);
         List<FakeRumor> events = Enumerable.Range(0, WorldEventPruner.MaxExternalDeedEvents + 5)
                                            .Select(i => ExternalDeed(1000 + i))
                                            .Append(fresh)
                                            .ToList();

         //Act
         WorldEventPruner.Prune(events, Today);

         //Assert
         events.Should().HaveCount(WorldEventPruner.MaxExternalDeedEvents);
         events.Should().Contain(fresh, "the newest entries survive the cap, the oldest are dropped first");
      }

      // The three groups (player deeds, rumours, external deeds) must be capped INDEPENDENTLY. If
      // external deeds shared the rumour cap, one flooding external mod could silently evict real,
      // organically-generated in-game gossip that had nothing to do with it.
      [Test]
      public void GIVEN_a_flood_of_external_deeds_WHEN_pruned_THEN_they_do_not_count_against_the_ordinary_rumour_cap()
      {
         //Arrange: enough external deeds to blow past the (much smaller) rumour cap, plus one ordinary rumour.
         List<FakeRumor> events = Enumerable.Range(0, WorldEventPruner.MaxExternalDeedEvents)
                                            .Select(_ => ExternalDeed(0))
                                            .Append(Rumour(0))
                                            .ToList();

         //Act
         WorldEventPruner.Prune(events, Today);

         //Assert: the ordinary rumour survives, it was never squeezed out by the deed flood.
         events.Should().Contain(e => !e.InvolvesPlayer && !e.IsPersistentDeed);
      }

      private sealed class FakeRumor : IRumorRecord
      {
         public FakeRumor(int day, bool involvesPlayer, bool isPersistentDeed = false)
         {
            Day = day;
            InvolvesPlayer = involvesPlayer;
            IsPersistentDeed = isPersistentDeed;
         }

         public int Day { get; }
         public bool InvolvesPlayer { get; }
         public bool IsPersistentDeed { get; }
      }

      #region private

      private static FakeRumor Deed(int ageDays) => new(Today - ageDays, true);

      private static FakeRumor Rumour(int ageDays) => new(Today - ageDays, false);

      private static FakeRumor ExternalDeed(int ageDays) => new(Today - ageDays, false, true);

      #endregion
   }
}