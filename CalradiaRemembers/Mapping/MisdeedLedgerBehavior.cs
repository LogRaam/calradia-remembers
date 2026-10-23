// Code written by Gabriel Mailhot, 23/10/2026.
// The player's misdeeds that no other ledger keeps as a deed, for the Misdeeds section of the player's page (Gabriel,
// 23/10/2026: "on pourrait aussi ajouter des trahisons, extortions, et tout autres gestes qui pourraient etres classes
// comme misdeeds", "adultere est un misdeed"). An adultery found out survived only as a jealousy grudge that also covers a
// rival suitor or a spurned admirer and keeps no stake; a realm forsaken only as a chronicle line that a parting by
// other means shares; a captive handed to his enemies only as a grudge that is pruned once resolved; a murder of a man of
// no house not at all (the game's death log keeps only clan members). Each is written here as it happens, by name, in an
// additive save key. Written whatever the adult level.

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
   /// <summary>The ledger of the player's misdeeds no other source records as a deed, one record per deed and day.</summary>
   public sealed class MisdeedLedgerBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CalradiaRemembers_MisdeedLedger_v1";

      private static List<MisdeedRecord> _ledger = new List<MisdeedRecord>();

      /// <summary>Created for every campaign started or loaded: nothing leaks from the last one.</summary>
      public MisdeedLedgerBehavior() => _ledger = new List<MisdeedRecord>();

      public override void RegisterEvents() { }

      public override void SyncData(IDataStore dataStore)
      {
         // One short line per deed ("victim|kind|day"), well under the native string limit.
         string blob = MisdeedsPagePolicy.Write(_ledger);
         dataStore.SyncData(SaveKey, ref blob);
         if (dataStore.IsLoading) _ledger = MisdeedsPagePolicy.Read(blob);
      }

      /// <summary>
      ///   Records a deed of the player against <paramref name="victimId" /> (a hero, or a kingdom for a realm forsaken),
      ///   today, once.
      /// </summary>
      public static void Record(string victimId, MisdeedKind kind)
      {
         try
         {
            if (string.IsNullOrEmpty(victimId) || victimId == Hero.MainHero?.StringId) return;

            var r = new MisdeedRecord {VictimId = victimId, Kind = kind, Day = (int) CampaignTime.Now.ToDays};
            if (MisdeedsPagePolicy.Holds(_ledger, r)) return;

            _ledger.Add(r);
            ModLog.Info($"[MISDEED] {victimId}: {kind}.");
         }
         catch (Exception ex) { ModLog.Error($"[MISDEED] not recorded: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>Every record, oldest first as written.</summary>
      public static IReadOnlyList<MisdeedRecord> All() => _ledger.ToList();
   }
}
