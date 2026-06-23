// Code written by Gabriel Mailhot, 23/06/2026.
// Companion missions (ROADMAP) — the PERSISTED record of one errand a companion is away on. The companion
// physically travels as a real clan party (vanilla-serialized), so this logical state rides alongside it
// in the save; a reload restores the in-flight errand exactly (no save-scum for instant news / re-rolls).
// The DECISION logic (outcome, duration, report sizing, destination) is pure in CalradiaRemembers.Logic.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;

#endregion

namespace CalradiaRemembers.Missions
{
    /// <summary>Where a companion's errand is in its out → linger → back → reunion arc.</summary>
    public enum CompanionMissionPhase
    {
        /// <summary>Riding to the destination town.</summary>
        Outbound,

        /// <summary>At the town, gathering word for a day or three.</summary>
        Lingering,

        /// <summary>Riding back to the (moving) player.</summary>
        Returning,

        /// <summary>Waylaid and taken prisoner — the news waits with them until they are free and rejoin you.</summary>
        AwaitingReunion
    }

    /// <summary>One errand a companion has been dispatched on, awaiting their return with a report.</summary>
    public sealed class CompanionMission
    {
        /// <summary>Stable unique id (assigned on dispatch).</summary>
        public string Id { get; set; }

        /// <summary>StringId of the dispatched companion (a real hero, vanilla-serialized).</summary>
        public string CompanionId { get; set; }

        /// <summary>Cached display name, so the report still reads right if the hero is gone on return.</summary>
        public string CompanionName { get; set; }

        public CompanionMissionType Type { get; set; }

        /// <summary>The town they ride to for news (StringId + cached name).</summary>
        public string DestinationSettlementId { get; set; }
        public string DestinationName { get; set; }

        /// <summary>StringId of the companion's traveling party while out (vanilla-serialized; null once captured/home).</summary>
        public string PartyId { get; set; }

        /// <summary>In-game day the errand began.</summary>
        public int DispatchDay { get; set; }

        /// <summary>In-game day the companion may leave the town for home (set on arrival).</summary>
        public int LingerUntilDay { get; set; }

        /// <summary>The errand's difficulty (0..100) — feeds the duration.</summary>
        public int Difficulty { get; set; }

        public CompanionMissionPhase Phase { get; set; }

        /// <summary>Outcome rolled AT DISPATCH and persisted, so a reload cannot re-roll a better report.</summary>
        public MissionResult Quality { get; set; }

        /// <summary>The news gathered AT ARRIVAL and persisted (a reload cannot fetch fresher word). Empty until then.</summary>
        public List<string> News { get; set; } = new List<string>();

        /// <summary>True once the errand's payload (news / intel / loot) was secured at the destination.</summary>
        public bool NewsGathered { get; set; }

        /// <summary>For a STEAL errand: the denars lifted (rolled at arrival, granted on reunion). 0 otherwise.</summary>
        public int LootGold { get; set; }
    }
}
