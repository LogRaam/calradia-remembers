// Code written by Gabriel Mailhot, 26/09/2026.
// fkasad's grid for a mercenary's departure (Nexus, 25/09/2026; ratified by Gabriel). The rules are
// MercenaryDeparturePolicy's (pure, tested); this is the engine half:
//
//   - When the player's contract ends (LeaveAsMercenary, fed by WorldEventFeeders), the realm's situation is read (at
//     war? losing, by its strength against its enemies'?) and the departure judged: at peace, let go with thanks, or
//     abandoned. The realm's lords remember it on the mod's own regard ledger and in their memory, doubled against a
//     company of their own people, and word of it gets about.
//   - The departure is kept for BetrayalWindowDays: joining the former employer's enemy within it upgrades the verdict
//     to a betrayal, charging only the difference.
//   - Joining a realm of one's own people in a war it is losing is remembered well by its lords.
//
// Additive save key.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.WorldEvents
{
   /// <summary>Judges the end of the player's mercenary contracts, and remembers them.</summary>
   public sealed class MercenaryReputationBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CR_MercenaryDeparture_v1bytes";

      public static MercenaryReputationBehavior Instance { get; private set; }

      private Departure _last;

      public MercenaryReputationBehavior()
      {
         Instance = this;
      }

      private static int Today => (int) CampaignTime.Now.ToDays;

      private static string PlayerName => Hero.MainHero?.Name?.ToString() ?? "the player";

      public override void RegisterEvents()
      {
      }

      /// <summary>
      ///   The player's mercenary contract with <paramref name="kingdom" /> ended: judged by the realm's situation,
      ///   remembered by its lords, and kept for a later betrayal. Returns false when nothing could be judged (the
      ///   caller then records the plain contract end).
      /// </summary>
      public bool OnLeft(Kingdom kingdom)
      {
         try
         {
            if (kingdom == null) return false;

            (bool atWar, float own, float enemies) = WarFooting(kingdom);
            MercenaryDepartureVerdict verdict = MercenaryDeparturePolicy.Judge(new MercenaryDepartureFacts {
               EmployerAtWar = atWar, EmployerStrength = own, EnemiesStrength = enemies
            });
            bool sameCulture = SameCulture(kingdom);

            Remember(kingdom, verdict, MercenaryDeparturePolicy.RegardShift(verdict, sameCulture), null);
            _last = new Departure {KingdomId = kingdom.StringId, Day = Today, Verdict = verdict, SameCulture = sameCulture};
            ModLog.Info($"[MERCENARY] contract with {kingdom.Name} ended: {verdict} (at war {atWar}, strength {own:0} vs {enemies:0}, same culture {sameCulture}).");

            return true;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MERCENARY] judging the departure failed: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }

      /// <summary>
      ///   The player's clan took service with <paramref name="kingdom" /> (as a company or a vassal): a recent
      ///   departure from its enemy becomes a betrayal, and coming to one's own people in a losing war is remembered.
      /// </summary>
      public void OnJoined(Kingdom kingdom)
      {
         try
         {
            if (kingdom == null) return;

            if (_last != null)
            {
               Kingdom former = Kingdom.All?.FirstOrDefault(k => k.StringId == _last.KingdomId);
               bool atWarWithFormer = former != null && former != kingdom && FactionManager.IsAtWarAgainstFaction(kingdom, former);

               if (former != null && MercenaryDeparturePolicy.IsBetrayal(_last.Day, Today, atWarWithFormer))
               {
                  int upgrade = MercenaryDeparturePolicy.BetrayalUpgradeShift(_last.Verdict, _last.SameCulture);
                  Remember(former, MercenaryDepartureVerdict.Betrayal, upgrade, kingdom.Name?.ToString());
                  ModLog.Info($"[MERCENARY] joining {kingdom.Name} turns the departure from {former.Name} into a betrayal ({upgrade} regard).");
               }

               _last = null;
            }

            (bool atWar, float own, float enemies) = WarFooting(kingdom);
            if (atWar && MercenaryDeparturePolicy.IsAidOfKin(SameCulture(kingdom), MercenaryDeparturePolicy.IsLosing(own, enemies)))
               RememberAidOfKin(kingdom);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MERCENARY] judging the new service failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Test seam (cr.testall): the verdict a contract with <paramref name="kingdom" /> would end on right now, and
      ///   the realm's footing, WITHOUT applying anything (no regard, no memory, no news).
      /// </summary>
      internal (MercenaryDepartureVerdict Verdict, bool AtWar, float Own, float Enemies) DebugJudge(Kingdom kingdom)
      {
         (bool atWar, float own, float enemies) = WarFooting(kingdom);

         return (MercenaryDeparturePolicy.Judge(new MercenaryDepartureFacts {EmployerAtWar = atWar, EmployerStrength = own, EnemiesStrength = enemies}),
                 atWar, own, enemies);
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_last, Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[MERCENARY] serialize failed, persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("null");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[MERCENARY] save failed: {ex.GetType().Name}: {ex.Message}"); }

            return;
         }

         byte[] loaded = null;
         try { dataStore.SyncData(SaveKey, ref loaded); }
         catch
         {
            /* key absent in older saves */
         }

         if (loaded is not {Length: > 0}) return;

         try { _last = JsonConvert.DeserializeObject<Departure>(Encoding.UTF8.GetString(loaded)); }
         catch (Exception ex)
         {
            _last = null;
            ModLog.Error($"[MERCENARY] load failed, the last departure is dropped. {ex.GetType().Name}: {ex.Message}");
         }
      }

      #region private

      /// <summary>Whether the realm is at war, its own strength, and its enemies' combined strength.</summary>
      private static (bool AtWar, float Own, float Enemies) WarFooting(Kingdom kingdom)
      {
         List<Kingdom> enemies = (Kingdom.All ?? new List<Kingdom>())
                                 .Where(k => k != null && k != kingdom && !k.IsEliminated && FactionManager.IsAtWarAgainstFaction(k, kingdom))
                                 .ToList();

         return (enemies.Count > 0, kingdom.CurrentTotalStrength, enemies.Sum(k => k.CurrentTotalStrength));
      }

      private static bool SameCulture(Kingdom kingdom)
         => kingdom?.Culture != null && Hero.MainHero?.Culture == kingdom.Culture;

      /// <summary>The realm's lords (its clan leaders, the ruler first), never the player's own clan.</summary>
      private static IEnumerable<Hero> RealmLords(Kingdom kingdom)
         => (kingdom.Clans ?? Enumerable.Empty<Clan>())
            .Where(c => c != null && c != Clan.PlayerClan && !c.IsEliminated)
            .Select(c => c.Leader)
            .Where(h => h is {IsAlive: true} && h != Hero.MainHero)
            .OrderByDescending(h => h == kingdom.Leader);

      /// <summary>The realm's lords take the verdict to heart (regard and memory), and word of it gets about.</summary>
      private static void Remember(Kingdom kingdom, MercenaryDepartureVerdict verdict, int regardShift, string enemyName)
      {
         string memory = MercenaryDeparturePolicy.LordMemory(verdict, PlayerName, enemyName);
         ShiftLords(kingdom, regardShift, memory);

         WorldEventStore.Instance?.Record(new WorldEvent {
            Type = WorldEventType.PlayerFeat,
            Day = Today,
            Magnitude = MercenaryDeparturePolicy.Magnitude(verdict),
            InvolvesPlayer = true,
            Summary = MercenaryDeparturePolicy.Summary(verdict, PlayerName, kingdom.Name?.ToString(), enemyName),
            ActorIds = new List<string> {Hero.MainHero?.StringId}.Where(id => id != null).ToList(),
            FactionIds = new List<string> {kingdom.StringId},
            ChronicleLine = MercenaryDeparturePolicy.ChronicleLine(verdict, PlayerName, kingdom.Name?.ToString(), enemyName)
         });
      }

      /// <summary>A realm of the player's own people, losing its war, remembers the company that came to its aid.</summary>
      private static void RememberAidOfKin(Kingdom kingdom)
      {
         ShiftLords(kingdom, MercenaryDeparturePolicy.AidOfKinGratitude, MercenaryDeparturePolicy.AidOfKinMemory(PlayerName));

         WorldEventStore.Instance?.Record(new WorldEvent {
            Type = WorldEventType.PlayerFeat,
            Day = Today,
            Magnitude = 50,
            InvolvesPlayer = true,
            Summary = $"{PlayerName} rode to the aid of {kingdom.Name}, a realm of their own people, while the war went against it.",
            ActorIds = new List<string> {Hero.MainHero?.StringId}.Where(id => id != null).ToList(),
            FactionIds = new List<string> {kingdom.StringId},
            ChronicleLine = $"{PlayerName} rode to the aid of {kingdom.Name}, their own people, in its hour of need"
         });
         ModLog.Info($"[MERCENARY] {kingdom.Name}, a realm of the player's own people, remembers the company come to its aid.");
      }

      /// <summary>Applies a regard shift and a memory to each of the realm's lords (a profile is made for one who has none).</summary>
      private static void ShiftLords(Kingdom kingdom, int shift, string memory)
      {
         var store = NpcMemoryCampaignBehavior.Instance?.Store;
         if (store == null || (shift == 0 && memory == null)) return;

         foreach (Hero lord in RealmLords(kingdom))
         {
            NpcProfile profile = store.Get(lord.StringId);
            if (profile == null)
            {
               profile = HeroProfileMapper.FromHero(lord);
               store.Set(profile);
            }

            profile.ReputationWithPlayer += shift;
            if (memory != null) profile.Events.Add(new NotableEvent(Today, NotableEventType.Other, memory));
            store.Set(profile);
         }
      }

      /// <summary>The last contract the player left, kept for a later betrayal.</summary>
      private sealed class Departure
      {
         public string KingdomId { get; set; }
         public int Day { get; set; }
         public MercenaryDepartureVerdict Verdict { get; set; }
         public bool SameCulture { get; set; }
      }

      #endregion
   }
}
