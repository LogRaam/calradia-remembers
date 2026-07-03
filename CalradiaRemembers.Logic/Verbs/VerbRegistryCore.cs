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
        public string ComposeTeachings(TContext ctx) => ComposeTeachings(ctx, _verbs.Select(v => v.Name));

        /// <summary>
        ///   Composes the teaching text of the named verbs, in the given order, skipping any name that is
        ///   not registered and any verb that is not currently eligible. Null when nothing was composed.
        /// </summary>
        public string ComposeTeachings(TContext ctx, IEnumerable<string> order)
        {
            if (order == null) return null;

            var sb = new StringBuilder();
            foreach (string name in order)
            {
                IVerb<TContext, TAction, TResult> verb = FindByName(name);
                if (verb == null || !verb.IsEligible(ctx)) continue;

                string text = verb.TeachingText(ctx);
                if (!string.IsNullOrEmpty(text)) sb.Append(text);
            }

            return sb.Length == 0 ? null : sb.ToString();
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
