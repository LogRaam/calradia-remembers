// Code written by Gabriel Mailhot, 23/06/2026.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;

#endregion

namespace CalradiaRemembers.WorldEvents
{
   /// <summary>The kind of world happening — drives both rumor phrasing and chronicle phrasing.</summary>
   public enum WorldEventType
   {
      Battle,
      SettlementCaptured,
      Marriage,
      Execution,
      WarDeclared,
      PeaceDeclared,

      /// <summary>A notable personal deed of the player's (a feat of arms, a tournament win, a wedding).</summary>
      PlayerFeat,

      Other,

      /// <summary>A public affront (an insult/disgrace before witnesses) — gossip that spreads, then fades.</summary>
      Affront,

      /// <summary>Bad blood between two lords, inferred from their standing — ambient gossip.</summary>
      Rivalry,

      /// <summary>
      ///   A hidden birth come to light — a bastard scandal that dents the player's name and spreads as
      ///   gossip. Added last to preserve the serialized ordinals of existing saves.
      /// </summary>
      Scandal,

      /// <summary>
      ///   A standing world condition (no vanilla event of its own): two kingdoms not yet at war but tense
      ///   enough that war seems near. Recorded by the weekly world-conditions feeder. Added last to preserve
      ///   the serialized ordinals of existing saves.
      /// </summary>
      WarLooming,

      /// <summary>
      ///   A standing world condition (no vanilla event of its own): a town in real economic distress (a food
      ///   shortage and/or collapsing prosperity). Recorded by the weekly world-conditions feeder. Added last
      ///   to preserve the serialized ordinals of existing saves.
      /// </summary>
      Hardship,

      /// <summary>
      ///   A piece of local news a CHARACTER asserted in conversation that nobody had told him - a blight,
      ///   brigands on a road, a death, an omen, a quarrel at some hall. Not something the engine recorded:
      ///   something an NPC made up, judged safe to adopt, and now travelling as ordinary rumour so a second
      ///   character can have heard it.
      ///   <para>
      ///     Gabriel, 2026-09-12: "Les nouveaux faits proviennent QUE des NPC, pas du joueur. Le joueur subit
      ///     le Monde alors que les NPC le définissent." Only subjects the engine does not model reach this
      ///     type, so an invention can never contradict a screen the player can open - see WorldClaimPolicy.
      ///   </para>
      ///   Added last to preserve the serialized ordinals of existing saves.
      /// </summary>
      HeardTell,

      /// <summary>
      ///   The world's own account of a season just ended: a realm grew richer, took a stronghold, saw its
      ///   armies thin. Computed deterministically from the almanac's power facts rather than written by a
      ///   model (Gabriel's ruling, 2026-09-12), and recorded as ordinary news so the ageing, the awareness
      ///   and the confidence rating all already apply - the historian must not become a fourth memory store.
      ///   Added last to preserve the serialized ordinals of existing saves.
      /// </summary>
      SeasonalTurn,

      /// <summary>
      ///   A house rose in rebellion against its realm (FloatingMammal, 02/10/2026: a former rebel, received back, spoke
      ///   as a loyalist). FactionIds[0] is the realm risen against; ActorIds are the house's members. Also how the
      ///   house's return to that realm is recognised. Added last to preserve the serialized ordinals of existing saves.
      /// </summary>
      Rebellion,

      /// <summary>
      ///   A marriage ended while both spouses live, by a mod other than CR (tashmetu, 02/10/2026: Bridges to Remember
      ///   divorces by clearing Hero.Spouse). Added last to preserve the serialized ordinals of existing saves.
      /// </summary>
      Divorce
   }

   /// <summary>
   ///   One dated thing that happened in Calradia. Mechanically generated (zero LLM) by feeders
   ///   listening to campaign events and mod outcomes, then consumed three ways: street rumors a
   ///   commoner shares, "what you've heard" injected into an NPC's prompt, and the player's own
   ///   chronicle. <see cref="InvolvesPlayer" /> events are the chronicle and are kept long-term;
   ///   third-party events are rumor fodder and age out. Serialized as JSON inside the save.
   /// </summary>
   public sealed class WorldEvent : IRumorRecord, IHistoryRecord
   {
      /// <summary>Hero StringIds involved (winner/loser, bride/groom, executioner/executed…).</summary>
      public List<string> ActorIds { get; set; } = new();

      /// <summary>
      ///   For an adversarial player deed: the HERO StringIds it harmed directly (the slain, the beaten,
      ///   the dispossessed). The influence loop reads it so a deed that struck the observer's own KIN
      ///   wounds them even when the broad faction tag does not match. Empty otherwise; save-safe default.
      /// </summary>
      public List<string> AggrievedActorIds { get; set; } = new();

      /// <summary>
      ///   For an adversarial deed of the PLAYER's (a battle won, an execution, a conquest): the faction
      ///   StringIds it HARMED — the losing/dispossessed side. The influence loop reads it to colour how
      ///   a lord who merely heard of the deed regards the player (a wound to one's own, a boon to one's
      ///   champion). Empty for benign deeds (marriage, peace) and for third-party events. Save-safe:
      ///   absent in pre-influence-loop saves, where it deserializes to an empty list (valence then
      ///   degrades to faction-only).
      /// </summary>
      public List<string> AggrievedFactionIds { get; set; } = new();

      /// <summary>
      ///   For <see cref="InvolvesPlayer" /> events: a chronicler-voice line (no date — the writer
      ///   prefixes it) used by the player's chronicle. Null for third-party events. Kept separate
      ///   from <see cref="Summary" /> so the same deed reads as neutral rumor to NPCs and as epic
      ///   chronicle to the player.
      /// </summary>
      public string ChronicleLine { get; set; }

      /// <summary>In-game day the event occurred.</summary>
      public int Day { get; set; }

      /// <summary>Faction (kingdom/clan) StringIds the event is relevant to, for awareness filtering.</summary>
      public List<string> FactionIds { get; set; } = new();

      /// <summary>Stable unique id (assigned on record).</summary>
      public string Id { get; set; }

      /// <summary>True when the player is one of the actors — marks this as chronicle material.</summary>
      public bool InvolvesPlayer { get; set; }

      /// <summary>
      ///   True for a fact fed through the Extension Surface (<see cref="Feed" />) with
      ///   <see cref="ExternalEventLifetime.Deed" />: kept long-term under its own cap
      ///   (<see cref="CalradiaRemembers.Logic.WorldEventPruner.MaxExternalDeedEvents" />) instead of
      ///   aging out as a rumour. False for CR's own events and for pre-existing saves, where it
      ///   deserializes to its default (save-safe).
      /// </summary>
      public bool IsPersistentDeed { get; set; }

      /// <summary>How far the news carries (0..100): a king's execution travels far, a skirmish barely.</summary>
      public int Magnitude { get; set; }

      /// <summary>StringId of the settlement it happened at/near, or null when placeless.</summary>
      public string PlaceId { get; set; }

      /// <summary>Reserved for v1.3 schemes (active/resolved/discovered); null for a settled fact.</summary>
      public string Status { get; set; }

      /// <summary>
      ///   The id of the mod that fed this event through the Extension Surface (<see cref="Feed" />), or null
      ///   for CR's own events. Lets a misbehaving source be debugged, filtered, or muted (discipline #4).
      /// </summary>
      public string SourceMod { get; set; }

      /// <summary>A short, ready-to-inject natural-language line ("Lord X put Lord Y to flight at Z").</summary>
      public string Summary { get; set; }

      public WorldEventType Type { get; set; }

      /// <summary>
      ///   Known only to those it names (ActorIds, AggrievedActorIds), whatever the score: set by the player with
      ///   cr.world_event_secret (IdentityCrysys, 03/10/2026). Absent on an older save, so false.
      /// </summary>
      public bool KnownOnlyToThoseInvolved { get; set; }

      /// <summary>Heroes the player kept unaware of this event (cr.world_event_hide). Absent on an older save, so empty.</summary>
      public List<string> HiddenFromIds { get; set; } = new();

      /// <summary>
      ///   For a battle: the men on the field when it began, both sides. The world's long-term memory keeps a battle of
      ///   several hundred men or more (Gabriel, 17/10/2026, design 7.7a), which the magnitude cannot tell from a clash
      ///   of a hundred. 0 when unknown: any other event, and battles recorded before 2.7.13.
      /// </summary>
      public int MenAtStart { get; set; }

      /// <summary>For a battle: it was a siege, which the long-term memory keeps whatever its size. Absent on an older save, so false.</summary>
      public bool IsSiege { get; set; }
   }
}