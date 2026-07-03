// Code written by Gabriel Mailhot, 03/07/2026.

#region

using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic.Verbs;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>
    ///   One self-contained conversation verb (e.g. give_influence): its eligibility, the prompt text it
    ///   teaches the LLM, and its execution when the LLM emits the matching [ACTION]. Closes
    ///   <see cref="IVerb{TContext,TAction,TResult}" /> over the mod's concrete types, so adding a verb is
    ///   "implement this interface, register it in VerbRegistry" (no switch statement to extend, no
    ///   prompt-builder method to touch).
    /// </summary>
    public interface IConversationVerb : IVerb<VerbContext, GameAction, ActionOutcome>
    {
    }
}
