// Code written by Gabriel Mailhot, 15/10/2026.
// The vassal war duty, engine side (Maifailun, Nexus 15/10/2026; Gabriel's ladder the same day: a summons to answer
// in person, a levy in coin, or troops, horses or arms, and a fief taken if nothing is given). The rules are pure
// (Logic/WarDuty); this keeps their ledger. It runs whether or not the MCM switch is on, so turning the duty on later
// never finds a stale ledger that would summon the player at once: the switch gates only what the RULER does.
//
// Service is any battle the player fights against a realm his own is at war with (won or lost: he rode), a siege
// among them, and every day his party marches in an army of his realm. Bandits are not the realm's war.

#region

using System;
using System.Linq;
using System.Text;
using CalradiaRemembers.Logic.WarDuty;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

#endregion

namespace CalradiaRemembers.WarDuty
{
   /// <summary>Keeps the war-duty ledger: the oath, the realm's wars, and the player's service.</summary>
   public sealed class WarDutyBehavior : CampaignBehaviorBase
   {
      // Additive: a save written before this has no such key and reads an empty ledger (save-migration rule).
      private const string SaveKey = "cr_war_duty";

      public WarDutyBehavior()
      {
         Instance = this;
      }

      /// <summary>The live behavior, or null outside a campaign.</summary>
      public static WarDutyBehavior Instance { get; private set; }

      /// <summary>The ledger (never null).</summary>
      public WarServiceLedger Ledger { get; internal set; } = new WarServiceLedger();

      public override void RegisterEvents()
      {
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
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
               ModLog.Error($"[WAR DUTY] serialize failed, saving an empty ledger: {ex.GetType().Name}: {ex.Message}");
               bytes = Array.Empty<byte>();
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex)
            {
               ModLog.Error($"[WAR DUTY] save failed: {ex.GetType().Name}: {ex.Message}");
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

            Ledger = WarServiceLedger.Deserialize(bytes is {Length: > 0} ? Encoding.UTF8.GetString(bytes) : null);
         }
      }

      /// <summary>The game day, as the ledger counts it.</summary>
      internal static int Today => (int) CampaignTime.Now.ToDays;

      /// <summary>What the duty reads live today.</summary>
      internal static WarDutyLive Live()
      {
         Clan clan = Clan.PlayerClan;
         Kingdom kingdom = clan?.Kingdom;

         return new WarDutyLive {
            IsSwornVassal = kingdom != null && !clan.IsUnderMercenaryService,
            RulesTheRealm = kingdom != null && kingdom.RulingClan == clan,
            IsCaptive = Hero.MainHero?.IsPrisoner == true,
            WindowDays = ModSettings.WarDutyWindowDays
         };
      }

      /// <summary>The realm the player is sworn to (not as a mercenary), or null.</summary>
      internal static Kingdom SwornKingdom()
      {
         Clan clan = Clan.PlayerClan;

         return clan?.Kingdom != null && !clan.IsUnderMercenaryService ? clan.Kingdom : null;
      }

      /// <summary>Brings the ledger up to today: the oath, the wars, a day in the realm's army. Also the cr.testall seam.</summary>
      internal void Upkeep(int today)
      {
         Kingdom kingdom = SwornKingdom();
         Ledger.NoteSworn(kingdom?.StringId, today);
         Ledger.NoteWars(kingdom == null ? Enumerable.Empty<string>() : FactionHelper.GetEnemyKingdoms(kingdom).Select(k => k.StringId), today);

         Army army = MobileParty.MainParty?.Army;
         if (kingdom != null && army != null && army.Kingdom == kingdom) Ledger.NoteServed(today);
      }

      private void OnDailyTick()
      {
         try { Upkeep(Today); }
         catch (Exception ex)
         {
            ModLog.Error($"[WAR DUTY] daily upkeep failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      private void OnMapEventEnded(MapEvent mapEvent)
      {
         try
         {
            if (mapEvent == null) return;

            bool attacker = mapEvent.AttackerSide?.IsMainPartyAmongParties() == true;
            bool defender = mapEvent.DefenderSide?.IsMainPartyAmongParties() == true;
            if (!attacker && !defender) return;

            MapEventSide other = attacker ? mapEvent.DefenderSide : mapEvent.AttackerSide;
            if (ServedAgainst(other?.LeaderParty?.MapFaction?.StringId))
               ModLog.Info($"[WAR DUTY] the player fought the realm's enemy ({other?.LeaderParty?.MapFaction?.Name}); service noted.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[WAR DUTY] battle check failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Notes service when the player fought a faction his realm is at war with, as the ledger knows its wars.
      ///   True when it counted. The cr.testall seam for a battle.
      /// </summary>
      internal bool ServedAgainst(string enemyFactionId)
      {
         if (string.IsNullOrEmpty(enemyFactionId)) return false;

         // A war declared today is not in the ledger until the daily upkeep: see it now.
         if (!Ledger.WarSeenSince.ContainsKey(enemyFactionId)) Upkeep(Today);
         if (!Ledger.WarSeenSince.ContainsKey(enemyFactionId)) return false;

         Ledger.NoteServed(Today);

         return true;
      }
   }
}
