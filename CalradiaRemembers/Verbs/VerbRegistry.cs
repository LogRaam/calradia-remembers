// Code written by Gabriel Mailhot, 03/07/2026.

#region

using System.Collections.Generic;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic.Verbs;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>
    ///   The one shared registry of every <see cref="IConversationVerb" />: composes eligible verbs'
    ///   teaching text (consumed by EncounterContextBuilder), and routes an incoming [ACTION] to its verb
    ///   (consumed by BannerlordGameStateBridge.ExecuteAction). Seeded with the four "RP with teeth" court
    ///   verbs plus <see cref="EndMarriageVerb" /> (the first verb DESIGNED for this contract, not merely
    ///   migrated onto it). A future verb, internal or one injected by another mod through
    ///   <see cref="ExternalVerbAdapter" />, registers here without touching either call site.
    /// </summary>
    public sealed class VerbRegistry
    {
        private readonly VerbRegistryCore<VerbContext, GameAction, ActionOutcome> _core =
            new VerbRegistryCore<VerbContext, GameAction, ActionOutcome>();

        /// <summary>The one instance shared by the whole mod. Not a CampaignBehaviorBase: nothing here is persisted or per-save.</summary>
        public static VerbRegistry Instance { get; } = BuildDefault();

        // TODO Extension Surface actions volet: public Register(...) with Feed.Record-style validation
        // (source id, quota, name collision) for a verb registered at runtime by another mod.

        /// <summary>Adds a verb, in the order its teaching should appear when composed without an explicit order.</summary>
        public void Register(IConversationVerb verb) => _core.Register(verb);

        /// <summary>Composes the teaching text of every currently-eligible registered verb, in registration order.</summary>
        public string ComposeTeachings(VerbContext ctx) => _core.ComposeTeachings(ctx);

        /// <summary>Composes the teaching text of exactly the named verbs, in the given order (used where their text must interleave with other, unmigrated teaching).</summary>
        public string ComposeTeachings(VerbContext ctx, IEnumerable<string> order) => _core.ComposeTeachings(ctx, order);

        /// <summary>Routes the action to the verb whose Name matches <see cref="GameAction.Type" />, or null when none is registered under that name.</summary>
        public ActionOutcome Execute(GameAction action, VerbContext ctx) => action == null ? null : _core.Execute(action.Type, action, ctx);

        private static VerbRegistry BuildDefault()
        {
            var registry = new VerbRegistry();
            registry.Register(new GiveInfluenceVerb());
            registry.Register(new LendTroopsVerb());
            registry.Register(new SwayOpinionVerb());
            registry.Register(new ReleasePrisonerVerb());
            registry.Register(new EndMarriageVerb());

            return registry;
        }
    }
}
