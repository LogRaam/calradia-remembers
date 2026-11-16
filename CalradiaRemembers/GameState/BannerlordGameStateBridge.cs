// Code written by Gabriel Mailhot, 10/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Bastards;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Commitments;
using CalradiaRemembers.Escort;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Items;
using CalradiaRemembers.Jealousy;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Companions;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Logic.Assemblies;
using CalradiaRemembers.Logic.AuthoredQuests;
using CalradiaRemembers.Logic.Captivity;
using CalradiaRemembers.Logic.Character;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Logic.Items;
using CalradiaRemembers.Logic.Marriage;
using CalradiaRemembers.Logic.Quests;
using CalradiaRemembers.Logic.Romance;
using CalradiaRemembers.Logic.WarDuty;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Marriage;
using CalradiaRemembers.Missions;
using CalradiaRemembers.Nemesis;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Retainer;
using CalradiaRemembers.Stance;
using CalradiaRemembers.Storage;
using CalradiaRemembers.Verbs;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Calendar;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.GameState
{
   /// <summary>
   ///   Concrete implementation of <see cref="IGameStateBridge" /> using the
   ///   TaleWorlds campaign API. This is the ONLY class in the mod that should
   ///   reach directly into <c>Hero.MainHero</c>, <c>Campaign.Current</c>,
   ///   <c>FactionManager</c>, etc. Everything else uses the abstraction.
   /// </summary>
   public sealed class BannerlordGameStateBridge : IGameStateBridge
   {
      // ── Captive escape ─────────────────────────────────────────────────────

      private const float EscapeChance = 0.10f; // a slim, deterministic chance — fate, not the LLM
      private const int InfluenceLossPerWitness = 3;
      private const int MaxHarmsPerScene = 5; // wounds beyond this are narrated but cost no HP
      private const int MaxInfluenceLoss = 18;
      private const int MaxRenownLoss = 12;

      /// <summary>
      ///   harm_prisoner: a captor inflicts real physical harm on the player during a captive
      ///   scene — a beating, a cut, a maiming, or the bruising toll of being taken by force.
      ///   Translates the narrated violence into actual lost health so captivity carries a body
      ///   cost. Only ever taught in the captive prompt rules (menace and spoil scenes), so a
      ///   normal NPC never emits it. Clamped to leave the player at 1 HP — a scene wounds, it
      ///   never kills (death from a scripted scene would be jarring and end the campaign).
      /// </summary>
      // A captive scene wounds but must never kill, nor whittle the player down to nothing when a
      // weak model emits harm_prisoner every single turn. Two guards: a per-scene cap on the number
      // of wounds that bite, and a HP floor well above death. Both reset at each scene's start.
      private const int MinSceneHitPoints = 15; // a scene never drives the player below this

      /// <summary>
      ///   Appreciations pillar: the fixed formation magnitude for a RansomedOrFreed plant (free_prisoner /
      ///   release_prisoner) — comparable to Tier2GrudgeRules.DeliveredPrisonerBaseMagnitude, being freed weighing roughly as
      ///   much as delivering a prisoner does on the other side of the ledger.
      /// </summary>
      private const int RansomedOrFreedMagnitude = 35;

      // ── Sprint 17: public humiliation standing penalty ────────────────────
      private const int RenownLossPerWitness = 2;

      // Set when a harm_prisoner action wounds the player during the current captive scene, so
      // ApplyForcedSceneToll knows not to double up. Reset at each scene's start.
      private static bool _captiveHarmApplied;

      // True only WHILE a captive scene is running (set at BeginCaptiveSceneTracking, cleared at scene close).
      // harm_prisoner deducts REAL health, so it re-validates against this so a stray emission outside a live
      // scene can never wound the player during an ordinary conversation.
      private static bool _captiveSceneActive;
      private static bool _forceNextEscape; // console test hook (cr.escape_force)
      private static bool _pendingEscape;
      private static bool _pendingRelease; // captor chose to free the player; applied at scene close
      private static Hero _pendingReleaseCaptor;
      private static int _sceneHarmCount;

      /// <inheritdoc />
      public int ApplyForcedSceneToll()
      {
         var player = Hero.MainHero;

         if (player == null || _captiveHarmApplied) return 0; // the captor's violence already wounded them

         const int forcedSceneDamage = 12; // the bruising cost of being taken by force
         int before = player.HitPoints;
         player.HitPoints = Math.Max(MinSceneHitPoints, before - forcedSceneDamage);
         int lost = before - player.HitPoints;

         if (lost <= 0) return 0;

         _captiveHarmApplied = true;
         ModLog.Info($"[CAPTIVE] forced-scene toll: player loses {lost} HP (now {player.HitPoints}).");

         return lost;
      }

      public (int renownLoss, int influenceLoss) ApplyPlayerPublicHumiliation(int witnessCount)
      {
         if (witnessCount <= 0 || Hero.MainHero?.Clan == null) return (0, 0);

         int renownLoss = ClampInt(witnessCount * RenownLossPerWitness, 0, MaxRenownLoss);
         int influenceLoss = ClampInt(witnessCount * InfluenceLossPerWitness, 0, MaxInfluenceLoss);

         // Only mutate the real campaign when the player is genuinely a prisoner.
         // Console test scenes (player not imprisoned) report the figures without
         // touching the save — so the mechanic can be verified safely.
         bool real = Hero.MainHero.IsPrisoner;
         if (real)
         {
            RenownPenalty.Apply(Hero.MainHero, renownLoss); // a negative GainRenownAction is a no-op
            ChangeClanInfluenceAction.Apply(Hero.MainHero.Clan, -influenceLoss);
         }

         ModLog.Info($"[HUMILIATION] witnesses={witnessCount} renown-{renownLoss} influence-{influenceLoss}" +
                     (real
                        ? ""
                        : " (test mode — not applied)"));

         return (renownLoss, influenceLoss);
      }

      /// <inheritdoc />
      public void BeginCaptiveSceneTracking()
      {
         _captiveHarmApplied = false;
         _captiveSceneActive = true;
         _sceneHarmCount = 0;
         _pendingEscape = false;
         _pendingRelease = false;
         _pendingReleaseCaptor = null;
      }

      /// <inheritdoc />
      public void EndCaptiveSceneTracking() => _captiveSceneActive = false;

      /// <inheritdoc />
      public bool CommitCaptorRelease(Hero captor)
      {
         try
         {
            if (captor == null) return false;

            // The prompt only PROPOSES; the bridge is the law. Re-validate the same gate the teaching used.
            // BOTH taught release paths are honored symmetrically: a captor talked round (AllowsPersuasion)
            // OR bought off with gold (AllowsRansom) frees the player. Only a vendetta captor, open to
            // neither, refuses. Honoring persuasion alone left a ransom-only captor (cruel, moderate grudge)
            // pocketing an agreed ransom yet keeping the player caged, a silent no-op.
            CaptorReleaseVerdict verdict = ResolveCaptorReleaseVerdict(captor);

            if (verdict.RefusesAll)
            {
               ModLog.Info($"[RELEASE] {captor.Name} refuses every release (vendetta): player stays captive, nothing granted.");

               return false;
            }

            _pendingRelease = true;
            _pendingReleaseCaptor = captor;
            ModLog.Info($"[RELEASE] {captor.Name} agreed to free the player (applied at scene close).");

            return true;
         }
         catch (Exception ex)
         {
            ModLog.Error($"CommitCaptorRelease failed: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }
      // ── Sprint 10: action execution ───────────────────────────────────────

      /// <summary>
      ///   Unified Action Catalog, Stage 1 (14/08/2026): the AUTHORITATIVE set of every action <c>type</c> this
      ///   switch below actually dispatches (the 60 cases, counting the shared VerbRegistry cases individually),
      ///   plus the two ChatViewModel chat-flow controls that are real LLM-emittable actions but never reach this
      ///   switch: <c>witness_leaves</c> (handled by <c>ChatViewModel.HandleWitnessLeaves</c> in
      ///   <c>ChatViewModel.Helpers.cs</c>) and <c>request_privacy</c> (resolved in
      ///   <c>ChatViewModel.Exchanges.cs</c>). Deliberately EXCLUDES <c>end_conversation</c>: it is a third
      ///   ChatViewModel chat-flow control, but it is already one of the Action Interpreter's five hand-tuned core
      ///   reactive signals (see <c>NpcMemoryService.Core.Prompts.ActionInterpreterPromptBuilder</c>), so it never
      ///   needed a catalog entry of its own.
      ///   <para>
      ///     This set is engine-bound (references live nowhere the pure SDK could reach), so it cannot be compared
      ///     against <c>NpcMemoryService.Core.Actions.GameActionCatalog.Types</c> (the SDK's own single source of
      ///     truth for the SAME verbs) by a pure unit test. The comparison instead runs as the in-game self-test
      ///     <c>action_catalog_parity</c> (<see cref="Diagnostics.CrSelfTest" />), part of <c>cr.test all</c>: any
      ///     verb added to this switch (or to the two chat-flow controls) without a matching catalog entry, or vice
      ///     versa, is reported by name so the drift this file header exists to prevent is caught immediately
      ///     rather than silently teaching a stale or incomplete vocabulary to the LLM.
      ///   </para>
      /// </summary>
      public static readonly HashSet<string> HandledActionTypes = new HashSet<string> {
         "change_relation", "give_gold", "take_gold", "pay_blackmail", "join_party", "join_as_mercenary",
         "end_mercenary", "join_as_vassal", "mediate_peace", "join_clan", "scheme_assist", "scheme_heed",
         "marry", "take_as_consort", "take_as_secret_lover", "open_relationship", "close_relationship",
         "accept_other_partners",
         "end_affair", "give_item", "sell_item", "give_prisoner", "free_prisoner", "counter_offer", "execute_prisoner", "execute_player",
         "turn_nemesis", "recruit_prisoner", "recruit_notable",
         "take_into_service", "grant_blessing", "arrange_marriage",
         "appoint_governor", "assign_party_role", "rejoin_party", "dispatch_mission", "grant_fief",
         "revoke_fief", "expel_from_clan", "grant_stipend", "swear_oath", "harm_prisoner", "impregnation_risk",
         "gather_news", "reassure_companion", "recall_companion", "follow_me", "dismiss_escort", "ride_with_me",
         "part_ways", "give_influence", "lend_troops", "give_troops", "spend_influence", "sway_opinion",
         "release_prisoner", "buy_prisoner", "sell_prisoner", "exchange_prisoners", "end_marriage", "leave_house_to_wed", "make_amends", "grudge_terms", "hear_account", "pledge_against", "press_for_war",
         "accept_divorce", "decline_divorce", "end_own_marriage", "teach_skill", "admit_secret_lover", "pay_for_silence",
         "rob_npc", "assault_npc", "kill_npc", "extort", "expose_secret", "reconcile_with", "reinforce_garrison", "share_intel", "deliver_recruits", "arrange_match", "enact_edict", "pay_bounty",
         // ChatViewModel chat-flow controls: real LLM-emittable actions, never dispatched by the switch below.
         "witness_leaves", "request_privacy", "retire"
      };

      public ActionOutcome ExecuteAction(GameAction action, Hero npcHero, ActionContext context)
      {
         if (action == null) return null;

         switch (action.Type.ToLowerInvariant())
         {
            case "change_relation":
               return ExecuteChangeRelation(action, npcHero, context);
            case "give_gold":
               return ExecuteGiveGold(action, npcHero, context);
            case "take_gold":
               return ExecuteTakeGold(action, npcHero);
            case "pay_blackmail":
               return ExecutePayBlackmail(npcHero);
            case "join_party":
               return ExecuteJoinParty(action, npcHero);
            case "join_as_mercenary":
               return ExecuteJoinAsMercenary(npcHero);
            case "end_mercenary":
               return ExecuteEndMercenary(npcHero);
            case "join_as_vassal":
               return ExecuteJoinAsVassal(npcHero);
            case "mediate_peace":
               return ExecuteMediatePeace(action, npcHero, context);
            case "join_clan":
               return ExecuteJoinClan(npcHero, context?.Profile);
            case "scheme_assist":
               return ExecuteSchemeAssist(npcHero);
            case "scheme_heed":
               return ExecuteSchemeHeed(npcHero);
            case "marry":
               return ExecuteMarry(npcHero, context);
            case "take_as_consort":
               return ExecuteTakeAsConsort(npcHero, context);
            case "take_as_secret_lover":
               return ExecuteTakeAsSecretLover(npcHero, context);
            case "open_relationship":
               return ExecuteOpenRelationship(npcHero, context);
            case "accept_other_partners":
               return ExecuteAcceptOtherPartners(npcHero, context);
            case "close_relationship":
               return ExecuteCloseRelationship(npcHero, context);
            case "end_affair":
               return ExecuteEndAffair(npcHero, context);
            case "give_item":
               return ExecuteGiveItem(action, npcHero, context);
            case "sell_item":
               return ExecuteSellItem(action, npcHero, context);
            case "give_prisoner":
               return ExecuteGivePrisoner(action, npcHero, context);
            case "free_prisoner":
               return ExecuteFreePrisoner(npcHero, context);
            case "counter_offer":
               return ExecuteCounterOffer(action, npcHero, context);
            case "execute_prisoner":
               return ExecuteExecutePrisoner(npcHero);
            case "execute_player":
               return ExecuteExecutePlayer(npcHero);
            case "turn_nemesis":
               return ExecuteTurnNemesis(npcHero, context?.Profile);
            case "recruit_prisoner":
               return ExecuteRecruitPrisoner(npcHero, context?.Profile);
            case "recruit_notable":
               return ExecuteRecruitNotable(npcHero, context?.Profile);
            case "take_into_service":
               return ExecuteTakeIntoService(action, npcHero);
            case "grant_blessing":
               return ExecuteGrantBlessing(action, npcHero);

            case "arrange_marriage":
               return ExecuteArrangeMarriage(action, npcHero);
            case "appoint_governor":
               return ExecuteAppointGovernor(action, npcHero, context);
            case "assign_party_role":
               return ExecuteAssignPartyRole(action, npcHero);
            case "rejoin_party":
               return ExecuteRejoinParty(npcHero);
            case "dispatch_mission":
               return ExecuteDispatchMission(action, npcHero);
            case "grant_fief":
               return ExecuteGrantFief(action, npcHero, context);
            case "revoke_fief":
               return ExecuteRevokeFief(action, npcHero);
            case "expel_from_clan":
               return ExecuteExpelFromClan(npcHero);
            case "grant_stipend":
               return ExecuteGrantStipend(action, npcHero);
            case "swear_oath":
               return ExecuteSwearOath(action, npcHero);
            case "harm_prisoner":
               return ExecuteHarmPrisoner(action, npcHero);
            case "impregnation_risk":
               return ExecuteImpregnationRisk(npcHero);
            case "gather_news":
               return ExecuteGatherNews(action, npcHero);
            case "reassure_companion":
               return ExecuteReassureCompanion(npcHero);
            case "recall_companion":
               return ExecuteRecallCompanion(npcHero);
            case "follow_me":
               return ExecuteFollowMe(npcHero);
            case "dismiss_escort":
               return ExecuteDismissEscort(npcHero);
            case "ride_with_me":
               return ExecuteRideWithMe(npcHero, context?.Profile);
            case "part_ways":
               return ExecutePartWays(npcHero);
            // give_influence, lend_troops, sway_opinion, release_prisoner: migrated to the
            // IConversationVerb contract (CalradiaRemembers.Verbs): VerbRegistry routes by Name,
            // reusing the same CourtActionResolver/CourtActionPolicy eligibility and math as before.
            // give_troops, spend_influence (04/07/2026): the RECIPROCAL direction — the player reinforces
            // a needy NPC instead of the NPC favouring the player — designed for the same contract.
            case "give_influence":
            case "lend_troops":
            case "give_troops":
            case "spend_influence":
            // reinforce_garrison (fkasad, 01/10/2026): the men a governor asked for, onto his walls.
            case "reinforce_garrison":
            // share_intel (fkasad, 01/10/2026): a gang leader telling a scout what he knows of his town.
            case "share_intel":
            // deliver_recruits (Lordfadooboo, 01/10/2026): the fresh recruits a lord asked for, into his party.
            case "deliver_recruits":
            // arrange_match (fkasad, 01/10/2026): a family giving a lady's hand to the suitor the player speaks for.
            case "arrange_match":
            case "release_prisoner":
            // buy_prisoner / sell_prisoner (prisoner trade, 08/07/2026): the NPC buys a captive the player
            // holds, or sells one his clan holds, for gold clamped around the game's ransom valuation.
            case "buy_prisoner":
            case "sell_prisoner":
            // exchange_prisoners (fkasad, 25/09/2026): one hero captive for another, gold evening an uneven swap.
            case "exchange_prisoners":
            // end_marriage (DIVORCE, Phase 1) and make_amends (Grudges pillar, resolution half): both
            // DESIGNED for the IConversationVerb contract rather than migrated onto it — routed
            // identically to the four above.
            case "end_marriage":
            case "leave_house_to_wed":
            case "make_amends":
            case "grudge_terms":
            case "hear_account":
            // admit_secret_lover (mystery-lover pillar phase 3b): the player confesses a hidden bond; the verb
            // records that this NPC now privately knows the identity. pay_for_silence (phase 3b-3): the player pays
            // a lord who holds that secret for his silence. Routed identically to the group above.
            case "admit_secret_lover":
            case "pay_for_silence":
            // Player violence (player request 2026-09-06): the PLAYER robbing, beating, or killing this NPC. The
            // verb re-reads the lethal MCM toggle and rolls the odds itself, so a narrated murder in a campaign
            // with killing switched off changes nothing.
            case "rob_npc":
            case "assault_npc":
            case "kill_npc":
            // Extortion (Gabriel, 23/10/2026): the verb reads the hold and the day's answer itself.
            case "extort":
            case "expose_secret":
            // reconcile_with (kurushimi00, 06/11/2026): the verb finds the one it makes peace with among those present.
            case "reconcile_with":
               return VerbRegistry.Instance.Execute(action, new VerbContext(npcHero, context?.Profile, this, null, context));
            case "sway_opinion":
               return ExecuteSwayOpinion(action, npcHero, context);
            case "pledge_against":
               return ExecutePledgeAgainst(action, npcHero, context);
            case "press_for_war":
            case "enact_edict":
            case "pay_bounty":
               return VerbRegistry.Instance.Execute(action, new VerbContext(npcHero, context?.Profile, this, null, context));
            case "accept_divorce":
               return ExecuteAcceptDivorce(npcHero);
            case "decline_divorce":
               return ExecuteDeclineDivorce(npcHero);
            case "end_own_marriage":
               return ExecuteEndOwnMarriage(npcHero);
            case "teach_skill":
               return ExecuteTeachSkill(action, npcHero, context);
            case Logic.AuthoredQuests.AuthoredQuestPrompt.StoryChoiceVerb:
               return ExecuteStoryChoice(action, npcHero);
            case Logic.AuthoredQuests.TaleArc.SignalVerb:
               return ExecuteTaleSignal(action, npcHero);
            // A gesture of lovemaking the player did or asked for, read against the character's tastes (Gabriel,
            // 26/10/2026: "un geste impose que le NPC n'aime pas devrait reduire le personal regard").
            case Logic.Romance.IntimateActReading.Verb:
               return Mapping.IntimateActApplier.Apply(action, npcHero, context?.Profile, this);
            default:
               // Extension Surface (CrActions.Register): a type that matches none of the cases above may
               // still be a THIRD-PARTY action a mod registered at runtime, not a hallucination. Try the
               // shared VerbRegistry first; only a genuinely unknown/invented type falls through to the
               // refusal below.
               ActionOutcome external = VerbRegistry.Instance.Execute(action, new VerbContext(npcHero, context?.Profile, this, null, context));
               if (external != null) return external;

               // Safety net: an unknown / hallucinated verb must SURFACE a refusal (player line + transcript
               // OOC note), never vanish as a silent null, or the model's invented deed goes uncorrected.
               ModLog.Info($"[ACTION] Unknown type '{action.Type}' refused (no executor).");

               return ActionOutcome.RefusedUnknownVerb(UnknownActionNotice.BaseMessage);
         }
      }

      /// <inheritdoc />
      public void FinalizePendingEscape()
      {
         if (!_pendingEscape) return;
         _pendingEscape = false;

         var player = Hero.MainHero;

         if (player is not {IsPrisoner: true}) return;

         try
         {
            EndCaptivityAction.ApplyByEscape(player);
            ModLog.Info("[ESCAPE] EndCaptivityAction.ApplyByEscape applied — player is free.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ESCAPE] freeing failed: {ex.Message}");
         }
      }

      /// <inheritdoc />
      public void FinalizePendingRelease()
      {
         if (!_pendingRelease) return;
         _pendingRelease = false;

         Hero captor = _pendingReleaseCaptor;
         _pendingReleaseCaptor = null;

         try
         {
            EndCaptivityAction.ApplyByReleasedByChoice(Hero.MainHero, captor);
            ModLog.Info("[RELEASE] EndCaptivityAction.ApplyByReleasedByChoice applied — the captor freed the player.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"FinalizePendingRelease failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <inheritdoc />
      public void ForceNextEscapeSuccess() => _forceNextEscape = true;

      public string GetActiveConflicts()
      {
         if (Kingdom.All == null) return "";

         var wars = new List<(string Side, string Enemy)>();
         List<Kingdom> kingdoms = RetrieveAllKingdoms();

         // Emitted side-by-side on purpose: every war of kingdoms[i] comes out together, which is what lets
         // ActiveConflictsFormatter write that kingdom's name once instead of once per enemy.
         for (var i = 0; i < kingdoms.Count; i++)
            for (int j = i + 1; j < kingdoms.Count; j++)
            {
               Kingdom a = kingdoms[i];
               Kingdom b = kingdoms[j];
               if (FactionManager.IsAtWarAgainstFaction(a, b))
                  wars.Add((a.Name?.ToString(), b.Name?.ToString()));
            }

         // The player's own realm's wars first, from its side (fkasad, 05/10/2026).
         return ActiveConflictsFormatter.Describe(wars, (Hero.MainHero?.MapFaction as Kingdom)?.Name?.ToString());
      }

      public string GetRealmStanding()
      {
         if (Kingdom.All == null) return "";

         List<Settlement> fiefs = Settlement.All?.Where(s => s != null && (s.IsTown || s.IsCastle)).ToList() ?? new List<Settlement>();
         var realms = new List<RealmStanding>();
         foreach (Kingdom k in Kingdom.All)
         {
            if (k == null) continue;
            realms.Add(new RealmStanding {
               Name = k.Name?.ToString(),
               Towns = k.IsEliminated ? 0 : fiefs.Count(s => s.IsTown && s.MapFaction == k),
               Castles = k.IsEliminated ? 0 : fiefs.Count(s => s.IsCastle && s.MapFaction == k),
               HasFallen = k.IsEliminated
            });
         }

         return RealmStandingFormatter.Describe(realms);
      }

      public string GetCurrentActivity(Hero npc)
      {
         try
         {
            MobileParty party = npc?.PartyBelongedTo;

            if (party == null) return null; // in a settlement / no party of their own — nothing to assert

            // 1. Actively besieging — the most concrete, unambiguous signal.
            if (party.BesiegedSettlement != null)
               return $"laying siege to {party.BesiegedSettlement.Name}";

            // 2. In an army: prefer the ARMY's strategic objective over this party's own
            //    waypoint — a member party follows a gathering point, not the target, so
            //    reading its own destination would itself produce the wrong place.
            Army army = party.Army;
            if (army != null)
            {
               string leader = army.LeaderParty?.LeaderHero?.Name?.ToString() ?? army.ArmyOwner?.Name?.ToString();

               if (army.LeaderParty?.BesiegedSettlement != null)
                  return $"marching with {leader}'s army, laying siege to {army.LeaderParty.BesiegedSettlement.Name}";

               // AiBehaviorObject is an IMapPoint; only name it when it is a settlement we can
               // point to — chasing a roaming party yields no place, so stay vague there.

               if (army.AiBehaviorObject is Settlement objective)
                  return $"marching with {leader}'s army toward {objective.Name}";

               return leader != null
                  ? $"marching with {leader}'s army"
                  : null;
            }

            // 3. A lone party with a set destination.
            if (party.TargetSettlement != null)
               return $"traveling toward {party.TargetSettlement.Name}";

            return null;
         }
         catch (Exception ex)
         {
            ModLog.Error($"GetCurrentActivity failed for {npc?.Name}: {ex.Message}");

            return null;
         }
      }

      public int GetCurrentGameDay()
         => (int) CampaignTime.Now.ToDays;

      public SceneType GetCurrentScene()
      {
         Settlement settlement = Hero.MainHero?.CurrentSettlement ?? PlayerEncounter.LocationEncounter?.Settlement;

         if (settlement == null)
            return SceneType.Outdoor;

         // The ROOM the player actually stands in outranks the settlement's type: a tavern is a tavern
         // whether it sits in a town or a castle, and the narration opens on that room (Gabriel 2026-07-19:
         // talking to a wanderer in a tavern, the scene was set in a keep). Same LocationComplex read the
         // witness filter already relies on.
         if (CurrentLocationId() == "tavern") return SceneType.Tavern;

         // ORDER MATTERS, and getting it wrong is what shipped: in Bannerlord a TOWN is also a fortification,
         // so testing IsFortification first swallowed every town into Keep and left SceneType.Settlement
         // unreachable for them. Castle first, by its own flag, then the rest.
         if (settlement.IsCastle)
            return SceneType.Keep;

         if (settlement.IsTown || settlement.IsVillage)
            return SceneType.Settlement;

         if (settlement.IsFortification)
            return SceneType.Keep;

         return SceneType.Outdoor;
      }

      /// <summary>
      ///   The LocationComplex room the player is assigned to ("tavern", "lordshall", "center"), or null when
      ///   the complex is unavailable (on the map, mid-transition). Fail-soft: an unknown room simply falls
      ///   back to the settlement's own type.
      /// </summary>
      private static string CurrentLocationId()
      {
         try
         {
            LocationComplex complex = LocationComplex.Current;
            if (complex == null || Hero.MainHero == null) return null;

            return complex.GetLocationOfCharacter(Hero.MainHero)?.StringId;
         }
         catch { return null; }
      }

      /// <summary>
      ///   True when <paramref name="npc" />'s faction is on the opposite side of an active war from the
      ///   player's (romance audit M-B3/M-B5 guards). False when either side has no map faction (nothing to be
      ///   at war over). Note it is a MAP FACTION test, not a personal or clan one: a lord who adores the
      ///   player is still "at war" with them while their kingdoms are, which is exactly what vanilla's own
      ///   marriage screen enforces.
      ///   INTERNAL on purpose: <see cref="Mapping.EncounterContextBuilder" /> asks the same question when it
      ///   decides whether to TEACH the marry action, and this bridge asks it again before SEALING one. Those
      ///   two must never drift, the same reason
      ///   <see cref="CalradiaRemembers.Logic.Marriage.MarriageEligibility.MarriageEligibilityThreshold" /> is a shared constant rather
      ///   than a number written twice. It used to be written out three times (here, in the builder, and in
      ///   <see cref="GetDiplomaticStatus" />); identical in every copy, which is precisely how such a thing
      ///   survives until the day one copy changes.
      /// </summary>
      internal static bool IsAtWarWithPlayer(Hero npc)
         => npc?.MapFaction != null && Hero.MainHero?.MapFaction != null
            && FactionManager.IsAtWarAgainstFaction(npc.MapFaction, Hero.MainHero.MapFaction);

      public DiplomaticStatus GetDiplomaticStatus(Hero npc)
      {
         if (npc == null || npc.MapFaction == null || Hero.MainHero?.MapFaction == null)
            return DiplomaticStatus.Unknown;

         // Same faction (or same clan, both treated as "allied" here)
         if (npc.MapFaction == Hero.MainHero.MapFaction)
            return DiplomaticStatus.Allied;

         if (IsAtWarWithPlayer(npc))
            return DiplomaticStatus.AtWar;

         return DiplomaticStatus.AtPeace;
      }

      public int GetPlayerRelation(Hero npc)
      {
         if (npc == null) return 0;

         // GetRelationWithPlayer reads the same store that
         // ChangeRelationAction.ApplyPlayerRelation writes to.
         // CharacterRelationManager.GetHeroRelation is a separate
         // internal dict and does not reflect ApplyPlayerRelation changes.
         return (int) npc.GetRelationWithPlayer();
      }

      public PlayerStatusVsNpc GetPlayerStatus(Hero npc)
         => PlayerStandingPolicy.Resolve(BuildStandingFacts(npc));

      /// <summary>
      ///   The realm that binds the player and this NPC when one of them rules it, for
      ///   <see cref="EncounterContext.SharedRealmName" />. Null when neither rules the other's kingdom.
      /// </summary>
      public string GetSharedRealmName(Hero npc)
      {
         if (npc == null || Hero.MainHero == null) return null;

         // One rule serves every bond the status can report: whoever rules it, and whether the player is
         // sworn to it or merely hired by it, the realm that grounds the note is the one they BOTH belong to.
         Kingdom playerRealm = Hero.MainHero.Clan?.Kingdom;

         return playerRealm != null && npc.Clan?.Kingdom == playerRealm
            ? playerRealm.Name?.ToString()
            : null;
      }

      /// <summary>
      ///   Reads the engine for the facts that place the player beside this NPC. The VERDICT is
      ///   <see cref="PlayerStandingPolicy" />'s: this method must not decide anything, only report.
      /// </summary>
      private static PlayerStandingFacts BuildStandingFacts(Hero npc)
      {
         if (npc == null || Hero.MainHero == null) return null;

         Kingdom playerRealm = Clan.PlayerClan?.Kingdom;
         bool sharesRealm = playerRealm != null && npc.Clan?.Kingdom == playerRealm;

         return new PlayerStandingFacts {PlayerIsCaptiveOfNpc = Hero.MainHero.IsPrisoner && Hero.MainHero.PartyBelongedToAsPrisoner?.LeaderHero == npc, NpcIsCaptiveOfPlayer = IsHeldByPlayer(npc), SharesPlayerClan = npc.Clan != null && npc.Clan == Clan.PlayerClan, NpcRulesPlayerRealm = Hero.MainHero.Clan?.Kingdom?.Leader == npc && npc != Hero.MainHero, PlayerRulesNpcRealm = npc.Clan?.Kingdom?.Leader == Hero.MainHero && Hero.MainHero.Clan?.Kingdom != null, PlayerIsMercenaryOfNpcRealm = sharesRealm && (Clan.PlayerClan?.IsUnderMercenaryService ?? false), SharesRealm = sharesRealm, NpcIsMercenaryOfSharedRealm = sharesRealm && (npc.Clan?.IsUnderMercenaryService ?? false)};
      }

      /// <inheritdoc />
      /// <remarks>
      ///   Was the season alone. bdionysis80 (Nexus, 12/11/2026): with no year given, the model worked one out of the
      ///   absolute day count and wrote "summer 1085" on summer 9, 1084.
      /// </remarks>
      public string GetCalendarDate() => ChronicleCalendar.GameDateOf((int) CampaignTime.Now.ToDays);

      /// <inheritdoc />
      public string GetTimeOfDay()
      {
         // CurrentHourInDay is 0–24. Map to a part of day so the scene's light matches vanilla.
         float hour = CampaignTime.Now.CurrentHourInDay;

         if (hour < 5f) return "deep night";
         if (hour < 8f) return "early morning, around dawn";
         if (hour < 12f) return "morning";
         if (hour < 14f) return "midday";
         if (hour < 18f) return "afternoon";
         if (hour < 21f) return "evening, around dusk";

         return "night";
      }

      public ActionOutcome GrantConditionalReward(Hero npcHero, NpcProfile profile, InformalQuest quest)
      {
         if (quest == null || quest.Reward == RewardGrant.None || npcHero == null) return null;

         try
         {
            switch (quest.Reward)
            {
               case RewardGrant.JoinParty:
                  return DoJoin(npcHero, 0, "joined the player as the agreed reward (paid in deed)");

               case RewardGrant.MarriageConsent:
                  return GrantMarriageConsent(npcHero, quest);

               // COUNCIL_ACTIONS.md Partie 5 (the "Caladog" case): gated behind ModSettings.AllowFiefAndMarriageQuestRewards
               // (default off) at every re-check inside these two methods, not merely trusted from issuance.
               case RewardGrant.GrantFief:
                  return GrantFiefReward(npcHero, profile, quest);

               case RewardGrant.MarriageReward:
                  return GrantMarriageRewardDirect(npcHero, profile, quest);

               // Recognized but not yet executable — declined cleanly rather than faked.
               // The quest factory refuses to issue these, so this is a defensive backstop.
               case RewardGrant.GiveItem:
               case RewardGrant.GiveTroops:
               case RewardGrant.ReleasePrisoner:
                  ModLog.Info($"[GRANT] '{quest.Reward}' not yet supported — declined for {npcHero.Name}.");

                  return ActionOutcome.Refused($"({npcHero.Name} cannot honor that kind of bargain yet.)");

               default:
                  return null;
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[GRANT] {quest.Reward} failed for {npcHero.Name}: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      public ActionOutcome GrantQuestReward(Hero npcHero, NpcProfile profile, int gold, int rewardRelation)
      {
         if (npcHero == null || Hero.MainHero == null || profile == null) return null;

         var parts = new List<string>();

         if (gold > 0)
         {
            int amount = ClampInt(gold, 1, 100000);
            GiveGoldAction.ApplyBetweenCharacters(npcHero, Hero.MainHero, amount, true);
            parts.Add($"{amount} denars");
         }

         if (rewardRelation > 0)
         {
            // Verified reward: applied straight to the personal score, bypassing the RelationGate cooldown/cap
            // (the deed was confirmed by real game events). 5.1: through the single clamp path ApplyReputationDelta.
            // 5.8 / M-C3 (regard audit): it does NOT stamp LastRelationGainHour. That stamp is the CONVERSATIONAL
            // cooldown; a verified reward is a separate channel, and stamping it silently gated a warm chat's +1 to
            // 0 for 8h after a turn-in. Reward and conversation are independent, so the reward leaves the clock be.
            int before = profile.ReputationWithPlayer;
            ProfileMutator.ApplyReputationDelta(profile, rewardRelation, NpcMemoryService.Core.Standing.RegardCause.Quest);
            int after = profile.ReputationWithPlayer;
            if (after != before) parts.Add($"+{after - before} regard");
            ModLog.Info($"[QUEST] reward relation: {npcHero.Name} {before}→{after} (+{rewardRelation} promised)");
         }

         if (parts.Count == 0) return null;

         var message = $"{npcHero.Name} rewards you: {string.Join(", ", parts)}.";
         MBInformationManager.AddQuickInformation(new TextObject(message), 0, npcHero.CharacterObject);
         PlaySound(ModSettings.RelationIncreaseSound);
         ModLog.Info($"[QUEST] {message}");

         return new ActionOutcome(message, true);
      }

      /// <inheritdoc />
      public bool ResolvePlayerEscapeAttempt(bool force)
      {
         if (_forceNextEscape)
         {
            _forceNextEscape = false;
            force = true;
         }

         // Against a nemesis captor the chance DECAYS with each recapture (he grows more careful, never a
         // perfect cage); an ordinary captor keeps the flat chance.
         double chance = NemesisBehavior.Instance?.ActiveNemesisEscapeChance() ?? EscapeChance;

         if (!force && MBRandom.RandomFloat >= chance) return false;

         // Commit the outcome NOW (irreversible), but defer the actual campaign action to scene
         // close: freeing mid-chat could pop an encounter/menu and disrupt the open scene window.
         _pendingEscape = true;
         ModLog.Info("[ESCAPE] attempt SUCCEEDED — freeing committed (applied at scene close).");

         return true;
      }

      public ActionOutcome SettleItemBargain(NpcProfile giver,
                                             Hero giverHero,
                                             InformalQuest quest,
                                             IReadOnlyList<ItemOffer> offered)
      {
         try
         {
            if (quest is not {Type: QuestType.DeliverItems}) return null;
            if (Hero.MainHero == null || offered == null || offered.Count == 0) return null;

            ItemRoster roster = MobileParty.MainParty?.ItemRoster;

            if (roster == null) return null;

            // Tally only what the player genuinely still holds; the bridge trusts the roster,
            // not the caller's value claim.
            var toRemove = new List<(EquipmentElement item, int count)>();
            var total = 0;
            foreach (ItemOffer offer in offered)
            {
               if (string.IsNullOrEmpty(offer.ItemId) || offer.Count <= 0) continue;
               ItemRosterElement held = Items.RosterGoods.Find(roster, offer.ItemId);
               int available = held.Amount;

               if (held.EquipmentElement.Item == null || available <= 0) continue;
               int take = Math.Min(offer.Count, available);
               total += Items.RosterGoods.ValueOf(held.EquipmentElement) * take;
               toRemove.Add((held.EquipmentElement, take));
            }

            if (total < quest.RequiredItemValue)
               return ActionOutcome.Refused(
                  $"({giverHero?.Name}: these goods are worth about {total} denars — I asked for " + $"{quest.RequiredItemValue}. Bring more.)");

            // Value met: hand the goods over, then honor the favor.
            foreach ((EquipmentElement item, int count) in toRemove)
               roster.AddToCounts(item, -count);

            ModLog.Info($"[BARGAIN] {giverHero?.Name}: accepted goods worth {total} denars " + $"(required {quest.RequiredItemValue}) for {quest.Reward}.");

            ActionOutcome grant = GrantConditionalReward(giverHero, giver, quest);

            // A rewarded bargain (e.g. RewardGrant.JoinParty) can still be REFUSED at the grant seam
            // (not recruitable, clan full). The goods were already removed above, so a failed grant must
            // put back EXACTLY what was taken: a refused hire never destroys the player's goods, and the
            // quest stays open to try again. RewardGrant.None returns a null grant that is a legitimate
            // "goods simply accepted", so only a non-null-but-unsuccessful grant triggers the refund.
            bool grantFailed = quest.Reward != RewardGrant.None && (grant == null || !grant.IsPositive);
            if (grantFailed)
            {
               foreach ((EquipmentElement item, int count) in toRemove)
                  roster.AddToCounts(item, count);

               ModLog.Info($"[BARGAIN] {giverHero?.Name}: reward '{quest.Reward}' refused after tally; goods returned to the player.");

               return grant ?? ActionOutcome.Refused($"({giverHero?.Name} cannot honor that bargain; your goods are returned.)");
            }

            return grant ?? new ActionOutcome($"{giverHero?.Name} accepts your goods.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[BARGAIN] SettleItemBargain failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   A gift of goods an outstanding supply task of <paramref name="giver" /> asks for (<see cref="SupplyGiftPolicy" />):
      ///   settled through <see cref="SettleSupplyQuest" /> with every matching good the party carries, refused when short,
      ///   or null when no task asks for them (a plain gift).
      /// </summary>
      private ActionOutcome TrySettleSupplyByGift(ItemObject gift, Hero giverHero, NpcProfile giver)
      {
         InformalQuest supply = giver?.ActiveQuests?.FirstOrDefault(q =>
            (q.Type == QuestType.ProvideGoods || q.Type == QuestType.CraftWeapon) && q.IsOutstanding
            && SupplyItemMatcher.Matches(gift, q.RequiredItemCategory));
         ItemRoster roster = MobileParty.MainParty?.ItemRoster;
         if (supply == null || roster == null) return null;

         var offers = new List<ItemOffer>();
         for (var i = 0; i < roster.Count; i++)
         {
            ItemRosterElement e = roster.GetElementCopyAtIndex(i);
            ItemObject item = e.EquipmentElement.Item;
            if (item != null && e.Amount > 0 && SupplyItemMatcher.Matches(item, supply.RequiredItemCategory))
               offers.Add(new ItemOffer(item.StringId, e.Amount));
         }

         int held = offers.Sum(o => o.Count);
         switch (SupplyGiftPolicy.Read(new SupplyGiftFacts {TaskAsksTheseGoods = true, Held = held, Asked = supply.RequiredItemCount}))
         {
            case SupplyGiftReading.Settles:
               ModLog.Info($"[QUEST] a gift of {gift.Name} settles {giverHero?.Name}'s supply task ({held} carried, {supply.RequiredItemCount} asked).");

               return SettleSupplyQuest(giver, giverHero, supply, offers);
            case SupplyGiftReading.Short:
               return ActionOutcome.Refused($"({giverHero?.Name} asked for {supply.RequiredItemCount} {SupplyItemMatcher.Label(supply.RequiredItemCategory)}; "
                                            + $"you carry {held}, so you keep them until you have them all.)");
            default:
               return null;
         }
      }

      /// <summary>
      ///   Settles a <see cref="QuestType.ProvideGoods" /> supply task: tallies the COUNT of handed-over goods
      ///   that genuinely match the quest's category (only they count, whatever the picker gathered), and, when
      ///   the required count is met, hands over exactly that many, then COMPLETES the quest here (pays the
      ///   gold/regard reward, closes the journal) since the goods are delivered directly to the giver in
      ///   conversation, there is nothing to return and collect. A short delivery is rejected, never faked.
      /// </summary>
      public ActionOutcome SettleSupplyQuest(NpcProfile giver,
                                             Hero giverHero,
                                             InformalQuest quest,
                                             IReadOnlyList<ItemOffer> offered)
      {
         try
         {
            if (quest is not {Type: QuestType.ProvideGoods or QuestType.CraftWeapon}) return null;
            if (giver == null || Hero.MainHero == null || offered == null || offered.Count == 0) return null;

            ItemRoster roster = MobileParty.MainParty?.ItemRoster;

            if (roster == null) return null;

            string categoryKey = quest.RequiredItemCategory;
            string label = SupplyItemMatcher.Label(categoryKey);

            var toRemove = new List<(EquipmentElement item, int count)>();
            var count = 0;
            foreach (ItemOffer offer in offered)
            {
               if (string.IsNullOrEmpty(offer.ItemId) || offer.Count <= 0) continue;
               ItemRosterElement held = Items.RosterGoods.Find(roster, offer.ItemId);
               int available = held.Amount;

               if (held.EquipmentElement.Item == null || available <= 0 || !SupplyItemMatcher.Matches(held.EquipmentElement.Item, categoryKey)) continue;
               int take = Math.Min(offer.Count, available);
               count += take;
               toRemove.Add((held.EquipmentElement, take));
            }

            if (count < quest.RequiredItemCount)
               return ActionOutcome.Refused($"({giverHero?.Name}: that is only {count} of the {quest.RequiredItemCount} {label} I asked for. Bring more.)");

            // A good asked for a town that lacks it goes into that town's market, not the giver's cellar (fkasad,
            // 01/10/2026), and costs the town nothing: it bypasses the market's purchase.
            string marketId = ProvideGoodsPolicy.MarketCategoryId(categoryKey);
            Settlement market = marketId != null ? TownNeeds.MarketFor(giverHero, marketId) : null;

            // Count met: hand over exactly the required number (never take more than asked).
            int remaining = quest.RequiredItemCount;
            foreach ((EquipmentElement item, int c) in toRemove)
            {
               if (remaining <= 0) break;
               int take = Math.Min(c, remaining);
               roster.AddToCounts(item, -take);
               market?.ItemRoster?.AddToCounts(item, take);
               // The weapon he asked to be forged is his to wield (Lordfadooboo, 01/10/2026): equipped as a gift is.
               if (quest.Type == QuestType.CraftWeapon) TryEquipGift(giverHero, item);
               remaining -= take;
            }

            var day = (int) CampaignTime.Now.ToDays;

            // Grain asked for while a place he answers for goes hungry feeds that place, not his cellar (fkasad,
            // 01/10/2026), and the place remembers who brought it.
            // Relief raises the town's food stocks, not a grain count, so any food feeds it.
            Settlement fed = categoryKey is ProvideGoodsPolicy.Grain or ProvideGoodsPolicy.Food ? WorldEvents.FoodTradeMemoryBehavior.DeliverRelief(giverHero, quest.RequiredItemCount, quest.TargetSettlement) : null;
            string forWhom = fed != null ? $" for the hungry of {fed.Name}" : market != null ? $" for the market of {market.Name}" : "";

            quest.SatisfiedOnDay = day;
            quest.Evidence = $"Delivered {quest.RequiredItemCount} {label}{forWhom} on day {day}.";
            quest.Status = QuestStatus.Completed;

            ActionOutcome reward = GrantQuestReward(giverHero, giver, quest.RewardGold, quest.RewardRelation);
            string brought = quest.RequiredItemCount == 1 ? $"the {label}" : $"the {quest.RequiredItemCount} {label}";
            giver.Events.Add(new NotableEvent(day, NotableEventType.Collaboration, $"{Hero.MainHero.Name} brought {brought} I needed{forWhom}."));
            QuestJournalBridge.CloseAsCompleted(quest, giverHero);
            StanceService.OnPromiseKept(giver);
            NpcMemoryCampaignBehavior.Instance?.Store?.Set(giver);

            ModLog.Info($"[QUEST] ProvideGoods completed: {giverHero?.Name} received {quest.RequiredItemCount} {label}.");

            return reward ?? new ActionOutcome($"{giverHero?.Name} accepts your {label} with thanks.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[QUEST] SettleSupplyQuest failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>The match some lord has sent the player to broker, when this character has the say over the lady (fkasad, 01/10/2026).</summary>
      public PendingMatch MatchToArrange(Hero authorizer)
      {
         LetterErrand errand = QuestService.FindOutstandingMatchFor(authorizer, NpcMemoryCampaignBehavior.Instance?.Store);
         if (errand != null) return new PendingMatch {BrideName = errand.Quest.TargetHeroName, SuitorName = errand.Giver.Name};

         // A match another mod registered (tashmetu, 16/10/2026: CrMatches).
         PendingMatchEntry entry = CrMatches.ForDecider(authorizer);
         if (entry == null) return null;
         Hero suitor = Hero.FindFirst(h => h.StringId == entry.SuitorId);
         Hero bride = Hero.FindFirst(h => h.StringId == entry.BrideId);

         return suitor == null || bride == null ? null : new PendingMatch {
            BrideName = bride.Name?.ToString(), SuitorName = suitor.Name?.ToString(), AsksThePlayer = entry.AsksThePlayer,
            AskerIsTheSuitor = entry.AsksThePlayer && authorizer == suitor
         };
      }

      /// <summary>
      ///   The match this character would ask the PLAYER to give a hand to (tashmetu, 20/10/2026), for the chat's [Y/n]
      ///   before arrange_match runs; null when the match he holds is not one that asks the player.
      /// </summary>
      public static PendingMatch HandAskedOfThePlayer(Hero asker)
      {
         PendingMatchEntry entry = asker == null ? null : CrMatches.ForDecider(asker);
         if (entry is not {AsksThePlayer: true}) return null;
         Hero suitor = Hero.FindFirst(h => h.StringId == entry.SuitorId), bride = Hero.FindFirst(h => h.StringId == entry.BrideId);

         return suitor == null || bride == null ? null : new PendingMatch {
            BrideName = bride.Name?.ToString(), SuitorName = suitor.Name?.ToString(), AsksThePlayer = true, AskerIsTheSuitor = asker == suitor
         };
      }

      /// <summary>
      ///   arrange_match landed (fkasad's matchmaker, Gabriel's real marriage, 01/10/2026): the law, her heart, her
      ///   family's consent, then the game weds them; the task itself is stamped by the marriage event.
      /// </summary>
      public ActionOutcome SettleMatch(Hero authorizer)
      {
         try
         {
            BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
            LetterErrand errand = QuestService.FindOutstandingMatchFor(authorizer, store);
            // A match another mod registered (CrMatches) when no lord sent the player by letter.
            PendingMatchEntry registered = errand == null ? CrMatches.ForDecider(authorizer) : null;
            if (errand == null && registered == null) return ActionOutcome.Refused("(No match has been asked of you; nothing is promised.)");

            string suitorId = errand?.Giver.Id ?? registered.SuitorId;
            string brideId = errand?.Quest.TargetHeroId ?? registered.BrideId;
            Hero suitor = Hero.FindFirst(h => h.StringId == suitorId);
            Hero bride = Hero.FindFirst(h => h.StringId == brideId);
            bool playerGives = registered?.AsksThePlayer == true;
            ArrangeMarriageVerdict v = playerGives
               ? CourtActionResolver.TryGiveHandOfPlayersHouse(suitor, bride, authorizer, apply: true)
               : CourtActionResolver.TryArrangeThirdPartyMatch(suitor, bride, authorizer, apply: true);

            string refusal = v.Result switch
            {
               ArrangeMarriageResult.NoAuthority => playerGives
                  ? $"({bride?.Name}'s hand is no longer yours to give, or {authorizer.Name} no longer speaks for {suitor?.Name}.)"
                  : $"({authorizer.Name} has no say over {bride?.Name}'s hand.)",
               ArrangeMarriageResult.NotSuitable => $"({suitor?.Name} and {bride?.Name} cannot lawfully wed.)",
               ArrangeMarriageResult.BlockedByGuard => $"({suitor?.Name} and {bride?.Name} cannot be wed now: not across a war, nor from captivity.)",
               ArrangeMarriageResult.NotWonOver => $"({bride?.Name} does not yet hold {suitor?.Name} dear enough; she must be won over first.)",
               ArrangeMarriageResult.NotCrossClan => $"({suitor?.Name} and {bride?.Name} are of one house.)",
               ArrangeMarriageResult.RefusedStanding => $"({authorizer.Name} will not give {bride?.Name} to {suitor?.Name}'s house on your word as it stands.)",
               ArrangeMarriageResult.NoTargetKin => "(The match can no longer be found.)",
               _ => null
            };

            if (registered != null)
            {
               if (refusal == null) CrMatches.Withdraw(registered.SuitorId, registered.BrideId);
               CrMatches.RaiseSettled(new CrMatchOutcome(registered, refusal == null, refusal));
            }

            if (refusal != null) return ActionOutcome.Refused(refusal);

            var day = (int) CampaignTime.Now.ToDays;
            string player = Hero.MainHero?.Name?.ToString() ?? "the player";
            foreach (Hero who in new[] {bride, authorizer}.Distinct())
            {
               NpcProfile p = store?.Get(who.StringId);
               if (p == null) continue;
               p.Events.Add(new NotableEvent(day, NotableEventType.Collaboration, playerGives
                  ? who == bride ? $"{player} gave my hand to {suitor.Name}."
                  : who == suitor ? $"I asked {player} for {bride.Name}'s hand, and it was given."
                  : $"I asked {player} for {bride.Name}'s hand for {suitor.Name}, and it was given."
                  : who == bride ? $"{player} brokered my marriage to {suitor.Name}." : $"At {player}'s asking, I gave {bride.Name} in marriage to {suitor.Name}."));
               store.Set(p);
            }

            ModLog.Info($"[QUEST] arrange_match: {suitor.Name} wed {bride.Name}, consent by {(playerGives ? "the player, asked by " : "")}{authorizer.Name}{(playerGives ? "" : ", brokered by the player")}.");

            return new ActionOutcome(playerGives
               ? $"You give {bride.Name}'s hand: {suitor.Name} and {bride.Name} are wed."
               : $"{authorizer.Name} consents: {suitor.Name} and {bride.Name} are wed.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[QUEST] SettleMatch failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>Whether some lord has sent the player to scout this town (fkasad, 01/10/2026).</summary>
      public bool HasScoutTask(Settlement town)
         => QuestService.FindOutstandingScoutFor(town, NpcMemoryCampaignBehavior.Instance?.Store) != null;

      /// <summary>
      ///   share_intel landed (fkasad, 01/10/2026): the town's true state, in the bands its gang leader was given, becomes
      ///   the scout_town task's evidence; the gang leader remembers telling, and the player reads what he heard.
      /// </summary>
      public ActionOutcome SettleScoutTask(Hero informant, Settlement town)
      {
         try
         {
            BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
            LetterErrand errand = QuestService.FindOutstandingScoutFor(town, store);
            if (errand == null || informant == null) return ActionOutcome.Refused("(Nobody has sent you here for what this one knows; nothing is recorded.)");

            var day = (int) CampaignTime.Now.ToDays;
            string report = ScoutTownPolicy.Report(EncounterContextBuilder.ReadTownAsItsNotablesKnowIt(town), informant.Name?.ToString(), day);
            QuestDeedTracker.StampDeed(errand.Giver, errand.Quest, report, day);

            NpcProfile told = store.Get(informant.StringId);
            if (told != null)
            {
               told.Events.Add(new NotableEvent(day, NotableEventType.Other,
                  $"Told {Hero.MainHero?.Name?.ToString() ?? "the player"} what I know of {town.Name}'s walls and stores; someone sent them for it."));
               store.Set(told);
            }

            ModLog.Info($"[QUEST] scout_town: {informant.Name} told the player about {town.Name} for {errand.Giver.Name}.");

            return new ActionOutcome($"What {informant.Name} told you of {town.Name}: {report.Substring(report.IndexOf(':') + 1).Trim()}.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[QUEST] SettleScoutTask failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   Completes a troop hand-over task once the verb has moved the men (a garrison reinforced, fkasad; recruits
      ///   delivered, Lordfadooboo; 01/10/2026): the same close as a supply hand-over (evidence, reward, journal,
      ///   memory, a promise kept), in the words each task gives.
      /// </summary>
      public ActionOutcome SettleTroopHandOver(NpcProfile giver, Hero giverHero, InformalQuest quest, TroopHandOver handOver)
      {
         try
         {
            if (giver == null || quest == null || handOver == null || handOver.Moved <= 0) return null;

            var day = (int) CampaignTime.Now.ToDays;
            quest.SatisfiedOnDay = day;
            quest.Evidence = $"{handOver.Evidence} on day {day}.";
            quest.Status = QuestStatus.Completed;

            ActionOutcome reward = GrantQuestReward(giverHero, giver, quest.RewardGold, quest.RewardRelation);
            giver.Events.Add(new NotableEvent(day, NotableEventType.Collaboration, handOver.Memory));
            QuestJournalBridge.CloseAsCompleted(quest, giverHero);
            StanceService.OnPromiseKept(giver);
            NpcMemoryCampaignBehavior.Instance?.Store?.Set(giver);

            ModLog.Info($"[QUEST] {quest.Type} completed for {giverHero?.Name}: {handOver.Evidence}.");

            return reward ?? new ActionOutcome(handOver.PlayerLine, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[QUEST] SettleTroopHandOver failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      #region private

      private static int ClampInt(int value, int min, int max)
         => value < min ? min : value > max ? max : value;

      /// <summary>
      ///   Satisfies AND COMPLETES an outstanding <see cref="QuestType.ProvideGold" /> quest (a support demand,
      ///   e.g. child support) the moment the player gives its giver any gold. The payment is made DIRECTLY to
      ///   the very person who asked, so there is nothing to "return and collect": completing it here, rather
      ///   than only stamping the deed done and waiting for the giver to emit [QUEST_COMPLETE] (which the model
      ///   rarely did), fixes a player report of the quest staying stuck "done, return for your reward" forever.
      ///   The goodwill reward is paid here too, straight to the personal score (verified deed bypasses the gate).
      /// </summary>
      private static void CompleteProvideGoldOnPayment(Hero npcHero, int amountGiven)
      {
         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;

         if (store == null || npcHero == null || Hero.MainHero == null) return;

         NpcProfile profile = store.Get(npcHero.StringId);

         InformalQuest quest = profile?.ActiveQuests.FirstOrDefault(q =>
            q.Type == QuestType.ProvideGold && q.IsOutstanding);

         if (quest == null) return;

         var day = (int) CampaignTime.Now.ToDays;

         // M14: a demand with a set total (child support) must actually be MET, not closed by a single denar.
         // Partial payments accumulate toward RequiredGold; the quest stays open, and the giver acknowledges the
         // instalment, until the total is reached. RequiredGold == 0 (old saves, or an open-ended ProvideGold)
         // keeps the legacy "any payment closes it" behaviour.
         if (quest.RequiredGold > 0)
         {
            quest.PaidGold += amountGiven;
            if (quest.PaidGold < quest.RequiredGold)
            {
               profile.Events.Add(new NotableEvent(day, NotableEventType.Collaboration,
                  $"The player paid {amountGiven} denars toward the {quest.RequiredGold} I asked ({quest.PaidGold} so far)."));
               store.Set(profile);
               MBInformationManager.AddQuickInformation(
                  new TextObject($"{npcHero.Name} accepts {amountGiven} denars ({quest.PaidGold}/{quest.RequiredGold})."),
                  0, npcHero.CharacterObject);
               ModLog.Info($"[QUEST] ProvideGold partial: {npcHero.Name} received {amountGiven} ({quest.PaidGold}/{quest.RequiredGold}); quest stays open.");

               return;
            }
         }

         quest.SatisfiedOnDay = day;
         quest.Evidence = $"Player provided {amountGiven} denars on day {day}.";

         if (quest.RewardRelation > 0)
         {
            ProfileMutator.ApplyReputationDelta(profile, quest.RewardRelation, NpcMemoryService.Core.Standing.RegardCause.Quest);
            profile.LastRelationGainHour = CampaignTime.Now.ToHours;
         }

         quest.Status = QuestStatus.Completed;
         profile.Events.Add(new NotableEvent(day, NotableEventType.Collaboration,
            $"The player gave {amountGiven} denars toward the support I asked of them."));
         QuestJournalBridge.CloseAsCompleted(quest, npcHero);
         StanceService.OnPromiseKept(profile);
         store.Set(profile);

         MBInformationManager.AddQuickInformation(new TextObject($"{npcHero.Name} is grateful for the support."), 0, npcHero.CharacterObject);
         ModLog.Info($"[QUEST] ProvideGold completed: {npcHero.Name} received {amountGiven} denars (quest closed).");
      }

      /// <summary>
      ///   Performs the actual hire — the native actions shared by the cash hire and the
      ///   in-kind bargain. The caller owns the price logic and affordability check; this
      ///   re-validates the NPC is genuinely recruitable (the bridge is the law) and then
      ///   transfers any cash, adds the companion to the clan, and moves them into the party.
      ///   A price of 0 means the payment was made some other way (goods already handed over).
      /// </summary>
      private static ActionOutcome DoJoin(Hero npcHero, int price, string logDetail)
      {
         if (!RecruitmentPricing.IsRecruitable(npcHero))
            return ActionOutcome.Refused($"({npcHero?.Name} is not free to take service.)");

         var clan = Clan.PlayerClan;
         int goldBefore = Hero.MainHero.Gold;
         if (price > 0) GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, npcHero, price);
         AddCompanionAction.Apply(clan, npcHero);
         AddHeroToPartyAction.Apply(npcHero, MobileParty.MainParty);

         ModLog.Info($"[ACTION] join_party: {npcHero.Name} {logDetail}; " + $"player gold {goldBefore} → {Hero.MainHero.Gold}.");

         string note = price > 0
            ? $"{price} denars — purse: {Hero.MainHero.Gold}"
            : "paid in goods";

         return new ActionOutcome($"{npcHero.Name} joins your party ({note}).", true);
      }

      /// <summary>
      ///   Divorce, Phase 2b (+ its escalation follow-up): accept_divorce — the player's OWN spouse has
      ///   been pressing a divorce demand in conversation (see <see cref="Mapping.EncounterContextBuilder" />'s
      ///   SpouseDivorceDemandNote), OR has since stopped waiting and begun an outright estrangement of
      ///   their own (SpouseEstrangementNote) — either way, the player consents in free text (no button).
      ///   Dissolves the marriage via <see cref="MarriageDissolutionService.TryDissolvePlayerMarriage" />,
      ///   whose own live re-validation (already-dissolved, no longer married) is what stops this from ever
      ///   double-dissolving against the weekly estrangement sweep. Mutual consent (this was the spouse's
      ///   own wish either way): the consequences are deliberately SOFT, a modest personal-regard settling
      ///   at most, never the full repudiated-divorce grudge matrix
      ///   <see cref="Marriage.DivorceProcessBehavior" /> applies to the NPC-NPC case.
      /// </summary>
      private static ActionOutcome ExecuteAcceptDivorce(Hero npcHero)
      {
         try
         {
            var player = Hero.MainHero;

            if (player == null || npcHero == null || npcHero != player.Spouse)
               return ActionOutcome.Refused(SilentRefusalMessages.DivorceNotFromSpouse());

            var demands = SpouseDivorceDemandBehavior.Instance;
            bool demanding = demands?.IsDemanding(npcHero.StringId) == true;
            bool estranging = demands?.IsEstranging(npcHero.StringId) == true;

            if (demands == null || !demanding && !estranging)
               return ActionOutcome.Refused(SilentRefusalMessages.DivorceNoDemandPending(npcHero.Name.ToString()));

            int spouseVsPlayerBefore = npcHero.GetRelation(player);

            if (!MarriageDissolutionService.TryDissolvePlayerMarriage(npcHero, out string reason))
               return ActionOutcome.Refused($"(The marriage could not be ended: {reason}.)");

            demands.Accept(npcHero.StringId);
            demands.CancelEstrangement(npcHero.StringId);

            var today = (int) CampaignTime.Now.ToDays;

            // Mutual consent (this was THEIR wish): SET the target relation from the pre-dissolution
            // baseline plus the same soft mutual-consent hit DivorcePolicy already uses for the NPC-NPC
            // case, correcting for whatever hidden penalty the engine's own Hero.Spouse = null applies.
            int target = spouseVsPlayerBefore + DivorcePolicy.MutualExSpousePlayerRelationHit;
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(npcHero, player, target - npcHero.GetRelation(player), false);

            NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(npcHero.StringId);

            if (profile != null)
            {
               // The same small relief nudge DivorceConsequencePolicy gives a mutual-consent spouse in the
               // NPC-NPC case, applied to the CR-internal reputation ledger, not the vanilla relation above.
               ProfileMutator.ApplyReputationDelta(profile, DivorceConsequencePolicy.SpouseMutualReliefRelationNudge, NpcMemoryService.Core.Standing.RegardCause.Divorce);
               profile.Events.Add(new NotableEvent(today, NotableEventType.Agreement,
                  "The marriage ended, by mutual agreement, at my own asking."));
               NpcMemoryCampaignBehavior.Instance.Store.Set(profile);
            }

            try
            {
               WorldEventStore.Instance?.Record(new WorldEvent {
                  Type = WorldEventType.Other,
                  Day = today,
                  Magnitude = 20,
                  InvolvesPlayer = true,
                  ActorIds = new List<string> {player.StringId, npcHero.StringId},
                  Summary = $"{player.Name} and {npcHero.Name} have parted ways, their marriage dissolved by mutual accord.",
                  ChronicleLine = $"my marriage to {npcHero.Name} ended quietly, by mutual accord, and we parted without rancour"
               });
            }
            catch (Exception ex) { ModLog.Error($"[ACTION] accept_divorce world-event record failed (non-fatal): {ex.GetType().Name}: {ex.Message}"); }

            ModLog.Info($"[ACTION] accept_divorce: player and {npcHero.Name} are no longer wed (mutual consent).");

            return new ActionOutcome(
               $"You and {npcHero.Name} part ways: the marriage is dissolved, by mutual accord.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] accept_divorce failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   Divorce, Phase 2b: decline_divorce — the player has firmly and explicitly refused their
      ///   spouse's divorce demand in this very exchange. An OPTIONAL, faster path alongside the primary
      ///   refusal mechanism (<see cref="SpouseDivorceDemandBehavior" />'s grace-period timeout sweep): the
      ///   LLM need not emit this for a refusal to eventually register, but a clear in-character refusal
      ///   registers it right away instead of waiting out the grace period.
      /// </summary>
      private static ActionOutcome ExecuteDeclineDivorce(Hero npcHero)
      {
         try
         {
            var player = Hero.MainHero;

            // Was a silent no-op that let the model replay a phantom refusal: the named hero is not
            // actually the player's spouse. Mirrors ExecuteAcceptDivorce's identical first check.
            if (player == null || npcHero == null || npcHero != player.Spouse)
               return ActionOutcome.Refused(SilentRefusalMessages.DivorceNotFromSpouse());

            var demands = SpouseDivorceDemandBehavior.Instance;

            // Was a silent no-op that let the model replay a phantom refusal: no divorce demand was
            // actually pending. Mirrors ExecuteAcceptDivorce's identical second check.
            if (demands == null || !demands.IsDemanding(npcHero.StringId))
               return ActionOutcome.Refused(SilentRefusalMessages.DivorceNoDemandPending(npcHero.Name.ToString()));

            demands.RegisterRefusal(npcHero, (int) CampaignTime.Now.ToDays);
            ModLog.Info($"[ACTION] decline_divorce: player refused {npcHero.Name}'s divorce demand.");

            return new ActionOutcome($"{npcHero.Name} takes your refusal hard; the wound between you deepens.", false);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] decline_divorce failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   Divorce, Phase 2a: end_own_marriage — the PLAYER themselves, in free text, chooses to end their
      ///   OWN marriage (see <see cref="Mapping.EncounterContextBuilder" />'s PlayerEndOwnMarriageNote for
      ///   the conditional teaching, taught only when speaking to the player's own living spouse). Dissolves
      ///   IMMEDIATELY via <see cref="MarriageDissolutionService.TryDissolvePlayerMarriage" />: no multi-day
      ///   estrangement process, unlike the NPC-NPC Phase 1 case (the player's own resolve needs no
      ///   resistance window). Mutual-vs-repudiated is decided live by
      ///   <see cref="PlayerDivorceConsentPolicy.SpouseConsents" />: the spouse's own pending Phase 2b
      ///   demand, or their relation with the player already reading unhappy by the same bar
      ///   <see cref="DivorcePolicy.UnhappyThreshold" /> uses for the NPC-NPC case. Either way, consequences
      ///   flow through <see cref="DivorceProcessBehavior.ApplyDivorceConsequences" />, the SAME tiered
      ///   engine the NPC-NPC case shares — mutual consent naturally resolves to soft Relief/None verdicts,
      ///   repudiation to the full grudge matrix (spouse + immediate family + three best friends), with no
      ///   parallel reimplementation here.
      /// </summary>
      private static ActionOutcome ExecuteEndOwnMarriage(Hero npcHero)
      {
         try
         {
            var player = Hero.MainHero;

            if (player == null || npcHero == null || npcHero != player.Spouse)
               return ActionOutcome.Refused("(There is no marriage between you and this person to end.)");

            var demands = SpouseDivorceDemandBehavior.Instance;
            bool spouseWasDemanding = demands?.IsDemanding(npcHero.StringId) == true;
            bool spouseWasEstranging = demands?.IsEstranging(npcHero.StringId) == true;

            int spouseVsPlayerBefore = npcHero.GetRelation(player);

            // An estrangement already under way is at least as clear a "they too want this" signal as a
            // pending Phase 2b demand — the spouse had already stopped merely asking.
            bool mutualConsent = PlayerDivorceConsentPolicy.SpouseConsents(new PlayerDivorceConsentFacts {
               SpouseHasPendingDemand = spouseWasDemanding || spouseWasEstranging,
               SpouseRelationWithPlayer = spouseVsPlayerBefore
            });

            if (!MarriageDissolutionService.TryDissolvePlayerMarriage(npcHero, out string reason))
               return ActionOutcome.Refused($"(The marriage could not be ended: {reason}.)");

            if (spouseWasDemanding) demands.Accept(npcHero.StringId);
            if (spouseWasEstranging) demands.CancelEstrangement(npcHero.StringId);

            var today = (int) CampaignTime.Now.ToDays;
            // Gabriel (17/10/2026): in-game text names the player's character, never "the player".
            string playerName = player.Name?.ToString();
            string cause = $"The marriage was ended by {playerName}'s own choice.";

            // The shared tiered engine (Phase 1's own consequence application, exposed for reuse): divorcer
            // and player are the SAME hero here, so this collapses to a single relation channel rather
            // than the NPC-NPC case's separate divorcer/player-blame split — there is no third party to
            // blame when the player is the one who acted.
            SafeRun.Step($"BannerlordGameStateBridge.ExecuteEndOwnMarriage.ApplyDivorceConsequences({npcHero.StringId})",
               () => DivorceProcessBehavior.Instance?.ApplyDivorceConsequences(
                  player, npcHero, mutualConsent, spouseVsPlayerBefore, spouseVsPlayerBefore, cause, today));

            NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(npcHero.StringId);

            if (profile != null)
            {
               profile.Events.Add(new NotableEvent(today,
                  mutualConsent
                     ? NotableEventType.Agreement
                     : NotableEventType.Betrayal,
                  mutualConsent
                     ? $"The marriage ended, by mutual agreement, when {playerName} chose to end it."
                     : $"Was set aside by {playerName}, who chose to end our marriage."));
               NpcMemoryCampaignBehavior.Instance.Store.Set(profile);
            }

            try
            {
               WorldEventStore.Instance?.Record(new WorldEvent {
                  Type = mutualConsent
                     ? WorldEventType.Other
                     : WorldEventType.Scandal,
                  Day = today,
                  Magnitude = mutualConsent
                     ? 20
                     : DivorcePolicy.ScaledScandalMagnitude(npcHero.Culture?.StringId),
                  InvolvesPlayer = true,
                  ActorIds = new List<string> {player.StringId, npcHero.StringId},
                  FactionIds = new List<string>(new[] {player.MapFaction?.StringId, npcHero.MapFaction?.StringId}.Where(id => !string.IsNullOrEmpty(id))),
                  Summary = mutualConsent
                     ? $"{player.Name} and {npcHero.Name} have parted ways, their marriage dissolved by mutual accord."
                     : $"Word spreads that {player.Name} has set aside {npcHero.Name}, dissolving their marriage.",
                  ChronicleLine = mutualConsent
                     ? $"my marriage to {npcHero.Name} ended quietly, by mutual accord, and we parted without rancour"
                     : $"at my own choosing, I set aside {npcHero.Name} and ended our marriage, to both our houses' outrage"
               });
            }
            catch (Exception ex) { ModLog.Error($"[ACTION] end_own_marriage world-event record failed (non-fatal): {ex.GetType().Name}: {ex.Message}"); }

            ModLog.Info($"[ACTION] end_own_marriage: player and {npcHero.Name} are no longer wed" +
                        $"{(mutualConsent ? " (mutual consent)" : " (repudiated)")}.");

            // A mutual parting cues the positive (green) system line; a repudiation cues the negative (red)
            // one, since the player has just wronged a spouse who did not want this and set a grudge storm
            // (spouse + their family + friends) against themselves. The deed succeeded either way; the cue
            // reflects its human weight, not its mechanical success.
            return new ActionOutcome(
               mutualConsent
                  ? $"You and {npcHero.Name} part ways: the marriage is dissolved, by mutual accord."
                  : $"You set aside {npcHero.Name}: the marriage is ended, at your own choosing.",
               mutualConsent);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] end_own_marriage failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   execute_prisoner: the player, mid PLAYER-AS-CAPTOR scene (the mod's <c>ChatViewModel.ActivateCaptorSceneMode</c>),
      ///   states a real, final killing act against the prisoner they hold, and the LLM recognized it (see
      ///   the SDK's <c>PromptBuilder.AppendPlayerCaptorSceneRules</c>). Guarded exactly like free_prisoner:
      ///   the prompt is advice, the bridge is law, so only a hero the player ACTUALLY holds prisoner right
      ///   now is executed; a stray or premature emission never kills the wrong person. Runs the same real
      ///   vanilla <see cref="KillCharacterAction.ApplyByExecution" /> the old map-popup entry point used,
      ///   which raises <see cref="CampaignEvents.HeroKilledEvent" /> with detail Executed: existing
      ///   listeners fire off that same event without any special-casing here. For a tracked nemesis,
      ///   <see cref="Nemesis.NemesisResolutionBehavior" /> records the closure deed; for any other captured
      ///   lord, <see cref="Grudges.ExecutedKinGrudgeBehavior" /> plants a vendetta on their living kin,
      ///   exactly as it already does for any other execution in the game.
      /// </summary>
      private static ActionOutcome ExecuteExecutePrisoner(Hero npcHero)
      {
         try
         {
            if (npcHero == null) return null;
            if (!npcHero.IsPrisoner || npcHero.PartyBelongedToAsPrisoner?.LeaderHero != Hero.MainHero)
               return ActionOutcome.Refused($"({npcHero.Name} is not yours to execute.)");

            string name = npcHero.Name?.ToString() ?? "the prisoner";
            KillCharacterAction.ApplyByExecution(npcHero, Hero.MainHero);
            ModLog.Info($"[ACTION] execute_prisoner: player executed {name}.");

            return new ActionOutcome($"{name} is executed at your hand.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] execute_prisoner failed: {ex}"); // the whole trace: the throw is in the engine's kill or a listener, and the message alone cannot say which

            return null;
         }
      }

      /// <summary>
      ///   execute_player (2026-08-04, player request via the "AI Executes" mod, COUNCIL_ACTIONS.md Partie
      ///   10.6): the CAPTOR of the player, mid a Hardcore Torture/Domination captive scene, chose to actually
      ///   end the player's life. HIGH STAKES AND IRREVERSIBLE, so every guard is RE-VALIDATED here from live
      ///   engine facts, through the exact same pure <see cref="PlayerExecutionPolicy" /> the mod's own
      ///   EncounterContextBuilder used to decide whether the action was even taught this turn ("the prompt
      ///   is advice, the bridge is law", never trusted here): the MCM opt-in must be on, the player must be
      ///   a genuine prisoner of THIS captor right now, the same Hardcore content floor every other
      ///   captive-scene act requires must hold, and Bannerlord's own campaign death setting
      ///   (<see cref="CampaignOptions.IsLifeDeathCycleDisabled" />) must permit a hero to die. Any missing
      ///   guard REFUSES outright (surfaced to the player and the model, never silent) and kills no one.
      ///   <para>
      ///     Calls <see cref="KillCharacterAction.ApplyByExecution" /> with <c>isForced: false</c>
      ///     (deliberately NOT true): decompiling the engine shows that for <see cref="Hero.MainHero" />
      ///     specifically, <c>ApplyInternal</c> takes an early, dedicated branch when <c>isForced</c> is
      ///     false, raising <c>CampaignEventDispatcher.OnBeforeMainCharacterDied</c> instead of the ordinary
      ///     hero-death path. SandBox's own <c>HeirSelectionCampaignBehavior</c> listens for exactly that
      ///     event and performs the real death (marks the hero dead, ends captivity, and either opens the
      ///     game's own heir-selection screen when the clan has an heir apparent, or ends the run when it does
      ///     not), the SAME vanilla path that already handles every other cause of the main hero's
      ///     death, so it correctly triggers heir succession or game over. Passing <c>isForced: true</c>
      ///     would skip that dedicated branch entirely and hard-kill the hero without ever notifying
      ///     SandBox's heir-selection logic, leaving the campaign in a broken state. Bannerlord's own
      ///     <c>CampaignOptions.IsLifeDeathCycleDisabled</c> (the "Death" campaign-creation option) only ever
      ///     blocks a NATURAL old-age death in the engine's own <c>Hero.CanDie</c>; it never gates an
      ///     execution. This action still honours it as an EXTRA, more conservative safety net of its own
      ///     (see <see cref="PlayerExecutionPolicy" />), matching the spirit of a player who disabled death
      ///     for their campaign, even though vanilla itself would not have stopped an execution.
      ///   </para>
      /// </summary>
      private static ActionOutcome ExecuteExecutePlayer(Hero npcHero)
      {
         try
         {
            var player = Hero.MainHero;
            if (npcHero == null || player == null) return null;

            bool playerIsCaptiveOfThisCaptor = player.IsPrisoner
               && player.PartyBelongedToAsPrisoner?.LeaderHero == npcHero;

            bool permitted = PlayerExecutionPolicy.CanExecute(new PlayerExecutionPolicy.ExecutionFacts {McmOn = ModSettings.AllowCaptorExecutions, PlayerIsCaptive = playerIsCaptiveOfThisCaptor, Hardcore = ModSettings.AdultContentLevel == AdultContentLevel.Hardcore, DeathAllowed = !CampaignOptions.IsLifeDeathCycleDisabled});

            if (!permitted)
            {
               ModLog.Info($"[ACTION] execute_player refused by PlayerExecutionPolicy (MCM={ModSettings.AllowCaptorExecutions}, " +
                           $"captive={playerIsCaptiveOfThisCaptor}, hardcore={ModSettings.AdultContentLevel == AdultContentLevel.Hardcore}, " +
                           $"deathAllowed={!CampaignOptions.IsLifeDeathCycleDisabled}).");

               return ActionOutcome.Refused("(The game does not allow an execution here, so none took place.)");
            }

            string npcName = npcHero.Name?.ToString() ?? "your captor";
            ModLog.Info($"[ACTION] execute_player: {npcName} executed the player. Game over or heir succession follows.");
            // isForced: false is deliberate (see the doc above): it is the same vanilla call that lets
            // SandBox's HeirSelectionCampaignBehavior run the real death (heir screen or game over).
            KillCharacterAction.ApplyByExecution(player, npcHero, showNotification: true, isForced: false);

            return new ActionOutcome($"{npcName} makes good on it. Your story ends here.", false);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] execute_player failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   free_prisoner: a captive lord the PLAYER holds has struck a bargain for their freedom and the
      ///   player agreed — release them. The reward they promised (goodwill, a ransom, or service) rides
      ///   alongside as the LLM's own change_relation / give_gold / join_clan actions. Guarded: only a
      ///   hero the player actually holds prisoner is freed, so a stray emission can never let one escape.
      ///   Stance mechanical behaviour-gating (increment 1, Fear): re-validated here, never trusted from
      ///   the LLM's own framing of the bargain — <see cref="StanceGatePolicy.ResolveCoercion" /> reads the
      ///   captive's live Fear/Respect and traits and can refuse the bargain outright (a captive too
      ///   unafraid and too contemptuous to be moved) or mark it as fear-coerced (softer terms for the
      ///   player, but a HIDDEN grudge instead of open regard).
      /// </summary>
      private static ActionOutcome ExecuteFreePrisoner(Hero npcHero, ActionContext context)
      {
         try
         {
            if (npcHero == null) return null;
            if (!IsHeldByPlayer(npcHero))
               return ActionOutcome.Refused($"({npcHero.Name} is not yours to set free.)");

            CoercionResult coercion = ResolveFreePrisonerCoercion(npcHero, context);

            // A captor can ALWAYS free their own prisoner unconditionally: the captive's Fear/Respect
            // only shapes the AFTERMATH of a coerced bargain (see YieldsSofterTerms below), never whether
            // the release itself happens. RefusesFlat therefore no longer blocks: it now just means no
            // fear-coercion took place, so the release proceeds as a plain mercy release below.

            EndCaptivityAction.ApplyByReleasedByChoice(npcHero, Hero.MainHero);
            StanceService.OnGiftReceived(npcHero); // freedom is a gift — a little trust is earned
            ModLog.Info($"[ACTION] free_prisoner: player released {npcHero.Name} from captivity.");

            // Appreciations pillar: being handed back one's own freedom is a remembered kindness in its
            // own right, a channel separate from the trust bump above. M-A2 (regard audit): only a MERCY release
            // earns it. A fear-COERCED yield plants its own hidden CoercedRelease grudge below instead; planting
            // the gratitude too made the tarnish cancel it to ~0, so the "kindness" was invisible the very next
            // conversation and, being hidden, the player could not tell why. Coercion earns resentment, not thanks.
            var today = (int) CampaignTime.Now.ToDays;
            if (coercion.Outcome != CoercionOutcome.YieldsSofterTerms)
            {
               AppreciationBehavior.Instance?.TryPlant(npcHero, Hero.MainHero, AppreciationSource.RansomedOrFreed,
                  "The player gave me back my freedom.", RansomedOrFreedMagnitude, true, today);
               Mapping.DeedRegardApplier.Apply(npcHero, Logic.PlayerDeed.FreedPrisoner); // mercy, not a cowed yield
            }

            // Grudges pillar (Deed gesture): freeing them is a verified real favour toward npcHero, a
            // channel separate from the trust bump and the appreciation plant above.
            string grudgeTail = GrudgeResolutionService.TryGrudgeDeedTail(npcHero, today);

            // Stance mechanical behaviour-gating (increment 1, Fear): a fear-coerced yield plants its
            // OWN hidden grudge on top of (never instead of) the deed-gesture resolution above — the
            // captive quietly resents being cowed even as any standing grievance toward them mends.
            // SEAM (flagged, not implemented): RansomMultiplier is meant to soften "whatever ransom/term
            // value the bargain uses", but free_prisoner carries no numeric ransom at this call site —
            // the LLM pairs its own freeform give_gold/join_clan/change_relation action(s) in the SAME
            // response, executed independently and unclamped by this bridge. There is no seam here to
            // scale that value without a larger cross-action correlation change, which this increment
            // does not make. See the increment's report for detail.
            if (coercion.Outcome == CoercionOutcome.YieldsSofterTerms)
               TryPlantCoercedGrudge(npcHero, coercion.HiddenGrudgeMagnitude, today);

            // Chronicle: a third, independent channel from the trust bump and the appreciation plant above.
            try
            {
               string pronoun = npcHero.IsFemale
                  ? "her"
                  : "his";
               WorldEventStore.Instance?.Record(new WorldEvent {
                  Type = WorldEventType.PlayerFeat,
                  Day = today,
                  Magnitude = 40,
                  InvolvesPlayer = true,
                  Summary = $"{Hero.MainHero.Name} freed {npcHero.Name} from captivity.",
                  ActorIds = new List<string> {Hero.MainHero.StringId, npcHero.StringId},
                  FactionIds = npcHero.MapFaction != null
                     ? new List<string> {npcHero.MapFaction.StringId}
                     : new List<string>(),
                  ChronicleLine = $"{Hero.MainHero.Name} gave {npcHero.Name} back {pronoun} freedom, and it was not forgotten"
               });
            }
            catch (Exception ex) { ModLog.Error($"[ACTION] free_prisoner chronicle record failed: {ex.GetType().Name}: {ex.Message}"); }

            return new ActionOutcome($"You set {npcHero.Name} free.{grudgeTail}", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] free_prisoner failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   The real terms a counter_offer would settle: shared between the confirmation controller (the question
      ///   the player is asked) and the executor (what actually applies), so the term the player says yes to is the
      ///   term that happens, exactly as <see cref="PreviewItem" /> is shared for a sale. Re-decides the SAME offer
      ///   the captive-terms prompt section decided (<see cref="Mapping.EncounterContextBuilder.ResolveCaptiveOffer" />)
      ///   and refuses any emitted term that is not open to her right now, by name: the prompt is advice, the bridge
      ///   is law. The ransom is clamped here, once, to what the payer holds and the most such a captive fetches.
      /// </summary>
      public static CounterOfferPreview PreviewCounterOffer(GameAction action, Hero npcHero, ActionContext context)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null || action?.Parameters == null) return null;
            if (!IsHeldByPlayer(npcHero))
               return new CounterOfferPreview {Refusal = $"({npcHero.Name} is not your prisoner to bargain with.)"};

            action.Parameters.TryGetValue("terms", out string termsText);
            action.Parameters.TryGetValue("amount", out string amountText);
            IReadOnlyList<CaptiveTerm> wanted = CaptiveOfferPolicy.ParseTerms(termsText);
            if (wanted.Count == 0)
               return new CounterOfferPreview {Refusal = "(No terms could be read from that offer, so nothing was settled.)"};

            NpcProfile profile = context?.Profile ?? NpcMemoryCampaignBehavior.Instance?.Store?.Get(npcHero.StringId);
            bool canRecruitPrisoner = PrisonerRecruitmentPolicy.CanRecruit(true, npcHero.Clan?.Leader == npcHero, profile?.ReputationWithPlayer ?? 0);
            CaptiveOffer offer = EncounterContextBuilder.ResolveCaptiveOffer(npcHero, profile, PlayerStatusVsNpc.NpcIsCaptive, canRecruitPrisoner);
            if (offer == null)
               return new CounterOfferPreview {Refusal = $"({npcHero.Name} has no offer open to settle.)"};

            foreach (CaptiveTerm term in wanted)
            {
               if (term == CaptiveTerm.Goodwill) continue; // always open, costs nothing, never gated

               if (!offer.Terms.Contains(term))
                  return new CounterOfferPreview {Refusal = $"({npcHero.Name} cannot really offer {ReadableCaptiveTerm(term)}; that is not open to her now.)"};
            }

            Hero payer = npcHero.Clan?.Leader ?? npcHero;
            bool wantsRansom = wanted.Contains(CaptiveTerm.Ransom);
            int named = CoinAmount.Parse(amountText);
            int ransom = wantsRansom ? Math.Min(named, Math.Min(payer.Gold, CalradiaRemembers.Logic.Prisoners.PrisonerTradePolicy.MaxPrice(CourtActionResolver.RansomAnchor(npcHero)))) : 0;
            bool purseHeld = wantsRansom && ransom < named && ransom == payer.Gold;

            return new CounterOfferPreview {
               Terms = wanted, Ransom = ransom, RansomClampedToPurse = purseHeld, RansomClampedToWorth = wantsRansom && ransom < named && !purseHeld,
               FealtyRealm = wanted.Contains(CaptiveTerm.Fealty) ? Hero.MainHero.Clan?.Kingdom?.Name?.ToString() : null
            };
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] counter_offer preview failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   counter_offer (Maifailun and Gabriel, 14/10/2026): a captive the player holds makes her own offer for
      ///   freedom, or agrees to terms the player named, and the deal is struck in this reply. REFUSES outright
      ///   unless <paramref name="npcHero" /> is genuinely the player's prisoner (same guard <see cref="ExecuteFreePrisoner" />
      ///   uses) and reruns <see cref="PreviewCounterOffer" />, which refuses any term not open to her right now,
      ///   by name, before a single piece of state moves. SERVICE RUNS FIRST: its own gate
      ///   (<see cref="PrisonerRecruitmentPolicy" />, via <see cref="ExecuteRecruitPrisoner" />) is the only term
      ///   whose refusal is still possible at this point (regard and leadership re-checked live), so putting it
      ///   first is what keeps the rest all-or-nothing, since every other term (gold, a realm, a scene already
      ///   played) has no refusal path left once reached. The final release is guarded by her STILL being a
      ///   prisoner, not merely by service not having freed her already, since a realm change can cascade into a
      ///   vanilla listener releasing her by some other path.
      /// </summary>
      private static ActionOutcome ExecuteCounterOffer(GameAction action, Hero npcHero, ActionContext context)
      {
         try
         {
            CounterOfferPreview preview = PreviewCounterOffer(action, npcHero, context);
            if (preview == null) return null;
            if (preview.Refusal != null) return ActionOutcome.Refused(preview.Refusal);

            var done = new List<string>();
            var freedByService = false;
            var ransomApplied = 0;
            string possessive = npcHero.IsFemale ? "her" : "his";

            if (preview.Terms.Contains(CaptiveTerm.Service))
            {
               ActionOutcome serviceOutcome = ExecuteRecruitPrisoner(npcHero, context?.Profile);
               if (serviceOutcome == null || serviceOutcome.WasRefused)
                  return serviceOutcome ?? ActionOutcome.Refused($"({npcHero.Name}'s service could not be settled after all.)");

               done.Add($"{possessive} service sworn to your clan");
               freedByService = true;
            }

            if (preview.Terms.Contains(CaptiveTerm.Ransom) && preview.Ransom > 0)
            {
               Hero payer = npcHero.Clan?.Leader ?? npcHero;
               ExchangeService.Pay(ExchangeKind.Ransom, payer, Hero.MainHero, preview.Ransom);
               ransomApplied = preview.Ransom;
               done.Add($"{preview.Ransom} denars ransomed from {payer.Name}{(preview.RansomClampedToPurse ? " (all they had)" : preview.RansomClampedToWorth ? " (the most they fetch)" : "")}");
            }

            if (preview.Terms.Contains(CaptiveTerm.Fealty) && npcHero.Clan != null && Hero.MainHero.Clan?.Kingdom != null)
            {
               Kingdom realm = Hero.MainHero.Clan.Kingdom;
               DiplomacyOutcome door = DiplomacyCommit.Realm(Logic.Diplomacy.DiplomacyChange.ClanJoinsKingdom, "captor_terms", npcHero.Clan, realm, npcHero,
                  () => ChangeKingdomAction.ApplyByJoinToKingdom(npcHero.Clan, realm));
               done.Add(door.Happened
                  ? $"the house of {npcHero.Clan.Name} sworn to {realm.Name}"
                  : door.NotHappenedLine($"The house of {npcHero.Clan.Name} sworn to {realm.Name}").Trim('(', ')', '.'));
            }

            if (preview.Terms.Contains(CaptiveTerm.Intimacy))
            {
               ModLog.Info($"[ACTION] counter_offer: {npcHero.Name} pays her term of intimacy in the scene itself; nothing further to move.");
               done.Add("herself, as agreed");
            }

            if (preview.Terms.Contains(CaptiveTerm.Goodwill))
               done.Add($"{possessive} lasting goodwill");

            if (!freedByService && npcHero.IsPrisoner) EndCaptivityAction.ApplyByReleasedByChoice(npcHero, Hero.MainHero);

            string recap = done.Count > 0 ? string.Join(", ", done) : "her bare word";
            ModLog.Info($"[ACTION] counter_offer: {npcHero.Name} freed on terms: {recap}.");

            return new ActionOutcome($"{npcHero.Name} is set free: {recap}.", true,
               appliedGoldAmount: ransomApplied > 0 ? (int?) ransomApplied : null);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] counter_offer failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>A plain-language name for a refused CaptiveTerm, for the player-facing refusal note.</summary>
      private static string ReadableCaptiveTerm(CaptiveTerm term) => term switch {
         CaptiveTerm.Ransom => "a ransom",
         CaptiveTerm.Fealty => "her house's fealty",
         CaptiveTerm.Service => "her service",
         CaptiveTerm.Intimacy => "herself",
         CaptiveTerm.Goodwill => "her goodwill",
         _ => "that term"
      };

      // give_influence, lend_troops, sway_opinion, release_prisoner executors moved to
      // CalradiaRemembers.Verbs (GiveInfluenceVerb / LendTroopsVerb / SwayOpinionVerb / ReleasePrisonerVerb),
      // routed by VerbRegistry from the switch above. Reuse CourtActionResolver/CourtActionPolicy exactly
      // as before; see those classes for the eligibility gates and cooldown markers. give_troops /
      // spend_influence (04/07/2026, GiveTroopsVerb / SpendInfluenceVerb) are the reciprocal direction,
      // routed identically.

      /// <summary>
      ///   gather_news is now a BACKWARD-COMPAT ALIAS of dispatch_mission (2026-08-05): the two overlapping errand
      ///   verbs were merged, and gather_news stopped being taught in the 1:1 prompt. This case survives only so a
      ///   stray or residual gather_news emission still works, routed through the SAME shared core
      ///   (<see cref="CourtActionResolver.TryDispatchMission" />) dispatch_mission runs. It maps the legacy 'errand'
      ///   vocabulary (news/scout/steal/trade/envoy) onto the merged mission set via <see cref="MissionTypeParser" />
      ///   (which accepts both spellings), defaulting a blank errand to gathernews (the old gather_news default), and
      ///   threads the optional 'about' topic through unchanged.
      /// </summary>
      private static ActionOutcome ExecuteGatherNews(GameAction action, Hero npcHero)
      {
         try
         {
            if (npcHero is not {IsAlive: true}) return null;

            // Which errand the player asked for (news / scout / steal / trade / envoy); a blank one keeps the old
            // gather_news default of "gathernews" rather than failing the merged verb's "name an errand" guard.
            action.Parameters.TryGetValue("errand", out string errand);
            if (string.IsNullOrWhiteSpace(errand)) errand = "gathernews";

            // Optional topic the LLM extracted from the player's request ("news about Sturgia" -> Sturgia);
            // null means any/nearest town. The core threads it to the mission behaviour.
            action.Parameters.TryGetValue("about", out string about);

            DispatchMissionVerdict verdict = CourtActionResolver.TryDispatchMission(npcHero, errand, about);

            switch (verdict.Result)
            {
               case DispatchMissionResult.ActorUnavailable:
                  return null;
               case DispatchMissionResult.NoMissionSystem:
                  return null;
               case DispatchMissionResult.NotACompanion:
                  return ActionOutcome.Refused($"({npcHero.Name} is not one of your companions to send.)");
               case DispatchMissionResult.NotInPlayerParty:
                  return ActionOutcome.Refused($"({npcHero.Name} does not ride in your party, and cannot be sent from it.)");
               case DispatchMissionResult.AlreadyAway:
                  return ActionOutcome.Refused($"({npcHero.Name} is already away on an errand.)");
               case DispatchMissionResult.NoValidMission:
                  return ActionOutcome.Refused(SilentRefusalMessages.DispatchMissionMissingErrand());
               case DispatchMissionResult.DispatchFailed:
                  return ActionOutcome.Refused($"({verdict.Message})");
            }

            ModLog.Info($"[ACTION] gather_news (alias -> dispatch_mission): {npcHero.Name} dispatched on a {verdict.Type} errand.");

            return new ActionOutcome(verdict.Message, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] gather_news failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      // Cabinaw (06/11/2026): the model paid a task's reward as a gift before the game saw the task done.
      private const string TaskRewardNotYetDueNote =
         "You tried to pay a task's reward as a gift, but the game has not yet recorded that task done. The agreed reward is paid in full, by itself, once it is; do not pay it as a gift before then.";

      private static ActionOutcome ExecuteGiveGold(GameAction action, Hero npcHero, ActionContext context)
      {
         if (!TryGetInt(action.Parameters, "amount", out int amount))
         {
            ModLog.Info($"[ACTION] give_gold from {npcHero?.Name} had no parsable amount: ignored.");

            // Was a silent no-op that let the model keep narrating a gift that never left anyone's purse.
            return ActionOutcome.Refused(SilentRefusalMessages.GiveGoldNoValidSum());
         }
         if (amount <= 0)
         {
            // Same silent-no-op fault as the unparsable case above, just a non-positive value instead.
            return ActionOutcome.Refused(SilentRefusalMessages.GiveGoldNoValidSum());
         }
         if (npcHero == null || Hero.MainHero == null) return null;

         // Solvency: an NPC cannot give gold it does not hold. And a gift has no worth behind it to measure a sum against,
         // so what they would part with bounds it (GiftCeilingPolicy): a hallucinated figure or a player's persuasion moves
         // no more than that.
         NpcProfile profile = context?.Profile ?? NpcMemoryCampaignBehavior.Instance?.Store?.Get(npcHero.StringId);
         GiftFacts gift = ExchangeService.GiftFactsFor(npcHero, profile);
         int payable = GiftCeilingPolicy.Ceiling(gift);
         if (npcHero.Gold < 1)
         {
            ModLog.Info($"[ACTION] give_gold: {npcHero.Name} has no gold to give: ignored.");
            LlmFeedbackNotes.Set(npcHero.StringId, GiftCeilingPolicy.ShortfallFeedback(amount, 0, true));

            // Was a silent no-op: the model had already told the player gold was coming, so the player was
            // left checking an unchanged purse with no explanation.
            return ActionOutcome.Refused(SilentRefusalMessages.GiveGoldGiverInsolvent(npcHero.Name.ToString()));
         }

         if (payable < 1)
         {
            ModLog.Info($"[ACTION] give_gold: {npcHero.Name} will give no more (regard {gift.Regard}, given {gift.GivenInWindow} in the window): ignored.");

            bool awaits = ExchangeService.TaskRewardAwaitsTheDeed(profile);
            // Aporo (10/11/2026): the model reads next turn that nothing moved, so the character does not act as if it had.
            LlmFeedbackNotes.Set(npcHero.StringId, awaits ? TaskRewardNotYetDueNote : GiftCeilingPolicy.ShortfallFeedback(amount, 0, false));

            return ActionOutcome.Refused(GiftCeilingPolicy.WillNotGiveNote(npcHero.Name.ToString(), awaits));
         }

         int asked = amount;
         bool purseHolds = payable >= npcHero.Gold;
         amount = ClampInt(amount, 1, payable);
         ExchangeService.Pay(ExchangeKind.Gift, npcHero, Hero.MainHero, amount);
         ExchangeService.RecordGift(profile, gift, amount);
         // Cabinaw: what paid a verified task's reward comes off it, so the settlement later this turn pays only the rest.
         int towardTasks = GiftCeilingPolicy.TowardTasks(gift, amount);
         if (towardTasks > 0)
         {
            ExchangeService.PayTowardTasks(profile, towardTasks);
            ModLog.Info($"[QUEST] give_gold: {towardTasks} of it pays {npcHero.Name}'s verified task reward.");
         }
         // R7-light (swear_oath): this is ALSO the pay_gold oath's own verification hook, whichever channel
         // the gold actually moved through (this 1:1 action, or the council's own give_gold resolution).
         CalradiaRemembers.Commitments.CommitmentBehavior.Instance?.OnGoldGivenToPlayer(npcHero.StringId, amount);
         ModLog.Info($"[ACTION] give_gold: {npcHero.Name} → player {amount} gold");

         // The map bar does not refresh while campaign time is frozen under the chat
         // overlay — showing the balance here is the player's only live confirmation.
         bool rewardAwaits = asked > amount && !purseHolds && ExchangeService.TaskRewardAwaitsTheDeed(profile);
         if (rewardAwaits) LlmFeedbackNotes.Set(profile.Id, TaskRewardNotYetDueNote);
         else if (asked > amount) LlmFeedbackNotes.Set(npcHero.StringId, GiftCeilingPolicy.ShortfallFeedback(asked, amount, purseHolds));
         string reduced = GiftCeilingPolicy.ReducedNote(new GiftOutcome {
            Asked = asked, Given = amount, LimitedByPurse = purseHolds, GiverName = npcHero.Name.ToString(), TaskRewardAwaitsTheDeed = rewardAwaits
         });

         return new ActionOutcome($"{npcHero.Name} gives you {amount} denars{reduced} (purse: {Hero.MainHero.Gold}).", true,
            appliedGoldAmount: amount);
      }

      /// <summary>
      ///   The roster item a give_item or sell_item names: the bag pick by id first (immune to the model rewording the
      ///   name), else an exact name, else the fuzzy resolver. A narrated gift word ("a pendant") rarely matches a roster
      ///   entry's literal name even when the player plainly holds something that fits (a "Jewelry" trade good; player
      ///   report: a wife offered "a pendant" got a false "you don't have that"), so a name miss is fuzzy-resolved
      ///   (exact/token/synonym-category) before giving up. Shared with the confirmation, so the item the player says
      ///   yes to is the item that leaves (3chmidt, 13/10/2026).
      /// </summary>
      internal static ItemRosterElement ResolveNamedItem(string itemName, string itemId, out bool fuzzy)
      {
         fuzzy = false;
         bool byName = string.IsNullOrWhiteSpace(itemId);
         ItemRosterElement found = !byName ? Items.RosterGoods.Find(MobileParty.MainParty?.ItemRoster, itemId) : FindItemInRoster(itemName);
         if (byName && found.EquipmentElement.Item == null && !string.IsNullOrWhiteSpace(itemName))
         {
            GiftCandidate? match = GiftNameResolver.Resolve(itemName, BuildGiftCandidates());
            if (match.HasValue)
            {
               found = Items.RosterGoods.Find(MobileParty.MainParty?.ItemRoster, match.Value.Id);
               fuzzy = true;
            }
         }

         return found;
      }

      /// <summary>
      ///   The item a sale sells: this turn's bag pick; else the item still on the table from an earlier bag pick in this
      ///   conversation, when the sale names nothing, names nothing the player holds, or names it in a shared word
      ///   (a sale haggled over several replies, 3chmidt 13/10/2026); else the item named.
      /// </summary>
      private static ItemRosterElement ResolveSaleItem(string itemName, ActionContext context)
      {
         if (!string.IsNullOrWhiteSpace(context?.OfferedItemId)) return ResolveNamedItem(itemName, context.OfferedItemId, out _);

         ItemRosterElement byName = ResolveNamedItem(itemName, null, out _);
         string table = context?.ItemOnTheTableId;
         if (string.IsNullOrWhiteSpace(table)) return byName;

         ItemRosterElement onTable = Items.RosterGoods.Find(MobileParty.MainParty?.ItemRoster, table);
         if (onTable.EquipmentElement.Item == null) return byName;
         if (byName.EquipmentElement.Item == null || string.IsNullOrWhiteSpace(itemName)) return onTable;

         string tableName = onTable.EquipmentElement.Item.Name?.ToString() ?? "";
         bool sharesAWord = itemName.Split(' ').Any(w => w.Length >= 3 && tableName.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0);

         return sharesAWord ? onTable : byName;
      }

      /// <summary>
      ///   The item a give_item or sell_item would take, for the player's confirmation: its id (carried into the pending
      ///   action so exactly this one leaves), its name and its market value. Null when the player holds nothing like it.
      /// </summary>
      public static ItemPreview PreviewItem(GameAction action, ActionContext context)
      {
         try
         {
            if (MobileParty.MainParty?.ItemRoster == null || action?.Parameters == null) return null;

            // A lot laid out for one price: the confirmation names the whole lot and its worth (null when none of it is held).
            if (string.Equals(action.Type, "sell_item", StringComparison.OrdinalIgnoreCase)
                && context?.OfferedBundle is {Items.Count: > 1, AskingPrice: not null, Handed: false} lot)
            {
               List<(ItemOffer Offer, ItemRosterElement Found)> goods = LotGoods(lot);
               (ItemOffer Offer, ItemRosterElement Found) first = goods.FirstOrDefault(g => g.Found.EquipmentElement.Item != null);
               if (first.Found.EquipmentElement.Item == null) return null;

               return new ItemPreview {Id = Items.RosterGoods.KeyOf(first.Found.EquipmentElement), Name = LotName(goods), Value = LotValue(goods), IsLot = true};
            }

            action.Parameters.TryGetValue("item", out string itemName);
            ItemRosterElement found = string.Equals(action.Type, "sell_item", StringComparison.OrdinalIgnoreCase)
               ? ResolveSaleItem(itemName, context)
               : ResolveNamedItem(itemName, context?.OfferedItemId, out _);
            ItemObject item = found.EquipmentElement.Item;
            if (item == null) return null;

            // A sale from the bag moves the amount set there (froggyluv, 04/11/2026): the confirmation names it and its worth.
            GiftBundle bundle = context?.OfferedBundle;
            int count = string.Equals(action.Type, "sell_item", StringComparison.OrdinalIgnoreCase) && bundle?.Items is {Count: 1}
                        && Items.RosterGoods.Means(bundle.Items[0].ItemId, found.EquipmentElement) && !bundle.Handed
               ? Math.Max(1, Math.Min(bundle.Items[0].Count, found.Amount))
               : 1;

            // The very piece, quality included (avalanche24, 05/11/2026): the confirmation names it and carries its key.
            string name = Items.RosterGoods.NameOf(found.EquipmentElement);

            return new ItemPreview {Id = Items.RosterGoods.KeyOf(found.EquipmentElement), Name = count > 1 ? $"{count} {name}" : name, Value = Items.RosterGoods.ValueOf(found.EquipmentElement) * count};
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] item preview failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   sell_item (3chmidt, 13/10/2026): the player's item goes to the NPC and the NPC's coin to the player, as one
      ///   deal, at the agreed price brought down to what the buyer can pay (ItemSalePolicy). The item leaves first and
      ///   comes back if the payment fails, so neither half can happen alone. The buyer may wear what they bought.
      /// </summary>
      private static ActionOutcome ExecuteSellItem(GameAction action, Hero npcHero, ActionContext context)
      {
         try
         {
            if (MobileParty.MainParty?.ItemRoster == null || Hero.MainHero == null) return null;
            if (npcHero == null) return ActionOutcome.Refused("(No one here has a purse of their own to buy it with.)");

            // Several kinds laid out in the bag for one price (Gabriel, 09/11/2026): the lot is sold whole, whatever good
            // the model named.
            if (context?.OfferedBundle is {Items.Count: > 1, AskingPrice: not null, Handed: false} lot) return SellBagLot(action, npcHero, lot);

            action.Parameters.TryGetValue("item", out string itemName);
            action.Parameters.TryGetValue("price", out string priceText);
            ItemRosterElement found = ResolveSaleItem(itemName, context);
            ItemObject item = found.EquipmentElement.Item;

            // froggyluv (04/11/2026): "I choose the amount of Food to sell but the mechanic may not fire": a sale moved
            // one unit whatever was agreed. The amount set in the bag for this very good decides; a sale in words stays one
            // (the Compact interpreter prompt has no room to teach sell_item a count).
            GiftBundle bundle = context?.OfferedBundle;
            bool fromBag = item != null && bundle?.Items is {Count: 1} && Items.RosterGoods.Means(bundle.Items[0].ItemId, found.EquipmentElement) && !bundle.Handed;
            int requested = fromBag ? bundle.Items[0].Count : 1;

            int count = Math.Max(1, GiftQuantityPolicy.Resolve(requested, found.Amount));

            ItemSaleDecision deal = ItemSalePolicy.Settle(new ItemSaleFacts {
               AgreedPrice = CoinAmount.Parse(priceText), BuyerPurse = npcHero.Gold, PlayerStillHolds = item != null && found.Amount > 0,
               ItemValue = Items.RosterGoods.ValueOf(found.EquipmentElement) * count, BuyerLovesItsKind = Mapping.NatureReader.LovesKindOf(item, npcHero, context?.Profile)
            });
            switch (deal.Refusal)
            {
               case ItemSaleRefusal.NoPrice:
                  return ActionOutcome.Refused("(No price was settled, so nothing was sold.)");
               case ItemSaleRefusal.ItemGone:
                  return ActionOutcome.Refused("(You don't seem to have anything like that to sell.)");
               case ItemSaleRefusal.BuyerCannotPay:
                  return ActionOutcome.Refused($"({npcHero.Name} has no coin to pay for it, so nothing was sold.)");
            }

            ExchangeService.TakeFromPlayer(ExchangeKind.ItemSale, found.EquipmentElement, count, npcHero);
            try { ExchangeService.Pay(ExchangeKind.ItemSale, npcHero, Hero.MainHero, deal.Price); }
            catch
            {
               ExchangeService.ReturnToPlayer(found.EquipmentElement, count);
               throw;
            }

            if (fromBag) bundle.Handed = true; // the bag's goods are sold: a give_item of the same turn moves nothing more
            string pieceName = Items.RosterGoods.NameOf(found.EquipmentElement);
            string name = count > 1 ? $"{count} {pieceName}" : pieceName;
            ModLog.Info($"[ACTION] sell_item: player sold '{name}' (value {Items.RosterGoods.ValueOf(found.EquipmentElement)}) to {npcHero.Name} for {deal.Price}{(deal.ClampedToPurse ? " (all they had)" : "")}");
            string equipTail = TryEquipGift(npcHero, found.EquipmentElement);

            return new ActionOutcome($"You sell {name} to {npcHero.Name} for {deal.Price} denars{ItemSalePolicy.DifferenceNote(deal, CoinAmount.Parse(priceText))} (purse: {Hero.MainHero.Gold}).{equipTail}", true,
               appliedGoldAmount: deal.Price);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] sell_item failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      // The goods of a lot as the roster holds them now, each with the amount set out in the bag.
      private static List<(ItemOffer Offer, ItemRosterElement Found)> LotGoods(GiftBundle lot)
         => lot.Items.Select(o => (Offer: o, Found: Items.RosterGoods.Find(MobileParty.MainParty?.ItemRoster, o.ItemId))).ToList();

      private static int LotValue(List<(ItemOffer Offer, ItemRosterElement Found)> goods)
         => goods.Sum(g => g.Found.EquipmentElement.Item == null ? 0 : Items.RosterGoods.ValueOf(g.Found.EquipmentElement) * g.Offer.Count);

      private static string LotName(List<(ItemOffer Offer, ItemRosterElement Found)> goods)
         => GiftBundlePolicy.Describe(goods.Where(g => g.Found.EquipmentElement.Item != null)
                                           .Select(g => new GiftLine {Name = Items.RosterGoods.NameOf(g.Found.EquipmentElement), Count = g.Offer.Count}).ToList());

      /// <summary>
      ///   sell_item for several goods laid out in the bag at one price: every good in the amount set out, paid once, or
      ///   nothing when the player no longer holds all of it (GiftBundlePolicy.WholeLotHeld). Priced at the lot's worth.
      /// </summary>
      private static ActionOutcome SellBagLot(GameAction action, Hero npcHero, GiftBundle lot)
      {
         List<(ItemOffer Offer, ItemRosterElement Found)> goods = LotGoods(lot);
         bool whole = GiftBundlePolicy.WholeLotHeld(goods.Select(g => new LaidOutGood {Count = g.Offer.Count, Held = g.Found.EquipmentElement.Item == null ? 0 : g.Found.Amount}).ToList());
         action.Parameters.TryGetValue("price", out string priceText);

         ItemSaleDecision deal = ItemSalePolicy.Settle(new ItemSaleFacts {
            AgreedPrice = CoinAmount.Parse(priceText), BuyerPurse = npcHero.Gold, PlayerStillHolds = whole, ItemValue = LotValue(goods)
         });
         switch (deal.Refusal)
         {
            case ItemSaleRefusal.NoPrice:
               return ActionOutcome.Refused("(No price was settled, so nothing was sold.)");
            case ItemSaleRefusal.ItemGone:
               return ActionOutcome.Refused("(You no longer hold all you laid out, so nothing was sold.)");
            case ItemSaleRefusal.BuyerCannotPay:
               return ActionOutcome.Refused($"({npcHero.Name} has no coin to pay for it, so nothing was sold.)");
         }

         var taken = new List<(EquipmentElement Goods, int Count)>();
         try
         {
            foreach ((ItemOffer offer, ItemRosterElement found) in goods)
            {
               ExchangeService.TakeFromPlayer(ExchangeKind.ItemSale, found.EquipmentElement, offer.Count, npcHero);
               taken.Add((found.EquipmentElement, offer.Count));
            }

            ExchangeService.Pay(ExchangeKind.ItemSale, npcHero, Hero.MainHero, deal.Price);
         }
         catch
         {
            foreach ((EquipmentElement back, int count) in taken) ExchangeService.ReturnToPlayer(back, count);
            throw;
         }

         lot.Handed = true; // the lot is sold: a give_item of the same turn moves nothing more
         string name = LotName(goods);
         ModLog.Info($"[ACTION] sell_item: player sold the lot '{name}' (value {LotValue(goods)}) to {npcHero.Name} for {deal.Price}{(deal.ClampedToPurse ? " (all they had)" : "")}");

         return new ActionOutcome($"You sell {name} to {npcHero.Name} for {deal.Price} denars{ItemSalePolicy.DifferenceNote(deal, CoinAmount.Parse(priceText))} (purse: {Hero.MainHero.Gold}).", true,
            appliedGoldAmount: deal.Price);
      }

      /// <summary>
      ///   give_item: transfers one unit of the named item from the player's party
      ///   inventory to the NPC. The item name must match a roster entry exactly
      ///   (case-insensitive). Quest items are always refused — this guard sits on both
      ///   the prompt side (picker excludes them) and here, so the bridge is the law.
      /// </summary>
      private ActionOutcome ExecuteGiveItem(GameAction action, Hero npcHero, ActionContext context)
      {
         try
         {
            if (MobileParty.MainParty?.ItemRoster == null) return null;

            // Goods chosen in the bag, each in the amount the player set (fearente, Nexus, 29/10/2026). The first
            // give_item of the turn hands them all over; any other the model writes in the same turn is the same
            // gesture said twice and moves nothing more (silently: "you have none" would contradict the handover).
            GiftBundle bundle = context?.OfferedBundle;
            ItemOffer? bagOne = null;
            if (bundle?.Items is {Count: > 0})
            {
               if (bundle.Handed) return null;
               // A price was asked in the bag (froggyluv, 04/11/2026): the goods leave only through sell_item, paid.
               if (bundle.AskingPrice.HasValue)
                  return ActionOutcome.Refused($"(You asked {bundle.AskingPrice.Value:N0} denars for it; nothing changes hands until a price is paid.)");
               bundle.Handed = true;
               if (bundle.Items.Count > 1) return GiveBundle(bundle, npcHero, context);
               bagOne = bundle.Items[0];
            }

            action.Parameters.TryGetValue("item", out string itemName);
            string itemId = bagOne?.ItemId ?? context?.OfferedItemId; // the exact item the player picked from the bag, if any

            // Was a silent no-op that let the model replay a phantom gift: no item was named at all.
            if (string.IsNullOrWhiteSpace(itemName) && string.IsNullOrWhiteSpace(itemId))
               return ActionOutcome.Refused(SilentRefusalMessages.GiveItemNoneNamed());

            bool isFreeTextGift = string.IsNullOrWhiteSpace(itemId);
            ItemRosterElement found = ResolveNamedItem(itemName, itemId, out bool fuzzy);

            if (found.EquipmentElement.Item == null)
            {
               // The fuzzy resolver also found nothing plausible: a softened, generic refusal rather than
               // echoing back the model's possibly odd-reading word.
               if (isFreeTextGift)
                  return ActionOutcome.Refused("(You don't seem to have anything like that to give.)");

               string label = !string.IsNullOrWhiteSpace(itemName)
                  ? itemName
                  : itemId;

               return ActionOutcome.Refused($"(You don't seem to have '{label}' to give.)");
            }

            if (fuzzy)
               ModLog.Info($"[ACTION] give_item: fuzzy-resolved '{itemName}' -> '{found.EquipmentElement.Item.Name}'");

            ActionOutcome intercepted = InterceptGift(found.EquipmentElement.Item, npcHero, context);
            if (intercepted != null) return intercepted;

            // Honour the STATED quantity, clamped to what the player holds (player report: "I give you 10 ingots"
            // moved a single one). The pure GiftQuantityPolicy decides; a missing count is one item. A bag pick carries
            // the amount the player chose in the bag (fearente, 29/10/2026), whatever count the model wrote.
            int requestedCount;
            if (bagOne != null) requestedCount = bagOne.Value.Count;
            else
            {
               action.Parameters.TryGetValue("count", out string countStr);
               int.TryParse(countStr?.Trim(), out requestedCount);
            }

            int count = GiftQuantityPolicy.Resolve(requestedCount, found.Amount);
            if (count <= 0) return ActionOutcome.Refused("(You don't seem to have any of that left to give.)");

            ExchangeService.TakeFromPlayer(ExchangeKind.ItemGift, found.EquipmentElement, count, npcHero);
            string actualName = Items.RosterGoods.NameOf(found.EquipmentElement);
            int unitValue = Items.RosterGoods.ValueOf(found.EquipmentElement);
            int totalValue = unitValue * count;
            string gaveDesc = count > 1 ? $"{count} {actualName}" : actualName;

            // The vassal war duty (Gabriel, 15/10/2026: "des troupes, des chevaux, des armes"): horses or weapons handed
            // to a liege the player owes a levy count toward it at market value. A levy is not a gift: no trust, no
            // gratitude, no grudge softened, nothing put on.
            int levyOwed = WarLevyDesk.Owed(npcHero);
            if (levyOwed > 0 && WarLevyPayment.Counts(LevyGoodsKindOf(found.EquipmentElement.Item)))
            {
               int credit = WarLevyPayment.GoodsCredit(totalValue, levyOwed);
               int stillOwed = WarLevyDesk.Pay?.Invoke(npcHero, credit) ?? levyOwed;
               ModLog.Info($"[ACTION] give_item: '{gaveDesc}' (value {totalValue}) handed to {npcHero?.Name} toward the levy, {credit} counted.");

               return new ActionOutcome($"You hand {gaveDesc} to {npcHero?.Name} toward the levy; {credit} denars of it are counted.{WarLevyDesk.AfterPayment(stillOwed)}", false);
            }

            return GiftEffects(npcHero, context, new List<(EquipmentElement Goods, int Count)> {(found.EquipmentElement, count)});
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] give_item failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   The goods a tale or a supply task asks of this person, handed over as a gift: they settle that ask instead
      ///   (or, short of the count, nothing leaves). Null when the gift answers no ask and is a plain gift.
      /// </summary>
      private ActionOutcome InterceptGift(ItemObject gift, Hero npcHero, ActionContext context)
      {
         // The goods a tale asks of this person, worded in the chat rather than handed with the bag (Gabriel,
         // 21/10/2026: a typed hand-over counts): exactly the count asked leaves and the tale moves, whatever count was
         // said (the confirmation named it); short of it nothing leaves, as with the bag.
         string giftId = gift.StringId;
         AuthoredQuestBehavior tales = AuthoredQuestBehavior.Instance;
         int taleHeld = 0;
         string taleLabel = null;
         int taleAsked = tales == null ? 0 : tales.AskedOfGift(npcHero, giftId, out taleHeld, out taleLabel);
         switch (TaleItemPolicy.ReadGift(taleAsked, taleHeld))
         {
            case TaleGiftReading.HandOver:
               return tales.HandOverHeld(npcHero, giftId, out string handed)
                  ? new ActionOutcome(handed, false)
                  : ActionOutcome.Refused($"({handed ?? "Nothing changed hands."})");
            case TaleGiftReading.Short:
               return ActionOutcome.Refused($"({npcHero?.Name} awaits {taleAsked} {taleLabel}; you carry {taleHeld}, so you keep them until you have them all.)");
         }

         // froggyluv (26/10/2026): goods a supply task asks of this person, handed over in words or through the Give
         // menu, settle it as the bag does; only the bag did, and the famine grain left the party while the task
         // expired as a broken promise. Short of the count nothing leaves, as with the bag.
         return TrySettleSupplyByGift(gift, npcHero, context?.Profile);
      }

      /// <summary>
      ///   Several goods handed over in one gesture, each in the amount chosen in the bag (fearente, Nexus, 29/10/2026:
      ///   grain, fish and meat had to go one by one, one unit each). Each good goes the road a single one does: a tale or
      ///   a supply task it answers settles it, horses and weapons owed to a liege count toward the levy, and the rest is
      ///   ONE gift: one trust bump, one remembered kindness weighed on the whole value, one grudge eased.
      /// </summary>
      private ActionOutcome GiveBundle(GiftBundle bundle, Hero npcHero, ActionContext context)
      {
         ItemRoster roster = MobileParty.MainParty.ItemRoster;
         var notes = new List<string>();
         var gifts = new List<(EquipmentElement Goods, int Count)>();
         var levyGoods = new List<(EquipmentElement Goods, int Count)>();
         int levyOwed = WarLevyDesk.Owed(npcHero);
         var refusedOnly = true;

         foreach (ItemOffer offer in bundle.Items)
         {
            ItemRosterElement found = Items.RosterGoods.Find(roster, offer.ItemId);
            ItemObject item = found.EquipmentElement.Item;
            if (item == null || found.Amount <= 0) continue;

            ActionOutcome intercepted = InterceptGift(item, npcHero, context);
            if (intercepted != null)
            {
               if (!string.IsNullOrWhiteSpace(intercepted.Message)) notes.Add(intercepted.Message);
               if (!intercepted.WasRefused) refusedOnly = false;

               continue;
            }

            int count = GiftQuantityPolicy.Resolve(offer.Count, found.Amount);
            if (count <= 0) continue;

            roster.AddToCounts(found.EquipmentElement, -count);
            refusedOnly = false;
            if (levyOwed > 0 && WarLevyPayment.Counts(LevyGoodsKindOf(item))) levyGoods.Add((found.EquipmentElement, count));
            else gifts.Add((found.EquipmentElement, count));
         }

         if (levyGoods.Count > 0)
         {
            int levyValue = levyGoods.Sum(g => Items.RosterGoods.ValueOf(g.Goods) * g.Count);
            int credit = WarLevyPayment.GoodsCredit(levyValue, levyOwed);
            int stillOwed = WarLevyDesk.Pay?.Invoke(npcHero, credit) ?? levyOwed;
            string levyDesc = GiftBundlePolicy.Describe(levyGoods.Select(g => new GiftLine {Name = Items.RosterGoods.NameOf(g.Goods), Count = g.Count}).ToList());
            ModLog.Info($"[ACTION] give_item: '{levyDesc}' (value {levyValue}) handed to {npcHero?.Name} toward the levy, {credit} counted.");
            notes.Add($"You hand {levyDesc} to {npcHero?.Name} toward the levy; {credit} denars of it are counted.{WarLevyDesk.AfterPayment(stillOwed)}");
         }

         if (gifts.Count > 0) notes.Insert(0, GiftEffects(npcHero, context, gifts).Message);

         if (notes.Count == 0) return ActionOutcome.Refused("(You no longer carry what you meant to give.)");

         return refusedOnly ? ActionOutcome.Refused(string.Join(" ", notes)) : new ActionOutcome(string.Join(" ", notes), false);
      }

      /// <summary>
      ///   What a freely given gift does, once for the whole of it: a little trust, a remembered kindness weighed on its
      ///   whole value and on how welcome it is, a grudge it may ease, and anything they can wear put on.
      /// </summary>
      private static ActionOutcome GiftEffects(Hero npcHero, ActionContext context, List<(EquipmentElement Goods, int Count)> gifts)
      {
         // Someone riding in the player's own company draws on its stores (Aporo, Discord 10/11/2026: a companion
         // "took item and never equipped it"): what they do not put on, and what they take off, goes back to the
         // stores instead of vanishing, and only what they keep is a gift (CompanyGiftPolicy).
         if (SharesTheStores(npcHero)) return CompanyGift(npcHero, context, gifts);

         return GiftEffectsKept(npcHero, context, gifts, null);
      }

      /// <summary>Someone in the player's own party, not a prisoner: they draw on the same stores.</summary>
      internal static bool SharesTheStores(Hero hero)
         => hero != null && hero != Hero.MainHero && MobileParty.MainParty != null && hero.PartyBelongedTo == MobileParty.MainParty;

      /// <summary>
      ///   A hand-over to someone sharing the stores: each good's piece is put on if it suits them, the rest and any
      ///   piece taken off go back to the stores, and only the kept pieces go on to the gift's effects.
      /// </summary>
      internal static ActionOutcome CompanyGift(Hero npcHero, ActionContext context, List<(EquipmentElement Goods, int Count)> gifts)
      {
         ItemRoster roster = MobileParty.MainParty.ItemRoster;
         var kept = new List<(EquipmentElement Goods, int Count)>();
         var backLines = new List<GiftLine>();
         var wearTails = new System.Text.StringBuilder();

         foreach ((EquipmentElement goods, int count) in gifts)
         {
            string tail = TryEquipGift(npcHero, goods, out EquipmentElement takenOff);
            CompanyGiftOutcome o = CompanyGiftPolicy.Settle(new CompanyGiftFacts {Count = count, PutOn = tail.Length > 0, ReplacedAPiece = takenOff.Item != null});

            if (o.BackToStores > 0)
            {
               roster.AddToCounts(goods, o.BackToStores);
               backLines.Add(new GiftLine {Name = Items.RosterGoods.NameOf(goods), Count = o.BackToStores});
            }

            if (o.ReplacedPieceToStores) roster.AddToCounts(takenOff, 1);
            if (o.KeptAsGift > 0)
            {
               kept.Add((goods, o.KeptAsGift));
               wearTails.Append(tail);
            }
         }

         string back = backLines.Count == 0 ? "" : $" {npcHero.Name} does not put on {GiftBundlePolicy.Describe(backLines)}: it stays in the company's stores.";
         ModLog.Info($"[ACTION] give_item: to {npcHero.Name}, who shares the company's stores: {kept.Count} piece(s) worn and kept, "
                     + $"{backLines.Sum(l => l.Count)} back to the stores.");

         if (kept.Count == 0) return new ActionOutcome(back.Trim(), false);

         return GiftEffectsKept(npcHero, context, kept, wearTails.ToString() + back);
      }

      /// <summary>
      ///   The gift's effects on what was really given. <paramref name="alreadyWorn" /> is null when nothing was put on
      ///   yet (each piece is then tried here), or the note of what was already put on.
      /// </summary>
      private static ActionOutcome GiftEffectsKept(Hero npcHero, ActionContext context, List<(EquipmentElement Goods, int Count)> gifts, string alreadyWorn)
      {
         int totalValue = gifts.Sum(g => Items.RosterGoods.ValueOf(g.Goods) * g.Count);
         string gaveDesc = GiftBundlePolicy.Describe(gifts.Select(g => new GiftLine {Name = Items.RosterGoods.NameOf(g.Goods), Count = g.Count}).ToList());

         StanceService.OnGiftReceived(npcHero); // a freely given gift builds a little trust
         ModLog.Info($"[ACTION] give_item: player gave '{gaveDesc}' (value {totalValue}) to {npcHero?.Name}");

         // Appreciations pillar: a freely given gift is also a remembered kindness, a channel separate
         // from the trust bump above and from the grudge-softening tail below. The whole gift's worth counts.
         // What You Know (Gabriel, 27/10/2026: "un NPC pourrait [...] preferer certains cadeaux"): a kind they love
         // earns twice the regard, a cold one half, and the player learns that taste by giving it.
         if (npcHero != null)
         {
            Logic.Character.GiftWelcome welcome = GiftBundlePolicy.Welcome(gifts.Select(g => Mapping.NatureReader.WelcomeOf(g.Goods.Item, npcHero, context?.Profile)).ToList());
            string cause = welcome == Logic.Character.GiftWelcome.Loved ? $"The player gave me {gaveDesc}, a gift after my own heart." : $"The player gave me {gaveDesc}.";
            TryPlantValueAppreciation(npcHero, AppreciationSource.GiftReceived, totalValue, cause, welcome);
         }

         // Grudges pillar (Gift gesture): the same gift may also soften or mend a grudge npcHero holds
         // against the player, a channel separate from the trust bump above.
         string grudgeTail = npcHero != null
            ? TryGrudgeGiftTail(npcHero, totalValue)
            : "";

         // If the gift is something they can wear or wield, they may put it on then and there: an empty slot
         // or a real upgrade always, a lateral gift only if they are fond of you (the wife wears the gifted
         // gown; a rival keeps his own blade). The bridge is the law, so this note claims it only when the
         // equip actually happened.
         string equipTail = alreadyWorn ?? string.Concat(gifts.Select(g => TryEquipGift(npcHero, g.Goods)));

         return new ActionOutcome($"You give {gaveDesc} to {npcHero?.Name}.{grudgeTail}{equipTail}", false);
      }

      /// <summary>What an item is, for the war levy: a mount, a weapon, or anything else (never the levy).</summary>
      private static LevyGoodsKind LevyGoodsKindOf(ItemObject item)
         => item == null ? LevyGoodsKind.Other
            : item.IsMountable ? LevyGoodsKind.Horse
            : item.HasWeaponComponent ? LevyGoodsKind.Weapon
            : LevyGoodsKind.Other;

      /// <summary>
      ///   The "gift equipment gets worn" feature: when a give_item gift is something the NPC can wear or wield,
      ///   decide (via the pure <see cref="GiftEquipPolicy" />) whether they equip it, and if so set the real
      ///   equipment slot. A civilian garment goes on their town outfit, war gear on their battle set. Returns a
      ///   short note when they equipped it (appended to the gift outcome so the model narrates the reaction),
      ///   or "" when they merely accepted it. Gender-locked items are never forced on.
      /// </summary>
      private static string TryEquipGift(Hero hero, EquipmentElement goods) => TryEquipGift(hero, goods, out _);

      /// <summary>As <see cref="TryEquipGift(Hero, EquipmentElement)" />, also giving the piece it took off (empty when none).</summary>
      private static string TryEquipGift(Hero hero, EquipmentElement goods, out EquipmentElement takenOff)
      {
         takenOff = EquipmentElement.Invalid;
         try
         {
            ItemObject item = goods.Item;
            if (hero == null || item == null) return "";

            // Respect the item's own gender lock: never force a man's harness onto a lady, or a gown onto a lord.
            if (hero.IsFemale && item.ItemFlags.HasFlag(ItemFlags.NotUsableByFemale)) return "";
            if (!hero.IsFemale && item.ItemFlags.HasFlag(ItemFlags.NotUsableByMale)) return "";

            // A held prisoner is rendered in her BATTLE set (the captor scene is a world-map conversation), so a
            // garment a captor puts on her must go on the battle set to be SEEN; otherwise the normal rule holds:
            // a civilian piece (a gown, a fine tunic) on their town outfit, war gear on their battle set, so a
            // gifted dress is seen in the streets and a gifted blade is drawn on the field.
            bool isHeldCaptive = hero.IsPrisoner && hero.PartyBelongedToAsPrisoner == MobileParty.MainParty?.Party;
            bool onBattle = GiftEquipPolicy.WearsOnBattleSet(isHeldCaptive, item.IsCivilian);
            Equipment equipment = onBattle ? hero.BattleEquipment : hero.CivilianEquipment;
            if (equipment == null) return "";

            if (!TryResolveEquipSlot(equipment, item, out EquipmentIndex slot)) return ""; // not wearable/wieldable

            // A captor may DRESS a held prisoner (imposed, regardless of her regard), but never ARM one: only a
            // wearable garment slot qualifies, never a weapon or a mount.
            bool isCaptiveDressing = isHeldCaptive && IsGarmentSlot(slot);

            EquipmentElement current = equipment[slot];
            int relation = NpcMemoryCampaignBehavior.Instance?.Store?.Get(hero.StringId)?.ReputationWithPlayer ?? 0;
            bool sentimental = hero.Spouse == Hero.MainHero || hero.IsPlayerCompanion;

            GiftEquipDecision decision = GiftEquipPolicy.Decide(new GiftEquipFacts {IsEquippable = true, SlotEmpty = current.Item == null, GiftValue = Items.RosterGoods.ValueOf(goods), CurrentSlotValue = current.Item == null ? 0 : Items.RosterGoods.ValueOf(current), Relation = relation, IsSentimental = sentimental, IsCaptiveDressing = isCaptiveDressing});

            if (!decision.ShouldEquip) return "";

            // The very piece, its quality kept (avalanche24, 05/11/2026: a Masterwork Noble Bow was worn as a plain one).
            takenOff = current;
            equipment[slot] = new EquipmentElement(item, goods.ItemModifier);
            ModLog.Info($"[ACTION] give_item: {hero.Name} equipped '{Items.RosterGoods.NameOf(goods)}' ({decision.Reason}) in " +
                        $"{(onBattle ? "battle" : "civilian")} slot {slot}.");

            return " " + DescribeEquip(hero, item, decision.Reason);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] give_item equip failed: {ex.GetType().Name}: {ex.Message}");

            return "";
         }
      }

      /// <summary>
      ///   Maps a gifted item to the single equipment slot it belongs in: armour and mount pieces to their fixed
      ///   slot, any weapon to a weapon slot (a same-kind slot if they carry one, else an empty one). Returns
      ///   false for a gift that is neither worn nor wielded (trade goods, a banner, an animal).
      /// </summary>
      private static bool TryResolveEquipSlot(Equipment equipment, ItemObject item, out EquipmentIndex slot)
      {
         slot = EquipmentIndex.Weapon0;
         switch (item.ItemType)
         {
            case ItemObject.ItemTypeEnum.HeadArmor: slot = EquipmentIndex.Head; return true;
            case ItemObject.ItemTypeEnum.BodyArmor:
            case ItemObject.ItemTypeEnum.ChestArmor: slot = EquipmentIndex.Body; return true;
            case ItemObject.ItemTypeEnum.LegArmor: slot = EquipmentIndex.Leg; return true;
            case ItemObject.ItemTypeEnum.HandArmor: slot = EquipmentIndex.Gloves; return true;
            case ItemObject.ItemTypeEnum.Cape: slot = EquipmentIndex.Cape; return true;
            case ItemObject.ItemTypeEnum.Horse: slot = EquipmentIndex.Horse; return true;
            case ItemObject.ItemTypeEnum.HorseHarness: slot = EquipmentIndex.HorseHarness; return true;
            default:
               if (!item.HasWeaponComponent) return false;
               slot = SelectWeaponSlot(equipment, item);

               return true;
         }
      }

      /// <summary>
      ///   Picks the weapon slot a gifted weapon should take: prefer replacing a weapon of the SAME kind (a
      ///   sword for a sword, so a bow is never knocked out for a shield), then the first empty weapon slot,
      ///   and only as a last resort the first slot.
      /// </summary>
      private static EquipmentIndex SelectWeaponSlot(Equipment equipment, ItemObject item)
      {
         for (EquipmentIndex i = EquipmentIndex.Weapon0; i <= EquipmentIndex.ExtraWeaponSlot; i++)
            if (equipment[i].Item != null && equipment[i].Item.ItemType == item.ItemType) return i;

         for (EquipmentIndex i = EquipmentIndex.Weapon0; i <= EquipmentIndex.ExtraWeaponSlot; i++)
            if (equipment[i].Item == null) return i;

         return EquipmentIndex.Weapon0;
      }

      /// <summary>A short, factual note that the NPC put the gift on, appended to the gift outcome so the model narrates it in voice.</summary>
      private static string DescribeEquip(Hero hero, ItemObject item, GiftEquipReason reason)
      {
         string name = item.Name?.ToString() ?? "it";

         switch (reason)
         {
            case GiftEquipReason.Imposed:
               return $"({hero.Name}, your prisoner, is now dressed in {name} at your command.)";
            case GiftEquipReason.Affection:
               return $"({hero.Name} wears {name} gladly, for your sake.)";
            default:
               return $"({hero.Name} puts {name} on at once.)";
         }
      }

      /// <summary>
      ///   True for a slot that holds a worn GARMENT (head/body/legs/hands/cape), the only kind a captor may
      ///   impose on a held prisoner. Weapon and mount slots are excluded, so a "gift" can never arm a captive.
      /// </summary>
      private static bool IsGarmentSlot(EquipmentIndex slot)
         => slot == EquipmentIndex.Head || slot == EquipmentIndex.Body || slot == EquipmentIndex.Leg
         || slot == EquipmentIndex.Gloves || slot == EquipmentIndex.Cape;

      /// <summary>
      ///   grant_blessing: the conversation NPC, as head of their clan, consents to the player
      ///   marrying one of their unwed kin (named in the action) or themselves. The bridge is the
      ///   law — it re-checks the NPC's authority and the match's eligibility before recording the
      ///   family's blessing, which the marriage-consequence resolver later honors as an alliance.
      /// </summary>
      private static ActionOutcome ExecuteGrantBlessing(GameAction action, Hero npcHero)
      {
         try
         {
            if (MarriageApprovalBehavior.Instance == null || npcHero == null) return null;
            if (Hero.MainHero?.Spouse != null)
               return ActionOutcome.Refused("(You are already wed — the blessing cannot apply.)");

            // Was a silent no-op that let the model replay a phantom blessing: no one was named to bless.
            if (!action.Parameters.TryGetValue("hero", out string spouseName) || string.IsNullOrWhiteSpace(spouseName))
               return ActionOutcome.Refused(SilentRefusalMessages.GrantBlessingNoOneNamed());

            Hero spouse = MarriageProspectResolver.ResolveCandidate(npcHero, spouseName);

            if (spouse == null)
               return ActionOutcome.Refused($"({npcHero.Name} names no one of their house by '{spouseName}'.)");

            if (!MarriageProspectResolver.HasAuthority(npcHero, spouse))
               return ActionOutcome.Refused($"({npcHero.Name} has no authority to bless that match.)");

            if (!MarriageProspectResolver.IsMarriageable(Hero.MainHero, spouse))
               return ActionOutcome.Refused($"({spouse.Name} is not free to be matched with you.)");

            // M-B3: a blessing from a dead, imprisoned, at-war, or openly hostile lord is not a blessing. The
            // regard read is the mod's own personal ledger (the number the player builds in chat), per the two-
            // ledger rule, not the vanilla clan relation.
            int blesserRegard = NpcMemoryCampaignBehavior.Instance?.Store?.Get(npcHero.StringId)?.ReputationWithPlayer
                                ?? npcHero.GetRelation(Hero.MainHero);
            if (!MarriageGuardPolicy.BlessingAllowed(new MarriageGuardPolicy.BlesserFacts {BlesserAlive = npcHero.IsAlive, BlesserPrisoner = npcHero.IsPrisoner, AtWarWithBlesser = IsAtWarWithPlayer(npcHero), BlesserRegard = blesserRegard}))
               return ActionOutcome.Refused($"({npcHero.Name} is in no position to bless a match with you now.)");

            var day = (int) CampaignTime.Now.ToDays;
            MarriageApprovalBehavior.Instance.Grant(spouse, npcHero, day);

            var msg = $"{npcHero.Name} blesses a match between you and {spouse.Name}. Their house will favor the union.";
            MBInformationManager.AddQuickInformation(new TextObject(msg), 0, npcHero.CharacterObject);
            ModLog.Info($"[ACTION] grant_blessing: {npcHero.Name} consented to the player marrying {spouse.Name}.");

            return new ActionOutcome(msg, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] grant_blessing failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   arrange_marriage (the MATCHMAKER): the player weds one of their OWN clan's unwed kin (player_kin) to
      ///   a noble of the conversation NPC's house (target_kin), a match in which the player is NEITHER spouse.
      ///   The bridge is the law: it delegates the whole decision, resolving both by name, checking the NPC's
      ///   authority over their kin, deferring HARD eligibility (age, kinship, marital status) to the engine's
      ///   own MarriageModel, then asking the pure consent policy (a systems decision, never the LLM's) before
      ///   applying the native marriage, to <see cref="CourtActionResolver.TryArrangeMarriage" /> (2026-08-04,
      ///   factored out so the council's own arrange_marriage resolution, <c>CalradiaRemembers.Chat.CouncilLift.SettleArrangeMarriage</c>,
      ///   reuses the IDENTICAL core rather than a second copy, mirroring pledge_against's own LaunchPledge
      ///   extraction). This method only resolves the ACTION's own message text per outcome; the behavior is
      ///   unchanged from before the extraction. Consent is given on regard or on marrying UP; the wedding is
      ///   real (MarriageAction.Apply).
      /// </summary>
      private static ActionOutcome ExecuteArrangeMarriage(GameAction action, Hero npcHero)
      {
         try
         {
            if (npcHero == null) return null;

            action.Parameters.TryGetValue("player_kin", out string playerKinName);
            action.Parameters.TryGetValue("target_kin", out string targetKinName);

            // Was a silent no-op that let the model replay a phantom betrothal: a kin name was missing.
            if (string.IsNullOrWhiteSpace(playerKinName) || string.IsNullOrWhiteSpace(targetKinName))
               return ActionOutcome.Refused(SilentRefusalMessages.ArrangeMarriageMissingKin());

            ArrangeMarriageVerdict verdict = CourtActionResolver.TryArrangeMarriage(playerKinName, targetKinName, npcHero);

            switch (verdict.Result)
            {
               case ArrangeMarriageResult.NoPlayerKin:
                  return ActionOutcome.Refused($"(No unwed kin of your own clan answers to '{playerKinName}'.)");
               case ArrangeMarriageResult.PlayerThemself:
                  return ActionOutcome.Refused($"(Arranging a marriage weds one of your kin, never you, so nothing was sealed. A wedding of your own is "
                                               + $"sealed only when the game can perform it: run cr.marry_check {npcHero.Name} to see what stands in the way.)");
               case ArrangeMarriageResult.NoTargetKin:
                  return ActionOutcome.Refused($"({npcHero.Name} names no one of their house by '{targetKinName}'.)");
               case ArrangeMarriageResult.NoAuthority:
                  return ActionOutcome.Refused($"({npcHero.Name} has no authority to promise {verdict.B.Name} in marriage.)");
               case ArrangeMarriageResult.NotSuitable:
                  return ActionOutcome.Refused($"({verdict.A.Name} and {verdict.B.Name} cannot lawfully wed.)");
               case ArrangeMarriageResult.NotCrossClan:
                  return ActionOutcome.Refused($"({verdict.B.Name} is already of your own clan.)");
               case ArrangeMarriageResult.RefusedStanding:
                  return ActionOutcome.Refused($"({npcHero.Name} will not give {verdict.B.Name} to your house on your present standing.)");
               case ArrangeMarriageResult.BlockedByGuard:
                  return ActionOutcome.Refused($"({verdict.A.Name} and {verdict.B.Name} cannot be wed now: not across a war, nor from captivity.)");
            }

            string msg = $"{npcHero.Name} consents: {verdict.A.Name} of your clan and {verdict.B.Name} of {verdict.B.Clan?.Name} are wed.";
            MBInformationManager.AddQuickInformation(new TextObject(msg), 0, npcHero.CharacterObject);
            ModLog.Info($"[ACTION] arrange_marriage: {verdict.A.Name} (player clan) wed {verdict.B.Name} ({verdict.B.Clan?.Name}), consent by {npcHero.Name}.");

            return new ActionOutcome(msg, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] arrange_marriage failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   appoint_governor (2026-08-05, the FIRST of the personal-command verbs brought to BOTH the 1:1 and
      ///   council pipelines): the player names the conversation NPC to govern one of their clan's fiefs. The
      ///   conversation NPC IS the prospective governor; the fief is named in the action's target_fief field. This
      ///   1:1 executor grounds the action from the conversation (the NPC must be of the player's own clan, since a
      ///   governor holds one of that clan's OWN fiefs), then calls the SAME shared core the council resolution
      ///   runs (<see cref="CourtActionResolver.TryAppointGovernor" />), which re-validates every remaining
      ///   invariant (player still leads the clan, the fief is still a player-clan town or castle, the NPC's own
      ///   standing clears the acceptance floor) before applying the post. Only TAUGHT when
      ///   <see cref="EncounterContext.NpcCanBeAppointedGovernor" /> holds (EncounterContextBuilder's own
      ///   GovernorAppointmentPolicy.CanOfferAppointment gate), but every guard is re-checked here and in the core:
      ///   the prompt is advice, the bridge is law. A declined or ungroundable appointment is surfaced as a refusal
      ///   (never a silent null) so the transcript OOC note fires and the model stops narrating a post it never got.
      /// </summary>
      private static ActionOutcome ExecuteAppointGovernor(GameAction action, Hero npcHero, ActionContext context)
      {
         try
         {
            NpcProfile profile = context?.Profile;

            if (npcHero is not {IsAlive: true} || npcHero.IsPrisoner) return null;
            if (profile == null || Hero.MainHero == null) return null;

            // 1:1 grounding, mirroring the council roster's own player-clan seating for this kind: a governor
            // holds one of the CLAN's fiefs, so the conversation NPC must belong to the player's own clan (a
            // companion or clan lord). The shared core re-checks the player's leadership, the fief, and the
            // standing floor; this clan-membership check is the 1:1 path's own grounding of WHO is being seated.
            if (npcHero.Clan == null || npcHero.Clan != Clan.PlayerClan)
               return ActionOutcome.Refused($"({npcHero.Name} is not of your clan, and cannot govern one of its fiefs.)");

            action.Parameters.TryGetValue("target_fief", out string fiefName);

            // Was a silent no-op class this project eliminates: a governorship agreed with no fief named.
            if (string.IsNullOrWhiteSpace(fiefName))
               return ActionOutcome.Refused(SilentRefusalMessages.AppointGovernorMissingFief());

            AppointGovernorVerdict verdict = CourtActionResolver.TryAppointGovernor(npcHero, profile.ReputationWithPlayer, fiefName);

            switch (verdict.Result)
            {
               case AppointGovernorResult.AppointeeUnavailable:
                  return ActionOutcome.Refused($"({npcHero.Name} cannot take a governorship right now.)");
               case AppointGovernorResult.PlayerNotClanLeader:
                  return ActionOutcome.Refused("(You no longer lead your clan, and cannot hand out its governorships.)");
               case AppointGovernorResult.NoSuchFief:
                  return ActionOutcome.Refused($"(Your clan holds no town or castle called '{fiefName}' with a post to fill.)");
               case AppointGovernorResult.PostOccupied:
                  return ActionOutcome.Refused($"({verdict.Fief.Name} already has a governor seated; relieve them first before naming another.)");
               case AppointGovernorResult.AppointeeAlreadyGoverns:
                  return ActionOutcome.Refused($"({npcHero.Name} already governs {npcHero.GovernorOf?.Name}, and cannot hold a second seat.)");
               case AppointGovernorResult.AppointeeAway:
                  return ActionOutcome.Refused($"({npcHero.Name} is away on an errand and cannot take up a governorship now.)");
               case AppointGovernorResult.Declined:
                  return ActionOutcome.Refused($"({npcHero.Name} does not yet trust you enough to take on governing {verdict.Fief.Name}.)");
            }

            string msg = $"{npcHero.Name} now governs {verdict.Fief.Name} in your name.";
            MBInformationManager.AddQuickInformation(new TextObject(msg), 0, npcHero.CharacterObject);
            ModLog.Info($"[ACTION] appoint_governor: {npcHero.Name} appointed governor of {verdict.Fief.Name}.");

            return new ActionOutcome(msg, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] appoint_governor failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   assign_party_role (2026-08-05, party-command batch, council + 1:1): the player names the conversation
      ///   companion to a role in their own party (Scout, Engineer, Quartermaster, Surgeon). The role is named in
      ///   the action's target_role field. Calls the SAME shared core the council resolution runs
      ///   (<see cref="CourtActionResolver.TryAssignPartyRole" />), which re-validates every invariant (alive, still
      ///   in the player's own party, the role resolves) before setting it. Only TAUGHT when
      ///   <see cref="EncounterContext.NpcCanBeAssignedPartyRole" /> holds, but every guard is re-checked here and in
      ///   the core: the prompt is advice, the bridge is law. A blank or ungroundable role is surfaced as a refusal
      ///   (never a silent null) so the transcript OOC note fires.
      /// </summary>
      private static ActionOutcome ExecuteAssignPartyRole(GameAction action, Hero npcHero)
      {
         try
         {
            if (npcHero is not {IsAlive: true}) return null;

            action.Parameters.TryGetValue("target_role", out string roleName);

            // Was a silent no-op class this project eliminates: a post agreed with no role named.
            if (string.IsNullOrWhiteSpace(roleName))
               return ActionOutcome.Refused(SilentRefusalMessages.AssignPartyRoleMissingRole());

            AssignPartyRoleVerdict verdict = CourtActionResolver.TryAssignPartyRole(npcHero, roleName);

            switch (verdict.Result)
            {
               case AssignPartyRoleResult.ActorUnavailable:
                  return null;
               case AssignPartyRoleResult.NotInPlayerParty:
                  return ActionOutcome.Refused($"({npcHero.Name} does not ride in your party, and can hold no post in it.)");
               case AssignPartyRoleResult.NoValidRole:
                  return ActionOutcome.Refused(SilentRefusalMessages.AssignPartyRoleMissingRole());
            }

            string msg = $"{npcHero.Name} now serves as your {verdict.Role}.";
            MBInformationManager.AddQuickInformation(new TextObject(msg), 0, npcHero.CharacterObject);
            ModLog.Info($"[ACTION] assign_party_role: {npcHero.Name} assigned party role {verdict.Role}.");

            return new ActionOutcome(msg, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] assign_party_role failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   rejoin_party (2026-08-05, party-command batch, council + 1:1): the conversation companion, currently away
      ///   from the player's party, agrees to come back to it. Actor only, no action field. Calls the SAME shared
      ///   core the council resolution runs (<see cref="CourtActionResolver.TryRejoinParty" />), which re-validates
      ///   every invariant (alive, not a prisoner, still a companion, genuinely outside the main party, not leading
      ///   their own company) and releases any governorship first before adding them back. Only TAUGHT when
      ///   <see cref="EncounterContext.NpcCanRejoinParty" /> holds, but every guard is re-checked here and in the
      ///   core: the prompt is advice, the bridge is law. A decline is surfaced as a refusal so the OOC note fires.
      /// </summary>
      private static ActionOutcome ExecuteRejoinParty(Hero npcHero)
      {
         try
         {
            if (npcHero == null) return null;

            RejoinPartyVerdict verdict = CourtActionResolver.TryRejoinParty(npcHero);

            switch (verdict.Result)
            {
               case RejoinPartyResult.ActorUnavailable:
                  return null;
               case RejoinPartyResult.ActorImprisoned:
                  return ActionOutcome.Refused($"({npcHero.Name} is held prisoner and cannot travel to you.)");
               case RejoinPartyResult.NotACompanion:
                  return ActionOutcome.Refused($"({npcHero.Name} is not one of your companions to recall.)");
               case RejoinPartyResult.AlreadyInParty:
                  return ActionOutcome.Refused($"({npcHero.Name} already rides in your party.)");
               case RejoinPartyResult.LeadsOwnCompany:
                  return ActionOutcome.Refused($"({npcHero.Name} leads their own company; disband it first.)");
            }

            string msg = verdict.WasGovernor
               ? $"{npcHero.Name} steps down from governing {verdict.GovernedFief} and rejoins your party."
               : $"{npcHero.Name} rejoins your party.";
            MBInformationManager.AddQuickInformation(new TextObject(msg), 0, npcHero.CharacterObject);
            ModLog.Info($"[ACTION] rejoin_party: {npcHero.Name} rejoined the player's party.");

            return new ActionOutcome(msg, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] rejoin_party failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   dispatch_mission (2026-08-05, party-command batch, council + 1:1): the player sends the conversation
      ///   companion on an errand (gather news, spy, steal, barter, envoy). The mission type is named in the action's
      ///   target_mission field; the destination is never named (the mission system picks it). Calls the SAME shared
      ///   core the council resolution runs (<see cref="CourtActionResolver.TryDispatchMission" />), which
      ///   re-validates every invariant (alive, still a companion, still in the party, a mission system exists, not
      ///   already away, the errand resolves) and reuses <see cref="CompanionMissionBehavior.Dispatch" /> end to end.
      ///   Only TAUGHT when <see cref="EncounterContext.NpcCanBeDispatchedOnMission" /> holds, but every guard is
      ///   re-checked here and in the core: the prompt is advice, the bridge is law. A blank, ungroundable, or
      ///   refused errand is surfaced as a refusal so the OOC note fires.
      /// </summary>
      private static ActionOutcome ExecuteDispatchMission(GameAction action, Hero npcHero)
      {
         try
         {
            if (npcHero is not {IsAlive: true}) return null;

            action.Parameters.TryGetValue("target_mission", out string missionName);

            // Was a silent no-op class this project eliminates: an errand agreed with none named.
            if (string.IsNullOrWhiteSpace(missionName))
               return ActionOutcome.Refused(SilentRefusalMessages.DispatchMissionMissingErrand());

            // Optional topic the player named ("word of Sturgia" -> Sturgia); the one capability gather_news had that
            // dispatch_mission lacked before the two were merged (2026-08-05). Null when the player left it open.
            action.Parameters.TryGetValue("about", out string about);

            // A patrol names its place (jaserms1, 16/10/2026); the number of men is chosen on the troop screen.
            action.Parameters.TryGetValue("target_settlement", out string place);

            DispatchMissionVerdict verdict = CourtActionResolver.TryDispatchMission(npcHero, missionName, about, place);

            switch (verdict.Result)
            {
               case DispatchMissionResult.ActorUnavailable:
                  return null;
               case DispatchMissionResult.NoMissionSystem:
                  return null;
               case DispatchMissionResult.NotACompanion:
                  return ActionOutcome.Refused($"({npcHero.Name} is not one of your companions to send.)");
               case DispatchMissionResult.NotInPlayerParty:
                  return ActionOutcome.Refused($"({npcHero.Name} does not ride in your party, and cannot be sent from it.)");
               case DispatchMissionResult.AlreadyAway:
                  return ActionOutcome.Refused($"({npcHero.Name} is already away on an errand.)");
               case DispatchMissionResult.NoValidMission:
                  return ActionOutcome.Refused(SilentRefusalMessages.DispatchMissionMissingErrand());
               case DispatchMissionResult.DispatchFailed:
                  return ActionOutcome.Refused($"({verdict.Message})");
               case DispatchMissionResult.NoSuchPlace:
                  return ActionOutcome.Refused($"(No place by that name to patrol: name a real town, castle or village for {npcHero.Name}.)");
               case DispatchMissionResult.PatrolOrdered:
                  ModLog.Info($"[ACTION] dispatch_mission: {npcHero.Name} takes a patrol order; the troop screen follows on the map.");

                  return new ActionOutcome(verdict.Message, true);
            }

            ModLog.Info($"[ACTION] dispatch_mission: {npcHero.Name} dispatched on a {verdict.Type} errand.");

            return new ActionOutcome(verdict.Message, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] dispatch_mission failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   grant_fief (2026-08-05, suzerain-command batch, council + 1:1): the player, AS SOVEREIGN, grants one of
      ///   their crown fiefs to the conversation NPC's house. The conversation NPC IS the recipient vassal; the fief
      ///   is named in the action's target_fief field. This 1:1 executor grounds the action from the conversation
      ///   (the recipient must be of a SEPARATE house, since a crown fief is granted across houses, never to the
      ///   player's own clan), then calls the SAME shared core the council resolution runs
      ///   (<see cref="CourtActionResolver.TryGrantFief" />), which re-validates every remaining invariant (recipient
      ///   free, player still rules a kingdom, the fief is still one of ours) and charges the influence cost before
      ///   the fief passes. Only TAUGHT when <see cref="EncounterContext.NpcCanBeGrantedFief" /> holds, but every
      ///   guard is re-checked here and in the core: the prompt is advice, the bridge is law. A blank or ungroundable
      ///   grant is surfaced as a refusal (never a silent null) so the transcript OOC note fires.
      /// </summary>
      /// <summary>
      ///   mediate_peace (third-party war mediation, 2026-08-06): the player, acting as a MEDIATOR in a 1:1
      ///   conversation with a FACTION LEADER (a king/ruler), brokers peace between that leader's faction and a
      ///   NAMED enemy faction (the action's target_faction field). The player's own faction need not be involved.
      ///   This is the third-party diplomacy verb that make_peace never covered: make_peace exists only as a
      ///   council Parley motion for the player's OWN faction, whereas this ends a war between OTHER realms. In v1
      ///   only the talked-to leader consents; the enemy's assent is abstracted (the mediator brokered acceptable
      ///   terms), so success applies peace mutually via the SAME native <see cref="MakePeaceAction.Apply" /> call
      ///   CouncilLift.SettleMakePeace already uses. Only TAUGHT when
      ///   <see cref="EncounterContext.PlayerCanMediatePeace" /> holds, but the bridge is law: EVERY guard is
      ///   re-checked here against live state (the NPC still leads their faction, that faction is still at war with
      ///   the resolved target, the target resolves and is not the leader's own faction, and the player still meets
      ///   the mediation gate). No gold cost: mediation is prestige and trust, not a purchase. Every failing gate
      ///   returns a refusal so the OOC "it did not happen" note fires and the model cannot claim a peace it never
      ///   brokered.
      /// </summary>
      private static ActionOutcome ExecuteMediatePeace(GameAction action, Hero npcHero, ActionContext context)
      {
         try
         {
            if (npcHero is not {IsAlive: true} || npcHero.IsPrisoner) return null;
            if (Hero.MainHero == null) return null;

            // The verb is scoped to kings/rulers: only a faction leader can commit their whole realm to peace.
            if (!npcHero.IsFactionLeader || npcHero.MapFaction == null)
               return ActionOutcome.Refused($"({npcHero.Name} speaks for no realm, and cannot be brought to a peace they do not command.)");

            IFaction leaderFaction = npcHero.MapFaction;

            action.Parameters.TryGetValue("target_faction", out string targetName);

            // Was a silent no-op class this project eliminates: a peace brokered with no enemy realm named. Since
            // a leader may hold several wars, the model must name WHICH one, so a blank target is refused honestly.
            if (string.IsNullOrWhiteSpace(targetName))
               return ActionOutcome.Refused($"(Which realm is the peace to be brokered with? {npcHero.Name}'s war has no named enemy here.)");

            // Same faction pool CouncilLift.SettleMakePeace/SettleDeclareWar resolve from (live Kingdoms and
            // independent Clans), so the console, the council lift, and this 1:1 verb ground a typed faction name
            // identically and can never disagree over what counts as a realm.
            IFaction target = CalradiaRemembers.Chat.CouncilLift.ResolveFactionByName(targetName);

            if (target == null)
               return ActionOutcome.Refused($"(There is no realm called '{targetName}' for {npcHero.Name} to make peace with.)");

            // Guard against brokering a "peace" of a faction with itself (a hallucinated or coaxed self-target).
            if (target == leaderFaction)
               return ActionOutcome.Refused($"(That is {npcHero.Name}'s own realm; there is no war there to end.)");

            if (!FactionManager.IsAtWarAgainstFaction(leaderFaction, target))
               return ActionOutcome.Refused($"({leaderFaction.Name} is not at war with {target.Name}, so there is no peace to broker.)");

            // The mediation gate (the same pure policy the teaching side runs): the leader must lead a realm at war
            // with the target (proven above), the player must carry genuine noble weight, and the leader must hold
            // them in deep trust. Regard reads the mod's OWN personal ledger (ReputationWithPlayer), falling back
            // to the vanilla relation when no profile exists for this leader.
            int regard = context?.Profile?.ReputationWithPlayer ?? npcHero.GetRelation(Hero.MainHero);
            int playerTier = Mapping.StatureReader.Player.ClanTier;

            switch (PeaceMediationPolicy.Evaluate(true, true, regard, playerTier))
            {
               case PeaceMediationBlock.StatureTooLow:
                  return ActionOutcome.Refused($"({npcHero.Name} will not be lectured on peace by one of no standing. Raise your house before you presume to broker between realms.)");
               case PeaceMediationBlock.RegardTooLow:
                  return ActionOutcome.Refused($"({npcHero.Name} does not trust you nearly enough to heed your counsel on so grave a matter as ending a war. Earn their deep trust first.)");
            }

            // The diplomacy door (Bellum Civile bridge RFC, 14/11/2026): an authority may refuse or defer the peace.
            DiplomacyOutcome door = DiplomacyCommit.Factions(Logic.Diplomacy.DiplomacyChange.MakePeace, "mediate_peace", leaderFaction, target, npcHero,
               () => MakePeaceAction.Apply(leaderFaction, target));
            if (!door.Happened) return ActionOutcome.Refused(door.NotHappenedLine($"Peace between {leaderFaction.Name} and {target.Name}"));

            string msg = $"By your mediation, {leaderFaction.Name} and {target.Name} lay down their arms and make peace.";
            MBInformationManager.AddQuickInformation(new TextObject(msg), 0, npcHero.CharacterObject);
            ModLog.Info($"[ACTION] mediate_peace: brokered peace between {leaderFaction.Name} and {target.Name}.");

            return new ActionOutcome(msg, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] mediate_peace failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      private static ActionOutcome ExecuteGrantFief(GameAction action, Hero npcHero, ActionContext context)
      {
         try
         {
            NpcProfile profile = context?.Profile;

            if (npcHero is not {IsAlive: true} || npcHero.IsPrisoner) return null;
            if (profile == null || Hero.MainHero == null) return null;

            // 1:1 grounding: a crown fief passes to another HOUSE, so the conversation NPC must belong to a clan
            // other than the player's own. The shared core re-checks the player's rule, the fief, and everything
            // else; this cross-house check is the 1:1 path's own grounding of WHO receives the grant.
            if (npcHero.Clan == null || npcHero.Clan == Clan.PlayerClan)
               return ActionOutcome.Refused($"({npcHero.Name} is of your own house; a crown fief is granted across houses.)");

            action.Parameters.TryGetValue("target_fief", out string fiefName);

            // Was a silent no-op class this project eliminates: a fief granted with none named.
            if (string.IsNullOrWhiteSpace(fiefName))
               return ActionOutcome.Refused(SilentRefusalMessages.GrantFiefMissingFief());

            GrantFiefVerdict verdict = CourtActionResolver.TryGrantFief(npcHero, fiefName);

            switch (verdict.Result)
            {
               case GrantFiefResult.RecipientUnavailable:
                  return ActionOutcome.Refused($"({npcHero.Name} cannot receive a fief right now.)");
               case GrantFiefResult.RecipientNotSeparateHouse:
                  return ActionOutcome.Refused($"({npcHero.Name} is of your own house; a crown fief is granted across houses.)");
               case GrantFiefResult.RecipientNotVassal:
                  return ActionOutcome.Refused($"({npcHero.Name} is not a vassal of your kingdom; a crown fief passes only to a lord sworn to your realm.)");
               case GrantFiefResult.PlayerNotKingdomLeader:
                  // Aporo (16/11/2026): a queen promising HIM a castle wrote grant_fief, the sovereign's decree, and he
                  // read "You no longer rule a kingdom" though he never had one. Say what the verb is, and the real way.
                  return ActionOutcome.Refused("(A crown fief is granted by your own decree as a sovereign, and you rule no kingdom."
                                               + (ModSettings.AllowFiefAndMarriageQuestRewards ? $" A fief {npcHero.Name} gives you comes only as a task's promised reward, paid when the task is done.)" : ")"));
               case GrantFiefResult.NoSuchFief:
                  return ActionOutcome.Refused($"(Your clan holds no crown fief called '{fiefName}' to grant.)");
            }

            string msg = $"{verdict.Fief.Name} passes to {npcHero.Name}'s house by your decree (-{FiefTransferPolicy.GrantInfluenceCost} influence).";
            MBInformationManager.AddQuickInformation(new TextObject(msg), 0, npcHero.CharacterObject);
            ModLog.Info($"[ACTION] grant_fief: {verdict.Fief.Name} granted to {npcHero.Name}'s clan by royal decision.");

            return new ActionOutcome(msg, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] grant_fief failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   revoke_fief (2026-08-05, suzerain-command batch, council + 1:1): the player, AS SOVEREIGN, strips a crown
      ///   fief BACK from the conversation NPC's house. The conversation NPC IS the vassal losing the fief; the fief
      ///   is named in the action's target_fief field. This 1:1 executor grounds the action from the conversation
      ///   (the vassal must be of a SEPARATE house, since a clan's own fiefs already belong to it), then calls the
      ///   SAME shared core the council resolution runs (<see cref="CourtActionResolver.TryRevokeFief" />), which
      ///   re-validates every invariant (vassal alive, player still rules a kingdom, the fief is still theirs) and
      ///   lands the grave side effects (a MAXIMUM FiefRevoked grudge through the real gate, the relation hit) before
      ///   the fief returns to the crown. Only TAUGHT when <see cref="EncounterContext.NpcCanHaveFiefRevoked" />
      ///   holds, but every guard is re-checked here and in the core: the prompt is advice, the bridge is law. A blank
      ///   or ungroundable revocation is surfaced as a refusal so the OOC note fires.
      /// </summary>
      private static ActionOutcome ExecuteRevokeFief(GameAction action, Hero npcHero)
      {
         try
         {
            if (npcHero is not {IsAlive: true}) return null;

            // 1:1 grounding: only a fief of a SEPARATE house can be revoked (a clan's own fiefs already belong to
            // it). The shared core re-checks the player's rule and the fief itself.
            if (npcHero.Clan == null || npcHero.Clan == Clan.PlayerClan)
               return ActionOutcome.Refused($"({npcHero.Name} holds no separate house whose fief you could revoke.)");

            action.Parameters.TryGetValue("target_fief", out string fiefName);

            // Was a silent no-op class this project eliminates: a fief revoked with none named.
            if (string.IsNullOrWhiteSpace(fiefName))
               return ActionOutcome.Refused(SilentRefusalMessages.RevokeFiefMissingFief());

            var today = (int) CampaignTime.Now.ToDays;
            RevokeFiefVerdict verdict = CourtActionResolver.TryRevokeFief(npcHero, fiefName, today);

            switch (verdict.Result)
            {
               case RevokeFiefResult.VassalUnavailable:
                  return null;
               case RevokeFiefResult.VassalNotSeparateHouse:
                  return ActionOutcome.Refused($"({npcHero.Name} holds no separate house whose fief you could revoke.)");
               case RevokeFiefResult.VassalNotInKingdom:
                  return ActionOutcome.Refused($"({npcHero.Name} is not a vassal of your kingdom; you cannot revoke a fief that lies outside your realm.)");
               case RevokeFiefResult.PlayerNotKingdomLeader:
                  return ActionOutcome.Refused("(You rule no kingdom, and cannot revoke a fief in its name.)");
               case RevokeFiefResult.NoSuchFief:
                  return ActionOutcome.Refused($"({npcHero.Name} holds no fief called '{fiefName}' to revoke.)");
            }

            string msg = $"{verdict.Fief.Name} is stripped from {npcHero.Name} and returns to the crown.";
            MBInformationManager.AddQuickInformation(new TextObject(msg), 0, npcHero.CharacterObject);
            ModLog.Info($"[ACTION] revoke_fief: {npcHero.Name} was stripped of {verdict.Fief.Name} by royal decision; a grudge is planted.");

            return new ActionOutcome(msg, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] revoke_fief failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   expel_from_clan (2026-08-05, suzerain-command batch, council + 1:1): the player casts the conversation
      ///   companion out of their own clan entirely. The conversation NPC IS the companion being expelled; actor only,
      ///   no action field. Calls the SAME shared core the council resolution runs
      ///   (<see cref="CourtActionResolver.TryExpelFromClan" />), which re-validates every invariant (the player still
      ///   leads their clan, the target is a live companion of it, is not the player's own spouse, is not away on an
      ///   errand), casts them out via RemoveCompanionAction.ApplyByFire (they become a Fugitive), and lands the side
      ///   effects (an Expelled grudge through the real gate, the relation hit). Only TAUGHT when
      ///   <see cref="EncounterContext.NpcCanBeExpelledFromClan" /> holds, but every guard is re-checked here and in
      ///   the core: the prompt is advice, the bridge is law. A decline is surfaced as a refusal so the OOC note fires.
      /// </summary>
      private static ActionOutcome ExecuteExpelFromClan(Hero npcHero)
      {
         try
         {
            if (npcHero is not {IsAlive: true}) return null;

            var today = (int) CampaignTime.Now.ToDays;
            ExpelFromClanVerdict verdict = CourtActionResolver.TryExpelFromClan(npcHero, today);

            switch (verdict.Result)
            {
               case ExpelFromClanResult.ActorUnavailable:
                  return null;
               case ExpelFromClanResult.PlayerNotClanLeader:
                  return ActionOutcome.Refused("(You no longer lead your clan, and cannot cast anyone out of it.)");
               case ExpelFromClanResult.NotACompanion:
                  return ActionOutcome.Refused($"({npcHero.Name} is not one of your clan's companions to cast out.)");
               case ExpelFromClanResult.IsSpouse:
                  return ActionOutcome.Refused($"({npcHero.Name} is your own spouse; that bond can only be ended another way.)");
               case ExpelFromClanResult.AwayOnErrand:
                  return ActionOutcome.Refused($"({npcHero.Name} is away on an errand; recall them before casting them out.)");
            }

            string msg = verdict.WasGovernor
               ? $"{npcHero.Name} is stripped of the governorship of {verdict.GovernedFief} and cast out of your clan."
               : $"{npcHero.Name} is cast out of your clan.";
            MBInformationManager.AddQuickInformation(new TextObject(msg), 0, npcHero.CharacterObject);
            ModLog.Info($"[ACTION] expel_from_clan: {npcHero.Name} was expelled from the player's clan; a grudge is planted.");

            return new ActionOutcome(msg, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] expel_from_clan failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   grant_stipend (2026-08-05, personal-command verb brought to BOTH the 1:1 and council pipelines, and one of
      ///   the two that touch PERSISTED save state): the player puts the conversation NPC, one of their own clan
      ///   companions, on a recurring daily wage. The daily amount is named in the action's target_amount field
      ///   (clamped to <see cref="StipendPolicy" />'s [50, 500] range). Calls the SAME shared core the council
      ///   resolution runs (<see cref="CourtActionResolver.TryGrantStipend" />), which re-validates every invariant
      ///   (alive, still a player companion, the escrow ledger exists, NO stipend already running, the full 30-day
      ///   tranche affordable) before funding it up front through the persisted <see cref="CouncilStipendBehavior" />.
      ///   The already-running guard is the load-bearing one here: it re-checks LIVE persisted state so a 1:1 grant can
      ///   never double-escrow a second tranche on top of one the player already funded (at a council or in an earlier
      ///   exchange). Only TAUGHT when <see cref="EncounterContext.NpcCanBeGrantedStipend" /> holds, but every guard is
      ///   re-checked here and in the core: the prompt is advice, the bridge is law. A blank amount or a refused grant
      ///   is surfaced as a refusal (never a silent null) so the transcript OOC note fires.
      /// </summary>
      private static ActionOutcome ExecuteGrantStipend(GameAction action, Hero npcHero)
      {
         try
         {
            if (npcHero is not {IsAlive: true}) return null;

            if (!npcHero.IsPlayerCompanion)
               return ActionOutcome.Refused($"({npcHero.Name} is not a companion of your clan to keep on a stipend.)");

            // Was a silent no-op class this project eliminates: a wage agreed with no amount named.
            if (!TryGetInt(action.Parameters, "target_amount", out int amount))
               return ActionOutcome.Refused(SilentRefusalMessages.GrantStipendMissingAmount());

            GrantStipendVerdict verdict = CourtActionResolver.TryGrantStipend(npcHero, amount);

            switch (verdict.Result)
            {
               case GrantStipendResult.RecipientUnavailable:
                  return null;
               case GrantStipendResult.NoLedger:
                  return null;
               case GrantStipendResult.NotPlayerCompanion:
                  return ActionOutcome.Refused($"({npcHero.Name} is not a companion of your clan to keep on a stipend.)");
               case GrantStipendResult.AlreadyActive:
                  return ActionOutcome.Refused($"({npcHero.Name} already draws a stipend from your coffers.)");
               case GrantStipendResult.CouldNotFund:
                  return ActionOutcome.Refused($"({verdict.FailReason})");
            }

            string msg = $"{npcHero.Name} will draw {verdict.ClampedAmountPerDay} denars a day from your coffers for the next {StipendPolicy.TrancheDays} days.";
            MBInformationManager.AddQuickInformation(new TextObject(msg), 0, npcHero.CharacterObject);
            ModLog.Info($"[ACTION] grant_stipend: {npcHero.Name} granted {verdict.ClampedAmountPerDay}/day for {StipendPolicy.TrancheDays} days.");

            return new ActionOutcome(msg, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] grant_stipend failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   swear_oath (2026-08-05, personal-command verb brought to BOTH the 1:1 and council pipelines, and one of the
      ///   two that touch PERSISTED save state): the conversation NPC swears a VERIFIABLE oath to the player, tracked
      ///   from here by the persisted <see cref="CommitmentBehavior" />. The oath kind is named in the action's
      ///   oath_kind field (pay_gold / keep_peace / protect); pay_gold also carries target_amount (the denars owed),
      ///   keep_peace also carries target_faction (the house to keep peace with). Calls the SAME shared core the
      ///   council resolution runs (<see cref="CourtActionResolver.TrySwearOath" />), which re-validates every
      ///   invariant (alive, free, not the player, the ledger exists, NO oath already outstanding, the kind resolves
      ///   through the whitelist, and that kind's own payload validates) before recording it. The 1:1 oath carries no
      ///   witnesses (a private vow costs less to break than one sworn before a full council table). Only TAUGHT when
      ///   <see cref="EncounterContext.NpcCanSwearOath" /> holds, but every guard is re-checked here and in the core:
      ///   the prompt is advice, the bridge is law. A blank, unknown, or invalid oath is surfaced as a refusal (never
      ///   a silent null) so the transcript OOC note fires.
      /// </summary>
      private static ActionOutcome ExecuteSwearOath(GameAction action, Hero npcHero)
      {
         try
         {
            if (npcHero is not {IsAlive: true} || npcHero.IsPrisoner) return null;

            action.Parameters.TryGetValue("oath_kind", out string oathKind);

            // Was a silent no-op class this project eliminates: an oath agreed with no kind named.
            if (string.IsNullOrWhiteSpace(oathKind))
               return ActionOutcome.Refused(SilentRefusalMessages.SwearOathMissingKind());

            // The one grounding slot each kind fills differently, mirroring the council's own grounding
            // (OathGroundingCodec over proposal.TargetAmount / proposal.TargetFaction): pay_gold names a sum,
            // keep_peace names a faction, protect names nothing. The core re-validates which value the resolved
            // kind actually needs.
            action.Parameters.TryGetValue("target_amount", out string amountRaw);
            action.Parameters.TryGetValue("target_faction", out string factionRaw);
            string targetValue = !string.IsNullOrWhiteSpace(amountRaw) ? amountRaw : factionRaw ?? string.Empty;

            int gameDay = (int) CampaignTime.Now.ToDays;
            SwearOathVerdict verdict = CourtActionResolver.TrySwearOath(npcHero, oathKind, targetValue, gameDay, null);

            switch (verdict.Result)
            {
               case SwearOathResult.SwearerUnavailable:
                  return null;
               case SwearOathResult.NoLedger:
                  return null;
               case SwearOathResult.AlreadyCommitted:
                  return ActionOutcome.Refused($"({npcHero.Name} has already given their word on another matter, and will swear no second oath until it is settled.)");
               case SwearOathResult.UnknownKind:
                  return ActionOutcome.Refused(SilentRefusalMessages.SwearOathUnknownKind());
               case SwearOathResult.InvalidAmount:
                  return ActionOutcome.Refused($"({npcHero.Name} named no clear sum to swear to pay.)");
               // 3chmidt (Nexus, 06/11/2026): "cannot swear a peace" showed up in a haggle over an axe. A peace sworn by someone
               // who leads no faction is the interpreter's misreading, not a vow the player asked for: logged, never shown.
               case SwearOathResult.CannotSwearPeace:
                  ModLog.Info($"[ACTION] swear_oath keep_peace by {npcHero.Name} ignored: they lead no faction (not shown to the player).");

                  return null;
               case SwearOathResult.NoSuchFaction:
                  return ActionOutcome.Refused("(No faction by that name could be found to swear peace with.)");
               case SwearOathResult.OwnFaction:
                  return ActionOutcome.Refused($"({npcHero.Name} cannot swear to keep the peace with their own house.)");
            }

            string msg;

            switch (verdict.Kind)
            {
               case CommitmentKind.PayGold:
                  msg = $"{npcHero.Name} swears to pay you {verdict.PromisedAmount} denars within {CommitmentPolicy.DeadlineDays} days.";

                  break;

               case CommitmentKind.KeepPeace:
                  msg = $"{npcHero.Name} swears to keep the peace with {verdict.TargetFactionName} for {CommitmentPolicy.DeadlineDays} days.";

                  break;

               case CommitmentKind.Protect:
                  msg = $"{npcHero.Name} swears to stand at your side in battle within {CommitmentPolicy.DeadlineDays} days.";

                  break;

               // NAMED, not a default (13/09/2026). This switch used to end in `default:` meaning Protect, so any
               // kind the whitelist ever resolves that is not PayGold or KeepPeace would be announced to the
               // player as a battle oath - a sentence about a war attached to a vow about something else. The
               // catch-all is now a refusal that says the truth instead of guessing.
               default:
                  ModLog.Error($"[ACTION] swear_oath: {verdict.Kind} has no player-facing wording; refused rather "
                             + "than announced as a battle oath.");

                  return ActionOutcome.Refused($"({npcHero.Name} gave their word, but on terms the chronicle "
                                             + "cannot set down.)");
            }

            MBInformationManager.AddQuickInformation(new TextObject(msg), 0, npcHero.CharacterObject);
            ModLog.Info($"[ACTION] swear_oath: {npcHero.Name} swore a {verdict.Kind} oath.");

            return new ActionOutcome(msg, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] swear_oath failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      private static ActionOutcome ExecuteHarmPrisoner(GameAction action, Hero npcHero)
      {
         var player = Hero.MainHero;

         if (player == null) return null;

         // The prompt is advice, the bridge is law: harm_prisoner deducts REAL health, so re-validate the
         // captivity, the live scene, and the Hardcore floor here (mirrors PlayerExecutionPolicy for
         // execute_player). A stray or premature emission outside a real captive scene must wound no one, and
         // the action is advertised Hardcore-only.
         bool playerIsCaptiveOfCaptor = player.IsPrisoner
            && player.PartyBelongedToAsPrisoner?.LeaderHero == npcHero;
         bool hardcore = ModSettings.AdultContentLevel == AdultContentLevel.Hardcore;

         if (!HarmPrisonerPolicy.CanHarm(new HarmPrisonerPolicy.HarmFacts {PlayerIsCaptiveOfCaptor = playerIsCaptiveOfCaptor, CaptiveSceneActive = _captiveSceneActive, Hardcore = hardcore}))
         {
            // Alfkona (Nexus, 07/11/2026): "(the game does not allow real harm here, so none was done)" in every kind of
            // conversation, even before their captor. The interpreter is taught this verb in every scene (its prompt is one
            // cached prefix) and reads it out of any rough prose; where real harm cannot happen, the words stay the
            // story's and nothing is announced. Logged with the three facts, so a log still says why.
            ModLog.Info($"[ACTION] harm_prisoner refused by HarmPrisonerPolicy (captive={playerIsCaptiveOfCaptor}, " +
                        $"sceneActive={_captiveSceneActive}, hardcore={hardcore}); nothing shown to the player.");

            return null;
         }

         action.Parameters.TryGetValue("severity", out string severity);
         int damage;
         switch ((severity ?? string.Empty).Trim().ToLowerInvariant())
         {
            case "severe":
            case "grievous":
               damage = 30;

               break;
            case "moderate":
            case "heavy":
               damage = 15;

               break;
            default: // light, mild, or unspecified
               damage = 6;

               break;
         }

         // Weak models sometimes emit harm_prisoner on every turn regardless of the fiction.
         // Past the per-scene cap, or once the player is at the floor, the wound is fiction-only.
         if (_sceneHarmCount >= MaxHarmsPerScene || player.HitPoints <= MinSceneHitPoints)
         {
            _captiveHarmApplied = true;
            ModLog.Info($"[ACTION] harm_prisoner suppressed (cap/floor reached; severity '{severity}', HP {player.HitPoints}).");

            return null;
         }

         int before = player.HitPoints;
         player.HitPoints = Math.Max(MinSceneHitPoints, before - damage); // a scene wounds, never kills
         int lost = before - player.HitPoints;

         if (lost <= 0) return null;

         _sceneHarmCount++;
         _captiveHarmApplied = true; // the guaranteed end-of-scene toll defers to this
         ModLog.Info($"[ACTION] harm_prisoner: player loses {lost} HP (severity '{severity}', now {player.HitPoints}, scene wound #{_sceneHarmCount}).");

         return new ActionOutcome($"You are hurt — you lose {lost} health.", false);
      }

      /// <summary>
      ///   impregnation_risk: the narration flagged a vaginal act carried to completion. The fertile
      ///   female of the pairing has a small chance to conceive — the PLAYER when she is the woman (the
      ///   NPC is the father), or the NPC when she is (the player is the father). A same-sex pairing or
      ///   an infertile female conceives nothing. The mother/father split is decided by the pure
      ///   <see cref="ConceptionResolver" />; the child follows the mother's clan. Silent to the player —
      ///   the pregnancy is discovered through normal game means — so this returns no chat outcome.
      /// </summary>
      private static ActionOutcome ExecuteImpregnationRisk(Hero npcHero)
      {
         var player = Hero.MainHero;

         if (npcHero == null || player == null) return null;

         // M-G1: impregnation_risk is only ever TAUGHT at Explicit or above, so re-validate that bar here (the
         // prompt is advice, the bridge is law). Without this a hallucinated emission at Off/Mature, the one
         // consequential action left un-revalidated, could conceive a child from a scene the content level never
         // permitted. The fertility split itself is still owned by ConceptionResolver below.
         if (!ConceptionActionGate.PermitsConception(ModSettings.AdultContentLevel))
         {
            ModLog.Info("[ACTION] impregnation_risk ignored: content level does not permit conception (M-G1 guard).");

            return null;
         }

         ConceptionRole role = PregnancyService.RoleOf(player, npcHero);

         switch (role)
         {
            case ConceptionRole.PlayerIsMother:
               PregnancyService.TryConceive(player, npcHero); // mother = player, father = NPC

               break;
            case ConceptionRole.NpcIsMother:
               PregnancyService.TryConceive(npcHero, player); // mother = NPC, father = player

               break;
         }

         return null;
      }

      /// <summary>
      ///   join_as_mercenary: a lord has offered — and the player has agreed — to enroll
      ///   the player's clan as paid mercenary swords under his kingdom's banner. The
      ///   prompt only teaches this action when the game confirms the player is free of
      ///   any existing kingdom obligation (ResolveMercenaryOfferKingdom), but every guard
      ///   is re-checked here — the bridge is the law.
      /// </summary>
      private static ActionOutcome ExecuteJoinAsMercenary(Hero npcHero)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null) return null;

            Kingdom kingdom = npcHero.Clan?.Kingdom;

            if (kingdom == null)
               return ActionOutcome.Refused($"({npcHero.Name} does not belong to a kingdom.)");

            var playerClan = Clan.PlayerClan;

            if (playerClan == null) return null;

            if (playerClan.Kingdom != null)
               return ActionOutcome.Refused(
                  $"(Your clan is already sworn to {playerClan.Kingdom.Name}. " + "Settle that obligation first.)");

            // v1.34.2 governance hardening (finding 4): a realm the player is at war with does not take them on as
            // its hired swords. Re-checked here through the same pure gate the teaching consults, the bridge being
            // the law; a refused enrolment is surfaced (never a silent no-op).
            bool playerAtWarWithKingdom = Hero.MainHero?.MapFaction != null
               && FactionManager.IsAtWarAgainstFaction(Hero.MainHero.MapFaction, kingdom);
            if (!MercenaryEnlistmentPolicy.CanJoin(playerClan.Kingdom == null, playerAtWarWithKingdom))
               return ActionOutcome.Refused(
                  $"(You are at war with {kingdom.Name}; they will not take your clan into their service while you fight them.)");

            // The 4th argument is the clan's MercenaryAwardMultiplier: the influence-to-denars CONVERSION
            // RATE of the contract (persisted on Clan.MercenaryAwardMultiplier), NOT a wage scalar. The engine
            // default is 50 (confirmed by reflection against the shipped ApplyByJoinFactionAsMercenary on game
            // build 1.3.15/1.4.7); CR hard-coded 1, so every CR-signed contract paid one denar per influence
            // point, one fiftieth of the intended rate. Player report (Nexus 2026-07-24): "Mercenary Contract
            // 26" where thousands were due. It surfaced on a RE-sign after Leave Kingdom because the first
            // contract still carried a leftover multiplier of 50 on the clan (set by whatever signed it first),
            // which CR's join then overwrote with 1 on the second. We pass the engine's own documented
            // default rather than a computed renown factor: matching vanilla's baseline exactly, with no
            // guessed formula, is the safe fix.
            // tashmetu (Nexus, 16/10/2026): the game's own hiring dialogue asks the model for the factor
            // (MinorFactionsModel.GetMercenaryAwardFactorToJoinKingdom, LordConversationsCampaignBehavior), so the
            // rate now follows it, with 50 kept only when no model answers.
            int mercenaryAwardMultiplier = Campaign.Current?.Models?.MinorFactionsModel?.GetMercenaryAwardFactorToJoinKingdom(playerClan, kingdom) ?? 50;
            if (mercenaryAwardMultiplier <= 0) mercenaryAwardMultiplier = 50;

            // shouldStayInKingdomUntil = default, as every vanilla hiring path passes it (no fixed term). tashmetu
            // (Nexus, 16/10/2026): CR passed CampaignTime.Now, and Mercenary Management reads that field as the
            // end of its contract, so a contract signed in the chat ran out on its first daily check, the clan
            // left, and every lord of the realm judged it an abandonment.
            DiplomacyOutcome door = DiplomacyCommit.Realm(Logic.Diplomacy.DiplomacyChange.ClanJoinsAsMercenary, "join_as_mercenary", playerClan, kingdom, npcHero,
               () => ChangeKingdomAction.ApplyByJoinFactionAsMercenary(playerClan, kingdom, default(CampaignTime), mercenaryAwardMultiplier));
            if (!door.Happened) return ActionOutcome.Refused(door.NotHappenedLine($"Your service under {kingdom.Name}'s banner"));

            ModLog.Info($"[ACTION] join_as_mercenary: player clan joined {kingdom.Name} as mercenary (award multiplier {mercenaryAwardMultiplier}, no fixed term).");

            return new ActionOutcome(
               $"Your clan takes service under {kingdom.Name}'s banner as hired swords.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] join_as_mercenary failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   end_mercenary: the MIRROR of join_as_mercenary. The King who hired the player's clan, one of his
      ///   lords speaking for him, or the player themselves, has agreed in conversation to end the mercenary
      ///   contract. Player report (2026-08-04): "The King agreed to free me from my mercenary contract.
      ///   Nothing happens." The prompt only teaches this action when the game confirms the player's clan is
      ///   truly under mercenary service right now (EncounterContext.PlayerIsMercenary), but every guard is
      ///   re-checked here, the bridge is the law. Deliberately NOT gated to the specific NPC being the exact
      ///   employer: the fiction decides who may credibly raise it, the bridge only enacts the dissolution
      ///   once the LLM judges the release is agreed.
      /// </summary>
      private static ActionOutcome ExecuteEndMercenary(Hero npcHero)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null) return null;

            var playerClan = Clan.PlayerClan;

            if (playerClan == null) return null;

            if (!MercenaryReleasePolicy.CanEnd(playerClan.IsUnderMercenaryService))
               return ActionOutcome.Refused("(You are not serving anyone as a mercenary.)");

            // Read the kingdom name BEFORE the leave call: ApplyByLeaveKingdomAsMercenary clears Clan.Kingdom.
            string kingdomName = playerClan.Kingdom?.Name?.ToString() ?? "your former employer";

            // The dedicated mercenary-leave overload (ChangeKingdomActionDetail.LeaveAsMercenary), confirmed by
            // decompile against the shipped engine: it clears Clan.Kingdom AND runs
            // EndMercenaryServiceAction.EndByLeavingKingdom internally, unlike the generic ApplyByLeaveKingdom
            // (built for a sworn vassal's departure, incl. settlement hand-back logic a mercenary never needs).
            DiplomacyOutcome door = DiplomacyCommit.Realm(Logic.Diplomacy.DiplomacyChange.ClanLeavesMercenaryService, "leave_mercenary", playerClan, playerClan.Kingdom, npcHero,
               () => ChangeKingdomAction.ApplyByLeaveKingdomAsMercenary(playerClan));
            if (!door.Happened) return ActionOutcome.Refused(door.NotHappenedLine($"The end of your contract with {kingdomName}"));

            ModLog.Info($"[ACTION] end_mercenary: player clan's mercenary contract with {kingdomName} dissolved.");

            return new ActionOutcome($"Your mercenary contract with {kingdomName} is dissolved.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] end_mercenary failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   join_as_vassal: the RULER of a kingdom has agreed, in conversation, to swear the player's clan
      ///   into full vassalage under his banner (a permanent oath of fealty, distinct from a mercenary
      ///   contract). Player report (2026-08-05): "asked the legitimate ruler to become her vassal, the LLM
      ///   narrated acceptance and threw 6000 denars, but the game only ever registered the player as a
      ///   mercenary", the same missing-executor class as end_mercenary. The prompt only teaches this action
      ///   when the game confirms the speaking NPC truly rules a kingdom and the player's clan is not already
      ///   a full vassal (EncounterContext.VassalOfferKingdom), but every guard is re-checked here, the bridge
      ///   is the law. Only the ruler's word suffices here (a kingdom-decision/council-vote path is out of
      ///   scope). A clan currently serving as a mercenary transitions cleanly: ApplyByJoinToKingdom itself
      ///   ends any existing mercenary contract (EndMercenaryServiceAction.EndByDefault, decompile-confirmed)
      ///   before binding the new kingdom, so no separate leave-mercenary step is needed here.
      /// </summary>
      private static ActionOutcome ExecuteJoinAsVassal(Hero npcHero)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null) return null;

            Kingdom kingdom = npcHero.Clan?.Kingdom;

            if (kingdom == null)
               return ActionOutcome.Refused($"({npcHero.Name} does not belong to a kingdom.)");

            var playerClan = Clan.PlayerClan;

            if (playerClan == null) return null;

            bool npcIsKingdomRuler = kingdom.Leader == npcHero;

            // "Already a full vassal" excludes mercenary service on purpose: a mercenary contract is paid
            // service, not an oath, so a mercenary clan is still free to swear real fealty (see the
            // ApplyByJoinToKingdom note above, which handles that transition on its own).
            bool playerAlreadyVassal = playerClan.Kingdom != null && !playerClan.IsUnderMercenaryService;

            // v1.34.2 governance hardening (finding 4): the player cannot swear fealty to a kingdom they are
            // actively fighting. Re-checked here through the same pure gate the teaching consults, the bridge
            // being the law.
            bool playerAtWarWithKingdom = Hero.MainHero?.MapFaction != null
               && FactionManager.IsAtWarAgainstFaction(Hero.MainHero.MapFaction, kingdom);

            // SgtAlexGreen (Nexus, 05/11/2026): "I have a kingdom but when I try and persuade a lord into joining it, it fails
            // saying he isn't the sovereign". The lord was to bring his house into the PLAYER's realm; this verb is the
            // opposite road, and no chat verb takes that one. Said as it is, before the sovereign refusal that read as nonsense.
            if (playerClan.Kingdom != null && playerClan.Kingdom.Leader == Hero.MainHero && playerClan.Kingdom != kingdom)
               return ActionOutcome.Refused(
                  $"(You rule {playerClan.Kingdom.Name} and swear fealty to no one. {npcHero.Name} bringing {(npcHero.IsFemale ? "her" : "his")} house into your realm is not settled in this talk: the game's own conversation, asking {(npcHero.IsFemale ? "her" : "him")} to join your kingdom, is the way.)");

            if (!VassalageOfferPolicy.CanJoin(new VassalageOfferPolicy.VassalageFacts {NpcIsKingdomRuler = npcIsKingdomRuler, PlayerAlreadyVassal = playerAlreadyVassal, PlayerAtWarWithKingdom = playerAtWarWithKingdom}))
            {
               if (!npcIsKingdomRuler)
                  return ActionOutcome.Refused(
                     $"({npcHero.Name} does not rule {kingdom.Name}; only the ruler's word can swear your clan to their banner.)");

               if (playerAtWarWithKingdom)
                  return ActionOutcome.Refused(
                     $"(You are at war with {kingdom.Name}; you cannot swear fealty to a realm you are fighting.)");

               return ActionOutcome.Refused(
                  $"(Your clan is already sworn to {playerClan.Kingdom.Name}. Settle that obligation first.)");
            }

            DiplomacyOutcome door = DiplomacyCommit.Realm(Logic.Diplomacy.DiplomacyChange.ClanJoinsKingdom, "join_as_vassal", playerClan, kingdom, npcHero,
               () => ChangeKingdomAction.ApplyByJoinToKingdom(playerClan, kingdom));
            if (!door.Happened) return ActionOutcome.Refused(door.NotHappenedLine($"Your oath to {kingdom.Name}"));

            ModLog.Info($"[ACTION] join_as_vassal: player clan sworn as vassal of {kingdom.Name}.");

            // Aporo (15/11/2026) / Gabriel (16/11/2026): the game's oath rewards, at the player's yes.
            string gift = VassalRewards.Grant(kingdom);

            return new ActionOutcome($"Your clan is sworn as a vassal of {kingdom.Name}." + (gift == null ? "" : " " + gift), true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] join_as_vassal failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   join_clan: a lord has agreed, in conversation, to leave their own house and join the
      ///   player's clan — the most powerful recruitment. Only taught when the game confirms every
      ///   gate (deep trust, the player's house outranking theirs, the lord being vulnerable — see
      ///   <see cref="LordRecruitmentFactsBuilder" /> + <see cref="LordRecruitment" />), but the whole
      ///   gate is re-checked here: the prompt is advice, the bridge is law. The lord's old clan
      ///   loses a member and its faction cools toward the player — vanilla consequences left to play out.
      ///   Stance mechanical behaviour-gating (increment 3, Respect, ratified 2026-07-04): additionally
      ///   re-validated against <see cref="StanceGatePolicy.CommandsRespectToJoinClan" /> (high relation
      ///   bypass so the Respect floor binds above the recruitment's own relation gate), the same discipline
      ///   as the Fear-1/Trust-2 gates. Skipped when there is no stance profile (join_clan never depended on
      ///   one before this increment).
      /// </summary>
      private static ActionOutcome ExecuteJoinClan(Hero npcHero, NpcProfile profile)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null) return null;

            var playerClan = Clan.PlayerClan;

            if (playerClan == null) return null;

            LordRecruitmentFacts facts = LordRecruitmentFactsBuilder.Build(npcHero);

            if (facts == null || !LordRecruitment.IsEligible(facts))
               return ActionOutcome.Refused($"({npcHero.Name} will not leave their house to join yours — not yet.)");

            if (!LordRecruitmentFactsBuilder.RespectsEnoughToJoin(profile))
               return ActionOutcome.Refused($"({npcHero.Name} does not respect you enough to throw in their lot with you. Earn their esteem first.)");

            Clan oldClan = npcHero.Clan;

            // A clan LEADER cannot dissolve their own house into the player's by a chat line: reassigning
            // npcHero.Clan would leave Clan.Leader / Kingdom.Leader dangling on the abandoned clan (save
            // corruption). Independent leaders (Kingdom == null), same-kingdom leaders and different-kingdom
            // leaders alike are ALL refused; only a non-leader member may defect. Re-checked against the same
            // IsClanLeader fact the teaching gate reads (LordRecruitmentFactsBuilder): the prompt is advice,
            // the bridge is law. The narrow old guard only caught a different-kingdom leader and let the other
            // two orphan a clan.
            if (oldClan?.Leader == npcHero)
               return ActionOutcome.Refused(
                  $"({npcHero.Name} leads the house of {oldClan?.Name?.ToString() ?? "their birth"}; a lord cannot dissolve their own clan into yours with a word.)");

            npcHero.Clan = playerClan;

            ModLog.Info($"[ACTION] join_clan: {npcHero.Name} left {oldClan?.Name?.ToString() ?? "their house"} to join the player's clan.");

            return new ActionOutcome(
               $"{npcHero.Name} forsakes the house of {oldClan?.Name?.ToString() ?? "their birth"} and swears to your clan.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] join_clan failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   join_party: a recruitable wanderer agrees in conversation to take service
      ///   with the player. The action is only taught when the game says the hire can
      ///   succeed (see EncounterContextBuilder.ResolveCompanionAskingPrice), but every
      ///   guard is re-checked here — the prompt is advice, the bridge is law. The agreed
      ///   price is clamped to a band around the vanilla hiring price (floor 75 %, cap
      ///   125 %): eloquence negotiates, it never breaks the hiring economy. Executes the
      ///   same native actions as a vanilla tavern hire, so clan limits, party rosters
      ///   and gold flows behave identically.
      /// </summary>
      private static ActionOutcome ExecuteJoinParty(GameAction action, Hero npcHero)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null) return null;

            if (!npcHero.IsAlive || npcHero.IsPrisoner || npcHero.Occupation != Occupation.Wanderer || npcHero.CompanionOf != null || npcHero.PartyBelongedTo != null)
               return ActionOutcome.Refused($"({npcHero.Name} is not free to take service.)");

            var clan = Clan.PlayerClan;

            if (clan == null) return null;
            if (clan.Companions.Count >= clan.CompanionLimit)
               return ActionOutcome.Refused(
                  $"(Your clan cannot support another companion — limit {clan.CompanionLimit}.)");

            int asking = AskingHirePrice(npcHero);

            // The deal is the deal (Gabriel's ruling 2026-09-08). The price agreed in conversation is charged
            // exactly, never renegotiated afterwards into a band: that band is what charged a player 446 while
            // his companion was saying 595 aloud. An unaffordable bargain is not quietly made cheaper either, it
            // simply does not happen. See CalradiaRemembers.Logic.HireAgreementPolicy.
            TryGetInt(action.Parameters, "price", out int proposed);
            int price = HireAgreementPolicy.AgreedPrice(proposed, asking);

            if (!HireAgreementPolicy.CanAfford(Hero.MainHero.Gold, price))
               return ActionOutcome.Refused($"(You cannot afford the agreed {price} denars.)");

            return DoJoin(npcHero, price,
               $"hired for {price} denars (agreed {proposed}, vanilla asking {asking})");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] join_party failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   marry: the NPC has proposed marriage to the player in conversation and the player
      ///   has accepted. Guards are re-checked here (the bridge is the law) before calling
      ///   <see cref="MarriageAction.Apply" />. The gating threshold is
      ///   <see cref="MarriageEligibility.MarriageEligibilityThreshold" />, met EITHER by the mod's
      ///   internal reputation score OR by the live vanilla clan relation
      ///   (<see cref="Hero.GetRelation" />) — kept in exact sync with
      ///   <see cref="EncounterContextBuilder.ResolveLoveMatchEligible" />, which teaches the action
      ///   using the same two-sided check.
      /// </summary>
      private static ActionOutcome ExecuteMarry(Hero npcHero, ActionContext context)
      {
         try
         {
            var player = Hero.MainHero;

            if (player == null || npcHero == null) return null;

            if (player.Spouse != null)
               return ActionOutcome.Refused(
                  "(You are already wed — no second marriage can be sealed in law.)");

            // "Marry your own companion" carve-out (2026-08-11): the SameClan bar exists to forbid wedding blood
            // kin, not a hired wanderer companion who shares the clan banner. An ELIGIBLE non-kin companion
            // (the SAME MarryCompanionResolver authority the prompt's carve-out consulted) is allowed past this
            // re-check and PROMOTED below before MarriageAction.Apply; a genuine blood-kin clan member is still
            // refused here, so the one line the bar truly holds stays intact.
            bool eligibleCompanionMatch = MarryCompanionResolver.IsEligibleCompanionMatch(npcHero);

            if (npcHero.Clan == Clan.PlayerClan && !eligibleCompanionMatch)
               return ActionOutcome.Refused(
                  $"({npcHero.Name} belongs to your own clan — custom forbids it.)");

            if (!MarriageProspectResolver.IsMarriageable(player, npcHero))
               return ActionOutcome.Refused(
                  $"({npcHero.Name} cannot be wed to you under the laws of Calradia.)");

            // RaveN017Mod (26/10/2026): the game weds no two heads of houses, and its Apply would do nothing.
            if (MarriageProspectResolver.HeadsOfTwoHouses(player, npcHero))
               return ActionOutcome.Refused(
                  $"({npcHero.Name} heads their own house, as you head yours: neither can wed without giving theirs up.)");

            // M-B5: vanilla blocks a marriage across an active war or with a captive at its own screen; a chat
            // marriage must honour the same law (the prompt is advice, the bridge is law).
            if (!MarriageGuardPolicy.MarriageAllowed(new MarriageGuardPolicy.MarriageStateFacts {CandidatePrisoner = npcHero.IsPrisoner, PlayerPrisoner = player.IsPrisoner, AtWarWithCandidate = IsAtWarWithPlayer(npcHero)}))
               return ActionOutcome.Refused(
                  $"(A marriage with {npcHero.Name} cannot be sealed now: not across a war, nor from captivity.)");

            int reputation = context?.Profile?.ReputationWithPlayer ?? 0;
            int vanillaRelation = npcHero.GetRelation(player);
            int bestRegard = Math.Max(reputation, vanillaRelation);

            // Ratified 2026-07-23 (player report: a player earned the family's blessing through a quest, then
            // hit the regard gate no one had ever named). History sides with the family: a house that has
            // BLESSED the match can bind its kin for political ends, so with the blessing in hand the wedding
            // proceeds even below the regard bar, and the unwon heart becomes a ForcedMarriage grudge instead
            // of an invisible refusal (planted after the union is sealed, below). Without the blessing the
            // regard bar still holds, and the refusal now names the other road.
            bool forcedUnion = ForcedMarriagePolicy.IsForcedUnion(bestRegard, ModSettings.MarriageRegardThreshold);

            // Stolen Love pillar, marry exit (ratified decision A): an NPC who left their OWN spouse for the player
            // has already done the courting, so the match is a willing love match below the regard bar with no
            // blessing and NO forced-marriage grudge. They are single here by construction (a married NPC is not
            // marriageable and would have been refused above).
            bool stolenLoveDivorcee = npcHero.Spouse == null
               && (NpcMarriageEstrangementBehavior.Instance?.HasConcludedFor(npcHero.StringId) ?? false);

            if (forcedUnion && !stolenLoveDivorcee && !(MarriageApprovalBehavior.Instance?.IsApproved(npcHero.StringId) ?? false))
               return ActionOutcome.Refused(
                  $"({npcHero.Name} does not yet hold you dear enough to take this step, " +
                  "and no family blessing compels them to.)");

            // "Marry your own companion": a wanderer companion is Occupation.Wanderer, which the engine's own
            // Hero.CanMarry()/IsSuitableForMarriage refuses, so a raw MarriageAction.Apply would silently no-op.
            // Promote them to Lord in the player's own clan first (decompile-verified recipe), refusing cleanly
            // if the promotion cannot be honoured, so the union is never left half-sealed.
            if (eligibleCompanionMatch && !TryPromoteCompanionForMarriage(npcHero, out string promotionRefusal))
               return ActionOutcome.Refused(promotionRefusal);

            MarriageApprovalBehavior.MarryThroughCr(player, npcHero);

            // RaveN017Mod (26/10/2026): MarriageAction.Apply refuses in SILENCE what the engine's marriage model
            // will not allow (an army, a battle, a courtship elsewhere), and the wedding used to be announced
            // anyway. Nothing sealed, nothing said: refuse plainly, for every match.
            if (player.Spouse != npcHero)
            {
               ModLog.Error($"[ACTION] marry: the engine sealed no marriage with {npcHero.Name}; refused.");

               return ActionOutcome.Refused(
                  $"(The marriage with {npcHero.Name} could not be sealed: Calradia's law would not allow it now.)");
            }

            // "Marry your own companion": assert the engine actually sealed the union and the ex-companion's
            // state is coherent (a promoted Lord, no longer a companion, in the player's clan, wed to the player).
            // If any of that failed, refuse cleanly rather than leave a half-married state; the promotion above is
            // idempotent enough that a retry after the block clears is safe.
            if (eligibleCompanionMatch && !(player.Spouse == npcHero
                  && npcHero.Occupation == Occupation.Lord
                  && npcHero.CompanionOf == null
                  && npcHero.Clan == Clan.PlayerClan
                  && !npcHero.IsPlayerCompanion))
            {
               ModLog.Error($"[ACTION] marry: companion {npcHero.Name} was promoted but the engine did not seal a coherent marriage "
                          + $"(spouse={(player.Spouse == npcHero)}, occ={npcHero.Occupation}, companionOf={(npcHero.CompanionOf == null ? "null" : "set")}).");

               return ActionOutcome.Refused(
                  $"(The union with {npcHero.Name} could not be sealed cleanly and was not recorded.)");
            }

            // The political match is sealed, and the bride's unwon heart takes shape as a real, visible,
            // RESOLVABLE grievance: it colours her prompt, shows on her encyclopedia page, tarnishes her
            // kindnesses, and fades if the marriage is lived well. Planted AFTER the union so a refused plant
            // (a merciful nature's trait-raised floor may decline a faint one) never blocks the wedding.
            // KnownToPlayer: a bride wed by her family's will hides nothing about how she came to the altar.
            if (forcedUnion && !stolenLoveDivorcee)
            {
               int resentment = ForcedMarriagePolicy.GrudgeMagnitude(bestRegard, ModSettings.MarriageRegardThreshold);
               bool planted = GrudgeBehavior.Instance?.TryPlant(npcHero, player, GrudgeSource.ForcedMarriage,
                  "Wed to them by my family's will, not my own.", resentment, true,
                  (int) CampaignTime.Now.ToDays) ?? false;
               ModLog.Info($"[ACTION] marry: a FORCED union (best regard {bestRegard} < " +
                           $"{ModSettings.MarriageRegardThreshold}); resentment {resentment} " +
                           $"{(planted ? "planted" : "refused by the formation floor")}.");
            }

            // C3: marriage to the player is an OPEN, legitimate bond, so pin the romantic status to Committed
            // (persisted by the post-action Store.Set, like take_as_consort). Without this the profile kept its
            // pre-wedding status and the next intimacy [EVENT] flipped a real spouse to SecretLover ("married to
            // another, clandestine"), froze them there for life, and made jealousy read the marriage as an affair.
            // Any clandestine flag from the courtship that preceded the vow is cleared for the same reason.
            NpcProfile marriedProfile = context?.Profile;
            if (marriedProfile?.Romantic != null)
            {
               RomanticStatus before = marriedProfile.Romantic.Status;
               marriedProfile.Romantic.Status = RomanticStatus.Committed;
               marriedProfile.Romantic.NoteChangeFrom(before, (int) CampaignTime.Now.ToDays); // the Heart page dates the wedding
               marriedProfile.Romantic.IsSecretLover = false;
               marriedProfile.Romantic.SecretLoverSinceDay = null; // the bond is open now: no hidden secret left to leak
            }

            ModLog.Info($"[ACTION] marry: player wed {npcHero.Name}.");

            return new ActionOutcome(
               $"You and {npcHero.Name} are joined in marriage, bound together before Calradia.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] marry failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   "Marry your own companion": promotes a wanderer companion out of <see cref="Occupation.Wanderer" />
      ///   into <see cref="Occupation.Lord" /> in the player's own clan so the engine's
      ///   <c>Hero.CanMarry()</c> / <c>IsSuitableForMarriage</c> will accept the union (a wanderer is refused;
      ///   a raw <see cref="MarriageAction.Apply" /> on one silently no-ops). The recipe is decompile-verified
      ///   (Bannerlord 1.3.15): set <c>Clan = PlayerClan</c> (the engine's <c>Clan</c> setter writes the backing
      ///   <c>_clan</c>, and stamps <c>_originClan</c> to the player's clan too since a companion's <c>_clan</c>
      ///   started null), null <c>CompanionOf</c>, then <c>SetNewOccupation(Lord)</c>. Because the couple then
      ///   shares the player's clan, <c>GetClanAfterMarriage</c> returns the player's clan and
      ///   <c>HandleClanChangeAfterMarriageForHero</c> never runs, so the companion stays in the party with no
      ///   fugitive/roster side effect. Refuses cleanly (no mutation) when the engine precondition
      ///   <c>PartyBelongedTo?.Army == null</c> is not met: a companion in the player's party while the player
      ///   leads an ARMY would make <c>IsSuitableForMarriage</c> false and no-op the wedding.
      /// </summary>
      private static bool TryPromoteCompanionForMarriage(Hero companion, out string refusal)
      {
         refusal = null;

         try
         {
            Hero player = Hero.MainHero;

            if (companion == null || player == null)
            {
               refusal = "(The match could not be made.)";

               return false;
            }

            // IsSuitableForMarriage requires PartyBelongedTo?.Army == null for BOTH parties. The companion rides
            // in the player's own party, so an army on either party is the same campaign obstacle; refuse before
            // any mutation rather than let Apply silently no-op.
            if (companion.PartyBelongedTo?.Army != null || player.PartyBelongedTo?.Army != null)
            {
               refusal = $"(You cannot wed {companion.Name} while on campaign in an army; leave or disband the army first.)";

               return false;
            }

            // A hero already caught up in a battle cannot be wed either (IsSuitableForMarriage: MapEvent == null).
            if (companion.PartyBelongedTo?.MapEvent != null || player.PartyBelongedTo?.MapEvent != null)
            {
               refusal = $"(You cannot wed {companion.Name} in the middle of a battle.)";

               return false;
            }

            companion.Clan = Clan.PlayerClan;
            companion.CompanionOf = null;
            companion.SetNewOccupation(Occupation.Lord);

            ModLog.Info($"[ACTION] marry: promoted companion {companion.Name} from Wanderer to Lord of the player's clan before the wedding.");

            return true;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] marry: companion promotion failed: {ex.GetType().Name}: {ex.Message}");
            refusal = "(The companion could not be raised to a station fit for marriage.)";

            return false;
         }
      }

      /// <summary>
      ///   pledge_against (RP with teeth, increment 2): a lord, moved by genuine and standing enmity, vows
      ///   to move against a named rival. This is a POLITICAL commitment ONLY — it launches a real scheme
      ///   (slander or sabotage) via <see cref="WorldEvents.SchemeStore" /> and records a
      ///   <see cref="TrackedCommitment" /> the world will hold the lord to; it never declares, nor implies,
      ///   war. Only taught when <see cref="CourtActionResolver.ResolvePledgeRivalName" /> names a genuine
      ///   rival, but every guard is re-checked here — the prompt is advice, the bridge is law: the target
      ///   is re-resolved by name, kin are excluded again, and a lord may hold only one pledge at a time.
      ///   Reuses an already-active scheme between the pair rather than erroring (<c>ForcePlot</c>'s own
      ///   guard), so a lord already secretly plotting can still be held to account for it. The actual launch
      ///   (scheme kind pick, <c>ForcePlot</c>, <see cref="TrackedCommitment" /> creation, and the promisor's own
      ///   memory of it) is factored into <see cref="CourtActionResolver.LaunchPledge" /> (2026-08-04, Partie 1's
      ///   council schemes kind), the SAME code path <c>CalradiaRemembers.Chat.CouncilLift.SettlePledgeAgainst</c>
      ///   now calls too, so a pledge behaves identically whichever table struck it.
      /// </summary>
      /// <summary>
      ///   sway_opinion through its verb, as before; a sway that lands may answer a plead_case task (the person the
      ///   player spoke to, won over toward the lord who sent him; Lordfadooboo, fkasad, 01/10/2026).
      /// </summary>
      private ActionOutcome ExecuteSwayOpinion(GameAction action, Hero npcHero, ActionContext context)
      {
         ActionOutcome outcome = VerbRegistry.Instance.Execute(action, new VerbContext(npcHero, context?.Profile, this, null, context));
         if (outcome is not {IsPositive: true}) return outcome;

         try
         {
            string name = null, stance = null;
            action.Parameters?.TryGetValue("target", out name);
            action.Parameters?.TryGetValue("stance", out stance);
            Hero about = CourtActionResolver.ResolveSwayTarget(name, npcHero);

            QuestDeedTracker.CreditPlea(new SwayFacts {PersuadedId = npcHero?.StringId, AboutId = about?.StringId, Warmer = SwayStanceParser.Parse(stance) == SwayStance.For});
         }
         catch (Exception ex) { ModLog.Error($"[QUEST] plead_case credit failed: {ex.GetType().Name}: {ex.Message}"); }

         return outcome;
      }

      private static ActionOutcome ExecutePledgeAgainst(GameAction action, Hero npcHero, ActionContext context)
      {
         try
         {
            NpcProfile profile = context?.Profile;

            if (npcHero is not {IsLord: true, IsAlive: true} || npcHero.IsPrisoner) return null;
            if (profile == null || Hero.MainHero == null) return null;

            var behavior = CommitmentBehavior.Instance;

            if (behavior == null) return null;

            if (behavior.HasActive(npcHero.StringId))
               return ActionOutcome.Refused($"({npcHero.Name} already has a pledge outstanding — one vow at a time.)");

            action.Parameters.TryGetValue("target", out string targetName);
            Hero target = CourtActionResolver.ResolvePledgeTarget(targetName, npcHero);

            if (target == null)
               return ActionOutcome.Refused("(No rival by that name is known to pledge against.)");

            if (CourtActionResolver.IsCloseKin(npcHero, target))
               return ActionOutcome.Refused($"({npcHero.Name} will not move against their own kin.)");

            var today = (int) CampaignTime.Now.ToDays;
            CourtActionResolver.LaunchPledge(npcHero, target, profile, behavior, today);

            return new ActionOutcome($"{npcHero.Name} pledges to move against {target.Name}.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] pledge_against failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   reassure_companion action: the player sincerely addressed an unhappy companion's grievance, so
      ///   they soften a little (the WellTreated happiness driver). Emitted only by a companion who was
      ///   voicing discontent; harmless on anyone else (the driver no-ops without a profile).
      /// </summary>
      private static ActionOutcome ExecuteReassureCompanion(Hero npcHero)
      {
         if (npcHero == null) return null;
         CompanionHappinessService.ApplyDriver(npcHero, HappinessDriver.WellTreated);
         ModLog.Info($"[ACTION] reassure_companion: {npcHero.Name} is a little more at ease.");

         return new ActionOutcome($"{npcHero.Name} seems a little more at ease in your service.", true);
      }

      /// <summary>
      ///   recall_companion action: the player met an away companion and ordered them to abandon the errand and
      ///   return; the companion agreed. We ACTUALLY bring them home (the behavior re-validates and rejoins them
      ///   to the party) — a verified deed, so the companion can never "agree" yet keep wandering off forever.
      /// </summary>
      private static ActionOutcome ExecuteRecallCompanion(Hero npcHero)
      {
         try
         {
            if (npcHero == null) return null;
            var behavior = CompanionMissionBehavior.Instance;

            if (behavior == null) return null;

            (bool recalled, string message) = behavior.Recall(npcHero);
            ModLog.Info($"[ACTION] recall_companion: {npcHero.Name} → {(recalled ? "returned" : "refused")}: {message}");

            return new ActionOutcome(message, recalled);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] recall_companion failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   follow_me: a lord has agreed, in conversation, to have their OWN party escort the player's across
      ///   the map for a while, keeping their own troops and command, distinct from join_party (they never
      ///   join the player's party or clan). Only taught when the game confirms the game can truly honour it
      ///   (see EncounterContextBuilder's EscortEligibilityPolicy.CanEscort check, mirrored 1:1 here), but every
      ///   guard is re-checked by FollowMeBehavior.TryStart: the prompt is advice, the bridge is law.
      /// </summary>
      private static ActionOutcome ExecuteFollowMe(Hero npcHero)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null) return null;

            var behavior = FollowMeBehavior.Instance;

            if (behavior == null) return null;

            if (!behavior.TryStart(npcHero, out string failReason))
               return ActionOutcome.Refused($"({npcHero.Name} cannot ride with you right now: {failReason})");

            ModLog.Info($"[ACTION] follow_me: {npcHero.Name} begins escorting the player.");

            return new ActionOutcome($"{npcHero.Name} will ride with you for a time.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] follow_me failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   dismiss_escort: the MIRROR of follow_me, the player (or the escorting lord themselves) ends an
      ///   active escort early, before its term runs out. Only taught when the game confirms this NPC is
      ///   genuinely escorting right now (EncounterContext.NpcIsEscortingPlayer), but re-checked here regardless.
      /// </summary>
      private static ActionOutcome ExecuteDismissEscort(Hero npcHero)
      {
         try
         {
            if (npcHero == null) return null;

            var behavior = FollowMeBehavior.Instance;

            if (behavior == null) return null;

            if (!behavior.TryDismiss(npcHero, out string failReason))
               return ActionOutcome.Refused($"({npcHero.Name} is not riding with you: {failReason})");

            ModLog.Info($"[ACTION] dismiss_escort: {npcHero.Name} breaks off the escort.");

            return new ActionOutcome($"{npcHero.Name} takes leave and turns back to their own business.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] dismiss_escort failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   ride_with_me: the idle-lord complement of follow_me. A lord/lady WITHOUT a field party of their own has
      ///   agreed, in conversation, to ride IN the player's OWN party for a time, keeping their OWN clan (no
      ///   marriage, no clan-join), for whatever reason the scene gave. Only taught when the game confirms it (see
      ///   EncounterContextBuilder's RideWithMeEligibilityPolicy.CanRideWithPlayer check, mirrored here) AND only
      ///   while the path is enabled; every guard is re-checked before the take: the prompt is advice, the bridge
      ///   is law. The leadsParty re-check is what keeps ride_with_me and follow_me mutually exclusive, so a
      ///   party-leader is refused here (that is follow_me's job) and their command is never orphaned.
      /// </summary>
      private static ActionOutcome ExecuteRideWithMe(Hero npcHero, NpcProfile profile)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null) return null;

            // Gate: while the player-facing path is off it is never taught, so an emission here means the model
            // invented it. Refuse plainly rather than silently letting the narration stand.
            if (!RetainerBehavior.Enabled)
               return ActionOutcome.Refused($"({npcHero.Name} cannot ride in your party right now.)");

            RetainerBehavior behavior = RetainerBehavior.Instance;

            if (behavior == null) return null;

            NpcProfile p = profile ?? NpcMemoryCampaignBehavior.Instance?.Store?.Get(npcHero.StringId);
            int regard = p?.ReputationWithPlayer ?? 0;
            MobileParty party = npcHero.PartyBelongedTo;
            bool leadsParty = party != null && party.LeaderHero == npcHero;
            bool atWar = npcHero.MapFaction != null && Hero.MainHero.MapFaction != null &&
                         FactionManager.IsAtWarAgainstFaction(npcHero.MapFaction, Hero.MainHero.MapFaction);

            if (!RideWithMeEligibilityPolicy.CanRideWithPlayer(new RideWithMeEligibilityPolicy.RideWithMeFacts {
                   IsLord = npcHero.IsLord,
                   IsAlive = npcHero.IsAlive,
                   IsPrisoner = npcHero.IsPrisoner,
                   LeadsOwnParty = leadsParty,
                   IsPlayerClan = npcHero.Clan == Clan.PlayerClan,
                   AtWarWithPlayer = atWar,
                   AlreadyRiding = behavior.IsRetainer(npcHero.StringId),
                   Regard = regard,
                   RealmAtWar = FactionIsAtWarWithAnyone(npcHero.MapFaction)
                }))
            {
               // The one refusal worth naming precisely: a lord who leads their own party belongs to follow_me,
               // not this verb. Every other block is a plain "not right now" so the model never narrates it done.
               if (leadsParty)
                  return ActionOutcome.Refused(
                     $"({npcHero.Name} leads their own party; they would ride ALONGSIDE you with follow_me, not inside your party.)");

               return ActionOutcome.Refused($"({npcHero.Name} will not ride in your party right now.)");
            }

            // Reuse the proven spike take: AddHeroToPartyAction into MobileParty.MainParty, Hero.Clan untouched,
            // tracked for the war/death/part_ways end conditions. No starting relation nudge is applied: unlike a
            // permanent clan-join (recruit_notable/recruit_prisoner), this is a temporary, clan-preserving
            // arrangement and the close-friend regard was already earned, so there is nothing new to represent.
            if (!behavior.TryTake(npcHero, out string report))
               return ActionOutcome.Refused($"({npcHero.Name} cannot ride in your party right now: {report})");

            ModLog.Info($"[ACTION] ride_with_me: {npcHero.Name} joins the player's party as a retainer.");

            return new ActionOutcome($"{npcHero.Name} rides at your side for a time, still of their own house.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] ride_with_me failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   True when <paramref name="faction" /> holds a live war against ANY other faction, not only the
      ///   player's own (the author's C-guard concern: a lord whose realm is at war must stay home for his
      ///   liege's muster call, not ride off in the player's own party and quietly weaken his faction). Walks the
      ///   same Kingdoms + independent-Clans pool EncounterContextBuilder's own peace-mediation teaching draws
      ///   from, so the teaching gate and this bridge re-check can never disagree. Guarded so a missing engine
      ///   snapshot never throws mid-turn.
      /// </summary>
      /// <summary>
      ///   True when this realm is at war with another REAL faction, the sense ride_with_me needs: a lord whose
      ///   realm is fighting a war stays home for his liege's muster instead of riding off with the player.
      ///   <para>
      ///     BANDIT factions are excluded, and that exclusion is the whole point. Every kingdom in Bannerlord is
      ///     permanently at war with the looters and sea raiders, so counting them made this return true for
      ///     everyone, always, and ride_with_me (shipped v2.4.4) could never accept a single lord. The cr.test all
      ///     failure that surfaced it read as a picker problem for two rounds before the real cause showed.
      ///   </para>
      ///   Internal rather than private so the self-test asserts the SAME code the bridge consults, instead of a
      ///   copy that can drift out of step with it (which is exactly how this hid).
      /// </summary>
      internal static bool FactionIsAtWarWithAnyone(IFaction faction)
      {
         try
         {
            if (faction == null) return false;

            if (Kingdom.All != null)
               foreach (Kingdom k in Kingdom.All)
                  if (k != faction && !k.IsBanditFaction && FactionManager.IsAtWarAgainstFaction(faction, k)) return true;

            if (Clan.All != null)
               foreach (Clan c in Clan.All)
                  if (c.Kingdom == null && c != faction && !c.IsBanditFaction
                      && FactionManager.IsAtWarAgainstFaction(faction, c)) return true;

            return false;
         }
         catch { return false; }
      }

      /// <summary>
      ///   part_ways: the MIRROR of ride_with_me, the player (or the retainer themselves) ends the arrangement and
      ///   the lord returns to their own clan and business. Only taught while THIS NPC is genuinely riding as a
      ///   retainer (EncounterContext.NpcIsRidingWithPlayer), but re-checked here regardless: TryRelease refuses
      ///   when they are not tracked.
      /// </summary>
      private static ActionOutcome ExecutePartWays(Hero npcHero)
      {
         try
         {
            if (npcHero == null) return null;

            RetainerBehavior behavior = RetainerBehavior.Instance;

            if (behavior == null) return null;

            if (!behavior.TryRelease(npcHero, out string report))
               return ActionOutcome.Refused($"({npcHero.Name} is not riding in your party: {report})");

            ModLog.Info($"[ACTION] part_ways: {npcHero.Name} leaves the player's party.");

            var leaveMsg = $"{npcHero.Name} takes their leave and returns to their own affairs.";
            MBInformationManager.AddQuickInformation(new TextObject(leaveMsg), 0, npcHero.CharacterObject);

            return new ActionOutcome(leaveMsg, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] part_ways failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   scheme_assist: the NPC (a plotter) has sounded the player out and the player has agreed to
      ///   abet their secret scheme against a rival. Only taught when the game confirms a recruitable
      ///   plot (SchemeStore.GetRecruitableTargetName), but re-validated here: the store re-checks the
      ///   gate, advances the plot, and the plotter warms to the player for throwing in.
      /// </summary>
      private static ActionOutcome ExecuteSchemeAssist(Hero npcHero)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null) return null;

            string result = SchemeStore.Instance?.PlayerAssists(npcHero);

            if (string.IsNullOrEmpty(result))
               return ActionOutcome.Refused("(There is no plot of theirs you could help with just now.)");

            // Throwing in together warms the plotter toward the player.
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(npcHero, Hero.MainHero, 3, false);
            ModLog.Info($"[ACTION] scheme_assist: player aided {npcHero.Name}'s scheme.");

            return new ActionOutcome(result, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] scheme_assist failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   scheme_heed: the player has warned this NPC of a secret plot against them, and the NPC
      ///   heeds it. Only taught when the game confirms such a plot (SchemeStore.HasSecretSchemeAgainst),
      ///   but re-validated: the store exposes the scheme (giving the target a chance to counter it on
      ///   later ticks) and the grateful target warms to the player.
      /// </summary>
      private static ActionOutcome ExecuteSchemeHeed(Hero npcHero)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null) return null;

            string result = SchemeStore.Instance?.TargetHeeds(npcHero);

            if (string.IsNullOrEmpty(result))
               return ActionOutcome.Refused("(There is no plot against them you could warn of just now.)");

            // Gratitude for the warning warms the target toward the player.
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(npcHero, Hero.MainHero, 4, false);
            ModLog.Info($"[ACTION] scheme_heed: player warned {npcHero.Name} of a plot against them.");

            return new ActionOutcome(result, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] scheme_heed failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   take_as_consort: both parties have openly named the committed bond between them.
      ///   Unlike legal marriage, this does not call <see cref="MarriageAction.Apply" /> —
      ///   it is purely mod-tracked. Sets <c>IsConsort = true</c> and
      ///   <c>Status = Committed</c> on the NPC's romantic profile.
      ///   The player may already be wed; consorts are not bound by legal exclusivity.
      /// </summary>
      private static ActionOutcome ExecuteTakeAsConsort(Hero npcHero, ActionContext context)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null || context?.Profile == null) return null;

            NpcProfile profile = context.Profile;

            if (profile.Romantic == null)
               return ActionOutcome.Refused($"({npcHero.Name} has no romantic connection to build upon.)");

            // M-R1: an ACTIVE intimate bond, not a raw ordinal test. Estranged/Broken sort after Intimate in the
            // enum, so "< Intimate" let a dead romance be taken as consort; IsIntimateOrDeeper excludes them.
            if (!profile.Romantic.Status.IsIntimateOrDeeper())
               return ActionOutcome.Refused($"({npcHero.Name} is not yet close enough to you for this bond.)");

            if (profile.Romantic.IsConsort || profile.Romantic.Status == RomanticStatus.Committed)
               return ActionOutcome.Refused($"(You and {npcHero.Name} are already bound as consorts.)");

            RomanticStatus before = profile.Romantic.Status;
            profile.Romantic.Status = RomanticStatus.Committed;
            profile.Romantic.NoteChangeFrom(before, (int) CampaignTime.Now.ToDays);
            profile.Romantic.IsConsort = true;

            ModLog.Info($"[ACTION] take_as_consort: {npcHero.Name} is now the player's consort.");

            try
            {
               var today = (int) CampaignTime.Now.ToDays;
               WorldEventStore.Instance?.Record(new WorldEvent {
                  Type = WorldEventType.PlayerFeat,
                  Day = today,
                  Magnitude = 45,
                  InvolvesPlayer = true,
                  Summary = $"{Hero.MainHero.Name} took {npcHero.Name} as consort.",
                  ActorIds = new List<string> {Hero.MainHero.StringId, npcHero.StringId},
                  FactionIds = npcHero.MapFaction != null
                     ? new List<string> {npcHero.MapFaction.StringId}
                     : new List<string>(),
                  ChronicleLine = $"{Hero.MainHero.Name} took {npcHero.Name} as consort, and tongues wagged in the halls"
               });
            }
            catch (Exception ex) { ModLog.Error($"[ACTION] take_as_consort chronicle record failed: {ex.GetType().Name}: {ex.Message}"); }

            // Openly taking a consort is the most public romantic commitment short of marriage: it rouses a
            // staked party AT THE ACT (an existing spouse above all), not only through the later daily
            // competition loop (jealousy audit 2026-07-10). Treated as witnessed, marriage-tier.
            JealousyBehavior.Instance?.OnRomanticAct(
               npcHero, RomanticActTier.Marriage, true, (int) CampaignTime.Now.ToDays);

            return new ActionOutcome(
               $"You and {npcHero.Name} are bound as consorts — a commitment not named by law, but no less real.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] take_as_consort failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   take_as_secret_lover: a faithful companion and the player have both welcomed a discreet, hidden bond
      ///   born of shared war. Purely mod-tracked and DELIBERATELY silent (no world-event, no chronicle that
      ///   gossip could spread): the secrecy is the point, kept to avoid stirring jealousy. Sets
      ///   <c>IsSecretLover = true</c> and <c>Status = SecretLover</c> on the companion's romantic profile.
      ///   Re-validates the eligibility guards (the prompt is advice, the bridge is law).
      /// </summary>
      private static ActionOutcome ExecuteTakeAsSecretLover(Hero npcHero, ActionContext context)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null || context?.Profile == null) return null;

            NpcProfile profile = context.Profile;

            // The bridge asks the SAME rule the prompt's teaching gate asks, rather than keeping its own list.
            // It used to keep one, and the two had drifted: this guard checked neither the adult-content switch
            // nor an ended romance, so a verb the prompt refused to teach would still execute if the model
            // emitted it untaught. The doctrine here is that the prompt proposes and the bridge is law, which
            // only holds while the law is the stricter of the two. See CalradiaRemembers.Logic.SecretBondEligibility.
            SecretBondVerdict verdict = SecretBondEligibility.Evaluate(
               EncounterContextBuilder.SecretBondFactsFor(npcHero, profile));

            if (verdict != SecretBondVerdict.Eligible)
               return ActionOutcome.Refused(SecretBondEligibility.Describe(verdict, npcHero.Name?.ToString()));

            RomanticStatus before = profile.Romantic.Status;
            profile.Romantic.Status = RomanticStatus.SecretLover;
            profile.Romantic.NoteChangeFrom(before, (int) CampaignTime.Now.ToDays);
            profile.Romantic.IsSecretLover = true;
            // Stamp the onset so the mystery-lover pillar can keep it genuinely secret for a while before whispers
            // of the bond's EXISTENCE (never the player's identity) may begin to circulate.
            profile.Romantic.SecretLoverSinceDay = (int) CampaignTime.Now.ToDays;

            ModLog.Info($"[ACTION] take_as_secret_lover: {npcHero.Name} is now the player's secret lover (its existence may, after a while, leak as an anonymous whisper).");

            return new ActionOutcome(
               $"You and {npcHero.Name} share something now, quiet and yours alone, kept from every other eye.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] take_as_secret_lover failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   open_relationship (Inc 2): the player and this COMMITTED partner have openly and mutually agreed to
      ///   reciprocal OPEN terms, each free to love another. Sets <c>IsOpenArrangement = true</c> on the
      ///   partner's romantic profile and persists it, so the jealousy system no longer counts her among the
      ///   aggrieved (see <see cref="CalradiaRemembers.Logic.Romance.OpenRelationshipPolicy" />). GUARD (the bridge is law): the target
      ///   must be the player's spouse, or a committed consort (<c>IsConsort</c> / <c>Status == Committed</c>);
      ///   anything less is refused, never a silent no-op. Mirrors <see cref="ExecuteTakeAsConsort" />'s
      ///   commitment path (mod-tracked only, no engine marriage call), with a hard partner-only guard.
      /// </summary>
      /// <summary>
      ///   accept_other_partners (22/09/2026, yozakura12): this committed partner accepts that the PLAYER keeps
      ///   other partners, while she herself remains his alone. A CLOSED, one-sided household, and deliberately
      ///   not <see cref="ExecuteOpenRelationship" />, which is reciprocal and suppresses her jealousy outright.
      ///   This only changes what jealousy COSTS her (<see cref="CalradiaRemembers.Logic.Romance.JealousyTrackPolicy" />): she keeps her
      ///   anger, her confrontations and her memories, and stops sliding toward hatred over an arrangement she
      ///   agreed to. GUARD (the bridge is law): the same <see cref="PluralAcceptancePolicy" /> the teaching was
      ///   gated on, re-asked here, so a verb the model reached for out of turn is refused in her own words.
      /// </summary>
      private static ActionOutcome ExecuteAcceptOtherPartners(Hero npcHero, ActionContext context)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null || context?.Profile == null) return null;

            NpcProfile profile = context.Profile;
            PluralAcceptanceFacts facts = PluralAcceptanceFactsFor(npcHero, profile);

            if (!PluralAcceptancePolicy.CanBeOffered(facts))
               return ActionOutcome.Refused($"({npcHero.Name} cannot accept such a household: {PluralAcceptancePolicy.WhyNot(facts)}.)");

            profile.Romantic.AcceptsPlayersOtherPartners = true;
            NpcMemoryCampaignBehavior.Instance?.Store?.Set(profile);

            ModLog.Info($"[ACTION] accept_other_partners: {npcHero.Name} accepts the player's other partners, remaining his alone.");

            var msg = $"{npcHero.Name} accepts that you keep others, and remains yours alone.";
            MBInformationManager.AddQuickInformation(new TextObject(msg), 0, npcHero.CharacterObject);

            return new ActionOutcome(msg, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"ExecuteAcceptOtherPartners failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   The facts <see cref="PluralAcceptancePolicy" /> judges, read once so the TEACHING gate and the BRIDGE
      ///   guard cannot drift apart: docs/maps/gates.md exists because they had, elsewhere.
      /// </summary>
      internal static PluralAcceptanceFacts PluralAcceptanceFactsFor(Hero npcHero, NpcProfile profile)
      {
         bool isSpouse = npcHero == Hero.MainHero?.Spouse;
         bool committed = profile?.Romantic != null
                       && (profile.Romantic.IsConsort || profile.Romantic.Status == RomanticStatus.Committed);

         return new PluralAcceptanceFacts {
            IsCommittedPartner = profile?.Romantic != null && (isSpouse || committed),
            AlreadyAccepted = profile?.Romantic?.AcceptsPlayersOtherPartners ?? false,
            AlreadyOnOpenTerms = profile?.Romantic?.IsOpenArrangement ?? false,
            CultureToleratesSharing = JealousyCultureLoader.GetCulture(npcHero?.Culture?.Name?.ToString())
                                                           .ToleratesSharedPartners(Mapping.StatureReader.Player.ClanTier),
            RegardWithPlayer = profile?.ReputationWithPlayer ?? 0
         };
      }

      private static ActionOutcome ExecuteOpenRelationship(Hero npcHero, ActionContext context)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null || context?.Profile == null) return null;

            NpcProfile profile = context.Profile;

            if (profile.Romantic == null)
               return ActionOutcome.Refused($"({npcHero.Name} has no bond with you to open.)");

            // The bridge is law: only a real committed partner (the player's own spouse, or a consort/committed
            // bond) can hold OPEN terms. A mere flirtation or an intimate-but-uncommitted bond cannot.
            bool isSpouse = npcHero == Hero.MainHero.Spouse;
            bool isCommittedConsort = profile.Romantic.IsConsort || profile.Romantic.Status == RomanticStatus.Committed;

            if (!isSpouse && !isCommittedConsort)
               return ActionOutcome.Refused($"({npcHero.Name} is not a committed partner of yours, so there is no bond to open.)");

            if (profile.Romantic.IsOpenArrangement)
               return ActionOutcome.Refused($"(You and {npcHero.Name} already hold open terms.)");

            profile.Romantic.IsOpenArrangement = true;
            NpcMemoryCampaignBehavior.Instance?.Store?.Set(profile);

            ModLog.Info($"[ACTION] open_relationship: {npcHero.Name} and the player now hold reciprocal open terms.");

            var msg = $"You and {npcHero.Name} now hold open terms: each free to love another, and neither wronged by it.";
            MBInformationManager.AddQuickInformation(new TextObject(msg), 0, npcHero.CharacterObject);

            return new ActionOutcome(msg, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] open_relationship failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   close_relationship (Inc 3b-b): the player REVOKES the open arrangement with this committed partner.
      ///   Sets <c>IsOpenArrangement = false</c> and persists it, so she takes no NEW lovers and Inc 2's jealousy
      ///   suppression naturally lifts (<see cref="CalradiaRemembers.Logic.Romance.OpenRelationshipPolicy" /> stops counting her among the
      ///   spared). Does NOT touch an existing <c>TakenLoverId</c>: ending the CURRENT affair is the separate
      ///   <see cref="ExecuteEndAffair" />. GUARD (the bridge is law): the target must be the player's spouse or a
      ///   committed consort who IS currently open; anything less is refused, never a silent no-op. The mirror of
      ///   <see cref="ExecuteOpenRelationship" />.
      /// </summary>
      private static ActionOutcome ExecuteCloseRelationship(Hero npcHero, ActionContext context)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null || context?.Profile == null) return null;

            NpcProfile profile = context.Profile;

            if (profile.Romantic == null)
               return ActionOutcome.Refused($"({npcHero.Name} has no bond with you to close.)");

            bool isSpouse = npcHero == Hero.MainHero.Spouse;
            bool isCommittedConsort = profile.Romantic.IsConsort || profile.Romantic.Status == RomanticStatus.Committed;

            if (!isSpouse && !isCommittedConsort)
               return ActionOutcome.Refused($"({npcHero.Name} is not a committed partner of yours, so there are no open terms to revoke.)");

            if (!profile.Romantic.IsOpenArrangement)
               return ActionOutcome.Refused($"(You and {npcHero.Name} hold no open terms to revoke.)");

            profile.Romantic.IsOpenArrangement = false;
            NpcMemoryCampaignBehavior.Instance?.Store?.Set(profile);

            ModLog.Info($"[ACTION] close_relationship: {npcHero.Name} and the player no longer hold open terms (existing lover, if any, untouched).");

            var msg = $"You and {npcHero.Name} no longer hold open terms: she will take no new lovers.";
            MBInformationManager.AddQuickInformation(new TextObject(msg), 0, npcHero.CharacterObject);

            return new ActionOutcome(msg, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] close_relationship failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   end_affair (Inc 3b-b): the current affair ENDS, at the player's asking, when this partner agrees (the
      ///   mechanical outcome the model emits on her assent; a refusal is simply no action). Clears
      ///   <c>TakenLoverId</c> and <c>LoverIsSecret</c> and resets the affair-tracking fields
      ///   (<c>LoverTrystCount</c>, <c>AffairDiscovered</c>, <c>LoverKnownToPlayer</c>), then persists. Records a
      ///   memory that the affair ended. Does NOT revoke the open arrangement itself (that is
      ///   <see cref="ExecuteCloseRelationship" />). GUARD (the bridge is law): the target must actually HAVE a
      ///   <c>TakenLoverId</c>; else refused, never a silent no-op.
      /// </summary>
      private static ActionOutcome ExecuteEndAffair(Hero npcHero, ActionContext context)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null || context?.Profile == null) return null;

            NpcProfile profile = context.Profile;

            if (profile.Romantic == null || string.IsNullOrEmpty(profile.Romantic.TakenLoverId))
               return ActionOutcome.Refused($"({npcHero.Name} has no lover to give up.)");

            // A lord's hidden affair is not the player's to end: only their own partner's lover is, "at my asking".
            if (Mapping.HiddenAffairBehavior.Instance?.IsHidden(npcHero.StringId) == true)
               return ActionOutcome.Refused($"(That is no affair of yours to end with {npcHero.Name}.)");

            Hero lover = Hero.FindFirst(h => h.StringId == profile.Romantic.TakenLoverId);
            string loverName = lover?.Name?.ToString() ?? "her lover";

            profile.Romantic.TakenLoverId = null;
            profile.Romantic.LoverIsSecret = false;
            profile.Romantic.LoverTrystCount = 0;
            profile.Romantic.AffairDiscovered = false;
            profile.Romantic.LoverKnownToPlayer = false;

            var day = (int) CampaignTime.Now.ToDays;
            profile.Events.Add(new NotableEvent(day, NotableEventType.Agreement,
               $"She gave up {loverName}, ending the affair at my asking."));

            NpcMemoryCampaignBehavior.Instance?.Store?.Set(profile);

            ModLog.Info($"[ACTION] end_affair: {npcHero.Name} gave up {loverName}; lover link and tracking fields reset.");

            var msg = $"{npcHero.Name} gives up {loverName}. The affair is ended.";
            MBInformationManager.AddQuickInformation(new TextObject(msg), 0, npcHero.CharacterObject);

            return new ActionOutcome(msg, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] end_affair failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      private static ActionOutcome ExecuteTakeGold(GameAction action, Hero npcHero)
      {
         if (!TryGetInt(action.Parameters, "amount", out int amount))
         {
            // Was a silent no-op that let the model keep narrating a payment that never left the player's purse.
            return ActionOutcome.Refused(SilentRefusalMessages.TakeGoldNoValidSum());
         }
         if (Hero.MainHero == null) return null;
         if (amount <= 0)
         {
            // Same silent-no-op fault as the unparsable case above, just a non-positive value instead.
            return ActionOutcome.Refused(SilentRefusalMessages.TakeGoldNoValidSum());
         }

         // Never take more than the player actually carries (bandits simply empty the purse).
         int asked = amount;
         amount = Math.Min(ClampInt(amount, 1, GoldTransferLimit.MaxSingleTransfer), Hero.MainHero.Gold);

         if (amount <= 0)
         {
            // Was a silent no-op: the model had already narrated the payment as handed over while the
            // player's purse (empty) never moved.
            return ActionOutcome.Refused(SilentRefusalMessages.TakeGoldPlayerInsolvent());
         }

         if (npcHero != null)
         {
            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, npcHero, amount);
            CompleteProvideGoldOnPayment(npcHero, amount); // any payment satisfies AND closes a ProvideGold quest

            // The vassal war duty: coin handed to a liege the player owes a levy counts toward it (-1: nothing owed).
            int levyOwed = WarLevyDesk.Owed(npcHero);
            int levyLeft = levyOwed > 0 ? WarLevyDesk.Pay?.Invoke(npcHero, amount) ?? levyOwed : -1;

            // Paying a scheming bastard-mother her demand IS settling the blackmail (buys her silence).
            bool silenced = BastardBehavior.Instance?.SettleByPayment(npcHero, amount) == true;
            ModLog.Info($"[ACTION] take_gold: player → {npcHero.Name} {amount} gold{(silenced ? " (bastard silence bought)" : "")}");

            // Romance audit C4: if she held a demand this payment did NOT meet, say so, so the coin is never
            // silently pocketed while the secret still leaks (MaxDemand is now capped to a single payable take,
            // so this only fires when the player chose to underpay, not when the demand was impayable).
            int owed = silenced ? 0 : (BastardBehavior.Instance?.OutstandingDemand(npcHero) ?? 0);

            // M-A3 (regard audit): a payment that bought (or went toward) blackmail silence is EXTORTION, not a
            // gift. Planting a "genuine gratitude" appreciation for it made the venal blackmailer's very next
            // prompt read "you feel a debt of gratitude", the exact opposite of the dynamic. A freely given
            // payment (no demand of any kind) is still a remembered kindness, a channel separate from the relation
            // bump above and the grudge-softening tail below.
            // A war levy is owed, not given: no gratitude for it either.
            bool coercedPayment = silenced || owed > 0 || levyLeft >= 0;
            if (!coercedPayment)
               TryPlantValueAppreciation(npcHero, AppreciationSource.GoldReceived, amount, "The player gave me gold.");

            // Grudges pillar (Gift gesture): a payment to the NPC may also soften or mend a grudge they
            // hold against the player, a channel separate from the relation bump above — never lets a
            // resolution failure break the gold transfer itself.
            string grudgeTail = levyLeft >= 0 ? "" : TryGrudgeGiftTail(npcHero, amount);

            string tail = levyLeft >= 0
               ? WarLevyDesk.AfterPayment(levyLeft)
               : silenced
               ? " Her silence is bought, for now."
               : owed > amount
                  ? $" It is not enough: she wants {owed} denars for her silence."
                  : grudgeTail;

            return new ActionOutcome($"You give {npcHero.Name} {amount} denars{GoldTransferLimit.CappedNote(asked, amount)} (purse: {Hero.MainHero.Gold}).{tail}", false,
               appliedGoldAmount: amount);
         }

         // No hero recipient (e.g. a synthesized bandit captor) — the coin is simply taken.
         Hero.MainHero.ChangeHeroGold(-amount);
         ModLog.Info($"[ACTION] take_gold: {amount} gold extorted (no hero recipient).");

         return new ActionOutcome($"They take {amount} denars from you (purse: {Hero.MainHero.Gold}).", false,
            appliedGoldAmount: amount);
      }

      /// <summary>
      ///   pay_blackmail (romance audit M-G5): the player agrees, in conversation, to buy a bastard-mother's
      ///   silence. A DETERMINISTIC settlement: the bridge itself looks up her exact outstanding demand and pays
      ///   it in full, so the outcome never depends on the LLM narrating a payment it forgot to enact (the player
      ///   thinks they paid while the secret still leaks) nor on it guessing the right number. Pays nothing and
      ///   settles nothing when she holds no demand, or when the player cannot cover it, telling them plainly.
      /// </summary>
      private static ActionOutcome ExecutePayBlackmail(Hero npcHero)
      {
         if (npcHero == null || Hero.MainHero == null || BastardBehavior.Instance == null) return null;

         int owed = BastardBehavior.Instance.OutstandingDemand(npcHero);

         if (owed <= 0)
            return ActionOutcome.Refused($"({npcHero.Name} holds no demand over you to settle.)");

         if (Hero.MainHero.Gold < owed)
            return ActionOutcome.Refused($"You cannot cover the {owed} denars {npcHero.Name} demands for her silence.");

         GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, npcHero, owed);
         bool silenced = BastardBehavior.Instance.SettleByPayment(npcHero, owed);
         ModLog.Info($"[ACTION] pay_blackmail: player paid {npcHero.Name} {owed} gold{(silenced ? " (silence bought)" : " (no demand settled)")}.");

         return new ActionOutcome(
            $"You pay {npcHero.Name} {owed} denars for her silence (purse: {Hero.MainHero.Gold}).{(silenced ? " Her silence is bought, for now." : "")}",
            false);
      }

      /// <summary>
      ///   turn_nemesis (Nemesis pillar increment 4, SPARE + TURN): mid a captor scene the player offers to
      ///   spare a tracked nemesis they hold prisoner and take him into their service, and the LLM recognized a
      ///   genuine acceptance. The prompt is advice, the bridge is law: this re-validates that the NPC really is
      ///   the nemesis the PLAYER currently holds, then re-runs the RATIFIED turn gate
      ///   (<see cref="NemesisTurnPolicy" />) on his live Fear/Respect, his grudge against the player, and his
      ///   traits. A refusal (fears you too little, respects you too little, too defiant, or hates you too
      ///   deeply) is honoured in character and changes nothing. On a genuine turn he is freed, sworn into the
      ///   player's clan and party, his vendetta closed, and the identity marked turned (never recurs as an
      ///   enemy captor) with a closure deed recorded (<see cref="Nemesis.NemesisResolutionBehavior.Turn" />).
      /// </summary>
      private static ActionOutcome ExecuteTurnNemesis(Hero npcHero, NpcProfile profile)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null) return null;

            Hero held = NemesisBehavior.Instance?.PlayerHeldNemesisPrisoner();

            if (held == null || held != npcHero)
               return ActionOutcome.Refused($"({npcHero.Name} is not a nemesis you hold prisoner to turn.)");

            NpcProfile p = profile ?? NpcMemoryCampaignBehavior.Instance?.Store?.Get(npcHero.StringId);

            if (p == null)
               return ActionOutcome.Refused($"({npcHero.Name} cannot be read well enough to judge whether he would turn.)");

            NpcStance stance = StanceService.ToStance(p);
            int honor = TraitLevelOf(npcHero, DefaultTraits.Honor);
            int valor = TraitLevelOf(npcHero, DefaultTraits.Valor);
            int calculating = TraitLevelOf(npcHero, DefaultTraits.Calculating);
            int grudge = NemesisGrudgeAgainstPlayer(npcHero);

            NemesisTurnVerdict verdict = NemesisTurnPolicy.Resolve(
               new NemesisTurnFacts {Fear = stance.Fear, Respect = stance.Respect, GrudgeMagnitude = grudge, Honor = honor, Valor = valor, Calculating = calculating});

            if (!verdict.CanTurn)
               return ActionOutcome.Refused($"({npcHero.Name} refuses to bend: he {TurnRefusalPhrase(verdict.Refusal)}.)");

            // He turns. Free him, swear him into the player's clan and party, and close the vendetta.
            Clan oldClan = npcHero.Clan;
            EndCaptivityAction.ApplyByReleasedByChoice(npcHero, Hero.MainHero);
            npcHero.Clan = Clan.PlayerClan;
            if (MobileParty.MainParty != null) AddHeroToPartyAction.Apply(npcHero, MobileParty.MainParty);

            // Identity + chronicle side (marks TurnedDay, clears his grudge against the player, records the deed).
            NemesisResolutionBehavior.Turn(npcHero);

            ModLog.Info($"[ACTION] turn_nemesis: {npcHero.Name} spared and turned (left {oldClan?.StringId ?? "?"}, sworn to the player's clan).");

            return new ActionOutcome(
               $"{npcHero.Name} bends the knee and swears himself to you. The hunter now rides at your side.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] turn_nemesis failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   recruit_prisoner (v1.34.3): a captured HERO the player holds prisoner has been persuaded, in
      ///   conversation, to switch sides and swear into the player's clan, and the LLM recognized a genuine
      ///   acceptance. Player report (Nexus): this exact turn was narrated but never happened, because no
      ///   executor existed for it (<see cref="ExecuteJoinClan" /> excludes prisoners; <see cref="ExecuteTurnNemesis" />
      ///   is reserved for a tracked bandit nemesis in a captor scene). Mirrors turn_nemesis for the
      ///   release+swear mechanic. The prompt is advice, the bridge is law: this re-runs the ratified gate
      ///   (<see cref="PrisonerRecruitmentPolicy" />) on LIVE facts, custody (held by the player's own party OR
      ///   a settlement the player's clan owns), leadership (a clan leader cannot dissolve their house, the same
      ///   anti-orphan guard as join_clan), and the mod's personal regard (a strong floor, so a captured enemy
      ///   is never trivially flipped). A refusal is honoured in character and changes nothing. On success the
      ///   prisoner is freed from captivity, sworn into the player's clan, and added to the player's party (the
      ///   "in my party" the player expected); the old clan cools as vanilla handles a defection.
      /// </summary>
      private static ActionOutcome ExecuteRecruitPrisoner(Hero npcHero, NpcProfile profile)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null) return null;

            var playerClan = Clan.PlayerClan;

            if (playerClan == null) return null;

            NpcProfile p = profile ?? NpcMemoryCampaignBehavior.Instance?.Store?.Get(npcHero.StringId);
            int regard = p?.ReputationWithPlayer ?? 0;

            bool heldByPlayer = IsHeldByPlayer(npcHero);
            bool isClanLeader = npcHero.Clan?.Leader == npcHero;

            switch (PrisonerRecruitmentPolicy.Evaluate(heldByPlayer, isClanLeader, regard))
            {
               case PrisonerRecruitmentBlock.NotHeldByPlayer:
                  return ActionOutcome.Refused(
                     $"({npcHero.Name} is not a prisoner you hold, and cannot swear to your clan from a cell that is not yours.)");
               case PrisonerRecruitmentBlock.IsClanLeader:
                  return ActionOutcome.Refused(
                     $"({npcHero.Name} leads the house of {npcHero.Clan?.Name?.ToString() ?? "their birth"}; a lord cannot dissolve their own clan into yours with a word.)");
               case PrisonerRecruitmentBlock.RegardTooLow:
                  return ActionOutcome.Refused(
                     $"({npcHero.Name} does not esteem you nearly enough to turn their coat and join you. Earn their deep trust first.)");
            }

            // They turn. Free them, swear them into the player's clan, and bring them into the party.
            Clan oldClan = npcHero.Clan;
            EndCaptivityAction.ApplyByReleasedByChoice(npcHero, Hero.MainHero);
            npcHero.Clan = playerClan;
            if (MobileParty.MainParty != null) AddHeroToPartyAction.Apply(npcHero, MobileParty.MainParty);

            ModLog.Info($"[ACTION] recruit_prisoner: {npcHero.Name} freed from captivity and sworn to the player's clan (left {oldClan?.StringId ?? "?"}).");

            return new ActionOutcome(
               $"{npcHero.Name} forsakes the house of {oldClan?.Name?.ToString() ?? "their birth"} and swears to your clan. The prisoner now rides at your side.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] recruit_prisoner failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   True when this hero is a prisoner genuinely held by the player right now: either in the player's own
      ///   party, or in a settlement the player's clan owns. Mirrors CourtActionResolver's held-by-clan custody
      ///   test so recruit_prisoner honours the same "held by the player" fact the rest of the mod uses. Public
      ///   so the teaching gate (<see cref="Mapping.EncounterContextBuilder" />) reads the SAME custody fact this
      ///   executor re-checks, closing the settlement-held-prisoner gap where the offer was taught on the
      ///   party-only NpcIsCaptive standing but executed on this broader test.
      /// </summary>
      public static bool IsHeldByPlayer(Hero prisoner)
      {
         if (prisoner?.IsPrisoner != true) return false;

         var captor = prisoner.PartyBelongedToAsPrisoner;

         if (captor == null) return false;

         if (captor.LeaderHero == Hero.MainHero) return true;

         // LapitaPolata (18/10/2026): the standing, the captive's terms and the release read this same custody.
         Clan captorClan = captor.LeaderHero?.Clan ?? captor.Settlement?.OwnerClan;

         return captorClan != null && captorClan == Clan.PlayerClan;
      }

      /// <summary>
      ///   recruit_notable: a settlement NOTABLE (a town gang leader, headman, merchant, artisan, rural notable
      ///   or preacher) has been persuaded, in conversation, to leave their post and swear into the player's
      ///   clan as a COMPANION. Two author calls are honoured here: (1) the notable BEQUEATHS their gang/role to
      ///   one of their own people in the SAME settlement, who is promoted so the town keeps a notable of that
      ///   kind (never left vacant); (2) a starting RELATION BONUS is applied to represent the standing the
      ///   notable brings from their gang. The prompt is advice, the bridge is law: the shared gate
      ///   (<see cref="NotableRecruitmentPolicy" />) is re-run on LIVE facts (is a notable, alive, free, not
      ///   already the player's, deep regard, and a real successor exists), and a refusal changes nothing.
      ///   ORDERING (all-or-nothing as far as practical): the succession is attempted FIRST and must succeed
      ///   before the notable joins, so a failed succession never leaves the recruit half-joined; only once the
      ///   town has its replacement notable is the departing hero converted to a companion (occupation set to
      ///   Wanderer so they read as a companion, not a notable, then <see cref="AddCompanionAction" /> +
      ///   <see cref="AddHeroToPartyAction" />, which also removes them from the settlement's notable list).
      ///
      ///   LIMITS (documented): the successor is an EXISTING hero of the same settlement (a non-notable commoner
      ///   when one is present, so no other post is disturbed; otherwise a fellow notable, whose own former
      ///   specialty the engine's periodic notable spawn refills over time). A gang leader's alleys and any open
      ///   issue are transferred to the successor; individual gang-member rosters are not modelled by the engine
      ///   as heroes, so "the gang" is represented by the alleys, the role, and the standing (Power) that pass
      ///   to the successor.
      /// </summary>
      /// <summary>
      ///   take_into_service (13/09/2026, Gabriel's ruling "un seigneur doit pouvoir recruter"): an NPC LORD
      ///   takes one of the player's own companions into their household. The companion leaves the player's
      ///   clan and party and joins the lord's clan and party.
      ///   <para>
      ///     THE ONLY VERB IN THE GAME THAT POINTS OUTWARD. recruit_notable, join_clan, join_party and
      ///     recruit_prisoner all bring somebody IN; this one gives somebody away, which is why its gates are
      ///     about the player's household (a spouse, a governor, someone away on an errand) rather than about
      ///     the lord's regard. Whether she WANTS him is hers to play in prose; whether the player can be
      ///     parted from him is law.
      ///   </para>
      ///   <para>
      ///     Built because the absence of it silently ate a scene: Ira agreed to take Nabb under her banner,
      ///     the model reached for the nearest verb it could see (recruit_notable, which refuses because she
      ///     is a lord and not a settlement notable), and the whole payoff evaporated with Nabb still in the
      ///     party.
      ///   </para>
      ///   <para>
      ///     The companion is named in the action's <c>target</c> field, which a lord taking the man in front
      ///     of her very often omits - so a blank target resolves to the single companion present, and to a
      ///     refusal when several are (see <see cref="CompanionServiceTransferPolicy.PickCompanion" />).
      ///   </para>
      ///   <para>
      ///     REMOVAL DETAIL MATTERS. RemoveCompanionAction.ApplyByFire runs MakeHeroFugitiveAction, which
      ///     would leave the hero a fugitive we then try to seat in a household. ApplyAfterQuest is the
      ///     neutral departure, which is what an honourable transfer is. Verified by reflection against the
      ///     shipped TaleWorlds.CampaignSystem.dll (the four Apply* overloads it actually exposes).
      ///   </para>
      /// </summary>
      private static ActionOutcome ExecuteTakeIntoService(GameAction action, Hero npcHero)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null) return null;

            Clan playerClan = Clan.PlayerClan;
            if (playerClan == null) return null;

            string spoken = null;
            action?.Parameters?.TryGetValue("target", out spoken);

            var candidates = new List<CompanionCandidate>();
            foreach (Hero h in HouseholdOfferableTo(playerClan))
               candidates.Add(new CompanionCandidate {HeroId = h.StringId, Name = h.Name?.ToString()});

            string chosenId = CompanionServiceTransferPolicy.PickCompanion(candidates, spoken);
            Hero companion = string.IsNullOrEmpty(chosenId)
               ? null
               : Hero.FindFirst(h => h != null && h.StringId == chosenId);

            Clan lordClan = npcHero.Clan;

            var facts = new CompanionServiceTransferFacts {
               LordIsAlive = npcHero.IsAlive,
               LordIsPrisoner = npcHero.IsPrisoner,
               LordIsThePlayer = npcHero == Hero.MainHero,
               LordHasClan = lordClan != null,
               LordClanIsPlayerClan = lordClan == playerClan,
               CompanionResolved = companion != null,
               CompanionIsAlive = companion?.IsAlive == true,
               CompanionIsPrisoner = companion?.IsPrisoner == true,
               CompanionIsInYourHousehold = companion != null && companion.Clan == playerClan,
               CompanionLeadsThePlayersClan = companion != null
                                           && (companion == Hero.MainHero || companion == playerClan.Leader),
               CompanionIsPlayerFamily = companion != null && IsPlayersFamily(companion),
               CompanionIsPlayerSpouse = companion != null && companion == Hero.MainHero.Spouse,
               CompanionAwayOnErrand = companion != null && CompanionMissionBehavior.Instance?.IsAway(companion.StringId) == true,
               CompanionGovernsPlayerFief = companion?.GovernorOf != null
            };

            switch (CompanionServiceTransferPolicy.Evaluate(facts))
            {
               case CompanionServiceTransferBlock.LordUnavailable:
                  return null;
               case CompanionServiceTransferBlock.LordHasNoHouse:
                  return ActionOutcome.Refused($"({npcHero.Name} holds no household of their own to take anyone into.)");
               case CompanionServiceTransferBlock.LordIsOfThePlayersOwnClan:
                  return ActionOutcome.Refused($"({npcHero.Name} is of your own clan; nobody would change hands.)");
               case CompanionServiceTransferBlock.NoCompanionNamed:
                  return ActionOutcome.Refused(candidates.Count > 1
                                                  ? "(Name which of your companions is to take service, and it will be done.)"
                                                  : "(You have no companion here to give over.)");
               case CompanionServiceTransferBlock.NotYourCompanion:
                  return ActionOutcome.Refused($"({companion?.Name} is not of your household to give over.)");
               case CompanionServiceTransferBlock.CompanionLeadsYourHouse:
                  return ActionOutcome.Refused($"({companion?.Name} leads your house, and is nobody's to give.)");
               // "Family", not "your own blood": this now refuses a daughter-in-law too, and telling the player she is
               // their own blood would be the one false sentence in an otherwise correct refusal.
               case CompanionServiceTransferBlock.CompanionIsYourFamily:
                  return ActionOutcome.Refused($"({companion?.Name} is family; placing kin in another house is not a term of service.)");
               case CompanionServiceTransferBlock.CompanionUnavailable:
                  return ActionOutcome.Refused($"({companion?.Name} is in no condition to take service with anyone.)");
               case CompanionServiceTransferBlock.CompanionIsYourSpouse:
                  return ActionOutcome.Refused($"({companion?.Name} is your own spouse; a marriage is not a term of service.)");
               case CompanionServiceTransferBlock.CompanionAwayOnErrand:
                  return ActionOutcome.Refused($"({companion?.Name} is away on an errand; recall them before giving them over.)");
               case CompanionServiceTransferBlock.CompanionGovernsAFief:
                  return ActionOutcome.Refused($"({companion?.Name} governs {companion?.GovernorOf?.Name}; appoint another governor first.)");
            }

            // ── the transfer proper ───────────────────────────────────────────────────────────────────
            // AfterQuest, never ByFire: ByFire makes them a FUGITIVE, and a fugitive cannot be seated in a
            // household. This is an honourable departure on their lord's own word.
            RemoveCompanionAction.ApplyAfterQuest(playerClan, companion);
            AddCompanionAction.Apply(lordClan, companion);

            // Into her party when she has one. A lord between parties (in a settlement, in a garrison) still
            // takes the companion into her HOUSE; the engine will place them with her clan in the ordinary way.
            MobileParty lordParty = npcHero.PartyBelongedTo;
            if (lordParty != null) AddHeroToPartyAction.Apply(companion, lordParty, false);

            // NO RELATION BONUS, and the reason is worth keeping. The first version warmed the new lord to
            // the companion "so they arrive on a recommendation, not as a stranger". cr.test all on Gabriel's
            // live campaign said the relation had not moved a point: a change between two heroes who are by
            // then in the SAME clan does not take. It could have been applied before the move instead - but
            // this project's own rule is that the ledger which matters is NpcProfile.ReputationWithPlayer,
            // and a vanilla relation between a lord and their own clan companion is read by nothing here.
            // A cosmetic poke the engine ignores is worse than none: it fails a test for something no player
            // can feel. Warming the LORD toward the PLAYER for the favour was considered and refused too -
            // progressive regard owns that, and change_relation is its verb.

            string where = lordParty != null ? $" and rides with {lordParty.Name}" : "";
            ModLog.Info($"[ACTION] take_into_service: {companion.Name} left the player's clan for "
                      + $"{lordClan.Name} at {npcHero.Name}'s word{where}.");

            var msg = $"{companion.Name} takes service with {npcHero.Name} and leaves your clan.";
            MBInformationManager.AddQuickInformation(new TextObject(msg), 0, companion.CharacterObject);

            return new ActionOutcome(msg, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] take_into_service failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   Everyone in the player's HOUSEHOLD a lord could be offered: the clan's heroes, less the ones nobody
      ///   may give away. Deliberately Clan.Heroes and not Clan.Companions - the mod's own turn_nemesis and
      ///   recruit_prisoner swear a hero in by assigning Hero.Clan directly, which never sets CompanionOf, so
      ///   IsPlayerCompanion stays false for exactly the retainers a player is proudest of (Gabriel presented a
      ///   turned nemesis as "my Master of Arms" and the verb was never even taught).
      /// </summary>
      private static IEnumerable<Hero> HouseholdOfferableTo(Clan playerClan)
      {
         if (playerClan?.Heroes == null) yield break;

         // Computed ONCE for the whole walk: the family tree does not change between one hero and the next.
         ISet<string> family = PlayerFamilyIds();

         foreach (Hero h in playerClan.Heroes)
         {
            if (!Mapping.ActionGate.Allows(h, Logic.ActionKind.EnterService)) continue; // Clan.Heroes keeps the dead and the disabled
            if (h == Hero.MainHero || h == playerClan.Leader) continue;
            if (h == Hero.MainHero?.Spouse) continue;
            if (h.StringId != null && family.Contains(h.StringId)) continue;
            if (h.GovernorOf != null) continue;
            if (CompanionMissionBehavior.Instance?.IsAway(h.StringId) == true) continue;

            yield return h;
         }
      }

      /// <summary>The teaching gate (EncounterContextBuilder) runs the same family rule; one copy, no drift.</summary>
      public static bool IsPlayersFamilyForGate(Hero hero) => IsPlayersFamily(hero);

      /// <summary>
      ///   Whether <paramref name="hero" /> is the player's family, per <see cref="HouseholdKinPolicy" />: blood kin
      ///   within three generations, or married to one of them. Kin are placed in another house by fosterage, not
      ///   service. It used to be parents, children and siblings only (reminensce, 19/09/2026).
      /// </summary>
      private static bool IsPlayersFamily(Hero hero)
         => hero?.StringId != null && Hero.MainHero != null && PlayerFamilyIds().Contains(hero.StringId);

      /// <summary>
      ///   The player's family as a set of hero ids, from the engine's own tree. Every clan hero and the player are
      ///   walked up to the policy's reach, dead forebears included, because a cousin is only a cousin through a
      ///   grandparent who may be long in the ground. Fail-soft to "nobody but the player": a tree that cannot be
      ///   read must never make a retainer look like family, since that would disable the verb, but must never
      ///   make family look offerable either, which is why the player's spouse keeps its own separate check.
      /// </summary>
      private static ISet<string> PlayerFamilyIds()
      {
         try
         {
            Hero player = Hero.MainHero;
            if (player?.StringId == null) return new HashSet<string>();

            var records = new Dictionary<string, KinRecord>();
            var reachedAt = new Dictionary<string, int>();

            void Walk(Hero h, int depthLeft)
            {
               if (h?.StringId == null) return;
               if (reachedAt.TryGetValue(h.StringId, out int seen) && seen >= depthLeft) return;

               reachedAt[h.StringId] = depthLeft;
               records[h.StringId] = new KinRecord {
                  Id = h.StringId,
                  FatherId = h.Father?.StringId,
                  MotherId = h.Mother?.StringId,
                  SpouseId = h.Spouse?.StringId
               };

               if (depthLeft <= 0) return;

               Walk(h.Father, depthLeft - 1);
               Walk(h.Mother, depthLeft - 1);
            }

            Walk(player, HouseholdKinPolicy.GenerationsUp);

            if (Clan.PlayerClan?.Heroes != null)
               foreach (Hero h in Clan.PlayerClan.Heroes)
                  Walk(h, HouseholdKinPolicy.GenerationsUp);

            return HouseholdKinPolicy.FamilyOf(player.StringId, records.Values);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] could not read the player's family tree: {ex.GetType().Name}: {ex.Message}");

            return new HashSet<string>();
         }
      }

      private static ActionOutcome ExecuteRecruitNotable(Hero npcHero, NpcProfile profile)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null) return null;

            var playerClan = Clan.PlayerClan;

            if (playerClan == null) return null;

            NpcProfile p = profile ?? NpcMemoryCampaignBehavior.Instance?.Store?.Get(npcHero.StringId);
            int regard = p?.ReputationWithPlayer ?? 0;

            Settlement settlement = npcHero.CurrentSettlement;
            Hero successor = FindNotableSuccessor(npcHero);

            switch (NotableRecruitmentPolicy.Evaluate(new NotableRecruitmentPolicy.NotableRecruitmentFacts {IsNotable = npcHero.IsNotable, IsAlive = npcHero.IsAlive, IsPrisoner = npcHero.IsPrisoner, IsPlayerClanAlready = npcHero.Clan == playerClan, Regard = regard, HasEligibleSuccessor = successor != null}))
            {
               case NotableRecruitmentBlock.NotANotable:
                  return ActionOutcome.Refused(
                     $"({npcHero.Name} is not a settlement notable, and cannot hand over a post they do not hold.)");
               case NotableRecruitmentBlock.NotAlive:
                  return null;
               case NotableRecruitmentBlock.IsPrisoner:
                  return ActionOutcome.Refused(
                     $"({npcHero.Name} is a captive; a prisoner cannot bequeath a post and walk free to your side.)");
               case NotableRecruitmentBlock.AlreadyPlayerClan:
                  return ActionOutcome.Refused($"({npcHero.Name} already rides with your clan.)");
               case NotableRecruitmentBlock.NoEligibleSuccessor:
                  return ActionOutcome.Refused(
                     $"({npcHero.Name} will not leave their people leaderless; there is no one here to take up the gang after them.)");
               case NotableRecruitmentBlock.RegardTooLow:
                  return ActionOutcome.Refused(
                     $"({npcHero.Name} does not esteem you nearly enough to give up their standing and follow you. Earn their deep trust first.)");
            }

            Occupation role = npcHero.Occupation;

            // Succession FIRST: if the role cannot be handed on, the recruit does NOT join (no half-join).
            if (!TryBequeathNotableRole(npcHero, successor, settlement, role))
               return ActionOutcome.Refused(
                  $"({npcHero.Name} would not leave until their people were provided for, and that could not be arranged.)");

            // Now convert the departing notable and bring them in. Occupation to Wanderer so they read as a
            // companion, not a lingering notable; AddHeroToPartyAction nulls StayingInSettlement, which drops
            // them from the settlement's notable cache.
            npcHero.SetNewOccupation(Occupation.Wanderer);
            AddCompanionAction.Apply(playerClan, npcHero);
            if (MobileParty.MainParty != null) AddHeroToPartyAction.Apply(npcHero, MobileParty.MainParty);

            // The standing the notable brings from their gang: they arrive an ally, not a stranger.
            ChangeRelationAction.ApplyPlayerRelation(npcHero, NotableRecruitmentPolicy.StartingRelationBonus, false, false);

            ModLog.Info(
               $"[ACTION] recruit_notable: {npcHero.Name} left their post in {settlement?.Name?.ToString() ?? "?"} " +
               $"(role {role} -> {successor.Name}) and joined the player's clan + party; starting relation +{NotableRecruitmentPolicy.StartingRelationBonus}.");

            return new ActionOutcome(
               $"{npcHero.Name} hands the gang to {successor.Name} and takes service in your clan, riding at your side.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] recruit_notable failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   Picks a plausible successor for a departing notable: an EXISTING hero staying in the SAME settlement
      ///   (one of their own people) who is free to take up the role. Prefers a non-notable commoner (a clean
      ///   promotion that leaves no other post disturbed); failing that, the strongest fellow notable. Null when
      ///   nobody in the settlement can inherit, which is the <see cref="NotableRecruitmentPolicy" />'s
      ///   no-successor refusal. Public so the teaching gate (<see cref="Mapping.EncounterContextBuilder" />)
      ///   and this executor share ONE succession decision and cannot drift.
      /// </summary>
      public static Hero FindNotableSuccessor(Hero departing)
      {
         Settlement settlement = departing?.CurrentSettlement;

         if (settlement == null) return null;

         bool Eligible(Hero h)
            => h != null && h != departing && h != Hero.MainHero && h.IsAlive && !h.IsChild && !h.IsPrisoner
               && !h.IsPlayerCompanion && h.Clan != Clan.PlayerClan && !h.IsLord && h.PartyBelongedTo == null;

         List<Hero> pool = settlement.HeroesWithoutParty.Where(Eligible).ToList();

         if (pool.Count == 0) return null;

         Hero commoner = pool.Where(h => !h.IsNotable).OrderByDescending(h => h.Power).FirstOrDefault();

         return commoner ?? pool.Where(h => h.IsNotable).OrderByDescending(h => h.Power).FirstOrDefault();
      }

      /// <summary>
      ///   Hands the departing notable's role to the chosen successor, mirroring the engine's own death-succession
      ///   (<c>NotablesCampaignBehavior.ChangeDeadNotable</c>): the gang leader's alleys and any open issue pass
      ///   to the successor, then the successor is promoted into the vacated role and (re)seated as a notable of
      ///   the settlement so the town keeps a notable of that kind. Returns true only when the succession is
      ///   verifiably in place (the successor is a notable and now listed among the settlement's notables), which
      ///   is the guarantee the executor requires before it lets the departing notable leave. Any failure is
      ///   caught and reported as false, so the recruit is refused rather than left half-joined.
      /// </summary>
      private static bool TryBequeathNotableRole(Hero departing, Hero successor, Settlement settlement, Occupation role)
      {
         if (successor == null || settlement == null) return false;

         try
         {
            // The gang's holdings: a gang leader owns alleys. SetOwner maintains both heroes' alley lists.
            if (departing.OwnedAlleys != null)
               foreach (Alley alley in departing.OwnedAlleys.ToList())
                  alley.SetOwner(successor);

            // Any open issue the notable was hosting moves to the successor (same as ChangeDeadNotable).
            if (departing.Issue != null)
               Campaign.Current?.IssueManager?.ChangeIssueOwner(departing.Issue, successor);

            // Promote + reseat: null then re-enter forces the settlement's notable cache to recompute with the
            // successor's new (notable) occupation, whether or not they were already a notable.
            successor.StayingInSettlement = null;
            successor.SetNewOccupation(role);
            if (successor.Power < departing.Power) successor.AddPower(departing.Power - successor.Power);
            EnterSettlementAction.ApplyForCharacterOnly(successor, settlement);

            return successor.IsNotable && settlement.Notables.Contains(successor);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] recruit_notable succession failed: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }

      /// <summary>
      ///   Builds the candidate list <see cref="GiftNameResolver" /> matches a narrated free-text gift word
      ///   against: every roster item with a positive amount and a positive value (mirrors
      ///   <see cref="Items.ItemPickerService" />'s own eligibility, so a quest item or a valueless prop is
      ///   never offered as a fuzzy match), with its category normalized to a lower-case token for the
      ///   synonym tier (the Jewelry trade good's own ItemCategory StringId is "jewelry").
      /// </summary>
      private static List<GiftCandidate> BuildGiftCandidates()
      {
         var list = new List<GiftCandidate>();
         if (MobileParty.MainParty?.ItemRoster == null) return list;

         foreach (ItemRosterElement el in MobileParty.MainParty.ItemRoster)
         {
            if (el.Amount <= 0) continue;
            ItemObject item = el.EquipmentElement.Item;

            if (item == null || item.Value <= 0) continue;

            string category = item.ItemCategory?.StringId?.ToLowerInvariant();
            list.Add(new GiftCandidate {Id = Items.RosterGoods.KeyOf(el.EquipmentElement), Name = Items.RosterGoods.NameOf(el.EquipmentElement), Category = category, Value = Items.RosterGoods.ValueOf(el.EquipmentElement)});
         }

         return list;
      }

      private static ItemRosterElement FindItemInRoster(string itemName)
      {
         if (MobileParty.MainParty?.ItemRoster == null) return default;
         List<ItemRosterElement> held = MobileParty.MainParty.ItemRoster.Where(e => e.Amount > 0 && e.EquipmentElement.Item != null).ToList();
         foreach (ItemRosterElement el in held)
            if (string.Equals(Items.RosterGoods.NameOf(el.EquipmentElement), itemName, StringComparison.OrdinalIgnoreCase))
               return el;

         // The bare item name ("Noble Bow" when the party holds a Masterwork one): the plain piece first.
         foreach (ItemRosterElement el in held.OrderBy(e => e.EquipmentElement.ItemModifier == null ? 0 : 1))
            if (string.Equals(el.EquipmentElement.Item.Name.ToString(), itemName, StringComparison.OrdinalIgnoreCase))
               return el;

         return default;
      }

      /// <summary>
      ///   marriage_consent: the conditioning deed is verified done — record the family's blessing
      ///   for the intended spouse (the consequence resolver later honors it as an alliance when the
      ///   wedding happens). The spouse was validated at issuance; we re-resolve them by id here.
      /// </summary>
      private static ActionOutcome GrantMarriageConsent(Hero npcHero, InformalQuest quest)
      {
         if (MarriageApprovalBehavior.Instance == null)
            return ActionOutcome.Refused($"({npcHero.Name} cannot formalize the betrothal just now.)");

         Hero spouse = string.IsNullOrEmpty(quest.MarriageSpouseId)
            ? null
            : Hero.FindFirst(h => h.StringId == quest.MarriageSpouseId);

         if (spouse == null)
            return ActionOutcome.Refused($"({npcHero.Name}: the match can no longer be honored.)");

         // M-B4: the quest may complete long after it was issued. Re-validate at the deed, not just at issue, so a
         // blessing is never recorded on a spouse who has since died or wed another, nor from a blesser who has
         // since died, been captured, or gone to war. The regard read is the mod's own personal ledger.
         if (!MarriageProspectResolver.IsMarriageable(Hero.MainHero, spouse))
            return ActionOutcome.Refused($"({spouse.Name} can no longer be wed to you.)");

         int blesserRegard = NpcMemoryCampaignBehavior.Instance?.Store?.Get(npcHero.StringId)?.ReputationWithPlayer
                             ?? npcHero.GetRelation(Hero.MainHero);
         if (!MarriageGuardPolicy.BlessingAllowed(new MarriageGuardPolicy.BlesserFacts {BlesserAlive = npcHero.IsAlive, BlesserPrisoner = npcHero.IsPrisoner, AtWarWithBlesser = IsAtWarWithPlayer(npcHero), BlesserRegard = blesserRegard}))
            return ActionOutcome.Refused($"({npcHero.Name} can no longer give that blessing.)");

         var day = (int) CampaignTime.Now.ToDays;
         MarriageApprovalBehavior.Instance.Grant(spouse, npcHero, day);
         ModLog.Info($"[GRANT] marriage_consent: {npcHero.Name} blesses the player's marriage to {spouse.Name} (deed done).");

         return new ActionOutcome(
            $"{npcHero.Name} gives their blessing — you have earned {spouse.Name}'s hand. Their house stands with you.", true);
      }

      /// <summary>
      ///   COUNCIL_ACTIONS.md Partie 5 (the "Caladog" case): pays out a RewardGrant.GrantFief quest. Re-reads
      ///   every precondition live (never trusted from issuance, weeks may have passed) through the shared
      ///   pure gate <see cref="QuestRewardGatePolicy.DecideFiefPayout" />, then delivers via the same
      ///   ApplyByKingDecision detail the council's own grant_fief/revoke_fief executors already use
      ///   (CalradiaRemembers.Chat.CouncilLift), only in the opposite direction: FROM the giver's own clan TO
      ///   the player. A failed re-check never vanishes silently: <see cref="DegradeHeavyReward" /> records
      ///   the debt and offers a modest consolation.
      /// </summary>
      private static ActionOutcome GrantFiefReward(Hero npcHero, NpcProfile profile, InformalQuest quest)
      {
         Settlement fief = string.IsNullOrEmpty(quest.PromisedSettlementId)
            ? null
            : Settlement.All?.FirstOrDefault(s => s.StringId == quest.PromisedSettlementId);

         bool giverAlive = npcHero is {IsAlive: true} && !npcHero.IsPrisoner;
         bool giverLeadsOwnClan = giverAlive && npcHero.Clan != null && npcHero.Clan.Leader == npcHero;
         bool fiefStillOwned = fief != null && (fief.IsTown || fief.IsCastle) && fief.OwnerClan == npcHero.Clan;
         bool atWar = IsAtWarWithPlayer(npcHero);

         QuestRewardPayoutVerdict verdict = QuestRewardGatePolicy.DecideFiefPayout(new QuestRewardGatePolicy.FiefRewardFacts {ToggleOn = ModSettings.AllowFiefAndMarriageQuestRewards, GiverAlive = giverAlive, GiverLeadsOwnClan = giverLeadsOwnClan, FiefStillOwnedByGiverClan = fiefStillOwned, PlayerAtWarWithGiverClan = atWar});

         if (verdict != QuestRewardPayoutVerdict.Deliver)
            return DegradeHeavyReward(npcHero, profile, $"the fief of {quest.PromisedSettlementName ?? "the promised land"}", atWar);

         // v1.34.2 governance hardening (finding 3): release the giver's own governor before the fief passes to
         // the player, or that hostile hero would keep governing a fief that is no longer theirs. Same shared
         // helper the grant and revoke paths use.
         CourtActionResolver.ReleaseSittingGovernor(fief);

         ChangeOwnerOfSettlementAction.ApplyByKingDecision(Hero.MainHero, fief);
         ModLog.Info($"[GRANT] grant_fief: {fief.Name} passes from {npcHero.Name}'s house to the player (quest reward honored).");

         return new ActionOutcome($"{npcHero.Name} makes good on their word: {fief.Name} passes to you.", true);
      }

      /// <summary>
      ///   COUNCIL_ACTIONS.md Partie 5 (Kimi review "Trou 2"): pays out a RewardGrant.MarriageReward quest by
      ///   sealing the ACTUAL union directly (MarriageAction.Apply), unlike the lighter MarriageConsent grant
      ///   above, which only records a blessing the player must still separately act on. Re-checks every
      ///   precondition live through <see cref="QuestRewardGatePolicy.DecideMarriageRewardPayout" />, including
      ///   the sovereignty guard again (the player's own standing may have changed since the promise).
      /// </summary>
      private static ActionOutcome GrantMarriageRewardDirect(Hero npcHero, NpcProfile profile, InformalQuest quest)
      {
         Hero spouse = string.IsNullOrEmpty(quest.MarriageSpouseId)
            ? null
            : Hero.FindFirst(h => h.StringId == quest.MarriageSpouseId);

         bool giverAlive = npcHero is {IsAlive: true} && !npcHero.IsPrisoner;
         bool hasAuthority = giverAlive && spouse != null && MarriageProspectResolver.HasAuthority(npcHero, spouse);
         bool marriageable = spouse != null && MarriageProspectResolver.IsMarriageable(Hero.MainHero, spouse);
         bool atWar = IsAtWarWithPlayer(npcHero);
         bool playerReigns = Clan.PlayerClan?.Kingdom != null && Clan.PlayerClan.Kingdom.Leader == Hero.MainHero;

         QuestRewardPayoutVerdict verdict = QuestRewardGatePolicy.DecideMarriageRewardPayout(new QuestRewardGatePolicy.MarriageRewardFacts {ToggleOn = ModSettings.AllowFiefAndMarriageQuestRewards, GiverAlive = giverAlive, GiverHasAuthorityOverSpouse = hasAuthority, SpouseStillMarriageable = marriageable, PlayerAtWarWithGiverClan = atWar, PlayerIsReigningSovereign = playerReigns});

         if (verdict != QuestRewardPayoutVerdict.Deliver)
            return DegradeHeavyReward(npcHero, profile, $"a marriage with {quest.MarriageSpouseName ?? "the promised spouse"}", atWar);

         MarriageApprovalBehavior.MarryThroughCr(Hero.MainHero, spouse);
         ModLog.Info($"[GRANT] marriage_reward: player wed {spouse.Name}, sealed on {npcHero.Name}'s own word (quest reward honored).");

         return new ActionOutcome($"{npcHero.Name} keeps their word: you and {spouse.Name} are wed.", true);
      }

      /// <summary>
      ///   COUNCIL_ACTIONS.md Partie 5 (graceful degradation, v1 scope): when a promised fief or marriage
      ///   cannot be honored at payout, this is NEVER a silent nothing. Records a permanent debt memory (it
      ///   colors future conversation and can resurface in negotiation) and offers a modest, fixed gold and
      ///   relation consolation, a small apology, never a substitute for the lost fief or match. Continuous
      ///   surveillance that would catch the break the MOMENT it happens (rather than only discovering it
      ///   here, at payout) is a documented FOLLOW-UP: this method only covers the payout-time half of the
      ///   promise's lifecycle.
      /// </summary>
      private static ActionOutcome DegradeHeavyReward(Hero npcHero, NpcProfile profile, string promiseLabel, bool atWar)
      {
         const int compensationGold = 500;
         const int compensationRelation = 5;

         var day = (int) CampaignTime.Now.ToDays;
         string cause = atWar
            ? "the two of you are now at war"
            : "the world has since moved against it";

         profile?.Events.Add(new NotableEvent(day, NotableEventType.Conflict,
            $"Could not honor the promise of {promiseLabel}: {cause}. The debt remains unpaid."));

         if (npcHero is {IsAlive: true}) GiveGoldAction.ApplyBetweenCharacters(npcHero, Hero.MainHero, compensationGold, true);
         if (profile != null) ProfileMutator.ApplyReputationDelta(profile, compensationRelation, NpcMemoryService.Core.Standing.RegardCause.Compensation);

         ModLog.Info($"[GRANT] heavy reward could not be honored for {npcHero?.Name} ({promiseLabel}): {cause}. " +
                     $"Compensated {compensationGold}g/+{compensationRelation} regard.");

         return new ActionOutcome(
            $"({npcHero?.Name} cannot honor the promise of {promiseLabel}: {cause}. They press {compensationGold} " +
            "denars into your hand as amends, but the debt is not forgotten.)", false);
      }

      /// <summary>The magnitude of the heaviest LIVE grudge this hero holds against the player, or 0 when none.</summary>
      private static int HeaviestGrudgeAgainstPlayer(Hero hero)
      {
         IReadOnlyList<Grudge> grudges = GrudgeBehavior.Instance?.GrudgesFor(hero?.StringId);

         if (grudges == null) return 0;

         string playerId = Hero.MainHero?.StringId;
         var max = 0;
         foreach (Grudge g in grudges)
            if (!g.IsResolved && g.TargetId == playerId && g.Magnitude > max)
               max = g.Magnitude;

         return max;
      }

      /// <summary>The deepest grudge the nemesis holds specifically against the player (0..100), or 0 when none.</summary>
      private static int NemesisGrudgeAgainstPlayer(Hero nemesis)
      {
         try
         {
            IReadOnlyList<Grudge> grudges = GrudgeBehavior.Instance?.GrudgesFor(nemesis.StringId);

            if (grudges == null) return 0;

            var max = 0;
            foreach (Grudge g in grudges)
               if (g != null && string.Equals(g.TargetId, Hero.MainHero?.StringId, StringComparison.Ordinal) && g.Magnitude > max)
                  max = g.Magnitude;

            return max;
         }
         catch { return 0; }
      }

      private static void PlaySound(string eventPath)
      {
         if (string.IsNullOrWhiteSpace(eventPath)) return;
         try
         {
            // PlaySound2D is the correct API for UI/2D sounds — it handles
            // non-positional playback without requiring a scene context.
            bool played = SoundEvent.PlaySound2D(eventPath);
            ModLog.Info($"[SOUND] PlaySound2D('{eventPath}') = {played}.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SOUND] PlaySound failed for '{eventPath}': {ex.Message}");
         }
      }

      /// <summary>
      ///   Re-validates a captor's willingness to free the player via the pure <see cref="CaptorReleasePolicy" />,
      ///   reading the captor's Mercy and the heaviest live grudge they hold against the player. Mirrors the
      ///   advice-side gate in <c>EncounterContextBuilder</c> so the prompt and the bridge cannot disagree.
      /// </summary>
      private static CaptorReleaseVerdict ResolveCaptorReleaseVerdict(Hero captor)
      {
         int mercy = TraitLevelOf(captor, DefaultTraits.Mercy);
         int grudge = HeaviestGrudgeAgainstPlayer(captor);

         return CaptorReleasePolicy.Resolve(mercy, grudge, false);
      }

      /// <summary>
      ///   Stance mechanical behaviour-gating (increment 1, Fear): reads the captive's LIVE Fear/Respect
      ///   from the persisted profile and traits from the live Hero, then resolves
      ///   <see cref="StanceGatePolicy.ResolveCoercion" />. A MISSING profile (no stance data at all —
      ///   the console self-test seam, or a captive the mod has never tracked) deliberately skips the
      ///   gate (<see cref="CoercionOutcome.Neutral" />, unchanged bargain): defaulting an untracked
      ///   captive's Fear/Respect to 0/0 would otherwise satisfy the RefusesFlat thresholds by construction
      ///   and block every ordinary release the gate has no real basis to judge.
      /// </summary>
      private static CoercionResult ResolveFreePrisonerCoercion(Hero npcHero, ActionContext context)
      {
         NpcProfile profile = context?.Profile;

         if (profile == null) return new CoercionResult(CoercionOutcome.Neutral, 1.0, 0);

         NpcStance stance = StanceService.ToStance(profile);

         int honor = TraitLevelOf(npcHero, DefaultTraits.Honor);
         int valor = TraitLevelOf(npcHero, DefaultTraits.Valor);
         int calculating = TraitLevelOf(npcHero, DefaultTraits.Calculating);

         return StanceGatePolicy.ResolveCoercion(new StanceGatePolicy.CoercionFacts {Fear = stance.Fear, Respect = stance.Respect, Honor = honor, Valor = valor, Calculating = calculating});
      }

      private static List<Kingdom> RetrieveAllKingdoms()
      {
         var kingdoms = new List<Kingdom>();
         foreach (Kingdom k in Kingdom.All)
            if (!k.IsEliminated)
               kingdoms.Add(k);

         return kingdoms;
      }

      /// <summary>A hero's trait level, or 0 when the hero or the trait cannot be read (never throws).</summary>
      private static int TraitLevelOf(Hero hero, TraitObject trait)
      {
         try { return hero?.GetTraitLevel(trait) ?? 0; }
         catch { return 0; }
      }

      /// <summary>The game's own hiring price for this wanderer, never below 1.</summary>
      private static int AskingHirePrice(Hero npcHero)
      {
         int asking = npcHero == null || Campaign.Current == null ? 0 : Campaign.Current.Models.CompanionHiringPriceCalculationModel.GetCompanionHiringPrice(npcHero);

         return asking <= 0 ? 1 : asking;
      }

      /// <summary>
      ///   The denars a join_party will charge for <paramref name="proposed" />: the price named, or the game's own hiring
      ///   price when none was. Енот Василий (Discord, 11/11/2026): the confirmation said "for 0 coins" and the hire then
      ///   charged the full price; the question now names this figure, the one that is paid.
      /// </summary>
      internal static int HirePriceToCharge(int proposed, Hero npcHero) => HireAgreementPolicy.AgreedPrice(proposed, AskingHirePrice(npcHero));

      private static bool TryGetInt(IReadOnlyDictionary<string, string> parameters, string key, out int value)
      {
         value = 0;

         return parameters.TryGetValue(key, out string raw) && int.TryParse(raw, out value);
      }

      /// <summary>
      ///   Grudges pillar (Gift gesture): routes a gold payment or an item's value, offered to
      ///   <paramref name="npcHero" />, through <see cref="GrudgeResolutionService.TryResolveDominant" />,
      ///   returning a short clause to append to the caller's own outcome message when it actually moved a
      ///   grudge, or an empty string otherwise. Never throws — a resolution failure must never break the
      ///   gift transfer that already succeeded.
      /// </summary>
      private static string TryGrudgeGiftTail(Hero npcHero, int giftValue)
      {
         try
         {
            var today = (int) CampaignTime.Now.ToDays;
            GrudgeResolutionAttemptResult result = GrudgeResolutionService.TryResolveDominant(npcHero, GrudgeGesture.Gift, giftValue, today);

            if (!result.Changed) return "";

            return result.Outcome == GrudgeResolutionOutcome.Resolved
               ? " It also mends an old grievance between you."
               : " It also softens an old grievance between you.";
         }
         catch (Exception ex)
         {
            ModLog.Error($"[GRUDGE] gift routing failed: {ex.GetType().Name}: {ex.Message}");

            return "";
         }
      }

      /// <summary>
      ///   Stance mechanical behaviour-gating (increment 1, Fear): plants a HIDDEN grudge against the
      ///   player for a fear-coerced release — resentment instead of open regard, per the ratified
      ///   axis→domain design (Fear → coercion). Never lets a plant failure break the release that
      ///   already succeeded.
      /// </summary>
      private static void TryPlantCoercedGrudge(Hero npcHero, int magnitude, int today)
      {
         try
         {
            GrudgeBehavior.Instance?.TryPlant(npcHero, Hero.MainHero, GrudgeSource.CoercedRelease,
               "They cowed me into freeing them out of fear, not goodwill.", magnitude, false, today);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[STANCE] coerced-release grudge plant failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Appreciations pillar: plants a GiftReceived/GoldReceived appreciation on <paramref name="npcHero" />
      ///   scaled from a denar value via <see cref="AppreciationValueScaling.FromValue" />. KnownToPlayer is
      ///   always true — the player performed (or plainly witnessed) the hand-off themselves. Never throws —
      ///   a failure here must never break the gift/payment transfer that already succeeded.
      /// </summary>
      private static void TryPlantValueAppreciation(Hero npcHero, AppreciationSource source, int value, string cause,
                                                    Logic.Character.GiftWelcome welcome = Logic.Character.GiftWelcome.Ordinary)
      {
         try
         {
            if (npcHero == null) return;

            var today = (int) CampaignTime.Now.ToDays;
            // Gabriel (23/10/2026): a gift warms regard by its worth, once a day (the appreciation's own day, so a reload
            // does not reopen it), before this gift renews that day.
            bool givenToday = AppreciationBehavior.Instance?.AppreciationsFor(npcHero.StringId)?
                                 .Any(a => a.Source == source && a.TargetId == Hero.MainHero.StringId && a.EventDay == today) == true;
            int magnitude = AppreciationValueScaling.FromValue(value);
            AppreciationBehavior.Instance?.TryPlant(npcHero, Hero.MainHero, source, cause, magnitude, true, today);
            int warmth = Logic.Character.GiftTastePolicy.Warmth(Logic.DeedRegardPolicy.GiftRegard(value, givenToday), welcome);
            if (warmth > 0) Mapping.DeedRegardApplier.Apply(npcHero, Logic.PlayerDeed.Gift, warmth);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[APPRECIATION] value-scaled plant failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>Maps a turn refusal to a short in-character phrase for the outcome line.</summary>
      private static string TurnRefusalPhrase(NemesisTurnRefusal reason) => reason switch {
         NemesisTurnRefusal.FearsYouTooLittle => "does not fear you enough to bend",
         NemesisTurnRefusal.RespectsYouTooLittle => "sees no honour in serving the likes of you",
         NemesisTurnRefusal.TooDefiant => "would sooner die free than serve his captor",
         NemesisTurnRefusal.HatesYouTooMuch => "hates you far too deeply to ever serve you",
         _ => "will not turn"
      };

      /// <summary>
      ///   Whether the intimacy consent bar permits intimacy to COUNT with <paramref name="npcHero" /> right
      ///   now (audit M3). The single answer both enforcement points read: the intimacy relation bonus here
      ///   in the bridge, and the romantic arc in <c>ProfileMutator</c>. Fail-safe: an unknown pair is a no.
      /// </summary>
      public bool PermitsIntimacyWith(Hero npcHero, NpcProfile profile)
      {
         try { return IntimacyThresholdPolicy.PermitsIntimacy(BuildConsentFacts(npcHero, profile)); }
         catch (Exception ex)
         {
            ModLog.Error($"[M3] consent check failed, refusing: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }

      /// <summary>
      ///   Gathers the engine-side facts the intimacy consent bar rests on (audit M3), so the bridge asks the
      ///   SAME pure question the prompt printed a verdict for. Building them here rather than in the policy
      ///   is what keeps the two from drifting: one rule, two callers.
      /// </summary>
      private static IntimacyConsentFacts BuildConsentFacts(Hero npcHero, NpcProfile profile)
      {
         RomanticProfile romantic = profile?.Romantic;
         bool marriedAtAll = npcHero?.Spouse != null;

         return new IntimacyConsentFacts {
            NpcIsPlayerSpouse = marriedAtAll && npcHero.Spouse == Hero.MainHero,
            NpcIsMarriedToAnother = marriedAtAll && npcHero.Spouse != Hero.MainHero,
            PrefersCasual = romantic?.Preferences?.Contains(RomanticPreference.Casual) == true,
            PrefersIntense = romantic?.Preferences?.Contains(RomanticPreference.Intense) == true,
            RegardWithPlayer = profile?.ReputationWithPlayer ?? 0,
            // Never consulted by PermitsIntimacy; carried so a caller that wants the VOICE of the refusal
            // gets the same verdict the prompt did.
            HasSharedIntimacyBefore = romantic?.Status.IsIntimateOrDeeper() == true,
            ThresholdRelief = IntimacyPacingRules.Relief(ModSettings.RelationWarmth)
         };
      }

      private ActionOutcome ExecuteChangeRelation(GameAction action, Hero npcHero, ActionContext context)
      {
         if (!TryGetInt(action.Parameters, "delta", out int proposed)) return null;
         if (npcHero == null || Hero.MainHero == null || context?.Profile == null) return null;

         bool captive = GetPlayerStatus(npcHero) == PlayerStatusVsNpc.NpcIsCaptive;

         // C1: an intimacy [EVENT] earns the big cooldown-bypassing bonus ONLY for a legitimate moment: adult
         // content on, an orientation-compatible pair, and an active romance already underway (Curious or deeper,
         // never a terminal Estranged/Broken). Otherwise RelationGate demotes it to a routine +1, so a model that
         // spams the intimacy tag cannot farm relation. The prompt is advice; this is the law.
         RomanticProfile romantic = context.Profile.Romantic;
         bool intimacyAllowed =
            ModSettings.AdultContentLevel != AdultContentLevel.Off
            && romantic != null
            && romantic.Status.IsCuriousOrDeeper()
            && romantic.IsCompatibleWith(Hero.MainHero?.IsFemale ?? false)
            // Audit M3: the consent bar the prompt states is now also LAW. Without this the thresholds were
            // prose a persistent player could simply argue past, and the model's yes still paid the full
            // intimacy bonus. Deliberately blind to a past affair (see IntimacyThresholdPolicy): a history
            // softens how the refusal is voiced, it never buys a standing mechanical exemption.
            && IntimacyThresholdPolicy.PermitsIntimacy(BuildConsentFacts(npcHero, context.Profile));

         int delta = RelationGate.Resolve(new RelationGate.RegardProposal {Proposed = proposed, Profile = context.Profile, EmittedEvent = context.EmittedEvent, CurrentHour = CampaignTime.Now.ToHours, NpcIsCaptive = captive, IntimacyAllowed = intimacyAllowed, Warmth = ModSettings.RelationWarmth, DecayDays = ModSettings.RegardDecayDays});

         if (delta == 0)
         {
            ModLog.Info($"[ACTION] change_relation: proposed {proposed:+#;-#;0} gated to 0 (progressive-regard: banked a warm moment, not enough for a point yet).");

            return null;
         }

         // Apply to OUR per-NPC personal score, NOT Bannerlord's relation. The game's
         // relation is clan-level by design (DiplomacyModel.GetHeroesForEffectiveRelation
         // collapses all clan members to the clan leader), so writing it spreads the
         // change across the whole clan. The personal score lives per-hero on the
         // profile — no spread — and drives the prompt's personal regard + greeting mood.
         // 5.1: single clamp/stamp path. The gating (cooldown/cap/stamp) already happened in RelationGate above;
         // ApplyReputationDelta only clamps to [-100, 100], the one place that mutation is bounded.
         int before = context.Profile.ReputationWithPlayer;
         ProfileMutator.ApplyReputationDelta(context.Profile, delta, NpcMemoryService.Core.Standing.RegardCause.Conversation);
         int after = context.Profile.ReputationWithPlayer;
         ModLog.Info($"[ACTION] change_relation (personal): {npcHero.Name} {before}→{after} (delta {delta:+#;-#;0}, proposed {proposed:+#;-#;0})");

         string message = delta > 0
            ? $"{npcHero.Name} regards you more warmly. ({delta:+#;-#;0})"
            : $"{npcHero.Name} regards you more coldly. ({delta:+#;-#;0})";

         // Banner: correct hero name + NPC portrait. No soundEventPath — that
         // parameter does not trigger custom-bank events reliably. Sound below.
         MBInformationManager.AddQuickInformation(
            new TextObject(message), 0, npcHero.CharacterObject);

         // Play the custom sound directly via FMOD event API.
         string soundPath = delta > 0
            ? ModSettings.RelationIncreaseSound
            : ModSettings.RelationDecreaseSound;
         PlaySound(soundPath);

         // 2.1: report the delta ACTUALLY applied (after gate + clamp), so the StoryLog and the affront rumor
         // read the real change rather than the raw proposal.
         return new ActionOutcome(message, delta > 0, appliedRelationDelta: after - before);
      }

      /// <summary>
      ///   A written tale's choice by words (branching tales, 04/10/2026), reported by the action interpreter, which is
      ///   taught only the choices open in this conversation. The tale decides: only a declared, open choice said to this
      ///   character is taken; anything else is refused and changes nothing.
      /// </summary>
      private static ActionOutcome ExecuteStoryChoice(GameAction action, Hero npcHero)
      {
         string id = action?.Parameters != null && action.Parameters.TryGetValue("choice", out string c) ? c : null;
         string taken = Quests.AuthoredQuestBehavior.Instance?.TakeSaidChoice(npcHero, id);

         return taken != null
            ? new ActionOutcome($"The tale takes a turn: {taken}.", true)
            // tashmetu (07/10/2026): the player cannot act on this; it stays in the log ([TALE] ... refused) and the
            // model is still told nothing happened, but no red line is shown.
            : ActionOutcome.Refused("");
      }

      /// <summary>
      ///   A signal of a tale's arc (TaleArc, Gabriel 25/10/2026: "signaux observes avec tirage en filet"), reported by
      ///   whoever reads the turn's actions. The arc decides: only a signal it watches for now is taken; nothing is shown
      ///   to the player either way (the arc's pacing is never announced).
      /// </summary>
      private static ActionOutcome ExecuteTaleSignal(GameAction action, Hero npcHero)
      {
         string id = action?.Parameters != null && action.Parameters.TryGetValue("signal", out string s) ? s : null;

         return Quests.AuthoredQuestBehavior.Instance?.TakeSignal(npcHero, id) == true ? new ActionOutcome("", true) : ActionOutcome.Refused("");
      }

      /// <summary>
      ///   teach_skill: an NPC genuinely skilled themselves instructs the player, granting real, bounded skill
      ///   XP. Closes the roadmap's empty-promise class: an NPC could already OFFER a lesson in prose with no
      ///   executor behind it, so nothing ever landed on the character sheet. Re-validates everything the
      ///   prompt only proposed ("the prompt is advice, the bridge is law"): the named skill must resolve to a
      ///   real <see cref="SkillObject" />, the NPC's own live value in it must clear
      ///   <see cref="TeachSkillPolicy.CanTeach" />, and the granted amount is decided by
      ///   <see cref="TeachSkillPolicy.GrantedXp" /> from the NPC's own mastery and their per-NPC cooldown
      ///   (<see cref="NpcProfile.LastTaughtHour" />), never trusted from the LLM's proposed amount.
      /// </summary>
      private static ActionOutcome ExecuteTeachSkill(GameAction action, Hero npcHero, ActionContext context)
      {
         try
         {
            if (npcHero == null || Hero.MainHero?.HeroDeveloper == null || context?.Profile == null) return null;

            action.Parameters.TryGetValue("skill", out string skillName);
            if (string.IsNullOrWhiteSpace(skillName))
               return ActionOutcome.Refused(SilentRefusalMessages.TeachSkillNoSkillNamed());

            SkillObject skill = FindSkillByName(skillName);
            if (skill == null)
               return ActionOutcome.Refused($"(You know no skill called '{skillName}' to teach.)");

            string skillLabel = skill.Name?.ToString() ?? skill.StringId;
            int teacherLevel = npcHero.GetSkillValue(skill);

            if (!TeachSkillPolicy.CanTeach(teacherLevel))
               return ActionOutcome.Refused($"({npcHero.Name} is no master of {skillLabel}, and has nothing worth teaching there.)");

            // The prompt only offers skills the player knows less of, but the prompt is advice and the bridge is
            // law: the model may name a skill from its own sense of the character, and the player's level may
            // have risen since the turn began.
            if (!TeachSkillPolicy.CanTeachPlayer(teacherLevel, Hero.MainHero.GetSkillValue(skill)))
               return ActionOutcome.Refused($"(You already know more of {skillLabel} than {npcHero.Name} does; there is nothing to learn from them there.)");

            // "Advisory" per the catalog: the LLM may omit amount, or write anything at all. Absent a parsed
            // figure, request comfortably above every possible ceiling (MaxCeiling) so the teacher's OWN
            // mastery, not a missing parameter, decides the real grant.
            if (!TryGetInt(action.Parameters, "amount", out int requested) || requested <= 0)
               requested = 1000;

            int granted = TeachSkillPolicy.GrantedXp(
               teacherLevel, requested, context.Profile.LastTaughtHour, CampaignTime.Now.ToHours);

            if (granted <= 0)
               return ActionOutcome.Refused($"({npcHero.Name} has taught you a lesson too recently to teach another now.)");

            Hero.MainHero.HeroDeveloper.AddSkillXp(skill, granted, false); // predictable: not further discounted by focus factor
            context.Profile.LastTaughtHour = CampaignTime.Now.ToHours;

            string message = $"{npcHero.Name} teaches you {skillLabel}. (+{granted} XP)";
            MBInformationManager.AddQuickInformation(new TextObject(message), 0, npcHero.CharacterObject);
            ModLog.Info($"[ACTION] teach_skill: {npcHero.Name} taught {skillLabel} to the player (+{granted} XP, teacher level {teacherLevel}, requested {requested}).");

            return new ActionOutcome(message, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] teach_skill failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   Resolves a skill named freely by the LLM against the game's own live skill roster
      ///   (<see cref="Skills.All" />), tolerant of spacing/case/punctuation (e.g. "One-Handed", "one handed"
      ///   and "OneHanded" all resolve to the same <see cref="SkillObject" />) and of the one vanilla display-vs-
      ///   StringId mismatch (Crafting's own display name is "Smithing"), matching against BOTH the skill's
      ///   display name and its StringId. Null when nothing matches.
      /// </summary>
      private static SkillObject FindSkillByName(string name)
      {
         if (string.IsNullOrWhiteSpace(name)) return null;

         string normalized = NormalizeSkillToken(name);
         foreach (SkillObject skill in Skills.All)
         {
            if (skill == null) continue;
            if (NormalizeSkillToken(skill.StringId) == normalized) return skill;
            if (NormalizeSkillToken(skill.Name?.ToString()) == normalized) return skill;
         }

         return null;
      }

      /// <summary>Lowercases and strips everything but letters/digits, so "One-Handed" and "One Handed" compare equal.</summary>
      private static string NormalizeSkillToken(string value)
      {
         if (string.IsNullOrEmpty(value)) return "";

         var sb = new System.Text.StringBuilder(value.Length);
         foreach (char c in value)
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));

         return sb.ToString();
      }

      /// <summary>
      ///   give_prisoner with no bargain to settle: the player gives a captive he holds, asking nothing (3chmidt, Nexus,
      ///   05/11/2026). The one named, or the only one held (CaptiveNameMatch.ForGift); into the receiver's own party or
      ///   holding, as a delivery goes. The receiver remembers a gift worth the captive's ransom; the captive resents being
      ///   handed on, as for a delivery; the same trade named again the same day is refused (the echo guard).
      /// </summary>
      private static ActionOutcome GiveCaptiveAsGift(GameAction action, Hero npcHero, NpcProfile profile)
      {
         List<Hero> held = BuyPrisonerVerb.PlayerHeldHeroPrisoners();
         if (held.Count == 0) return ActionOutcome.Refused(SilentRefusalMessages.GivePrisonerNoBargain());

         string named = null;
         action?.Parameters?.TryGetValue("target", out named);
         int at = Logic.Prisoners.CaptiveNameMatch.ForGift(held.Select(h => h.Name?.ToString() ?? "").ToList(), named);
         if (at < 0)
            return ActionOutcome.Refused(string.IsNullOrWhiteSpace(named)
               ? "(You hold several captives; name the one you give.)"
               : $"(You hold no captive named {named.Trim()}, or the name fits more than one.)");

         Hero captive = held[at];
         if (BuyPrisonerVerb.IsEchoOfLastTrade(captive, npcHero)) return ActionOutcome.Refused(BuyPrisonerVerb.EchoRefusal(npcHero, captive));

         // Given to his own side, he goes home free, as a sale to them already does (cr.test prisoner_gift, 08/11/2026:
         // since the gift was routed here on 05/11, a lord given to his own clan leader was put in that leader's cells,
         // fkasad's "escaped" of 26/09 again).
         bool goesHome = BuyPrisonerVerb.GoesHomeTo(captive, npcHero);
         PartyBase dest = goesHome ? null : BuyPrisonerVerb.ResolveCaptorPrison(npcHero);
         if (!goesHome && dest == null) return ActionOutcome.Refused($"({npcHero.Name} has no party or holding to keep {captive.Name} in.)");

         int captiveRegardForPlayer = Mapping.PairBondReader.Points(captive, Hero.MainHero);
         if (goesHome)
         {
            EndCaptivityAction.ApplyByRansom(captive, npcHero);
         }
         else
         {
            PartyBase source = captive.PartyBelongedToAsPrisoner ?? MobileParty.MainParty.Party;
            TransferPrisonerAction.Apply(captive.CharacterObject, source, dest);
         }

         BuyPrisonerVerb.RecordTrade(captive, npcHero);

         var today = (int) CampaignTime.Now.ToDays;
         int worth = BuyPrisonerVerb.RansomAnchor(captive);
         profile.Events.Add(new NotableEvent(today, NotableEventType.Collaboration, $"The player gave me the captive {captive.Name}, asking nothing."));
         StanceService.OnGiftReceived(npcHero);
         TryPlantValueAppreciation(npcHero, AppreciationSource.GiftReceived, worth, $"The player gave me the captive {captive.Name}, asking nothing.");
         // Sent home free, he has nothing to resent.
         if (!goesHome) CaptiveHandedOnward(captive, npcHero, captiveRegardForPlayer, today);
         ModLog.Info($"[ACTION] give_prisoner: player gave {captive.Name} to {npcHero.Name} as a gift (worth {worth}){(goesHome ? ", and he went home free" : "")}.");

         return new ActionOutcome(goesHome
            ? $"You give {captive.Name} to {npcHero.Name} for nothing, and {captive.Name} goes home free."
            : $"You give {captive.Name} into {npcHero.Name}'s keeping, asking nothing.", true);
      }

      /// <summary>
      ///   give_prisoner: the player hands over the captive an outstanding deliver_prisoner bargain named.
      ///   The deed names the target, so the bridge resolves which held prisoner satisfies it (no picker),
      ///   transfers them to the lord, and settles the bargain — reward, favour, journal close — in one
      ///   turn (mirrors SettleItemBargain). A spurious emission (no bargain, or the captive not held) is
      ///   reported, never faked.
      /// </summary>
      private ActionOutcome ExecuteGivePrisoner(GameAction action, Hero npcHero, ActionContext context)
      {
         try
         {
            NpcProfile profile = context?.Profile;

            if (profile == null || npcHero == null) return null;

            // An envoy brings the man HOME (fkasad, 01/10/2026): handing a lord his own man frees him rather than
            // jailing him again, and the release completes the errand (QuestDeedTracker hears it as the player's).
            InformalQuest envoy = profile.ActiveQuests.FirstOrDefault(q => q.Type == QuestType.RansomEnvoy && q.IsOutstanding);
            Hero envoysMan = envoy == null ? null : BuyPrisonerVerb.PlayerHeldHeroPrisoners().FirstOrDefault(h => h.StringId == envoy.TargetHeroId);
            if (envoysMan != null)
            {
               EndCaptivityAction.ApplyByReleasedByChoice(envoysMan, Hero.MainHero);
               ModLog.Info($"[QUEST] ransom_envoy: {envoysMan.Name} handed home to {npcHero.Name}, free.");

               return new ActionOutcome($"{envoysMan.Name} is free, home with {npcHero.Name}.", true);
            }

            // A capture or delivery task whose man escaped after the player beat him may take a lord of his realm in his
            // place (fkasad, 01/10/2026): that is a bargain too.
            InformalQuest anyBargain = profile.ActiveQuests.FirstOrDefault(q =>
               (q.Type == QuestType.DeliverPrisoner && q.IsOutstanding) || EscapedTargetPolicy.MaySettleByVictory(q));

            // 3chmidt (Nexus, 05/11/2026): "It would be useful if you could give your prisoners with *action* too... NPC's don't
            // understand why you would say that if you already agreed to giving your prisoners to them." With no bargain to
            // settle, the captive handed over is a gift.
            if (anyBargain == null) return GiveCaptiveAsGift(action, npcHero, profile);

            // THE WHOLE OF THE PLAYER'S CUSTODY, and every outstanding bargain rather than only the first. This
            // read only the main party's prison and only the first open bargain, while the offer bag and
            // buy_prisoner already counted a captive left in the player's own dungeon: a lord held there showed
            // up in the bag and was then refused here as "not held". And with two bargains open, a captive who
            // settled the SECOND was refused because the first did not match him.
            List<Hero> held = BuyPrisonerVerb.PlayerHeldHeroPrisoners();

            string preferredId = null;
            if (action?.Parameters != null
                && action.Parameters.TryGetValue("target", out string targetName)
                && !string.IsNullOrWhiteSpace(targetName))
               preferredId = BuyPrisonerVerb.ResolvePlayerHeldPrisonerByName(targetName)?.StringId;

            DeliveryMatch match = DeliveryQuestMatcher.FirstDelivery(
               profile.ActiveQuests,
               held.Select(h => new HeldCaptive {Id = h.StringId, FactionId = h.MapFaction?.StringId}),
               preferredId);

            // No captive settles a bargain as named: a lord of an escaped target's realm may stand in, the giver having
            // chosen to take him (this verb is his acceptance). The preferred captive first, as for a delivery.
            if (match == null)
               foreach (Hero h in held.OrderByDescending(h => h.StringId == preferredId))
               {
                  var c = new HeldCaptive {Id = h.StringId, FactionId = h.MapFaction?.StringId};
                  InformalQuest stood = profile.ActiveQuests.FirstOrDefault(q => EscapedTargetPolicy.SubstituteSettles(q, c));
                  if (stood == null) continue;

                  match = new DeliveryMatch {Quest = stood, CaptiveId = h.StringId};
                  ModLog.Info($"[QUEST] {npcHero.Name} takes {h.Name} in place of {stood.TargetHeroName}, who escaped.");
                  break;
               }

            if (match == null)
               return ActionOutcome.Refused($"(You do not hold {anyBargain.TargetHeroName ?? "the captive"} I asked for.)");

            InformalQuest quest = match.Quest;
            Hero prisonerHero = held.First(h => h.StringId == match.CaptiveId);
            CharacterObject prisonerChar = prisonerHero.CharacterObject;

            // Captured BEFORE the transfer, per the rule's own "prior regard" framing (mirrors
            // DivorceFacts.SpouseRegardForPlayer's low-regard-nurses-harder read).
            int captiveRegardForPlayer = Hero.MainHero != null
               ? prisonerHero.GetRelation(Hero.MainHero)
               : 0;

            // Resolve a REAL destination prison BEFORE settling anything: the lord's own party, else the
            // settlement they stand in, else a holding their clan owns. A lord with nowhere to keep a
            // prisoner is REFUSED here (quest left open, captive untouched), never handed a phantom: the old
            // AddToCounts(-1) fallback deleted the captive and left them a prisoner of no one while the
            // bargain still paid out (verified state corruption).
            PartyBase dest = BuyPrisonerVerb.ResolveCaptorPrison(npcHero);
            if (dest == null)
               return ActionOutcome.Refused($"({npcHero.Name} has no party or holding to keep {prisonerHero.Name} in.)");

            // From wherever the captive is actually held, as buy_prisoner already does: a dungeon captive
            // transferred "from the main party" would move nothing and still settle the bargain.
            PartyBase source = prisonerHero.PartyBelongedToAsPrisoner ?? MobileParty.MainParty.Party;
            TransferPrisonerAction.Apply(prisonerChar, source, dest);

            var today = (int) CampaignTime.Now.ToDays;
            quest.SatisfiedOnDay = today;
            quest.Status = QuestStatus.Completed;
            quest.Evidence = $"Delivered {prisonerHero.Name} to {npcHero.Name} on day {today}.";

            ActionOutcome reward = GrantQuestReward(npcHero, profile, quest.RewardGold, quest.RewardRelation);
            ActionOutcome grant = quest.Reward != RewardGrant.None
               ? GrantConditionalReward(npcHero, profile, quest)
               : null;

            StanceService.OnPromiseKept(profile); // a kept bargain earns trust
            QuestJournalBridge.CloseAsCompleted(quest, npcHero);
            ModLog.Info($"[ACTION] give_prisoner: player delivered {prisonerHero.Name} to {npcHero.Name} ({quest.Reward}).");

            CaptiveHandedOnward(prisonerHero, npcHero, captiveRegardForPlayer, today);

            // Chronicle: a third, independent channel alongside the reward/grant and the grudge above.
            try
            {
               WorldEventStore.Instance?.Record(new WorldEvent {
                  Type = WorldEventType.PlayerFeat,
                  Day = today,
                  Magnitude = 40,
                  InvolvesPlayer = true,
                  Summary = $"{Hero.MainHero.Name} delivered {prisonerHero.Name} to {npcHero.Name}.",
                  ActorIds = new List<string> {Hero.MainHero.StringId, prisonerHero.StringId, npcHero.StringId},
                  FactionIds = npcHero.MapFaction != null
                     ? new List<string> {npcHero.MapFaction.StringId}
                     : new List<string>(),
                  ChronicleLine = $"{Hero.MainHero.Name} delivered {prisonerHero.Name} in chains to {npcHero.Name}"
               });
            }
            catch (Exception ex) { ModLog.Error($"[ACTION] give_prisoner chronicle record failed: {ex.GetType().Name}: {ex.Message}"); }

            return grant ?? reward ?? new ActionOutcome($"{npcHero.Name} takes custody of {prisonerHero.Name}.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] give_prisoner failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   Grudges pillar (Tier 2, DeliveredPrisoner): the captive ALWAYS resents being handed onward (a betrayal of the
      ///   custody the player held over them), heavier when handed to their own foe. Shared by a bargain's delivery and a
      ///   gift. Guarded in its own try/catch so a plant failure never claws back the transfer that already succeeded.
      /// </summary>
      private static void CaptiveHandedOnward(Hero prisonerHero, Hero npcHero, int captiveRegardForPlayer, int today)
      {
            try
            {
               bool toEnemy = prisonerHero.MapFaction != null &&
                              npcHero.MapFaction != null &&
                              FactionManager.IsAtWarAgainstFaction(prisonerHero.MapFaction, npcHero.MapFaction);

               var facts = new DeliveredPrisonerFacts {
                  DeliveredToEnemyOfCaptive = toEnemy,
                  CaptiveRegardForPlayer = captiveRegardForPlayer
               };
               GrudgeProposal proposal = Tier2GrudgeRules.DeliveredPrisonerGrudge(facts);

               if (proposal.Forms)
               {
                  string grudgeCause = toEnemy
                     ? "The player handed me to my enemy."
                     : "The player sold me off like chattel.";

                  GrudgeBehavior.Instance?.TryPlant(prisonerHero, Hero.MainHero, GrudgeSource.DeliveredPrisoner,
                     grudgeCause, proposal.Magnitude, proposal.KnownToPlayer, today);
               }
               if (toEnemy) Mapping.MisdeedLedgerBehavior.Record(prisonerHero.StringId, Logic.Trauma.MisdeedKind.HandedToEnemy); // Gabriel 23/10: a misdeed
               Mapping.DeedRegardApplier.Apply(prisonerHero, toEnemy ? Logic.PlayerDeed.HandedToEnemy : Logic.PlayerDeed.SoldAsChattel);
            }
            catch (Exception ex)
            {
               ModLog.Error($"[ACTION] give_prisoner grudge plant failed: {ex.GetType().Name}: {ex.Message}");
            }
      }

      #endregion
   }
}
