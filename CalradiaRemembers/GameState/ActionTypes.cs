// Code written by Gabriel Mailhot, 30/05/2026. Sprint 10.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.GameState
{
    /// <summary>
    ///   Per-exchange context an action needs to be resolved correctly — chiefly
    ///   the NPC profile (for the relation cooldown stamp) and the event the LLM
    ///   emitted this turn (which gates how large a positive relation gain may be).
    /// </summary>
    public sealed class ActionContext
    {
        public ActionContext(NpcProfile profile, NotableEventType? emittedEvent)
        {
            Profile = profile;
            EmittedEvent = emittedEvent;
        }

        public NpcProfile Profile { get; }
        public NotableEventType? EmittedEvent { get; }

        /// <summary>
        ///   StringId of the item the player picked from the bag this turn, when any. give_item transfers
        ///   exactly this, so a bag-picked gift never depends on the LLM echoing the item's display name.
        /// </summary>
        public string OfferedItemId { get; set; }
    }

    /// <summary>
    ///   The visible result of executing one action, surfaced as a line in the chat
    ///   window. Returned null by the bridge when an action changed nothing
    ///   (unknown type, gated out, invalid parameters).
    /// </summary>
    public sealed class ActionOutcome
    {
        public ActionOutcome(string message, bool isPositive, int? appliedRelationDelta = null)
        {
            Message = message;
            IsPositive = isPositive;
            AppliedRelationDelta = appliedRelationDelta;
        }

        /// <summary>Short, in-character-adjacent description, e.g. "Martira regards you more warmly. (+1)".</summary>
        public string Message { get; }

        /// <summary>True if the outcome is favorable to the player (drives the chat line color).</summary>
        public bool IsPositive { get; }

        /// <summary>
        ///   Regard audit 2.1: for a change_relation action, the regard actually APPLIED after gating and clamping
        ///   (which may be 0, or smaller in magnitude than the LLM proposed). Null for every other action. The
        ///   StoryLog and the public-affront rumor read THIS, not the raw proposed delta, so all three channels
        ///   (score, journal, gossip) tell one truth: a gain gated to 0 leaves no "seems to appreciate you" line,
        ///   and an affront that never applied never becomes a rumor.
        /// </summary>
        public int? AppliedRelationDelta { get; }
    }
}
