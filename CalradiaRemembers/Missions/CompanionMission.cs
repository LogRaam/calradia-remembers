// Code written by Gabriel Mailhot, 23/06/2026.
// Companion missions (ROADMAP "Also planned") — the persisted record of one errand a companion is away
// on. The DECISION logic (outcome, duration, report sizing) is pure in CalradiaRemembers.Logic; this is
// just the serialized state the behavior tracks between dispatch and return.

#region

using CalradiaRemembers.Logic;

#endregion

namespace CalradiaRemembers.Missions
{
    /// <summary>One errand a companion has been dispatched on, awaiting their return with a report.</summary>
    public sealed class CompanionMission
    {
        /// <summary>Stable unique id (assigned on dispatch).</summary>
        public string Id { get; set; }

        /// <summary>StringId of the dispatched companion.</summary>
        public string CompanionId { get; set; }

        /// <summary>Cached display name, so a report still reads right if the hero is gone on return.</summary>
        public string CompanionName { get; set; }

        public CompanionMissionType Type { get; set; }

        /// <summary>In-game day the errand began.</summary>
        public int DispatchDay { get; set; }

        /// <summary>In-game day the companion is due back with a report.</summary>
        public int ReturnDay { get; set; }

        /// <summary>The errand's difficulty (0..100) — feeds the outcome roll and the duration.</summary>
        public int Difficulty { get; set; }
    }
}
