// Code written by Gabriel Mailhot, 04/11/2026.
// Where each pair's history begins: the value a pair held just before the first change CR saw (PairBondPolicy). The game
// writes its starting draw, and another mod its own seeding, without a word; a change made through the game's relation
// action is announced. So the first announced change marks the line between the start and what happened since, whenever
// the start was written (tashmetu, Nexus, 04/11/2026: Regard and Standing clears and seeds "in the first hours" of a new
// campaign, after the game's draw). Saved under one additive key; an old save reads an empty ledger, every value a start.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NpcMemoryService.Core.Standing;

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

      /// <summary>
      ///   Drops what the ledger no longer needs, and returns how many it dropped (tashmetu, Nexus 09/11/2026: this ledger
      ///   reached 18.2 million bytes in a long campaign; Gabriel, 09/11/2026: "reduire le poids de ses donnees"):
      ///   <list type="bullet">
      ///     <item>a pair whose value is back where it began: a baseline equal to today's value reads exactly as no baseline
      ///     (PairBondPolicy.Assess: nothing earned, all start), and the next change marks the same value again;</item>
      ///     <item>a pair one of whom is no longer in the world (<paramref name="valueNow" /> answers null).</item>
      ///   </list>
      ///   <paramref name="valueNow" /> gives the pair's value today by their two ids, or null when either is gone. A key that
      ///   does not split into two ids is kept.
      /// </summary>
      public int Prune(Func<string, string, int?> valueNow)
      {
         if (valueNow == null || Baselines.Count == 0) return 0;

         var dropped = new List<string>();
         foreach (KeyValuePair<string, int> entry in Baselines)
         {
            string[] ids = entry.Key.Split('|');
            if (ids.Length != 2) continue;

            int? now = valueNow(ids[0], ids[1]);
            if (now == null || Clamp(now.Value) == Clamp(entry.Value)) dropped.Add(entry.Key);
         }

         foreach (string key in dropped)
            Baselines.Remove(key);

         return dropped.Count;
      }

      private static int Clamp(int v) => Math.Max(-VanillaRelation.Bound, Math.Min(VanillaRelation.Bound, v));

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
