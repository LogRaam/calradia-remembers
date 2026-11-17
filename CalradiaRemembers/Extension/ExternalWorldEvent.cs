// Code written by Gabriel Mailhot, 27/06/2026.
// Extension Surface, Volet A — the PUBLIC inbound contract. A third-party mod constructs an ExternalWorldEvent
// and hands it to CR via CalradiaRemembers.Feed.Record(...); CR validates it at the door and weaves it into the
// same world-event store its own Chronicle, rumours and NPC awareness already read. This is a one-way WRITE
// door: outsiders push facts in; they never read or mutate CR's internals. Kept free of Bannerlord types (ids
// are plain strings) so the contract stays stable while CR's internals change behind it.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers
{
   /// <summary>
   ///   How long a fed event is STORED: a <see cref="Rumor" /> is pruned after a few weeks, a <see cref="Deed" />
   ///   is kept (under its own cap), so dedup on its id holds for good. Who hears it, and for how long, follows the
   ///   same recency curve for both.
   /// </summary>
   public enum ExternalEventLifetime
   {
      Rumor,
      Deed
   }

   /// <summary>The outcome of feeding an event through <see cref="Feed" /> (record / update / retract).</summary>
   public enum FeedResult
   {
      Recorded,
      RejectedInvalid,
      RejectedDuplicate,
      RejectedThrottled,
      RejectedNoStore,

      /// <summary>An existing entry with the same id was replaced (<see cref="Feed.Update" />).</summary>
      Updated,

      /// <summary>An entry was removed (<see cref="Feed.Retract" />).</summary>
      Retracted,

      /// <summary>No entry with that id was found to retract (<see cref="Feed.Retract" />).</summary>
      NotFound
   }

   /// <summary>
   ///   One world fact a third-party mod wants CR to know about. CR normalises it (clamps magnitude, bounds
   ///   the summary, resolves-or-drops ids, tags the source, throttles floods) and records it; from there an
   ///   NPC who'd plausibly have heard it gossips about it on their own.
   /// </summary>
   public sealed class ExternalWorldEvent
   {
      /// <summary>The source's STABLE id for this fact — CR dedups on it. Leave null and CR mints one.</summary>
      public string Id { get; set; }

      /// <summary>The source's own free-form category label (informational; CR files it as a generic event).</summary>
      public string Category { get; set; }

      /// <summary>The ready-to-inject natural-language line ("The grey mare of Lord Caladog won the Pravend races").</summary>
      public string Summary { get; set; }

      /// <summary>In-game day the fact occurred; CR fills today when 0 or less.</summary>
      public int Day { get; set; }

      /// <summary>How far the news carries (0..100); CR clamps it.</summary>
      public int Magnitude { get; set; }

      /// <summary>
      ///   Hero StringIds the fact CONCERNS (the owner of a besieged town, the victim of an execution); CR keeps only
      ///   those that resolve to real heroes. A named hero hears of it more readily, and it surfaces when the player
      ///   speaks of them. Naming the player here does not make it the player's deed (it did before 2.5.23).
      /// </summary>
      public List<string> HeroIds { get; set; } = new();

      /// <summary>
      ///   Hero StringIds of whoever DID it (the besieger, the executioner, the lord who took the town); may be empty.
      ///   Actors count as concerned too, so there is no need to repeat them in <see cref="HeroIds" />. List them
      ///   whoever they are: when the PLAYER is among them, CR files the fact as the player's own deed (their
      ///   chronicle, spoken of as something they did), so a bridge never has to ask whether an actor is the player.
      /// </summary>
      public List<string> ActorHeroIds { get; set; } = new();

      /// <summary>Faction (kingdom/clan) StringIds the fact is relevant to; CR keeps only those that resolve.</summary>
      public List<string> FactionIds { get; set; } = new();

      /// <summary>
      ///   The StringId of the settlement (town, castle or village) where it happened, or null. CR keeps it only
      ///   if it resolves. Characters near that place are likelier to have heard it, the nearer the likelier:
      ///   without a place, distance plays no part and only magnitude, realm and naming carry the news.
      /// </summary>
      public string PlaceId { get; set; }

      /// <summary>
      ///   Whether the stored fact is pruned after a few weeks (<see cref="ExternalEventLifetime.Rumor" />) or
      ///   kept (<see cref="ExternalEventLifetime.Deed" />). This governs STORAGE only: characters stop bringing
      ///   either up on the same recency curve.
      /// </summary>
      public ExternalEventLifetime Lifetime { get; set; } = ExternalEventLifetime.Rumor;

      /// <summary>The feeding mod's id — REQUIRED, for provenance, throttling, and muting a misbehaving source.</summary>
      public string SourceMod { get; set; }

      /// <summary>
      ///   A secret, not news: known only to the heroes it names (HeroIds, ActorHeroIds) and to <see cref="KnownToHeroIds" />,
      ///   however great or near. VonLemberg's Bellum Civile bridge RFC (14/11/2026): "Private deliberation, authorized NPC
      ///   knowledge, possible rumors, confirmed public outcome": a council's secret stays with its members, its outcome can
      ///   be fed later as news. Default false (public news, as before). Since 2.8.10.
      /// </summary>
      public bool KnownOnlyToThoseInvolved { get; set; }

      /// <summary>Heroes (StringIds) let in on a secret beyond those it names. Since 2.8.10.</summary>
      public List<string> KnownToHeroIds { get; set; } = new();

      /// <summary>Heroes (StringIds) who never hear of this event, secret or news. Since 2.8.10.</summary>
      public List<string> HiddenFromHeroIds { get; set; } = new();

      /// <summary>
      ///   The event's full text, beyond the short <see cref="Summary" /> characters hear (a kingdom digest of many developments,
      ///   a homeland's fall and who survived it). Kept whole, OUTSIDE the save, up to 20,000 characters; longer is refused
      ///   whole (RejectedInvalid), never cut. Comes back whole through <see cref="Feed.List" />, so a mod can digest or compress
      ///   it on its side and Update the record. Not put in any character's prompt today. On <see cref="Feed.Update" />, null
      ///   keeps the details already kept and an empty string removes them. Since 2.8.15 (Bellum Civile / AI Rebel Relay
      ///   integration request, 17/11/2026).
      /// </summary>
      public string Details { get; set; }
   }
}
