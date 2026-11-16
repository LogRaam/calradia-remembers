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

      /// <summary>Whether <paramref name="heroId" /> would by now have heard of the event <paramref name="eventId" />. False when either is unknown.</summary>
      public static bool IsAwareOf(string heroId, string eventId)
      {
         WorldEventStore store = WorldEventStore.Instance;
         Hero hero = string.IsNullOrEmpty(heroId) || Campaign.Current == null ? null : Hero.FindFirst(h => h.StringId == heroId);
         WorldEvent e = string.IsNullOrEmpty(eventId) ? null : store?.GetById(eventId);

         return store != null && hero != null && e != null && store.IsAwareOf(hero, e, (int) CampaignTime.Now.ToDays);
      }

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
      ///   (CrConversation.ShowPicture and OpenSessionId).
      ///   Older doors keep their own version constants (CrNpcMemory.ApiVersion, Feed.ApiVersion). A mod without a compile-time
      ///   reference reads this field by reflection: absent means older than 2.8.10.
      /// </summary>
      public const int Version = 3;
   }
}
