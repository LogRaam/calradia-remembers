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
   /// <summary>How long a fed event lives: a <see cref="Rumor" /> ages out, a <see cref="Deed" /> is kept.</summary>
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

      /// <summary>Hero StringIds the fact involves; CR keeps only those that resolve to real heroes.</summary>
      public List<string> HeroIds { get; set; } = new();

      /// <summary>Faction (kingdom/clan) StringIds the fact is relevant to; CR keeps only those that resolve.</summary>
      public List<string> FactionIds { get; set; } = new();

      /// <summary>Whether the fact ages out as a rumour or is kept as a deed.</summary>
      public ExternalEventLifetime Lifetime { get; set; } = ExternalEventLifetime.Rumor;

      /// <summary>The feeding mod's id — REQUIRED, for provenance, throttling, and muting a misbehaving source.</summary>
      public string SourceMod { get; set; }
   }
}
