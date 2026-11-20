// Code written by Gabriel Mailhot, 14/11/2026.
// Extension Surface: what a character knows of the world. VonLemberg's Bellum Civile bridge RFC (14/11/2026, P0-C): "an API
// that lets a background NPC interaction access the same actor-scoped knowledge that CR already calculates for
// conversations", with "no omniscience". CR already decides, for any hero at any time, which world events have reached
// them, how surely, and which secrets they were let in on (WorldEventStore); this is a read of it, so another mod can keep
// its own prompts true to what each character knows. Gabriel: "go pour les étapes 1 et 2". Since 2.8.10.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using CalradiaRemembers.WorldEvents;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers
{
   /// <summary>One world event a character knows of. Since 2.8.10.</summary>
   public sealed class CrKnownEvent
   {
      /// <summary>The event's id (a Feed event keeps the id its mod gave it).</summary>
      public string Id { get; set; }

      /// <summary>What happened, as CR tells it.</summary>
      public string Summary { get; set; }

      /// <summary>The game day it happened.</summary>
      public int Day { get; set; }

      /// <summary>How much it weighs, 0 to 100.</summary>
      public int Magnitude { get; set; }

      /// <summary>How surely it reached them: "well known", "secondhand" or "distant" (uncertain).</summary>
      public string Confidence { get; set; }

      /// <summary>A secret they were let in on, not public news.</summary>
      public bool IsSecret { get; set; }

      /// <summary>The mod that fed it, or null for CR's own.</summary>
      public string SourceMod { get; set; }
   }

   /// <summary>One public event of a realm, for a mod writing its own digest. Since 2.8.15.</summary>
   public sealed class CrRealmEvent
   {
      /// <summary>The event's id (a Feed event keeps the id its mod gave it).</summary>
      public string Id { get; set; }

      /// <summary>What happened, as CR tells it (the short line characters hear).</summary>
      public string Summary { get; set; }

      /// <summary>The game day it happened.</summary>
      public int Day { get; set; }

      /// <summary>How much it weighs, 0 to 100.</summary>
      public int Magnitude { get; set; }

      /// <summary>The heroes it names (StringIds).</summary>
      public List<string> HeroIds { get; set; } = new();

      /// <summary>The kingdoms and clans it concerns (StringIds).</summary>
      public List<string> FactionIds { get; set; } = new();

      /// <summary>The settlement where it happened (StringId), or null.</summary>
      public string PlaceId { get; set; }

      /// <summary>The mod that fed it, or null for CR's own.</summary>
      public string SourceMod { get; set; }
   }

   /// <summary>The door to what characters know of the world. Read only; game thread. Since 2.8.10.</summary>
   public static class CrWorld
   {
      /// <summary>
      ///   The world events <paramref name="heroId" /> knows of today, most telling first, at most <paramref name="max" />: news
      ///   that reached them by distance, realm, rank and recency, and secrets they are in on; never what was hidden from them.
      ///   The same selection a conversation with them is built from. Empty for an unknown hero or outside a campaign.
      /// </summary>
      public static IReadOnlyList<CrKnownEvent> KnownEvents(string heroId, int max = 10)
      {
         WorldEventStore store = WorldEventStore.Instance;
         Hero hero = string.IsNullOrEmpty(heroId) || Campaign.Current == null ? null : Hero.FindFirst(h => h.StringId == heroId);
         if (store == null || hero == null || max <= 0) return Array.Empty<CrKnownEvent>();

         var today = (int) CampaignTime.Now.ToDays;

         return store.GetKnownEventsRated(hero, today, max)
                     .Where(x => x.Event != null)
                     .Select(x => new CrKnownEvent {
                        Id = x.Event.Id, Summary = x.Event.Summary, Day = x.Event.Day, Magnitude = x.Event.Magnitude, Confidence = Word(x.Confidence),
                        IsSecret = x.Event.KnownOnlyToThoseInvolved, SourceMod = x.Event.SourceMod
                     })
                     .ToList();
      }

      /// <summary>
      ///   Whether <paramref name="heroId" /> would by now have heard of the event <paramref name="eventId" />. False when either is
      ///   unknown. This is plausibility TODAY, recomputed at each call from where the hero stands, the realm they serve and the
      ///   event's age; nothing is recorded, so the answer can turn false as the news ages (it fades over 40 days), as the hero
      ///   rides away or changes realm. It is not a record of what they were told (VonLemberg, 16/11/2026): keep your own if you
      ///   need one.
      /// </summary>
      public static bool IsAwareOf(string heroId, string eventId)
      {
         WorldEventStore store = WorldEventStore.Instance;
         Hero hero = string.IsNullOrEmpty(heroId) || Campaign.Current == null ? null : Hero.FindFirst(h => h.StringId == heroId);
         WorldEvent e = string.IsNullOrEmpty(eventId) ? null : store?.GetById(eventId);

         return store != null && hero != null && e != null && store.IsAwareOf(hero, e, (int) CampaignTime.Now.ToDays);
      }

      /// <summary>
      ///   The PUBLIC events of a realm since <paramref name="sinceDay" />, oldest first, at most <paramref name="max" /> (the newest
      ///   kept): what CR recorded (wars, sieges, marriages, executions, its own and every mod's feed) and still holds, recent
      ///   news and the long-term history alike, that names the realm or one of its clans, or a hero of it. For a mod writing
      ///   its own digest of a kingdom (Bellum Civile / AI Rebel Relay integration request, 17/11/2026). Never a secret: an event
      ///   known only to those involved, let in on by a few, hidden from anyone, or only a guess, stays out, so a digest
      ///   cannot spread what CR keeps hidden. The short summary only; another mod's full Details stay with that mod.
      ///   Empty for an unknown realm or outside a campaign. Since 2.8.15.
      /// </summary>
      public static IReadOnlyList<CrRealmEvent> RealmEvents(string factionId, int sinceDay = 0, int max = 100)
      {
         WorldEventStore store = WorldEventStore.Instance;
         if (store == null || Campaign.Current == null || string.IsNullOrEmpty(factionId) || max <= 0) return Array.Empty<CrRealmEvent>();

         Kingdom kingdom = Kingdom.All?.FirstOrDefault(k => k.StringId == factionId);
         Clan clan = kingdom == null ? Clan.All?.FirstOrDefault(c => c.StringId == factionId) : null;
         if (kingdom == null && clan == null) return Array.Empty<CrRealmEvent>();

         var ofRealm = new HashSet<string> {factionId};
         if (kingdom != null)
            foreach (Clan c in kingdom.Clans ?? Enumerable.Empty<Clan>())
               if (c != null) ofRealm.Add(c.StringId);

         bool Concerns(WorldEvent w)
         {
            if (w.FactionIds?.Any(ofRealm.Contains) == true) return true;

            return w.ActorIds?.Any(id => Hero.FindFirst(h => h.StringId == id)?.Clan is { } hc && ofRealm.Contains(hc.StringId)) == true;
         }

         return store.All().Concat(store.AllHistory())
                     .Where(w => w != null && w.Day >= sinceDay && IsPublic(w) && Concerns(w))
                     .GroupBy(w => w.Id ?? w.GetHashCode().ToString()).Select(g => g.First())
                     .OrderByDescending(w => w.Day).Take(max).OrderBy(w => w.Day)
                     .Select(w => new CrRealmEvent {
                        Id = w.Id, Summary = w.Summary, Day = w.Day, Magnitude = w.Magnitude, PlaceId = w.PlaceId, SourceMod = w.SourceMod,
                        HeroIds = (w.ActorIds ?? new List<string>()).ToList(), FactionIds = (w.FactionIds ?? new List<string>()).ToList()
                     })
                     .ToList();
      }

      // What a whole realm may be told: nothing kept to a few, kept from anyone, or only guessed.
      internal static bool IsPublic(WorldEvent w)
         => !w.KnownOnlyToThoseInvolved && !w.OnlyAGuess && (w.HiddenFromIds == null || w.HiddenFromIds.Count == 0)
            && (w.KnownToIds == null || w.KnownToIds.Count == 0);

      private static string Word(RumorConfidence confidence) => confidence switch {
         RumorConfidence.WellKnown => "well known",
         RumorConfidence.Secondhand => "secondhand",
         _ => "distant"
      };
   }

   /// <summary>The version of CR's whole extension surface, for a mod that detects what it can use. Since 2.8.10.</summary>
   public static class CrApi
   {
      /// <summary>
      ///   Raised by one at each release that adds a door; never lowered, and a door once shipped stays. 1 = Calradia Remembers
      ///   2.8.10 (CrDiplomacy, CrWorld, CrCharacterText, CrLore, CrPortraits, Feed secrecy fields, CrPrompt.Reload,
      ///   CrPrompt.StockSections, Feed.List, CrAims.List). 2 = 2.8.12 (CrPrompt.Persona and PersonaText, CrLlm). 3 = 2.8.13
      ///   (CrConversation.ShowPicture and OpenSessionId). 4 = 2.8.15 (CrLlmRequest.Model, CrLlmResult.Model,
      ///   ExternalWorldEvent.Details, CrWorld.RealmEvents). 5 = 2.8.17 (CrBalanceOfPower).
      ///   Older doors keep their own version constants (CrNpcMemory.ApiVersion, Feed.ApiVersion). A mod without a compile-time
      ///   reference reads this field by reflection: absent means older than 2.8.10.
      /// </summary>
      public const int Version = 5;
   }
}
