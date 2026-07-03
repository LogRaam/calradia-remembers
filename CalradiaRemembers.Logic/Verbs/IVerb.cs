// Code written by Gabriel Mailhot, 03/07/2026.
// Conversation verb contract spike: the pure, engine-agnostic shape shared by every conversation VERB
// (give_influence, lend_troops, sway_opinion, release_prisoner today; any future verb, internal or
// injected by another mod, tomorrow). TContext/TAction/TResult are left generic so this interface stays
// free of Hero/GameAction/ActionOutcome (all mod types) and is fully unit-testable in isolation; the
// mod's CalradiaRemembers.Verbs.IConversationVerb closes the generics over its own concrete types.

namespace CalradiaRemembers.Logic.Verbs
{
    /// <summary>
    ///   One self-contained verb: whether it currently applies, what it teaches the LLM, and how it
    ///   executes when invoked. A verb never needs to be understood by touching a switch statement or a
    ///   prompt-builder method elsewhere: everything about it lives on its own implementation.
    /// </summary>
    public interface IVerb<TContext, TAction, TResult>
    {
        /// <summary>The action vocabulary name this verb answers to (matches the [ACTION] "type:" the LLM emits).</summary>
        string Name { get; }

        /// <summary>True when this verb currently applies and should be taught / may be executed.</summary>
        bool IsEligible(TContext ctx);

        /// <summary>The prompt text taught to the LLM while this verb is eligible, or null/empty when not.</summary>
        string TeachingText(TContext ctx);

        /// <summary>Executes the verb. Implementations re-validate their own eligibility against live state ("the bridge is law").</summary>
        TResult Execute(TAction action, TContext ctx);
    }
}
