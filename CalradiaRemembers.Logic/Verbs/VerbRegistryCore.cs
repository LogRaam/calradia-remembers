// Code written by Gabriel Mailhot, 04/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic.Verbs
{
   /// <summary>
   ///   Holds a set of <see cref="IVerb{TContext,TAction,TResult}" /> and composes/dispatches over them.
   ///   TResult is expected to be a reference type: a lookup miss or an ineligible verb yields null.
   /// </summary>
   public sealed class VerbRegistryCore<TContext, TAction, TResult>
      where TResult : class
   {
      private readonly List<IVerb<TContext, TAction, TResult>> _verbs = new();

      /// <summary>The names of the registered verbs that <paramref name="include" /> keeps, in registration order.</summary>
      public List<string> NamesWhere(Func<IVerb<TContext, TAction, TResult>, bool> include)
         => _verbs.Where(v => include == null || include(v)).Select(v => v.Name).ToList();

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

         return sb.Length == 0
            ? null
            : sb.ToString();
      }

      /// <summary>
      ///   Composes the teaching text of the eligible verbs <paramref name="include" /> selects, in registration order.
      ///   For a kind of verb no fixed name list can know in advance: another mod's actions (tashmetu, 05/10/2026).
      /// </summary>
      public string ComposeTeachingsWhere(TContext ctx, Func<IVerb<TContext, TAction, TResult>, bool> include, Action<Exception> onFault = null)
         => include == null ? null : ComposeTeachings(ctx, _verbs.Where(include).Select(v => v.Name).ToList(), onFault);

      /// <summary>
      ///   Composes the guidance text of EVERY registered verb that implements
      ///   <see cref="IProvidesGuidance{TContext}" />, in registration order, regardless of eligibility: a
      ///   verb whose eligibility currently fails still gets a chance to tell the LLM to decline in-character
      ///   this turn (Extension Surface actions guidance, 30/08/2026), the opposite gate from
      ///   <see cref="ComposeTeachings(TContext, System.Action{System.Exception})" />, which hides an
      ///   ineligible verb's teaching entirely. A verb not implementing the interface contributes nothing.
      ///   Fault-safe, mirroring <see cref="SafeTeachingFor" />: a throwing Guidance callback is skipped,
      ///   never allowed to escape (a third-party mod's callback is arbitrary code CR does not control).
      ///   Null when nothing was composed.
      /// </summary>
      public string ComposeGuidance(TContext ctx, Action<Exception> onFault = null)
      {
         var sb = new StringBuilder();

         foreach (IVerb<TContext, TAction, TResult> verb in _verbs)
         {
            if (!(verb is IProvidesGuidance<TContext> provider)) continue;

            string text = SafeGuidanceFor(provider, ctx, onFault);
            if (!string.IsNullOrEmpty(text)) sb.Append(text);
         }

         return sb.Length == 0
            ? null
            : sb.ToString();
      }

      /// <summary>
      ///   The closed door named by EVERY registered verb that implements
      ///   <see cref="IStatesUnavailability{TContext}" />, in registration order, regardless of eligibility. The
      ///   inward twin of <see cref="ComposeGuidance" />: same gate (asked always), same fault safety, but a
      ///   STRUCTURED answer, so <see cref="DeedUnavailabilityPolicy" /> can render every verb's refusal in one
      ///   voice and inside one budget rather than fourteen verbs each inventing a paragraph.
      ///   <para>
      ///     Returns the refusals a verb chose to state, NOT every verb that happens to be ineligible: staying
      ///     silent is the default, and each verb decides when its deed is plausibly in play (see the interface).
      ///   </para>
      ///   Never null; empty when nothing is shut, or nothing worth saying.
      /// </summary>
      public List<DeedRefusal> ComposeUnavailability(TContext ctx, Action<Exception> onFault = null)
      {
         var refusals = new List<DeedRefusal>();

         foreach (IVerb<TContext, TAction, TResult> verb in _verbs)
         {
            if (!(verb is IStatesUnavailability<TContext> stater)) continue;

            DeedRefusal refusal = SafeUnavailabilityFor(stater, ctx, onFault);
            if (refusal != null) refusals.Add(refusal);
         }

         return refusals;
      }

      /// <summary>
      ///   Routes to the verb whose Name matches (ordinal, case-insensitive), or null when none is registered under that
      ///   name.
      /// </summary>
      public TResult Execute(string name, TAction action, TContext ctx)
      {
         IVerb<TContext, TAction, TResult> verb = FindByName(name);

         return verb?.Execute(action, ctx);
      }

      /// <summary>Adds a verb. Order matters for the no-explicit-order overload of <see cref="ComposeTeachings(TContext, System.Action{System.Exception})" />.</summary>
      public void Register(IVerb<TContext, TAction, TResult> verb)
      {
         if (verb != null) _verbs.Add(verb);
      }

      /// <summary>
      ///   Removes the verb whose Name matches (ordinal, case-insensitive, the same match FindByName/Execute use),
      ///   so a registration can be cleanly undone. Returns false, as a no-op, when <paramref name="name" /> is
      ///   null or empty, or when nothing is registered under that name. When, somehow, more than one verb shares
      ///   the name, every matching verb is removed.
      /// </summary>
      public bool Unregister(string name)
      {
         if (string.IsNullOrEmpty(name)) return false;

         int removed = _verbs.RemoveAll(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));

         return removed > 0;
      }

      #region private

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
            return verb.IsEligible(ctx)
               ? verb.TeachingText(ctx)
               : null;
         }
         catch (Exception ex)
         {
            // A verb that faults (e.g. touches a game API absent on this build) is treated as simply
            // unavailable this turn, rather than crashing the whole conversation.
            onFault?.Invoke(ex);

            return null;
         }
      }

      /// <summary>
      ///   A verb's guidance text, or null when it throws. Reported to <paramref name="onFault" /> the same
      ///   way a faulting eligibility/teaching check is (see <see cref="SafeTeachingFor" />).
      /// </summary>
      private static string SafeGuidanceFor(IProvidesGuidance<TContext> provider, TContext ctx, Action<Exception> onFault)
      {
         try
         {
            return provider.Guidance(ctx);
         }
         catch (Exception ex)
         {
            onFault?.Invoke(ex);

            return null;
         }
      }

      /// <summary>
      ///   A verb's stated closed door, or null when it throws. Reported to <paramref name="onFault" /> the same
      ///   way a faulting eligibility/teaching/guidance check is: this runs on every conversation open, and one
      ///   verb misreading the campaign must never cost the player the conversation.
      /// </summary>
      private static DeedRefusal SafeUnavailabilityFor(IStatesUnavailability<TContext> stater, TContext ctx, Action<Exception> onFault)
      {
         try
         {
            return stater.Unavailable(ctx);
         }
         catch (Exception ex)
         {
            onFault?.Invoke(ex);

            return null;
         }
      }

      private IVerb<TContext, TAction, TResult> FindByName(string name)
         => string.IsNullOrEmpty(name)
            ? null
            : _verbs.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));

      #endregion
   }
}