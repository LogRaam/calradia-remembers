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

      private sealed class FakeRumor : IRumorRecord
      {
         public FakeRumor(int day, bool involvesPlayer)
         {
            Day = day;
            InvolvesPlayer = involvesPlayer;
         }

         public int Day { get; }
         public bool InvolvesPlayer { get; }
      }

      #region private

      private static FakeRumor Deed(int ageDays) => new(Today - ageDays, true);

      private static FakeRumor Rumour(int ageDays) => new(Today - ageDays, false);

      #endregion
   }
}