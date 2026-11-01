// Code written by Gabriel Mailhot, 01/11/2026.
// DepaHN (Nexus, 27/10/2026): something extraordinary "could become a legend that characters retell, differently depending on their
// connection to the event". Every battle's end is read here, before the chronicle's own notability gate, since a lord's small host
// outnumbered three to one is no clash of armies. The rule is LegendPolicy's. A legend is also recorded once as ordinary news: that
// carries it to the rumours and the chronicle, and feeds the player's renown, so nothing here has to know any of them.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Legends;
using CalradiaRemembers.Mapping;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.WorldEvents
{
   /// <summary>Raises the campaign's legends and tells each character the one they carry.</summary>
   public sealed class LegendBehavior : CampaignBehaviorBase
   {
      // Additive: a save written before this has no such key and reads an empty ledger (save-migration rule).
      private const string SaveKey = "cr_legends";

      /// <summary>How loudly a legend travels as news: above an exploit (55), for a deed told everywhere.</summary>
      private const int NewsMagnitude = 80;

      public LegendBehavior()
      {
         Instance = this;
      }

      /// <summary>The live behavior, or null outside a campaign.</summary>
      public static LegendBehavior Instance { get; private set; }

      /// <summary>The campaign's legends (never null).</summary>
      public LegendLedger Ledger { get; internal set; } = new LegendLedger();

      private static int Today => (int) CampaignTime.Now.ToDays;

      public override void RegisterEvents()
      {
         CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(Ledger.Serialize()); }
            catch (Exception ex)
            {
               ModLog.Error($"[LEGEND] serialize failed, saving an empty ledger: {ex.GetType().Name}: {ex.Message}");
               bytes = Array.Empty<byte>();
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex)
            {
               ModLog.Error($"[LEGEND] save failed: {ex.GetType().Name}: {ex.Message}");
            }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               /* key absent in a save written before this feature */
            }

            Ledger = LegendLedger.Deserialize(bytes is {Length: > 0} ? Encoding.UTF8.GetString(bytes) : null);
         }
      }

      /// <summary>The block this character carries into a talk, or null when no legend has reached them.</summary>
      internal static string LineFor(Hero hero)
      {
         if (hero == null || Instance == null || !ModSettings.Legends) return null;

         Legend legend = LegendPolicy.Closest(Instance.Ledger.Legends, l => BondOf(hero, l), Today);

         return legend == null ? null : LegendPolicy.PromptLine(legend, BondOf(hero, legend), HeartPageReader.When(legend.Day));
      }

      private static LegendBond BondOf(Hero hero, Legend legend) => LegendPolicy.BondOf(new LegendTellerFacts {
         IsTheHero = hero.StringId == legend.HeroId,
         FoughtBeside = legend.BesideIds.Contains(hero.StringId),
         FoughtAgainst = legend.AgainstIds.Contains(hero.StringId),
         SameHouse = hero.Clan != null && hero.Clan.StringId == legend.ClanId,
         SameRealm = hero.MapFaction != null && hero.MapFaction.StringId == legend.RealmId,
         OpposingRealm = hero.MapFaction != null && hero.MapFaction.StringId == legend.OpposingRealmId
      });

      private void OnMapEventEnded(MapEvent mapEvent)
      {
         try
         {
            if (!ModSettings.Legends || mapEvent == null || mapEvent.WinningSide == BattleSideEnum.None) return;
            if (!mapEvent.IsFieldBattle && !mapEvent.IsSiegeAssault && !mapEvent.IsSallyOut && !mapEvent.IsSiegeOutside) return;

            MapEventSide winners = mapEvent.GetMapEventSide(mapEvent.WinningSide);
            MapEventSide losers = mapEvent.GetMapEventSide(mapEvent.DefeatedSide);
            Hero winLeader = winners?.LeaderParty?.LeaderHero;
            Hero loseLeader = losers?.LeaderParty?.LeaderHero;

            LegendKind? kind = LegendPolicy.Classify(new LegendBattleFacts {
               WinnerMen = winners?.HealthyTroopCountAtMapEventStart ?? 0,
               LoserMen = losers?.HealthyTroopCountAtMapEventStart ?? 0,
               WinnerLosses = winners?.TroopCasualties ?? 0,
               GarrisonWon = mapEvent.IsSiegeAssault && mapEvent.WinningSide == BattleSideEnum.Defender,
               LordsOnBothSides = IsALord(winLeader) && IsALord(loseLeader)
            });
            if (kind == null) return;

            bool lastStand = kind == LegendKind.LastStand;
            MapEventSide honoured = lastStand ? losers : winners;
            MapEventSide other = lastStand ? winners : losers;
            bool thePlayers = honoured.IsMainPartyAmongParties();
            if (!LegendPolicy.MayRaise(Ledger.LastDay, Today, thePlayers)) return;

            Settlement place = mapEvent.MapEventSettlement ?? WorldEventFeeders.NearestSettlementTo(mapEvent);
            Hero hero = honoured.LeaderParty.LeaderHero;
            Raise(new Legend {
               Kind = kind.Value, Day = Today, HeroId = hero.StringId, HeroName = WorldEventFeeders.Named(hero), ClanId = hero.Clan?.StringId,
               RealmId = hero.MapFaction?.StringId, OpposingRealmId = other.LeaderParty.MapFaction?.StringId, OpposingRealmName = other.LeaderParty.MapFaction?.Name?.ToString(),
               PlaceName = place?.Name?.ToString(), FewMen = honoured.HealthyTroopCountAtMapEventStart, ManyMen = other.HealthyTroopCountAtMapEventStart,
               Toll = lastStand ? winners.TroopCasualties : 0, BesideIds = HeroesOf(honoured), AgainstIds = HeroesOf(other),
               Name = LegendPolicy.Name(kind.Value, place?.Name?.ToString(), hero.Name?.ToString())
            }, thePlayers);
         }
         catch (Exception ex) { ModLog.Error($"[LEGEND] battle not read: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>Enters a legend in the ledger and sends it out as news. Internal for cr.legend and cr.test all.</summary>
      internal void Raise(Legend legend, bool thePlayers)
      {
         Ledger.Legends.Add(legend);
         ModLog.Info($"[LEGEND] {legend.Name}: {LegendPolicy.Deed(legend)}.");

         WorldEventStore.Instance?.Record(new WorldEvent {
            Type = thePlayers ? WorldEventType.PlayerFeat : WorldEventType.Battle,
            Day = legend.Day,
            Magnitude = NewsMagnitude,
            InvolvesPlayer = thePlayers,
            Summary = $"{legend.Name}: {LegendPolicy.Deed(legend)}.",
            ChronicleLine = thePlayers ? $"the deed passed into legend, and men call it {legend.Name}" : null,
            ActorIds = new List<string> {legend.HeroId},
            FactionIds = new[] {legend.RealmId, legend.OpposingRealmId}.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList()
         });
      }

      private static bool IsALord(Hero hero) => hero is {IsLord: true} && hero.MapFaction?.IsBanditFaction != true;

      private static List<string> HeroesOf(MapEventSide side)
      {
         var ids = new List<string>();
         foreach (MapEventParty party in side.Parties)
         {
            TroopRoster roster = party.Party?.MemberRoster;
            if (roster == null) continue;

            foreach (TroopRosterElement troop in roster.GetTroopRoster())
               if (troop.Character?.HeroObject != null)
                  ids.Add(troop.Character.HeroObject.StringId);
         }

         return ids.Distinct().ToList();
      }
   }
}
