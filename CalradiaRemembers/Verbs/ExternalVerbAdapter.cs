// Code written by Gabriel Mailhot, 03/07/2026.
// Extension Surface, Actions volet (spike): proves an externally-defined verb can sit in the SAME
// VerbRegistry as CR's own court verbs, taught and routed identically. NOT wired to a public entry point
// yet: see the TODO on VerbRegistry for the follow-up Actions.Register(...) validation pipeline.

#region

using System;
using CalradiaRemembers.GameState;
using NpcMemoryService.Core.Extension;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>
    ///   Wraps an externally-supplied <see cref="ExternalVerbContract" /> (an SDK-shaped, engine-agnostic
    ///   verb defined by ANOTHER mod) as an <see cref="IConversationVerb" />. CR's half of the work is
    ///   projecting the live <see cref="VerbContext" /> down to the engine-agnostic <see cref="VerbFacts" />
    ///   the contract's callbacks expect (a Hero and a profile become plain ids and flags, via the injected
    ///   <see cref="_factsProjector" />); the external mod's own callbacks do whatever engine work THEY need
    ///   from those ids, entirely on their own side.
    /// </summary>
    public sealed class ExternalVerbAdapter : IConversationVerb
    {
        private readonly ExternalVerbContract _contract;
        private readonly Func<VerbContext, VerbFacts> _factsProjector;

        public ExternalVerbAdapter(ExternalVerbContract contract, Func<VerbContext, VerbFacts> factsProjector)
        {
            _contract = contract;
            _factsProjector = factsProjector;
        }

        public string Name => _contract?.Name;

        public bool IsEligible(VerbContext ctx)
            => _contract?.IsEligible != null && _factsProjector != null && _contract.IsEligible(_factsProjector(ctx));

        public string TeachingText(VerbContext ctx)
            => _contract?.TeachingText != null && _factsProjector != null
                ? _contract.TeachingText(_factsProjector(ctx))
                : null;

        public ActionOutcome Execute(GameAction action, VerbContext ctx)
        {
            if (_contract?.Execute == null || _factsProjector == null) return null;

            var request = new ActionRequest {
                VerbName = _contract.Name,
                Parameters = action?.Parameters,
                Facts = _factsProjector(ctx)
            };

            ActionResult result = _contract.Execute(request);

            return result == null ? null : new ActionOutcome(result.Message, result.Success);
        }
    }
}
