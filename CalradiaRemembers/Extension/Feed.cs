// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.WorldEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

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
      /// <summary>
      ///   The Extension Surface's capability version. Bumped only on a breaking contract change (a
      ///   removed or renamed public member of <see cref="Feed" /> or <see cref="ExternalWorldEvent" />);
      ///   additive members never require a bump. A reflection-based caller (a mod with no compile-time
      ///   reference to CR) reads this before calling anything, to detect what it can safely use.
      /// </summary>
      public const int ApiVersion = 1;

      private static readonly Dictionary<string, int[]> _quota = new(); // source -> [day, countThatDay]

      // Per-source daily send count (in-memory; resets on day change). Persistence is a later refinement —
      // dedup-by-id (against the persisted store) already stops re-recording the same fact across a reload.
      // Guarded by _quotaLock: a third-party mod may call Record from a background thread.
      private static readonly object _quotaLock = new();

      /// <summary>
      ///   Validates a fed event at the door and records it into the world-event store. Returns why it was
      ///   rejected, or <see cref="FeedResult.Recorded" /> on success. Mutates nothing the caller owns.
      /// </summary>
      public static FeedResult Record(ExternalWorldEvent e)
      {
         if (e == null || string.IsNullOrWhiteSpace(e.SourceMod)) return FeedResult.RejectedInvalid;

         string summary = ExternalEventRules.BoundSummary(e.Summary);

         if (summary == null) return FeedResult.RejectedInvalid;

         string details = ExternalEventRules.NormalizeDetails(e.Details);
         if (!DetailsAccepted(e, details)) return FeedResult.RejectedInvalid;

         var store = WorldEventStore.Instance;

         if (store == null) return FeedResult.RejectedNoStore;

         // Dedup on the source's stable id (survives reload — the store is persisted).
         if (!string.IsNullOrEmpty(e.Id) && store.GetById(e.Id) != null) return FeedResult.RejectedDuplicate;

         var today = (int) CampaignTime.Now.ToDays;

         if (!ConsumeQuota(e.SourceMod, today)) return FeedResult.RejectedThrottled;

         WorldEvent worldEvent = Build(e, summary, today);
         if (!KeepDetails(worldEvent, e.SourceMod, details)) return FeedResult.RejectedNoStore;
         store.Record(worldEvent);
         NoteSummaryCut(e);
         ModLog.Info($"[FEED] {e.SourceMod} recorded a world event (id {worldEvent.Id}, mag {worldEvent.Magnitude}, {worldEvent.ActorIds.Count} hero ref(s), {worldEvent.FactionIds.Count} faction ref(s), place {worldEvent.PlaceId ?? "none"}): {summary}");

         if (!string.IsNullOrEmpty(e.PlaceId) && worldEvent.PlaceId == null)
            ModLog.Info($"[FEED] {e.SourceMod}: PlaceId '{e.PlaceId}' is not a settlement StringId, so the event was kept without a place.");

         return FeedResult.Recorded;
      }

      /// <summary>
      ///   Retracts a fact that is no longer true (a homestead destroyed or converted to a settlement): removes
      ///   the entry with this id so NPCs stop speaking of it. Returns <see cref="FeedResult.Retracted" /> when
      ///   one was removed, or <see cref="FeedResult.NotFound" /> when nothing carried that id.
      /// </summary>
      public static FeedResult Retract(string id)
      {
         if (string.IsNullOrWhiteSpace(id)) return FeedResult.RejectedInvalid;

         var store = WorldEventStore.Instance;

         if (store == null) return FeedResult.RejectedNoStore;

         bool removed = store.Remove(id);
         ModLog.Info($"[FEED] retract '{id}' -> {(removed ? "removed" : "not found")}.");

         return removed
            ? FeedResult.Retracted
            : FeedResult.NotFound;
      }

      /// <summary>
      ///   Corrects a standing fact: replaces the entry carrying <see cref="ExternalWorldEvent.Id" /> with new
      ///   content (a homestead that moved, an NPC renamed), or records it fresh if none exists yet. The id is
      ///   REQUIRED — there is nothing to target without it. No throttle: an upsert can't grow the store
      ///   unbounded the way repeated records could. Returns <see cref="FeedResult.Updated" /> when an existing
      ///   entry was replaced, or <see cref="FeedResult.Recorded" /> when it was inserted fresh.
      /// </summary>
      public static FeedResult Update(ExternalWorldEvent e)
      {
         if (e == null || string.IsNullOrWhiteSpace(e.SourceMod) || string.IsNullOrEmpty(e.Id)) return FeedResult.RejectedInvalid;

         string summary = ExternalEventRules.BoundSummary(e.Summary);

         if (summary == null) return FeedResult.RejectedInvalid;

         string details = ExternalEventRules.NormalizeDetails(e.Details);
         if (!DetailsAccepted(e, details)) return FeedResult.RejectedInvalid;

         var store = WorldEventStore.Instance;

         if (store == null) return FeedResult.RejectedNoStore;

         // Null details keep what was kept (a bridge correcting only the summary must not lose its digest); "" removes them.
         string keptKey = e.Details == null ? store.GetById(e.Id)?.DetailsKey : null;
         var today = (int) CampaignTime.Now.ToDays;
         WorldEvent updated = Build(e, summary, today);
         if (keptKey != null) updated.DetailsKey = keptKey;
         else if (!KeepDetails(updated, e.SourceMod, details)) return FeedResult.RejectedNoStore;
         bool replaced = store.Remove(e.Id); // drop the stale entry, then re-record the corrected one
         store.Record(updated);
         NoteSummaryCut(e);
         ModLog.Info($"[FEED] {e.SourceMod} {(replaced ? "updated" : "recorded")} '{e.Id}': {summary}");

         return replaced
            ? FeedResult.Updated
            : FeedResult.Recorded;
      }

      #region private

      /// <summary>Maps a validated external event to a stored world event (shared by Record and Update).</summary>
      /// <summary>
      ///   What your mod has recorded that CR still holds, as copies (changing them changes nothing; use Update or Retract),
      ///   oldest first. HeroIds carries every hero the event names (CR keeps no separate list of actors), Category is not
      ///   kept, and a rumor CR has already let fade is gone. Empty for a blank source or outside a campaign. Call it on the
      ///   game thread. Since 2.8.10 (Bhelogan, Discord, 14/11/2026).
      /// </summary>
      public static IReadOnlyList<ExternalWorldEvent> List(string sourceMod)
      {
         WorldEventStore store = WorldEventStore.Instance;
         if (store == null || string.IsNullOrWhiteSpace(sourceMod)) return new List<ExternalWorldEvent>();

         string source = sourceMod.Trim();

         return store.All()
                     .Where(w => w != null && string.Equals(w.SourceMod, source, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(w => w.Day)
                     .Select(w => new ExternalWorldEvent {
                        Id = w.Id, Summary = w.Summary, Day = w.Day, Magnitude = w.Magnitude, SourceMod = w.SourceMod, PlaceId = w.PlaceId,
                        HeroIds = (w.ActorIds ?? new List<string>()).ToList(), FactionIds = (w.FactionIds ?? new List<string>()).ToList(),
                        Lifetime = w.IsPersistentDeed ? ExternalEventLifetime.Deed : ExternalEventLifetime.Rumor,
                        KnownOnlyToThoseInvolved = w.KnownOnlyToThoseInvolved, KnownToHeroIds = (w.KnownToIds ?? new List<string>()).ToList(),
                        HiddenFromHeroIds = (w.HiddenFromIds ?? new List<string>()).ToList(),
                        Details = EventDetailsStore.Read(w.DetailsKey)
                     })
                     .ToList();
      }

      // Details beyond the cap are refused whole: the request was "no silent truncation" (17/11/2026).
      private static bool DetailsAccepted(ExternalWorldEvent e, string details)
      {
         if (ExternalEventRules.DetailsFit(details)) return true;

         ModLog.Info($"[FEED] {e.SourceMod}: details of {details.Length} characters refused whole (at most {ExternalEventRules.MaxDetailsChars}); the event was not recorded.");

         return false;
      }

      private static bool KeepDetails(WorldEvent worldEvent, string sourceMod, string details)
      {
         if (details == null) return true;

         string key = ExternalEventRules.DetailsKey(sourceMod, details);
         if (!EventDetailsStore.Write(key, details)) return false;

         worldEvent.DetailsKey = key;

         return true;
      }

      private static void NoteSummaryCut(ExternalWorldEvent e)
      {
         if (ExternalEventRules.SummaryIsCut(e.Summary))
            ModLog.Info($"[FEED] {e.SourceMod}: the summary of '{e.Id ?? "(no id)"}' was cut to {ExternalEventRules.MaxSummaryChars} characters (what characters hear); put the full text in Details.");
      }

      private static WorldEvent Build(ExternalWorldEvent e, string summary, int today)
         => new() {
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
            PlaceId = ResolveSettlement(e.PlaceId),
            ActorIds = ResolveHeroes((e.HeroIds ?? new List<string>()).Concat(e.ActorHeroIds ?? new List<string>()).Distinct()),
            FactionIds = ResolveFactions(e.FactionIds),
            InvolvesPlayer = ExternalEventRules.IsPlayersOwnDeed(e.ActorHeroIds, Hero.MainHero?.StringId),
            IsPersistentDeed = e.Lifetime == ExternalEventLifetime.Deed,
            // Secrets and who is kept out (since 2.8.10, Bellum Civile bridge RFC): the store's own fields, which every
            // awareness query already honours.
            KnownOnlyToThoseInvolved = e.KnownOnlyToThoseInvolved,
            KnownToIds = ResolveHeroes(e.KnownToHeroIds ?? new List<string>()),
            HiddenFromIds = ResolveHeroes(e.HiddenFromHeroIds ?? new List<string>())
         };

      private static bool ConsumeQuota(string source, int today)
      {
         lock (_quotaLock)
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
      }

      /// <summary>Keeps only the ids that resolve to a real kingdom or clan — resolve-or-drop.</summary>
      private static List<string> ResolveFactions(IEnumerable<string> ids)
      {
         var kept = new List<string>();

         if (ids == null) return kept;

         foreach (string id in ids)
            if (!string.IsNullOrEmpty(id) && (Kingdom.All?.Any(k => k.StringId == id) == true || Clan.All?.Any(c => c.StringId == id) == true))
               kept.Add(id);

         return kept;
      }

      /// <summary>The id if it names a real settlement, else null: resolve-or-drop, like the hero and faction ids.</summary>
      private static string ResolveSettlement(string id)
         => !string.IsNullOrEmpty(id) && Settlement.All?.Any(s => s.StringId == id) == true
            ? id
            : null;

      /// <summary>Keeps only the ids that resolve to a real hero (alive or dead) — resolve-or-drop.</summary>
      private static List<string> ResolveHeroes(IEnumerable<string> ids)
      {
         var kept = new List<string>();

         if (ids == null) return kept;

         // Hero.FindFirst is the reliable lookup; MBObjectManager.GetObject<Hero> does not resolve heroes by StringId.
         foreach (string id in ids)
            if (!string.IsNullOrEmpty(id) && Hero.FindFirst(h => h.StringId == id) != null)
               kept.Add(id);

         return kept;
      }

      #endregion
   }
}