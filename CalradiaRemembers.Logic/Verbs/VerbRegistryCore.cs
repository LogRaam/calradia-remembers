// Code written by Gabriel Mailhot, 03/07/2026.
// Pure dispatch/composition engine behind every VerbRegistry: registers IVerb implementations, composes
// the teaching text of every currently-eligible one (in registration order, or an explicit order), and
// routes an incoming action to the verb whose Name matches. No engine dependency, so the algorithm itself
// is protected by tests, even though its concrete instantiation (VerbContext/GameAction/ActionOutcome)
// lives in the mod (CalradiaRemembers.Verbs.VerbRegistry).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CalradiaRemembers.Logic.Verbs
{
    /// <summary>
    ///   Holds a set of <see cref="IVerb{TContext,TAction,TResult}" /> and composes/dispatches over them.
    ///   TResult is expected to be a reference type: a lookup miss or an ineligible verb yields null.
    /// </summary>
    public sealed class VerbRegistryCore<TContext, TAction, TResult>
        where TResult : class
    {
        private readonly List<IVerb<TContext, TAction, TResult>> _verbs = new List<IVerb<TContext, TAction, TResult>>();

        /// <summary>Adds a verb. Order matters for the no-explicit-order overload of <see cref="ComposeTeachings(TContext)" />.</summary>
        public void Register(IVerb<TContext, TAction, TResult> verb)
        {
            if (verb != null) _verbs.Add(verb);
        }

        /// <summary>Composes the teaching text of every currently-eligible registered verb, in registration order.</summary>
        public string ComposeTeachings(TContext ctx, Action<Exception> onFault = null)
            => ComposeTeachings(ctx, _verbs.Select(v => v.Name), onFault);

        /// <summary>
        ///   Composes the teaching text of the named verbs, in the given order, skipping any name that is
        ///   not registered and any verb that is not currently eligible. Null when nothing was composed.
        ///   A verb whose eligibility or teaching THROWS is skipped, never allowed to escape: this runs on
        ///   every conversation open, and one buggy verb must not brick the chat window (a v1.21.0 crash
        ///   where a missing game API in a verb's IsEligible closed every conversation).
        /// </summary>
        public string ComposeTeachings(TContext ctx, IEnumerable<string> order, Action<Exception> onFault = null)
        {
            if (order == null) return null;

            var sb = new StringBuilder();
            foreach (string name in order)
            {
                string text = SafeTeachingFor(FindByName(name), ctx, onFault);
                if (!string.IsNullOrEmpty(text)) sb.Append(text);
            }

            return sb.Length == 0 ? null : sb.ToString();
        }

        /// <summary>
        ///   The eligible verb's teaching text, or null when it is unregistered, ineligible, or throws.
        ///   A faulting verb is reported to <paramref name="onFault" /> (so the host can trace a game/mod
        ///   API mismatch) and then skipped, never allowed to crash the conversation.
        /// </summary>
        private static string SafeTeachingFor(IVerb<TContext, TAction, TResult> verb, TContext ctx, Action<Exception> onFault)
        {
            if (verb == null) return null;

            try
            {
                return verb.IsEligible(ctx) ? verb.TeachingText(ctx) : null;
            }
            catch (Exception ex)
            {
                // A verb that faults (e.g. touches a game API absent on this build) is treated as simply
                // unavailable this turn, rather than crashing the whole conversation.
                onFault?.Invoke(ex);

                return null;
            }
        }

        /// <summary>Routes to the verb whose Name matches (ordinal, case-insensitive), or null when none is registered under that name.</summary>
        public TResult Execute(string name, TAction action, TContext ctx)
        {
            IVerb<TContext, TAction, TResult> verb = FindByName(name);

            return verb?.Execute(action, ctx);
        }

        private IVerb<TContext, TAction, TResult> FindByName(string name)
            => string.IsNullOrEmpty(name)
                ? null
                : _verbs.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
