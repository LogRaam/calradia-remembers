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

        private ActionOutcome(string message)
        {
            Message = message;
            IsPositive = false;
            WasRefused = true;
        }

        /// <summary>
        ///   The bridge DECLINED this action: a guard fired and no game state was touched. Use this rather
        ///   than a plain unfavorable outcome, because the two are not the same thing and the chat tells the
        ///   model about them differently. A refusal is written into the transcript as an out-of-character
        ///   note ("it did not happen, do not treat it as done") so the model cannot celebrate a marriage
        ///   the bridge refused and then call the player "husband" on the next turn.
        ///   <para>
        ///     <see cref="IsPositive" /> alone cannot carry this: it drives the chat line COLOUR, so it is
        ///     false both for "the bridge said no" and for "this really happened and it hurt" (a relation
        ///     loss that WAS applied, a gift that WAS handed over, a wound that WAS taken). Reading it as a
        ///     refusal told the model that real, applied consequences had never happened, which is the exact
        ///     lie the transcript note exists to prevent.
        ///   </para>
        /// </summary>
        public static ActionOutcome Refused(string message) => new(message);

        /// <summary>Short, in-character-adjacent description, e.g. "Martira regards you more warmly. (+1)".</summary>
        public string Message { get; }

        /// <summary>True if the outcome is favorable to the player (drives the chat line color).</summary>
        public bool IsPositive { get; }

        /// <summary>
        ///   True only when the bridge REFUSED to act and changed nothing (see <see cref="Refused" />).
        ///   False for an action that executed, however unfavorable its result. Defaults to false so a
        ///   site that forgets to declare itself merely loses the transcript note, rather than telling
        ///   the model that something which really happened did not.
        /// </summary>
        public bool WasRefused { get; }

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
