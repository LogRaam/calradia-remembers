// Code written by Gabriel Mailhot, 30/08/2026.
// Extension Surface, Actions guidance: an optional capability a verb (built-in, or in practice an
// ExternalVerbAdapter wrapping a third-party ExternalVerbContract) can implement to tell the LLM, PRE-response
// and regardless of eligibility, whether this turn's deed should be accepted or declined. Kept as a SEPARATE
// interface from IVerb rather than a new member on it, so every existing verb (built-in or already-registered
// external) keeps compiling unchanged; VerbRegistryCore only calls it when a verb actually implements it.

namespace CalradiaRemembers.Logic.Verbs
{
    /// <summary>
    ///   A verb that can offer the LLM guidance about itself EVERY turn, independent of
    ///   <see cref="IVerb{TContext,TAction,TResult}.IsEligible" />: the whole point is to let an ineligible
    ///   verb still tell the NPC to decline in-character, instead of the NPC roleplaying agreement to a deed
    ///   that will then fail at execution (the modder report this contract answers).
    /// </summary>
    public interface IProvidesGuidance<TContext>
    {
        /// <summary>
        ///   The guidance text to inject into the prompt this turn, positive or negative, or null/empty when
        ///   this verb has nothing to say. Called UNCONDITIONALLY, whether or not the verb is eligible.
        /// </summary>
        string Guidance(TContext ctx);
    }
}
