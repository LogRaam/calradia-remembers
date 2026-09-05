// Code written by Gabriel Mailhot, 03/07/2026.

// yet: see the TODO on VerbRegistry for the follow-up Actions.Register(...) validation pipeline.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Chat;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic.Verbs;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Extension;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Verbs
{
   /// <summary>
   ///   Wraps an externally-supplied <see cref="ExternalVerbContract" /> (an SDK-shaped, engine-agnostic
   ///   verb defined by ANOTHER mod) as an <see cref="IConversationVerb" />. CR's half of the work is
   ///   projecting the live <see cref="VerbContext" /> down to the engine-agnostic <see cref="VerbFacts" />
   ///   the contract's callbacks expect (a Hero and a profile become plain ids and flags, via the injected
   ///   <see cref="_factsProjector" />); the external mod's own callbacks do whatever engine work THEY need
   ///   from those ids, entirely on their own side. Also implements <see cref="IProvidesGuidance{TContext}" />
   ///   (Extension Surface actions guidance, 30/08/2026) so a registered action can tell the LLM, PRE-response
   ///   and regardless of eligibility, to decline or accept in-character.
   /// </summary>
   public sealed class ExternalVerbAdapter : IConversationVerb, IProvidesGuidance<VerbContext>
   {
      private readonly ExternalVerbContract _contract;
      private readonly Func<VerbContext, VerbFacts> _factsProjector;

      public ExternalVerbAdapter(ExternalVerbContract contract, Func<VerbContext, VerbFacts> factsProjector)
      {
         _contract = contract;
         _factsProjector = factsProjector;
      }

      public string Name => _contract?.Name;

      public ActionOutcome Execute(GameAction action, VerbContext ctx)
      {
         if (_contract?.Execute == null || _factsProjector == null) return null;

         var request = new ActionRequest {
            VerbName = _contract.Name,
            Parameters = action?.Parameters,
            Facts = _factsProjector(ctx)
         };

         ActionResult result = _contract.Execute(request);

         // A third-party verb reporting failure did NOT do the thing, so it is a refusal, not merely bad
         // news: the chat owes the model an out-of-character note saying so, or it will build the next
         // turns on a deed the host mod declined to perform.
         // Carry the optional Recap through on BOTH paths: a REFUSED deed is exactly a case the NPC should voice
         // ("I will not rule on your claim..."), not only a successful one, so the follow-up fires either way.
         return result == null
            ? null
            : result.Success
               ? new ActionOutcome(result.Message, true, recap: result.Recap)
               : ActionOutcome.Refused(result.Message, result.Recap);
      }

      public bool IsEligible(VerbContext ctx)
         => _contract?.IsEligible != null && _factsProjector != null && _contract.IsEligible(_factsProjector(ctx));

      public string TeachingText(VerbContext ctx)
         => _contract?.TeachingText != null && _factsProjector != null
            ? _contract.TeachingText(_factsProjector(ctx))
            : null;

      /// <summary>
      ///   Runs the contract's <see cref="ExternalVerbContract.Guidance" /> callback, if any, EVERY turn
      ///   regardless of <see cref="IsEligible" />. Entity resolution (heroes/factions/settlements the
      ///   player named) is skipped entirely when the contract has no Guidance callback: no point paying for
      ///   matching a modder never asked for.
      /// </summary>
      public string Guidance(VerbContext ctx)
      {
         if (_contract?.Guidance == null || _factsProjector == null) return null;

         var context = new ActionGuidanceContext(
            _factsProjector(ctx),
            ctx?.PlayerMessage,
            IsEligible(ctx),
            ResolveMentionedHeroes(ctx),
            ResolveMentionedFactions(ctx),
            ResolveMentionedSettlements(ctx));

         return _contract.Guidance(context);
      }

      #region private

      /// <summary>Heroes the player named, via <see cref="ContextualNameResolver" />'s own matching. Never throws.</summary>
      private static IReadOnlyList<ResolvedEntity> ResolveMentionedHeroes(VerbContext ctx)
      {
         try
         {
            var result = new List<ResolvedEntity>();

            foreach (var (hero, fullName, relationship) in ContextualNameResolver.ResolveMentionedHeroes(ctx?.Npc, ctx?.PlayerMessage))
               result.Add(new ResolvedEntity(hero.StringId, fullName, relationship));

            return result;
         }
         catch
         {
            return Array.Empty<ResolvedEntity>();
         }
      }

      /// <summary>Factions the player named, via <see cref="CouncilLift" />'s own faction pool/matching. Never throws.</summary>
      private static IReadOnlyList<ResolvedEntity> ResolveMentionedFactions(VerbContext ctx)
      {
         try
         {
            var result = new List<ResolvedEntity>();

            foreach (IFaction faction in CouncilLift.ResolveFactionsMentioned(ctx?.PlayerMessage))
               result.Add(new ResolvedEntity(faction.StringId, faction.Name?.ToString(), faction is Kingdom ? "kingdom" : "clan"));

            return result;
         }
         catch
         {
            return Array.Empty<ResolvedEntity>();
         }
      }

      /// <summary>Settlements the player named, via <see cref="SettlementContextResolver" />'s own matching. Never throws.</summary>
      private static IReadOnlyList<ResolvedEntity> ResolveMentionedSettlements(VerbContext ctx)
      {
         try
         {
            var result = new List<ResolvedEntity>();

            foreach (Settlement settlement in SettlementContextResolver.MatchMentioned(ctx?.PlayerMessage, ctx?.Npc?.CurrentSettlement))
               result.Add(new ResolvedEntity(settlement.StringId, settlement.Name?.ToString(), SettlementKind(settlement)));

            return result;
         }
         catch
         {
            return Array.Empty<ResolvedEntity>();
         }
      }

      private static string SettlementKind(Settlement s)
         => s.IsTown ? "town" : s.IsCastle ? "castle" : s.IsVillage ? "village" : "holding";

      #endregion
   }
}