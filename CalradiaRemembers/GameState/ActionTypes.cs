// Code written by Gabriel Mailhot, 30/05/2026. Sprint 10.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
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

        /// <summary>
        ///   StringId of the item the player last held out with the bag in THIS conversation, kept past its turn so a
        ///   sale haggled over several replies still sells that item (3chmidt, 13/10/2026). Only sell_item reads it; a
        ///   give_item stays bound to this turn's <see cref="OfferedItemId" />.
        /// </summary>
        public string ItemOnTheTableId { get; set; }

        /// <summary>
        ///   Everything the player has said in this conversation, or null on a path that carries no conversation
        ///   (a council lift, a manual menu, a self-test). A verb that acts "at the player's word" reads it to know
        ///   the word was said (Maifailun, 01/10/2026: a bow turned a lady against someone nobody had named).
        /// </summary>
        public string PlayerWordsThisConversation { get; set; }
    }

    /// <summary>The roster item a give_item or sell_item would take, as the player's confirmation names it.</summary>
    public sealed class ItemPreview
    {
        /// <summary>Its StringId, carried into the pending action so exactly this item leaves.</summary>
        public string Id { get; set; }

        /// <summary>Its display name.</summary>
        public string Name { get; set; }

        /// <summary>Its market value in denars.</summary>
        public int Value { get; set; }
    }

    /// <summary>
    ///   The real terms a counter_offer would settle, for the player's confirmation and the executor alike (shared
    ///   the same way <see cref="ItemPreview" /> is for a sale): the parsed terms re-checked against what is open
    ///   to her NOW (CaptiveOfferPolicy, re-decided through EncounterContextBuilder.ResolveCaptiveOffer), the
    ///   ransom already clamped to the payer's purse and the single-transfer ceiling, and the realm fealty would
    ///   swear her house to. <see cref="Refusal" /> set means nothing else here is meaningful: the bridge refuses
    ///   and changes nothing.
    /// </summary>
    public sealed class CounterOfferPreview
    {
        /// <summary>The terms she struck, in the order named, each one confirmed open to her right now.</summary>
        public IReadOnlyList<CaptiveTerm> Terms { get; set; } = new List<CaptiveTerm>();

        /// <summary>The ransom in denars, clamped to the payer's purse and GoldTransferLimit.MaxSingleTransfer.</summary>
        public int Ransom { get; set; }

        /// <summary>The ransom above was brought down from the amount named.</summary>
        public bool RansomClampedToPurse { get; set; }

        /// <summary>The realm she would swear her house to, when fealty is one of her terms; null otherwise.</summary>
        public string FealtyRealm { get; set; }

        /// <summary>Why the offer could not be settled, as a plain in-character note; null when it can be.</summary>
        public string Refusal { get; set; }
    }

    /// <summary>
    ///   The visible result of executing one action, surfaced as a line in the chat
    ///   window. Returned null by the bridge when an action changed nothing
    ///   (unknown type, gated out, invalid parameters).
    /// </summary>
    public sealed class ActionOutcome
    {
        public ActionOutcome(string message, bool isPositive, int? appliedRelationDelta = null, int? appliedGoldAmount = null, string recap = null)
        {
            Message = message;
            IsPositive = isPositive;
            AppliedRelationDelta = appliedRelationDelta;
            AppliedGoldAmount = appliedGoldAmount;
            Recap = recap;
        }

        private ActionOutcome(string message, bool unknownVerb = false, string recap = null)
        {
            Message = message;
            IsPositive = false;
            WasRefused = true;
            WasUnknownVerb = unknownVerb;
            Recap = recap;
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
        public static ActionOutcome Refused(string message, string recap = null) => new(message, unknownVerb: false, recap: recap);

        /// <summary>
        ///   The bridge refused this action because its type is not a real mod verb (the model invented one,
        ///   e.g. "grant a fief" / "bless a marriage" with no executor). Like <see cref="Refused" /> it changed
        ///   nothing and earns the transcript note, but it ALSO flags <see cref="WasUnknownVerb" /> so the chat
        ///   can (a) always log the invented verb with its context for the author and (b) show a deed-scoped
        ///   base message plus, when the player opts in, a report hint, instead of the raw refusal line.
        /// </summary>
        public static ActionOutcome RefusedUnknownVerb(string message) => new(message, unknownVerb: true);

        /// <summary>Short, in-character-adjacent description, e.g. "Martira regards you more warmly. (+1)".</summary>
        public string Message { get; }

        /// <summary>
        ///   Optional plain-fact recap an EXTERNAL action asked the NPC to voice in the same beat (from
        ///   <see cref="NpcMemoryService.Core.Extension.ActionResult.Recap" />). Null for every built-in action and
        ///   for any external action that did not set it. When present and a conversation is open, the chat fires
        ///   one voice-only NPC follow-up turn so the NPC speaks the outcome its pre-execution reply could not know.
        /// </summary>
        public string Recap { get; }

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
