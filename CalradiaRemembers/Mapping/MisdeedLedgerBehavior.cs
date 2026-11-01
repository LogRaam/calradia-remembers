// Code written by Gabriel Mailhot, 23/10/2026.
// The player's misdeeds that no other ledger keeps as a deed, for the Misdeeds section of the player's page (Gabriel,
// 23/10/2026: "on pourrait aussi ajouter des trahisons, extortions, et tout autres gestes qui pourraient etres classes
// comme misdeeds", "adultere est un misdeed"). An adultery found out survived only as a jealousy grudge that also covers a
// rival suitor or a spurned admirer and keeps no stake; a realm forsaken only as a chronicle line that a parting by
// other means shares; a captive handed to his enemies only as a grudge that is pruned once resolved; a murder of a man of
// no house not at all (the game's death log keeps only clan members); a village raided or a town devastated nowhere past
// the game's seven-day log (LogEntryHistory.DeleteOutdatedLogs, 1.4.8), a raid the player led and a sack the player chose.
// Each is written here as it happens, by name, in an additive save key. Written whatever the adult level.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Trauma;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

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

      public override void RegisterEvents()
      {
         CampaignEvents.VillageStateChanged.AddNonSerializedListener(this, OnVillageStateChanged);
         CampaignEvents.OnSiegeAftermathAppliedEvent.AddNonSerializedListener(this, OnSiegeAftermath);
      }

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

      // The party that leads a raid is the one named here: in another lord's army, the raid is his.
      internal static void OnVillageStateChanged(Village village, Village.VillageStates oldState, Village.VillageStates newState, MobileParty raider)
      {
         if (raider != MobileParty.MainParty || newState != Village.VillageStates.BeingRaided) return;

         Record(village?.Settlement?.StringId, MisdeedsPagePolicy.RaidKind(DaysAtWarWith(village?.Settlement?.MapFaction)));
      }

      // Show Mercy is no misdeed. The choice is the army leader's, and a leader who is not the player has made it for them.
      internal static void OnSiegeAftermath(MobileParty attacker, Settlement place, SiegeAftermathAction.SiegeAftermath aftermath, Clan previousOwner, Dictionary<MobileParty, float> contributions)
      {
         if (attacker != MobileParty.MainParty || aftermath == SiegeAftermathAction.SiegeAftermath.ShowMercy) return;

         Record(place?.StringId, aftermath == SiegeAftermathAction.SiegeAftermath.Devastate ? MisdeedKind.Devastated : MisdeedKind.Pillaged);
      }

      private static int? DaysAtWarWith(IFaction faction)
      {
         IFaction mine = Hero.MainHero?.MapFaction;
         if (faction == null || mine == null || faction == mine || !mine.IsAtWarWith(faction)) return null;

         return (int) mine.GetStanceWith(faction).WarStartDate.ElapsedDaysUntilNow;
      }

      /// <summary>Every record, oldest first as written.</summary>
      public static IReadOnlyList<MisdeedRecord> All() => _ledger.ToList();
   }
}
