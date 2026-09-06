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
      private const string KnownSaveKey = "CalradiaRemembers_SecretLoverKnown_v1bytes";
      private const int ScandalMagnitude = 60; // realm-shaking enough to travel and clear the gossip threshold

      public static SecretLoverGossipBehavior Instance;

      // StringIds of the secret-lover NPCs whose EXISTENCE-whisper has already been born, so it is emitted once, not
      // re-rolled daily. Persisted; additive key, empty on older saves.
      private HashSet<string> _leaked = new();

      // "lordId|subjectId": that lord PRIVATELY knows the player is subjectId's secret lover, a fact no one ever
      // deduces, learned ONLY because the player told him (phase 3b). Seeds later propagation among his friends and
      // an enemy lord's use of it against the player. Persisted; additive key, empty on older saves.
      private HashSet<string> _known = new();

      public SecretLoverGossipBehavior() => Instance = this;

      public override void RegisterEvents()
         => CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            SaveLedger(dataStore, SaveKey, _leaked);
            SaveLedger(dataStore, KnownSaveKey, _known);
         }
         else
         {
            _leaked = LoadLedger(dataStore, SaveKey);
            _known = LoadLedger(dataStore, KnownSaveKey);
         }
      }

      // byte[]-JSON persistence (the ~65535 native-string cap makes a raw string list unsafe); a serialize failure
      // persists an empty list rather than aborting the whole save.
      private static void SaveLedger(IDataStore dataStore, string key, HashSet<string> ledger)
      {
         byte[] bytes;
         try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new List<string>(ledger), Formatting.None)); }
         catch (Exception ex)
         {
            ModLog.Error($"[SECRETLOVER] serialize failed for {key}, persisting none this save. {ex.GetType().Name}: {ex.Message}");
            bytes = Encoding.UTF8.GetBytes("[]");
         }

         try { dataStore.SyncData(key, ref bytes); }
         catch (Exception ex) { ModLog.Error($"[SECRETLOVER] save failed for {key}: {ex.GetType().Name}: {ex.Message}"); }
      }

      // Additive load: a key absent in an older save (or a corrupt payload) yields an empty ledger, never a throw.
      private static HashSet<string> LoadLedger(IDataStore dataStore, string key)
      {
         byte[] bytes = null;
         try { dataStore.SyncData(key, ref bytes); }
         catch
         {
            /* key absent in older saves, start empty */
         }

         if (bytes is not {Length: > 0}) return new HashSet<string>();

         try
         {
            var loaded = JsonConvert.DeserializeObject<List<string>>(Encoding.UTF8.GetString(bytes));

            return new HashSet<string>(loaded ?? new List<string>());
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SECRETLOVER] load failed for {key}, dropped for this load. {ex.GetType().Name}: {ex.Message}");

            return new HashSet<string>();
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

      private static string KnownKey(string lordId, string subjectId) => lordId + "|" + subjectId;

      /// <summary>Records that <paramref name="lord" /> now privately knows the player is the secret lover of <paramref name="subjectId" /> (phase 3b: learned only because the player told him). Idempotent.</summary>
      public void RecordKnows(Hero lord, string subjectId)
      {
         if (lord == null || string.IsNullOrEmpty(lord.StringId) || string.IsNullOrEmpty(subjectId)) return;

         if (_known.Add(KnownKey(lord.StringId, subjectId)))
            ModLog.Info($"[SECRETLOVER] {lord.Name} now knows the player is the secret lover of {subjectId} (the player told him).");
      }

      /// <summary>True when <paramref name="lordId" /> privately knows the player is <paramref name="subjectId" />'s secret lover.</summary>
      public bool Knows(string lordId, string subjectId)
         => !string.IsNullOrEmpty(lordId) && !string.IsNullOrEmpty(subjectId) && _known.Contains(KnownKey(lordId, subjectId));

      /// <summary>Retracts a recorded knowledge entry (cr.testall cleanup so a self-test leaves nothing persisted).</summary>
      public void ForgetKnows(Hero lord, string subjectId)
      {
         if (lord == null || string.IsNullOrEmpty(lord.StringId) || string.IsNullOrEmpty(subjectId)) return;

         _known.Remove(KnownKey(lord.StringId, subjectId));
      }

      /// <summary>
      ///   StringIds of the heroes the player is currently a secret lover of (companion-only, by construction of
      ///   take_as_secret_lover). Feeds admit_secret_lover eligibility and the knowledge ledger: the player can only
      ///   confess a bond that actually exists.
      /// </summary>
      public List<string> PlayerSecretLoverSubjectIds()
      {
         var subjects = new List<string>();
         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         Clan clan = Clan.PlayerClan;
         if (behavior?.Store == null || clan == null) return subjects;

         foreach (Hero h in clan.Companions)
         {
            if (h == null || h == Hero.MainHero) continue;

            RomanticProfile r = behavior.Store.Get(h.StringId)?.Romantic;
            if (r != null && r.IsSecretLover) subjects.Add(h.StringId);
         }

         return subjects;
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
