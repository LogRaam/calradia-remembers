// Code written by Gabriel Mailhot, 09/09/2026.
// The inward half of IProvidesGuidance (30/08/2026), which was built so a third-party action could tell an NPC to
// decline in character instead of roleplaying agreement to a deed that would then fail at execution, and whose
// only implementor in this repository is ExternalVerbAdapter. This is the same idea for CR's OWN verbs, structured
// rather than free-form so one policy owns the voice and the budget (see DeedUnavailabilityPolicy for why the two
// channels are kept apart).
//
// Optional, like IProvidesGuidance and for the same reason: every existing verb keeps compiling untouched, and
// VerbRegistryCore only asks a verb that actually implements it.

namespace CalradiaRemembers.Logic.Verbs
{
    /// <summary>
    ///   A verb that can name its own closed door BEFORE the model speaks, instead of only refusing afterwards.
    ///   Asked every turn, regardless of <see cref="IVerb{TContext,TAction,TResult}.IsEligible" />, which is the
    ///   entire point: an eligible verb has nothing to say here, and an ineligible one has everything.
    /// </summary>
    public interface IStatesUnavailability<TContext>
    {
        /// <summary>
        ///   The closed door to name this turn, or null to stay silent.
        ///   <para>
        ///     Return null when the verb is currently ELIGIBLE: it is being taught in full, and saying it cannot
        ///     be done would contradict the teaching two paragraphs above.
        ///   </para>
        ///   <para>
        ///     Return null, too, when the deed is not plausibly IN PLAY. This is the judgement each verb owns, and
        ///     getting it wrong is the one way this feature can hurt: a conversation with a village girl does not
        ///     need to be told that she cannot lend the player soldiers, buy a captive, or grant influence at
        ///     court. Silence is the default; a refusal is worth a line only when the deed is close enough that
        ///     the NPC might otherwise have promised it.
        ///   </para>
        /// </summary>
        DeedRefusal Unavailable(TContext ctx);
    }
}
