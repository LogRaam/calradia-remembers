// Code written by Gabriel Mailhot, 20/06/2026.

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