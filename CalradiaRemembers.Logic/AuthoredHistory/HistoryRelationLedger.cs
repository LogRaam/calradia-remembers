// Code written by Gabriel Mailhot, 17/10/2026.
// Starting vanilla relations from an authored history (Gabriel, 17/10/2026: "it can also be able to set a starting
// vanilla"; docs/design/AUTHORED_HISTORY.md section 6). The file is read at every load and may be edited between two,
// so a relation must not stack: the save keeps a ledger of what the file already applied, and each sync applies only
// the difference. The ledger tracks what the FILE did, never the relation's value: the game moving it afterwards is the
// point of a starting relation.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.AuthoredHistory
{
   /// <summary>What the file already did to one pair for one event (saved).</summary>
   public sealed class HistoryRelationLedgerEntry
   {
      /// <summary><see cref="HistoryRelationLedger.KeyOf" />: event id and the pair, in a fixed order.</summary>
      public string Key { get; set; }

      public string A { get; set; }

      public string B { get; set; }

      /// <summary>The relation the file has added to the pair so far.</summary>
      public int Applied { get; set; }

      /// <summary>For a "set" entry, the value it set; null for a "change".</summary>
      public int? Target { get; set; }
   }

   /// <summary>One relation move to make now.</summary>
   public sealed class HistoryRelationStep
   {
      public string A { get; init; }

      public string B { get; init; }

      /// <summary>Added to the pair's vanilla relation (and to the personal regard when the player is one side).</summary>
      public int Delta { get; init; }

      public bool TouchesPlayer { get; init; }
   }

   /// <summary>The moves of one sync, and the ledger to save after them.</summary>
   public sealed class HistoryRelationPlan
   {
      public List<HistoryRelationStep> Steps { get; } = new();

      public List<HistoryRelationLedgerEntry> Ledger { get; } = new();
   }

   /// <summary>Plans the relation moves of a sync. Pure.</summary>
   public static class HistoryRelationLedger
   {
      /// <summary>The ledger key of a pair in an event; the pair is ordered, so a/b and b/a are one entry.</summary>
      public static string KeyOf(string eventId, string a, string b)
         => string.CompareOrdinal(a, b) <= 0 ? $"{eventId}|{a}|{b}" : $"{eventId}|{b}|{a}";

      /// <summary>
      ///   The moves that bring the world from what the ledger says the file did to what the file now asks.
      ///   <paramref name="currentRelation" /> reads a pair's vanilla relation today (for a first "set").
      /// </summary>
      public static HistoryRelationPlan Plan(IEnumerable<AcceptedHistoryEvent> events, IEnumerable<HistoryRelationLedgerEntry> ledger, Func<string, string, int> currentRelation,
                                             string playerId)
      {
         var plan = new HistoryRelationPlan();
         Dictionary<string, HistoryRelationLedgerEntry> before = (ledger ?? Enumerable.Empty<HistoryRelationLedgerEntry>())
                                                                 .Where(l => l?.Key != null).GroupBy(l => l.Key).ToDictionary(g => g.Key, g => g.First());
         var wanted = new HashSet<string>();

         foreach (AcceptedHistoryEvent e in events ?? Enumerable.Empty<AcceptedHistoryEvent>())
         foreach (AcceptedRelation r in e.Relations ?? new List<AcceptedRelation>())
         {
            string key = KeyOf(e.Id, r.A, r.B);
            if (!wanted.Add(key)) continue; // the same pair twice in one event: the first wins

            before.TryGetValue(key, out HistoryRelationLedgerEntry had);
            int applied = had?.Applied ?? 0;

            // A switch between "change" and "set" undoes the old entry and starts afresh.
            if (had != null && had.Target.HasValue != r.Set.HasValue)
            {
               Step(plan, r.A, r.B, -applied, playerId);
               applied = 0;
               had = null;
            }

            int delta;
            int? target = null;
            if (r.Set.HasValue)
            {
               target = r.Set.Value;
               delta = had == null ? r.Set.Value - (currentRelation?.Invoke(r.A, r.B) ?? 0) : r.Set.Value - had.Target.GetValueOrDefault();
            }
            else
            {
               delta = r.Change.GetValueOrDefault() - applied;
            }

            Step(plan, r.A, r.B, delta, playerId);
            plan.Ledger.Add(new HistoryRelationLedgerEntry {Key = key, A = r.A, B = r.B, Applied = applied + delta, Target = target});
         }

         // An entry the file no longer asks for is undone.
         foreach (HistoryRelationLedgerEntry gone in before.Values.Where(l => !wanted.Contains(l.Key)))
            Step(plan, gone.A, gone.B, -gone.Applied, playerId);

         return plan;
      }

      private static void Step(HistoryRelationPlan plan, string a, string b, int delta, string playerId)
      {
         if (delta == 0) return;

         plan.Steps.Add(new HistoryRelationStep {A = a, B = b, Delta = delta, TouchesPlayer = playerId != null && (a == playerId || b == playerId)});
      }
   }
}
