// Code written by Gabriel Mailhot, 28/08/2026.
// Extension Surface: CrActions.Register is the PUBLIC, ONE-CALL door for a third-party mod to give the LLM
// a real, executable deed of its own, replacing the fragile pattern of casting GameActionCatalog.All to a
// List<GameActionSpec> and mutating it by reflection (which a bridge mod resorted to, since no supported
// door existed). One call wires the action into every place CR itself teaches and dispatches an action:
// the interpreter catalog (GameActionCatalog), the bridge's own verb registry (VerbRegistry, via an
// ExternalVerbAdapter), and the bridge's dispatch table (BannerlordGameStateBridge.HandledActionTypes, kept
// in lockstep so the action_catalog_parity self-test stays green). Nothing here executes the mod's own
// engine work: that lives entirely inside the mod's own ExternalActionSpec.Execute callback.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Verbs;
using NpcMemoryService.Core.Actions;
using NpcMemoryService.Core.Extension;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers
{
   /// <summary>The public facade a third-party mod calls to register (or unregister) one executable conversation action.</summary>
   public static class CrActions
   {
      // Chat-flow controls ChatViewModel itself handles, deliberately absent from GameActionCatalog (see its
      // own header): a third-party Name colliding with one of these would shadow a control CR's own chat
      // window depends on, even though GameActionCatalog.Register would never itself catch the collision
      // (it only guards against a real catalog entry, and these are not catalog entries).
      private static readonly string[] ReservedNames = {
         "end_conversation", "witness_leaves", "request_privacy", "retire", "challenge_duel",
         "release_player", "deliver_prisoner"
      };

      /// <summary>
      ///   Registers one externally-defined action so the LLM is taught it (interpreter catalog + 1:1 prose
      ///   vocabulary) and CR dispatches it (bridge + VerbRegistry) exactly like a built-in verb. A single
      ///   call replaces every seam a third-party mod previously had to patch or reach into by reflection.
      ///   Rejects, without throwing, a null <paramref name="spec" />, a blank
      ///   <see cref="ExternalActionSpec.Name" />, a null <see cref="ExternalActionSpec.Execute" /> (all
      ///   <see cref="ActionRegistrationResult.RejectedInvalid" />), a Name reserved for a CR chat-flow
      ///   control, or a Name that collides with an already-registered action, built-in or external (both
      ///   <see cref="ActionRegistrationResult.RejectedDuplicate" />). Any unexpected exception mid-registration
      ///   is logged and rolled back so a half-registration never lingers.
      /// </summary>
      public static ActionRegistrationResult Register(ExternalActionSpec spec)
      {
         string trimmed = null;

         try
         {
            if (spec == null || string.IsNullOrWhiteSpace(spec.Name) || spec.Execute == null)
               return ActionRegistrationResult.RejectedInvalid;

            trimmed = spec.Name.Trim();

            foreach (string reserved in ReservedNames)
               if (string.Equals(reserved, trimmed, StringComparison.OrdinalIgnoreCase))
                  return ActionRegistrationResult.RejectedDuplicate;

            var catalogSpec = new GameActionSpec(trimmed, spec.Description ?? "",
               spec.WhenToEmit, spec.WhenNotToEmit, ToGameActionParams(spec.Parameters));

            if (!GameActionCatalog.Register(catalogSpec))
               return ActionRegistrationResult.RejectedDuplicate;

            var contract = new ExternalVerbContract {
               Name = trimmed,
               IsEligible = spec.IsEligible,
               TeachingText = spec.TeachingText,
               Execute = spec.Execute,
               Guidance = spec.Guidance
            };

            VerbRegistry.Instance.Register(new ExternalVerbAdapter(contract, ProjectFacts));
            BannerlordGameStateBridge.HandledActionTypes.Add(trimmed);

            ModLog.Info($"[EXT ACTION] registered '{trimmed}' from {spec.SourceMod ?? "(unknown mod)"}");

            return ActionRegistrationResult.Registered;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[EXT ACTION] registration of '{spec?.Name}' threw and was rolled back: {ex.GetType().Name}: {ex.Message}");

            // Best-effort: undo whichever of the three registries the failed attempt already reached, so a
            // half-registered action can never linger and silently drift the catalog/bridge/registry apart.
            if (trimmed != null) Unregister(trimmed);

            return ActionRegistrationResult.RejectedInvalid;
         }
      }

      /// <summary>
      ///   Unregisters a previously-registered action by its <see cref="ExternalActionSpec.Name" />, undoing
      ///   every part of <see cref="Register" /> (catalog entry, verb registration, dispatch membership).
      ///   Returns true when ANY of the three actually held it; false, without throwing, for a null/blank
      ///   name or a name nothing was registered under.
      /// </summary>
      public static bool Unregister(string name)
      {
         if (string.IsNullOrWhiteSpace(name)) return false;

         string trimmed = name.Trim();

         bool removedFromCatalog = GameActionCatalog.Unregister(trimmed);
         bool removedFromVerbs = VerbRegistry.Instance.Unregister(trimmed);
         bool removedFromHandled = BannerlordGameStateBridge.HandledActionTypes.Remove(trimmed);

         return removedFromCatalog || removedFromVerbs || removedFromHandled;
      }

      /// <summary>
      ///   Projects the live <see cref="VerbContext" /> down to the engine-agnostic <see cref="VerbFacts" /> an
      ///   external mod's callbacks reason over. <see cref="NpcProfile.ReputationWithPlayer" /> is CR's OWN
      ///   personal-regard ledger (never synced from vanilla clan relation), the same value every internal
      ///   verb reads for "how this NPC feels about the player".
      /// </summary>
      internal static VerbFacts ProjectFacts(VerbContext ctx)
         => new VerbFacts {
            NpcId = ctx?.Npc?.StringId ?? "",
            RelationToPlayer = ctx?.Profile?.ReputationWithPlayer ?? 0,
            FactionId = ctx?.Npc?.MapFaction?.StringId,
            IsLord = ctx?.Npc?.IsLord ?? false,
            IsPrisoner = ctx?.Npc?.IsPrisoner ?? false,
            IsFemale = ctx?.Npc?.IsFemale ?? false
         };

      #region private

      private static IReadOnlyList<GameActionParam> ToGameActionParams(IReadOnlyList<ExternalActionParam> source)
      {
         var list = new List<GameActionParam>();

         if (source == null) return list;

         foreach (ExternalActionParam p in source)
            if (p != null) list.Add(new GameActionParam(p.Name, p.Meaning));

         return list;
      }

      #endregion
   }
}
