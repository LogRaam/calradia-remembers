// Code written by Gabriel Mailhot, 04/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
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
   ///   verbs, their 04/07/2026 reciprocals <see cref="GiveTroopsVerb" />/<see cref="SpendInfluenceVerb" />
   ///   (the player reinforces a needy NPC instead of the NPC favouring the player), plus
   ///   <see cref="EndMarriageVerb" /> and <see cref="MakeAmendsVerb" /> (both DESIGNED for this contract,
   ///   not merely migrated onto it). A future verb, internal or one injected by another mod through
   ///   <see cref="ExternalVerbAdapter" />, registers here without touching either call site.
   /// </summary>
   public sealed class VerbRegistry
   {
      // Routes a verb that faults while being composed (e.g. one touching a game API this build lacks) to
      // the always-on compat trace, so a missing-API failure on the conversation path is never silent.
      // Names the option and where it threw (superdeeplearningaibot, Discord 10/11/2026, 2.8.7: "a conversation option
      // failed: NullReferenceException" every turn with one notable, and nothing said which option or where).
      private static readonly Action<Exception> FaultSink = ex => ModLog.Fault(FaultLabel(ex), ex);

      private static string FaultLabel(Exception ex)
      {
         string name = VerbRegistryCore<VerbContext, GameAction, ActionOutcome>.VerbNameOf(ex);
         string where = ex?.StackTrace?.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);

         return $"the conversation option '{name ?? "?"}'" + (string.IsNullOrEmpty(where) ? "" : $" ({where})");
      }
      private readonly VerbRegistryCore<VerbContext, GameAction, ActionOutcome> _core = new();

      /// <summary>The one instance shared by the whole mod. Not a CampaignBehaviorBase: nothing here is persisted or per-save.</summary>
      public static VerbRegistry Instance { get; } = BuildDefault();

      /// <summary>Composes the teaching text of every currently-eligible registered verb, in registration order.</summary>
      public string ComposeTeachings(VerbContext ctx) => _core.ComposeTeachings(ctx, FaultSink);

      /// <summary>
      ///   Composes the teaching text of exactly the named verbs, in the given order (used where their text must
      ///   interleave with other, unmigrated teaching).
      /// </summary>
      public string ComposeTeachings(VerbContext ctx, IEnumerable<string> order) => _core.ComposeTeachings(ctx, order, FaultSink);

      /// <summary>
      ///   Composes the teaching text of every eligible verb another mod registered through CrActions (an
      ///   <see cref="ExternalVerbAdapter" />), in registration order. CR's own verbs are composed by name lists where
      ///   their text must interleave; an external verb has no place in those lists, so until 05/10/2026 its
      ///   TeachingText reached no prompt at all (tashmetu, Bridges to Remember).
      /// </summary>
      public string ComposeExternalTeachings(VerbContext ctx) => _core.ComposeTeachingsWhere(ctx, v => v is ExternalVerbAdapter, FaultSink);

      /// <summary>
      ///   Composes the guidance text of every registered verb that implements <see cref="IProvidesGuidance{TContext}" />
      ///   (in practice, an <see cref="ExternalVerbAdapter" /> whose contract supplied a Guidance callback),
      ///   run UNCONDITIONALLY regardless of eligibility (Extension Surface actions guidance, 30/08/2026).
      /// </summary>
      public string ComposeGuidance(VerbContext ctx) => _core.ComposeGuidance(ctx, FaultSink);

      /// <summary>
      ///   The closed doors CR's OWN verbs choose to name this turn (<see cref="IStatesUnavailability{TContext}" />),
      ///   asked regardless of eligibility, so the model knows the gating BEFORE it declares anything rather than
      ///   being refused after (Gabriel's ruling, 09/09/2026). Rendered by
      ///   <see cref="DeedUnavailabilityPolicy.Compose" />; never null.
      /// </summary>
      public List<DeedRefusal> ComposeUnavailability(VerbContext ctx) => _core.ComposeUnavailability(ctx, FaultSink);

      /// <summary>
      ///   Routes the action to the verb whose Name matches <see cref="GameAction.Type" />, or null when none is
      ///   registered under that name.
      /// </summary>
      public ActionOutcome Execute(GameAction action, VerbContext ctx) => action == null
         ? null
         : _core.Execute(action.Type, action, ctx);

      // TODO Extension Surface actions volet: public Register(...) with Feed.Record-style validation
      // (source id, quota, name collision) for a verb registered at runtime by another mod.

      /// <summary>The names of CR's own verbs (not another mod's), in registration order.</summary>
      public List<string> OwnNames => _core.NamesWhere(v => !(v is ExternalVerbAdapter));

      /// <summary>Adds a verb, in the order its teaching should appear when composed without an explicit order.</summary>
      public void Register(IConversationVerb verb) => _core.Register(verb);

      /// <summary>Removes the verb registered under this name, so a registration can be cleanly undone.</summary>
      public bool Unregister(string name) => _core.Unregister(name);

      #region private

      private static VerbRegistry BuildDefault()
      {
         var registry = new VerbRegistry();
         registry.Register(new GiveInfluenceVerb());
         registry.Register(new LendTroopsVerb());
         registry.Register(new GiveTroopsVerb());
         registry.Register(new ReinforceGarrisonVerb());
         registry.Register(new ShareIntelVerb());
         registry.Register(new DeliverRecruitsVerb());
         registry.Register(new ArrangeMatchVerb());
         registry.Register(new SpendInfluenceVerb());
         registry.Register(new SwayOpinionVerb());
         registry.Register(new PressForWarVerb());
         registry.Register(new EnactEdictVerb());
         registry.Register(new ReleasePrisonerVerb());
         registry.Register(new BuyPrisonerVerb());
         registry.Register(new SellPrisonerVerb());
         registry.Register(new ExchangePrisonersVerb());
         registry.Register(new EndMarriageVerb());
         registry.Register(new LeaveHouseToWedVerb());
         registry.Register(new MakeAmendsVerb());
         registry.Register(new HearAccountVerb());
         registry.Register(new GrudgeTermsVerb());
         registry.Register(new AdmitSecretLoverVerb());
         registry.Register(new PayBountyVerb());
         registry.Register(new CalradiaRemembers.Violence.ExtortVerb());
         registry.Register(new CalradiaRemembers.Violence.ExposeSecretVerb());
         registry.Register(new ReconcileWithVerb());
         registry.Register(new PayForSilenceVerb());
         registry.Register(new CalradiaRemembers.Violence.PlayerViolenceVerb(CalradiaRemembers.Logic.Violence.ViolentAct.Rob));
         registry.Register(new CalradiaRemembers.Violence.PlayerViolenceVerb(CalradiaRemembers.Logic.Violence.ViolentAct.Assault));
         registry.Register(new CalradiaRemembers.Violence.PlayerViolenceVerb(CalradiaRemembers.Logic.Violence.ViolentAct.Kill));

         return registry;
      }

      #endregion
   }
}