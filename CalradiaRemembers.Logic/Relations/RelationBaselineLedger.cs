// Code written by Gabriel Mailhot, 04/11/2026.
// Where each pair's history begins: the value a pair held just before the first change CR saw (PairBondPolicy). The game
// writes its starting draw, and another mod its own seeding, without a word; a change made through the game's relation
// action is announced. So the first announced change marks the line between the start and what happened since, whenever
// the start was written (tashmetu, Nexus, 04/11/2026: Regard and Standing clears and seeds "in the first hours" of a new
// campaign, after the game's draw). Saved under one additive key; an old save reads an empty ledger, every value a start.

#region

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

#endregion

namespace CalradiaRemembers.Logic.Relations
{
   /// <summary>The value each pair held just before CR first saw it change, by pair key.</summary>
   public sealed class RelationBaselineLedger
   {
      /// <summary>The baselines, by <see cref="ForeignDivorceDetection.PairKey" /> (order-free, as the game's own slot).</summary>
      public Dictionary<string, int> Baselines { get; set; } = new Dictionary<string, int>();

      /// <summary>The pair's baseline, or null when CR never saw it change.</summary>
      public int? BaselineOf(string a, string b)
         => a != null && b != null && Baselines.TryGetValue(ForeignDivorceDetection.PairKey(a, b), out int v) ? v : (int?) null;

      /// <summary>Marks the pair's value before a change, the first time only. Returns whether it was marked now.</summary>
      public bool Saw(string a, string b, int before)
      {
         if (a == null || b == null || a == b) return false;

         string key = ForeignDivorceDetection.PairKey(a, b);
         if (Baselines.ContainsKey(key)) return false;

         Baselines[key] = before;

         return true;
      }

      /// <summary>Forgets the pair's baseline (a self-test cleaning up after itself).</summary>
      public bool Forget(string a, string b) => a != null && b != null && Baselines.Remove(ForeignDivorceDetection.PairKey(a, b));

      /// <summary>The ledger as saved.</summary>
      public string Serialize() => JsonConvert.SerializeObject(this, Formatting.None);

      /// <summary>Reads a saved ledger; null, empty or broken input reads an empty ledger (an old save).</summary>
      public static RelationBaselineLedger Deserialize(string json)
      {
         if (string.IsNullOrWhiteSpace(json)) return new RelationBaselineLedger();

         try
         {
            RelationBaselineLedger ledger = JsonConvert.DeserializeObject<RelationBaselineLedger>(json);

            return ledger?.Baselines == null ? new RelationBaselineLedger() : ledger;
         }
         catch (JsonException)
         {
            return new RelationBaselineLedger();
         }
      }
   }
}
