// Code written by Gabriel Mailhot, 05/09/2026.
// Mystery-lover pillar (design [[project_npc_recent_matter]] phase 3a), a deliberate reversal of the old "a secret
// lover is totally invisible" rule (Gabriel ratified 2026-09-05): a secret lover is kept genuinely secret for a
// while (SecretLoverLeakPolicy.LeakDelayDays), then the bond's EXISTENCE, never the player's identity, may begin to
// circulate as an anonymous whisper ("X has taken a secret lover, though none know who"). That known-unknown is a
// Scandal world event with InvolvesPlayer FALSE, so the world-gossip register (InterceptionBehavior) has curious
// lords probe the PLAYER about it, and when the player IS the hidden lover the irony writes itself (the lord asks
// innocently; only the player knows). The identity staying hidden is what preserves the original "avoid jealousy"
// intent. Consequences once a lord pieces it together are a later increment (phase 3b).

#region

using System;
using System.Collections.Generic;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.WorldEvents
{
   /// <summary>Once a secret lover has been secret long enough, an anonymous whisper of its EXISTENCE may begin to circulate (identity never leaks).</summary>
   public sealed class SecretLoverGossipBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CalradiaRemembers_SecretLoverLeaks_v1bytes";
      private const int ScandalMagnitude = 60; // realm-shaking enough to travel and clear the gossip threshold

      public static SecretLoverGossipBehavior Instance;

      // StringIds of the secret-lover NPCs whose EXISTENCE-whisper has already been born, so it is emitted once, not
      // re-rolled daily. Persisted; additive key, empty on older saves.
      private HashSet<string> _leaked = new();

      public SecretLoverGossipBehavior() => Instance = this;

      public override void RegisterEvents()
         => CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new List<string>(_leaked), Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[SECRETLOVER] serialize failed, persisting none this save. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("[]");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[SECRETLOVER] save failed: {ex.GetType().Name}: {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               /* key absent in older saves, start with an empty leaked ledger */
            }

            if (bytes is not {Length: > 0}) return;

            try
            {
               var loaded = JsonConvert.DeserializeObject<List<string>>(Encoding.UTF8.GetString(bytes));
               _leaked = new HashSet<string>(loaded ?? new List<string>());
            }
            catch (Exception ex)
            {
               _leaked = new HashSet<string>();
               ModLog.Error($"[SECRETLOVER] load failed, dropped for this load. {ex.GetType().Name}: {ex.Message}");
            }
         }
      }

      private void OnDailyTick()
      {
         try
         {
            NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
            if (behavior?.Store == null || WorldEventStore.Instance == null || Hero.MainHero == null) return;

            var today = (int) CampaignTime.Now.ToDays;

            foreach (Hero h in Hero.AllAliveHeroes)
            {
               if (h == null || h == Hero.MainHero) continue;

               RomanticProfile r = behavior.Store.Get(h.StringId)?.Romantic;
               if (r == null || !r.IsSecretLover) continue;
               if (!SecretLoverLeakPolicy.IsEligibleToLeak(r.SecretLoverSinceDay, today, _leaked.Contains(h.StringId))) continue;
               if (MBRandom.RandomInt(100) >= SecretLoverLeakPolicy.DailyLeakChancePercent) continue;

               LeakExistence(h, today);
            }
         }
         catch (Exception ex) { ModLog.Error($"[SECRETLOVER] daily tick failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      // Leaks the existence AND marks it so the daily roll never re-emits it.
      private void LeakExistence(Hero h, int today)
      {
         RecordExistenceWhisper(h, today);
         _leaked.Add(h.StringId);
         ModLog.Info($"[SECRETLOVER] whispers of {h.Name}'s secret lover begin to circulate (identity hidden).");
      }

      // Records the anonymous existence-whisper as a Scandal world event: the subject is named (so the gossip has a
      // WHO it is about) but the lover is NOT (InvolvesPlayer false, no player id), which is the whole point. The
      // deterministic id keeps it a single standing fact.
      private static void RecordExistenceWhisper(Hero h, int today)
      {
         string name = HeroLabel.Describe(h.Name?.ToString(), h.Clan?.Name?.ToString(), h.Culture?.Name?.ToString());

         WorldEventStore.Instance.Record(new WorldEvent
         {
            Id = "cr_secretlover_" + h.StringId,
            Type = WorldEventType.Scandal,
            Day = today,
            Magnitude = ScandalMagnitude,
            InvolvesPlayer = false,
            ActorIds = new List<string> {h.StringId},
            FactionIds = h.MapFaction != null ? new List<string> {h.MapFaction.StringId} : new List<string>(),
            Summary = $"Whispers hold that {name} has taken a secret lover, though none can say who.",
            ChronicleLine = $"It was whispered that {name} had taken a secret lover, their identity known to no one."
         });
      }

      /// <summary>cr.testall seam: record the existence-whisper now (bypassing the delay/roll) WITHOUT marking it, so a test can assert then retract it. Returns false when the hero is null.</summary>
      internal bool DebugForceLeak(Hero h)
      {
         if (h == null || WorldEventStore.Instance == null) return false;

         RecordExistenceWhisper(h, (int) CampaignTime.Now.ToDays);

         return true;
      }
   }
}
