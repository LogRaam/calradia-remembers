// Code written by Gabriel Mailhot, 17/10/2026.
// Starting vanilla relations from an authored history (Gabriel, 17/10/2026: "it can also be able to set a starting
// vanilla"; docs/design/AUTHORED_HISTORY.md section 6). The file is read at every load and edited between loads, so
// HistoryRelationLedger plans only the difference between what the file asks and what it already did. Wrong, and each
// reload adds the betrayal's -40 again until every pair in the history sits at -100, or an event the author deleted
// leaves its hatred behind for ever.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.AuthoredHistory;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class HistoryRelationLedgerTests
   {
      private static AcceptedHistoryEvent Event(params AcceptedRelation[] relations) => new() {Id = "betrayal", Relations = relations.ToList()};

      private static AcceptedRelation Change(int v) => new() {A = "x", B = "y", Change = v};

      private static AcceptedRelation Set(int v) => new() {A = "x", B = "y", Set = v};

      private static HistoryRelationPlan Plan(AcceptedHistoryEvent e, List<HistoryRelationLedgerEntry> ledger, int current = 80)
         => HistoryRelationLedger.Plan(e == null ? new List<AcceptedHistoryEvent>() : new List<AcceptedHistoryEvent> {e}, ledger, (_, _) => current, "main_hero");

      // STAKE: the first load applies the change once.
      [Test]
      public void GIVEN_a_new_change_WHEN_synced_THEN_it_is_applied_and_recorded()
      {
         HistoryRelationPlan plan = Plan(Event(Change(-40)), new List<HistoryRelationLedgerEntry>());

         plan.Steps.Single().Delta.Should().Be(-40);
         plan.Ledger.Single().Applied.Should().Be(-40);
      }

      // STAKE: a reload must not stack: otherwise every load deepens the feud.
      [Test]
      public void GIVEN_a_change_already_applied_WHEN_synced_again_THEN_nothing_moves()
      {
         HistoryRelationPlan first = Plan(Event(Change(-40)), new List<HistoryRelationLedgerEntry>());

         Plan(Event(Change(-40)), first.Ledger).Steps.Should().BeEmpty();
      }

      // STAKE: an edit on the page from -40 to -60 moves the pair by the difference only.
      [Test]
      public void GIVEN_an_edited_change_WHEN_synced_THEN_only_the_difference_is_applied()
      {
         HistoryRelationPlan first = Plan(Event(Change(-40)), new List<HistoryRelationLedgerEntry>());

         Plan(Event(Change(-60)), first.Ledger).Steps.Single().Delta.Should().Be(-20);
      }

      // STAKE: a deleted event takes back what it gave; otherwise the hatred outlives the story that caused it.
      [Test]
      public void GIVEN_an_event_the_author_removed_WHEN_synced_THEN_its_relation_is_undone()
      {
         HistoryRelationPlan first = Plan(Event(Change(-40)), new List<HistoryRelationLedgerEntry>());

         HistoryRelationPlan after = Plan(null, first.Ledger);

         after.Steps.Single().Delta.Should().Be(40);
         after.Ledger.Should().BeEmpty();
      }

      // STAKE: the case named to Gabriel: the game made the pair +80, the author wants them enemies at -60. "set" lands
      // them at -60, where a "change" of -40 would leave them friends at +40.
      [Test]
      public void GIVEN_a_set_on_a_pair_the_game_made_friends_WHEN_synced_THEN_the_pair_starts_at_the_value()
      {
         HistoryRelationPlan plan = Plan(Event(Set(-60)), new List<HistoryRelationLedgerEntry>(), current: 80);

         plan.Steps.Single().Delta.Should().Be(-140);
         plan.Ledger.Single().Target.Should().Be(-60);
      }

      // STAKE: a starting value is a start: the game moving the pair afterwards is not undone at the next load.
      [Test]
      public void GIVEN_a_set_already_applied_and_the_game_moved_the_pair_since_WHEN_synced_again_THEN_nothing_moves()
      {
         HistoryRelationPlan first = Plan(Event(Set(-60)), new List<HistoryRelationLedgerEntry>(), current: 80);

         Plan(Event(Set(-60)), first.Ledger, current: -30).Steps.Should().BeEmpty();
      }

      // STAKE: an edited starting value moves the pair by what the author changed, not back to the value (which would
      // erase what the campaign did to the pair since).
      [Test]
      public void GIVEN_an_edited_set_WHEN_synced_THEN_the_pair_moves_by_the_edit()
      {
         HistoryRelationPlan first = Plan(Event(Set(-60)), new List<HistoryRelationLedgerEntry>(), current: 80);

         Plan(Event(Set(-30)), first.Ledger, current: -50).Steps.Single().Delta.Should().Be(30);
      }

      // STAKE: switching a relation from "change" to "set" must not leave the old change behind.
      [Test]
      public void GIVEN_a_change_switched_to_a_set_WHEN_synced_THEN_the_change_is_undone_before_the_set()
      {
         HistoryRelationPlan first = Plan(Event(Change(-40)), new List<HistoryRelationLedgerEntry>(), current: 80);

         HistoryRelationPlan switched = Plan(Event(Set(-60)), first.Ledger, current: 40);

         switched.Steps.Select(s => s.Delta).Should().Equal(40, -100);
      }

      // STAKE: the player's side is flagged so the mod writes the personal regard too.
      [Test]
      public void GIVEN_a_relation_with_the_player_WHEN_synced_THEN_the_step_says_so()
      {
         var e = Event(new AcceptedRelation {A = "main_hero", B = "y", Change = -40});

         Plan(e, new List<HistoryRelationLedgerEntry>()).Steps.Single().TouchesPlayer.Should().BeTrue();
      }

      // STAKE: a/b and b/a are one pair; read as two, a reordered edit would apply twice.
      [Test]
      public void GIVEN_the_pair_written_the_other_way_round_WHEN_keyed_THEN_it_is_the_same_entry()
      {
         HistoryRelationLedger.KeyOf("e", "x", "y").Should().Be(HistoryRelationLedger.KeyOf("e", "y", "x"));
      }
   }
}
