// Code written by Gabriel Mailhot, 15/10/2026.
// What the war duty needs to remember, which the game does not keep (traced 15/10/2026): since when the player has been
// sworn to his realm, since when the realm has been at war, when he last served it, and the reckoning in progress.
// WorldEventStore keeps only notable battles, so a small ledger of its own. Plain values, saved as JSON under one
// additive key; an old save reads an empty ledger, and the first day fills it in AS OF THAT DAY, so loading a save in
// the middle of a long war never summons anyone on day one (save-migration rule).

#region

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

#endregion

namespace CalradiaRemembers.Logic.WarDuty
{
   /// <summary>What the host reads live from the game on the day the duty is judged.</summary>
   public sealed class WarDutyLive
   {
      /// <summary>The player's clan is sworn to a realm and not under mercenary service.</summary>
      public bool IsSwornVassal { get; init; }

      /// <summary>The player leads the realm.</summary>
      public bool RulesTheRealm { get; init; }

      /// <summary>The player is a captive.</summary>
      public bool IsCaptive { get; init; }

      /// <summary>The MCM window in days.</summary>
      public int WindowDays { get; init; } = WarDutyPolicy.DefaultWindowDays;
   }

   /// <summary>The war-duty ledger: service, wars, and the reckoning in progress.</summary>
   public sealed class WarServiceLedger
   {
      /// <summary>The realm the player is sworn to, as last seen; null when sworn to none.</summary>
      public string SwornKingdomId { get; set; }

      /// <summary>The day the ledger first saw him sworn to that realm.</summary>
      public int SwornSinceDay { get; set; }

      /// <summary>Each enemy of that realm, with the day the ledger first saw the war.</summary>
      public Dictionary<string, int> WarSeenSince { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);

      /// <summary>The last day he served the realm; null when never seen.</summary>
      public int? LastServedDay { get; set; }

      /// <summary>The day the last reckoning was opened; null when never. One reckoning per stretch of war.</summary>
      public int? LastCalledDay { get; set; }

      /// <summary>The reckoning in progress, or the last one closed; null when none ever.</summary>
      public WarDutyCase Case { get; set; }

      /// <summary>
      ///   Records the realm he is sworn to today (null for none). A new realm starts the count again and forgets the
      ///   old realm's wars: his duty is to the realm he swore to, from the day he swore.
      /// </summary>
      public void NoteSworn(string kingdomId, int today)
      {
         string k = string.IsNullOrWhiteSpace(kingdomId) ? null : kingdomId.Trim();
         if (string.Equals(SwornKingdomId, k, StringComparison.Ordinal)) return;

         SwornKingdomId = k;
         SwornSinceDay = today;
         WarSeenSince.Clear();
      }

      /// <summary>
      ///   Records the realm's enemies today: a new war is seen from today, a war that ended is forgotten. Peace with
      ///   everyone ends the stretch of war.
      /// </summary>
      public void NoteWars(IEnumerable<string> enemyIds, int today)
      {
         var now = new HashSet<string>((enemyIds ?? Enumerable.Empty<string>()).Where(e => !string.IsNullOrWhiteSpace(e)), StringComparer.Ordinal);

         foreach (string gone in WarSeenSince.Keys.Where(k => !now.Contains(k)).ToList())
            WarSeenSince.Remove(gone);

         foreach (string e in now)
            if (!WarSeenSince.ContainsKey(e)) WarSeenSince[e] = today;
      }

      /// <summary>Records a day of service (a battle against the realm's enemy, a siege, a day in its army).</summary>
      public void NoteServed(int today)
      {
         if (!LastServedDay.HasValue || today > LastServedDay.Value) LastServedDay = today;
      }

      /// <summary>The first day of the current stretch of war (the oldest war still running); null at peace.</summary>
      public int? WarStretchSince => WarSeenSince.Count == 0 ? (int?) null : WarSeenSince.Values.Min();

      /// <summary>The facts WarDutyPolicy reads, from this ledger and the live ones the host passes.</summary>
      public WarDutyFacts Facts(int today, WarDutyLive live)
      {
         live ??= new WarDutyLive();
         int? since = WarStretchSince;

         return new WarDutyFacts {
            IsSwornVassal = live.IsSwornVassal,
            RulesTheRealm = live.RulesTheRealm,
            IsCaptive = live.IsCaptive,
            DaysAtWar = since.HasValue ? today - since.Value : (int?) null,
            DaysAsVassal = today - SwornSinceDay,
            DaysSinceServed = LastServedDay.HasValue ? today - LastServedDay.Value : (int?) null,
            WindowDays = live.WindowDays,
            AlreadyCalledThisWar = since.HasValue && LastCalledDay.HasValue && LastCalledDay.Value >= since.Value
                                   || Case != null && Case.IsOpen
         };
      }

      /// <summary>Opens a reckoning today and marks the stretch of war as called.</summary>
      public WarDutyCase OpenCase(string rulerId, int levyAmount, int today)
      {
         Case = WarDutyLadder.Open(SwornKingdomId, rulerId, levyAmount, today);
         LastCalledDay = today;

         return Case;
      }

      /// <summary>Saves the ledger as JSON.</summary>
      public string Serialize() => JsonConvert.SerializeObject(this, Formatting.None);

      /// <summary>Reads a saved ledger; null, empty or broken input reads an empty ledger (an old save).</summary>
      public static WarServiceLedger Deserialize(string json)
      {
         if (string.IsNullOrWhiteSpace(json)) return new WarServiceLedger();

         try
         {
            WarServiceLedger ledger = JsonConvert.DeserializeObject<WarServiceLedger>(json) ?? new WarServiceLedger();
            ledger.WarSeenSince = ledger.WarSeenSince == null
               ? new Dictionary<string, int>(StringComparer.Ordinal)
               : new Dictionary<string, int>(ledger.WarSeenSince, StringComparer.Ordinal);

            return ledger;
         }
         catch
         {
            return new WarServiceLedger();
         }
      }
   }
}
