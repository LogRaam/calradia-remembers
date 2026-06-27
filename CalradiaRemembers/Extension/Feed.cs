// Code written by Gabriel Mailhot, 27/06/2026.
// Extension Surface, Volet A — the ingestion FACADE. The single, one-way door a third-party mod writes facts
// through. Validation lives at the door (pure scalars in CalradiaRemembers.Logic.ExternalEventRules; id
// resolution + dedup + per-source throttle here, against live game state), and the accepted fact is recorded
// into the SAME WorldEventStore CR's own Chronicle, rumours and NPC awareness already read — no new plumbing
// downstream. CR becomes the narrative-memory bus of a modlist.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using CalradiaRemembers.WorldEvents;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers
{
   /// <summary>
   ///   The public, one-way inbound API: a third-party mod calls <see cref="Record" /> to feed CR a world
   ///   fact. CR normalises and records it; it never reads or mutates the caller's state, and the caller never
   ///   touches CR's internals.
   /// </summary>
   public static class Feed
   {
      // Per-source daily send count (in-memory; resets on day change). Persistence is a later refinement —
      // dedup-by-id (against the persisted store) already stops re-recording the same fact across a reload.
      private static readonly Dictionary<string, int[]> _quota = new(); // source -> [day, countThatDay]

      /// <summary>
      ///   Validates a fed event at the door and records it into the world-event store. Returns why it was
      ///   rejected, or <see cref="FeedResult.Recorded" /> on success. Mutates nothing the caller owns.
      /// </summary>
      public static FeedResult Record(ExternalWorldEvent e)
      {
         if (e == null || string.IsNullOrWhiteSpace(e.SourceMod)) return FeedResult.RejectedInvalid;

         string summary = ExternalEventRules.BoundSummary(e.Summary);
         if (summary == null) return FeedResult.RejectedInvalid;

         WorldEventStore store = WorldEventStore.Instance;
         if (store == null) return FeedResult.RejectedNoStore;

         // Dedup on the source's stable id (survives reload — the store is persisted).
         if (!string.IsNullOrEmpty(e.Id) && store.GetById(e.Id) != null) return FeedResult.RejectedDuplicate;

         int today = (int) CampaignTime.Now.ToDays;
         if (!ConsumeQuota(e.SourceMod, today)) return FeedResult.RejectedThrottled;

         var worldEvent = new WorldEvent {
            Id = string.IsNullOrEmpty(e.Id)
               ? null
               : e.Id,
            Type = WorldEventType.Other,
            Day = e.Day > 0
               ? e.Day
               : today,
            Magnitude = ExternalEventRules.ClampMagnitude(e.Magnitude),
            Summary = summary,
            SourceMod = e.SourceMod,
            ActorIds = ResolveHeroes(e.HeroIds),
            FactionIds = ResolveFactions(e.FactionIds),
            InvolvesPlayer = Hero.MainHero != null && e.HeroIds != null && e.HeroIds.Contains(Hero.MainHero.StringId)
         };

         store.Record(worldEvent);
         ModLog.Info($"[FEED] {e.SourceMod} recorded a world event (mag {worldEvent.Magnitude}, {worldEvent.ActorIds.Count} hero ref(s)): {summary}");

         return FeedResult.Recorded;
      }

      private static bool ConsumeQuota(string source, int today)
      {
         if (!_quota.TryGetValue(source, out int[] q) || q[0] != today) q = new[] {today, 0};

         if (!ExternalEventRules.WithinDailyQuota(q[1]))
         {
            _quota[source] = q;

            return false;
         }

         q[1]++;
         _quota[source] = q;

         return true;
      }

      /// <summary>Keeps only the ids that resolve to a real hero (alive or dead) — resolve-or-drop.</summary>
      private static List<string> ResolveHeroes(IEnumerable<string> ids)
      {
         var kept = new List<string>();
         if (ids == null) return kept;

         // Hero.FindFirst is the reliable lookup; MBObjectManager.GetObject<Hero> does not resolve heroes by StringId.
         foreach (string id in ids)
            if (!string.IsNullOrEmpty(id) && Hero.FindFirst(h => h.StringId == id) != null) kept.Add(id);

         return kept;
      }

      /// <summary>Keeps only the ids that resolve to a real kingdom or clan — resolve-or-drop.</summary>
      private static List<string> ResolveFactions(IEnumerable<string> ids)
      {
         var kept = new List<string>();
         if (ids == null) return kept;

         foreach (string id in ids)
            if (!string.IsNullOrEmpty(id)
             && (Kingdom.All?.Any(k => k.StringId == id) == true || Clan.All?.Any(c => c.StringId == id) == true))
               kept.Add(id);

         return kept;
      }
   }
}
