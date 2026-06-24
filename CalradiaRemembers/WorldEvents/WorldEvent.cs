// Code written by Gabriel Mailhot, 19/06/2026.
// v1.2 (world-event / rumor store): a dated, persisted record of something that happened in the
// world — the shared source for street rumors, NPC awareness, and the player's chronicle.

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
        Rivalry
    }

    /// <summary>
    ///   One dated thing that happened in Calradia. Mechanically generated (zero LLM) by feeders
    ///   listening to campaign events and mod outcomes, then consumed three ways: street rumors a
    ///   commoner shares, "what you've heard" injected into an NPC's prompt, and the player's own
    ///   chronicle. <see cref="InvolvesPlayer"/> events are the chronicle and are kept long-term;
    ///   third-party events are rumor fodder and age out. Serialized as JSON inside the save.
    /// </summary>
    public sealed class WorldEvent : IRumorRecord
    {
        /// <summary>Stable unique id (assigned on record).</summary>
        public string Id { get; set; }

        public WorldEventType Type { get; set; }

        /// <summary>In-game day the event occurred.</summary>
        public int Day { get; set; }

        /// <summary>StringId of the settlement it happened at/near, or null when placeless.</summary>
        public string PlaceId { get; set; }

        /// <summary>True when the player is one of the actors — marks this as chronicle material.</summary>
        public bool InvolvesPlayer { get; set; }

        /// <summary>How far the news carries (0..100): a king's execution travels far, a skirmish barely.</summary>
        public int Magnitude { get; set; }

        /// <summary>A short, ready-to-inject natural-language line ("Lord X put Lord Y to flight at Z").</summary>
        public string Summary { get; set; }

        /// <summary>
        ///   For <see cref="InvolvesPlayer"/> events: a chronicler-voice line (no date — the writer
        ///   prefixes it) used by the player's chronicle. Null for third-party events. Kept separate
        ///   from <see cref="Summary"/> so the same deed reads as neutral rumor to NPCs and as epic
        ///   chronicle to the player.
        /// </summary>
        public string ChronicleLine { get; set; }

        /// <summary>Hero StringIds involved (winner/loser, bride/groom, executioner/executed…).</summary>
        public List<string> ActorIds { get; set; } = new List<string>();

        /// <summary>Faction (kingdom/clan) StringIds the event is relevant to, for awareness filtering.</summary>
        public List<string> FactionIds { get; set; } = new List<string>();

        /// <summary>
        ///   For an adversarial deed of the PLAYER's (a battle won, an execution, a conquest): the faction
        ///   StringIds it HARMED — the losing/dispossessed side. The influence loop reads it to colour how
        ///   a lord who merely heard of the deed regards the player (a wound to one's own, a boon to one's
        ///   champion). Empty for benign deeds (marriage, peace) and for third-party events. Save-safe:
        ///   absent in pre-influence-loop saves, where it deserializes to an empty list (valence then
        ///   degrades to faction-only).
        /// </summary>
        public List<string> AggrievedFactionIds { get; set; } = new List<string>();

        /// <summary>
        ///   For an adversarial player deed: the HERO StringIds it harmed directly (the slain, the beaten,
        ///   the dispossessed). The influence loop reads it so a deed that struck the observer's own KIN
        ///   wounds them even when the broad faction tag does not match. Empty otherwise; save-safe default.
        /// </summary>
        public List<string> AggrievedActorIds { get; set; } = new List<string>();

        /// <summary>Reserved for v1.3 schemes (active/resolved/discovered); null for a settled fact.</summary>
        public string Status { get; set; }
    }
}
