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
        public ActionOutcome(string message, bool isPositive, int? appliedRelationDelta = null, int? appliedGoldAmount = null)
        {
            Message = message;
            IsPositive = isPositive;
            AppliedRelationDelta = appliedRelationDelta;
            AppliedGoldAmount = appliedGoldAmount;
        }

        private ActionOutcome(string message, bool unknownVerb = false)
        {
            Message = message;
            IsPositive = false;
            WasRefused = true;
            WasUnknownVerb = unknownVerb;
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

        /// <summary>
        ///   The bridge refused this action because its type is not a real mod verb (the model invented one,
        ///   e.g. "grant a fief" / "bless a marriage" with no executor). Like <see cref="Refused" /> it changed
        ///   nothing and earns the transcript note, but it ALSO flags <see cref="WasUnknownVerb" /> so the chat
        ///   can (a) always log the invented verb with its context for the author and (b) show a deed-scoped
        ///   base message plus, when the player opts in, a report hint, instead of the raw refusal line.
        /// </summary>
        public static ActionOutcome RefusedUnknownVerb(string message) => new(message, true);

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
        ///   True only when the refusal was an UNKNOWN VERB: the model emitted an [ACTION] whose type has no
        ///   executor (see <see cref="RefusedUnknownVerb" />). Distinct from a known verb refused by a guard,
        ///   because the chat handles it differently: it logs the invented verb for the author and shows a
        ///   deed-scoped notice. Defaults to false, so an ordinary guard refusal is never mistaken for one.
        /// </summary>
        public bool WasUnknownVerb { get; }

        /// <summary>
        ///   Regard audit 2.1: for a change_relation action, the regard actually APPLIED after gating and clamping
        ///   (which may be 0, or smaller in magnitude than the LLM proposed). Null for every other action. The
        ///   StoryLog and the public-affront rumor read THIS, not the raw proposed delta, so all three channels
        ///   (score, journal, gossip) tell one truth: a gain gated to 0 leaves no "seems to appreciate you" line,
        ///   and an affront that never applied never becomes a rumor.
        /// </summary>
        public int? AppliedRelationDelta { get; }

        /// <summary>
        ///   Truthful-channels (Pattern 3): for a give_gold / take_gold action, the coin actually MOVED after
        ///   clamping to the real purse (which may be smaller than the LLM proposed, or 0). Null for every other
        ///   action. The StoryLog reads THIS, not the raw parsed amount, so the journal never claims a sum the
        ///   purse never saw. Mirrors <see cref="AppliedRelationDelta" />.
        /// </summary>
        public int? AppliedGoldAmount { get; }
    }
}
