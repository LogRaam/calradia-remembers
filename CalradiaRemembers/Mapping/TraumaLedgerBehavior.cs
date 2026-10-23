// Code written by Gabriel Mailhot, 20/10/2026.
// The aggressions characters lived through before the player's eyes, kept for the encyclopedia's Trauma section (Gabriel,
// 20/10/2026, Hardcore, characters' pages only). Nothing kept this before: a captive scene's intent was computed when it
// closed and then discarded, and a grudge is no record of what happened (it can be refused, decay, resolve, and the
// companion's "let me be broken" grudge targets a player who did not do it). Written whatever the adult level, so turning
// Hardcore on later shows what already happened; shown only at Hardcore (TraumaPagePolicy.Shows). Additive save key.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Trauma;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>The ledger of aggressions the player witnessed or committed, one record per aggression.</summary>
   public sealed class TraumaLedgerBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CalradiaRemembers_TraumaLedger_v1";

      private static List<TraumaRecord> _ledger = new List<TraumaRecord>();

      /// <summary>Created for every campaign started or loaded: nothing leaks from the last one.</summary>
      public TraumaLedgerBehavior() => _ledger = new List<TraumaRecord>();

      public override void RegisterEvents() { }

      public override void SyncData(IDataStore dataStore)
      {
         // One short line per aggression ("victim|captor|kind|author|day"), well under the native string limit.
         string blob = TraumaPagePolicy.Write(_ledger);
         dataStore.SyncData(SaveKey, ref blob);
         if (dataStore.IsLoading) _ledger = TraumaPagePolicy.Read(blob);
      }

      /// <summary>
      ///   Records an aggression <paramref name="victim" /> suffered, once (a scene closed twice is written once).
      ///   <paramref name="captor" /> is the named captor, or null for the player or a faceless captor.
      /// </summary>
      public static void Record(Hero victim, Hero captor, TraumaKind kind, TraumaAuthor author)
      {
         try
         {
            if (victim == null || victim == Hero.MainHero) return;

            var r = new TraumaRecord {VictimId = victim.StringId, CaptorId = captor?.StringId, Kind = kind, Author = author, Day = (int) CampaignTime.Now.ToDays};
            if (TraumaPagePolicy.Holds(_ledger, r)) return;

            _ledger.Add(r);
            ModLog.Info($"[TRAUMA] {victim.Name}: {kind} by {author}{(captor != null ? $" ({captor.Name})" : "")}.");
         }
         catch (Exception ex) { ModLog.Error($"[TRAUMA] not recorded: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>Every record, for the Heart section's account of what the player did (HeartIntimacyPolicy.ForcedByThePlayer).</summary>
      public static IReadOnlyList<TraumaRecord> All() => _ledger.ToList();

      /// <summary>What <paramref name="victim" /> lived through, oldest first.</summary>
      public static IReadOnlyList<TraumaRecord> Of(Hero victim) => victim == null ? Array.Empty<TraumaRecord>() : TraumaPagePolicy.Of(_ledger, victim.StringId);
   }
}
