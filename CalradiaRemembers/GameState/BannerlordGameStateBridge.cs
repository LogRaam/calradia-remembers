// Code written by Gabriel Mailhot, 10/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Bastards;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Commitments;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Escort;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Items;
using CalradiaRemembers.Jealousy;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Logic.Assemblies;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Marriage;
using CalradiaRemembers.Missions;
using CalradiaRemembers.Nemesis;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Stance;
using CalradiaRemembers.Storage;
using CalradiaRemembers.Verbs;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
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
            GainRenownAction.Apply(Hero.MainHero, -renownLoss, true);
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

      public ActionOutcome ExecuteAction(GameAction action, Hero npcHero, ActionContext context)
      {
         if (action == null) return null;

         switch (action.Type.ToLowerInvariant())
         {
            case "change_relation":
               return ExecuteChangeRelation(action, npcHero, context);
            case "give_gold":
               return ExecuteGiveGold(action, npcHero);
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
            case "give_item":
               return ExecuteGiveItem(action, npcHero, context);
            case "give_prisoner":
               return ExecuteGivePrisoner(npcHero, context);
            case "free_prisoner":
               return ExecuteFreePrisoner(npcHero, context);
            case "execute_prisoner":
               return ExecuteExecutePrisoner(npcHero);
            case "execute_player":
               return ExecuteExecutePlayer(npcHero);
            case "turn_nemesis":
               return ExecuteTurnNemesis(npcHero, context?.Profile);
            case "recruit_prisoner":
               return ExecuteRecruitPrisoner(npcHero, context?.Profile);
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
            // give_influence, lend_troops, sway_opinion, release_prisoner: migrated to the
            // IConversationVerb contract (CalradiaRemembers.Verbs): VerbRegistry routes by Name,
            // reusing the same CourtActionResolver/CourtActionPolicy eligibility and math as before.
            // give_troops, spend_influence (04/07/2026): the RECIPROCAL direction — the player reinforces
            // a needy NPC instead of the NPC favouring the player — designed for the same contract.
            case "give_influence":
            case "lend_troops":
            case "give_troops":
            case "spend_influence":
            case "sway_opinion":
            case "release_prisoner":
            // buy_prisoner / sell_prisoner (prisoner trade, 08/07/2026): the NPC buys a captive the player
            // holds, or sells one his clan holds, for gold clamped around the game's ransom valuation.
            case "buy_prisoner":
            case "sell_prisoner":
            // end_marriage (DIVORCE, Phase 1) and make_amends (Grudges pillar, resolution half): both
            // DESIGNED for the IConversationVerb contract rather than migrated onto it — routed
            // identically to the four above.
            case "end_marriage":
            case "make_amends":
               return VerbRegistry.Instance.Execute(action, new VerbContext(npcHero, context?.Profile, this, null, context));
            case "pledge_against":
               return ExecutePledgeAgainst(action, npcHero, context);
            case "accept_divorce":
               return ExecuteAcceptDivorce(npcHero);
            case "decline_divorce":
               return ExecuteDeclineDivorce(npcHero);
            case "end_own_marriage":
               return ExecuteEndOwnMarriage(npcHero);
            default:
               // Safety net: an unknown / hallucinated verb must SURFACE a refusal (player line + transcript
               // OOC note), never vanish as a silent null, or the model's invented deed goes uncorrected.
               ModLog.Info($"[ACTION] Unknown type '{action.Type}' refused (no executor).");

               return ActionOutcome.Refused("(That cannot be done here. Nothing happened.)");
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

         var wars = new List<string>();
         List<Kingdom> kingdoms = RetrieveAllKingdoms();

         for (var i = 0; i < kingdoms.Count; i++)
            for (int j = i + 1; j < kingdoms.Count; j++)
            {
               Kingdom a = kingdoms[i];
               Kingdom b = kingdoms[j];
               if (FactionManager.IsAtWarAgainstFaction(a, b))
                  wars.Add($"{a.Name} vs {b.Name}");
            }

         return wars.Count > 0
            ? string.Join("; ", wars)
            : "";
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
      ///   <see cref="Logic.MarriageEligibility.MarriageEligibilityThreshold" /> is a shared constant rather
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

         return new PlayerStandingFacts(
            Hero.MainHero.IsPrisoner && Hero.MainHero.PartyBelongedToAsPrisoner?.LeaderHero == npc,
            npc.IsPrisoner && npc.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero,
            npc.Clan != null && npc.Clan == Clan.PlayerClan,
            Hero.MainHero.Clan?.Kingdom?.Leader == npc && npc != Hero.MainHero,
            npc.Clan?.Kingdom?.Leader == Hero.MainHero && Hero.MainHero.Clan?.Kingdom != null,
            sharesRealm && (Clan.PlayerClan?.IsUnderMercenaryService ?? false),
            sharesRealm,
            sharesRealm && (npc.Clan?.IsUnderMercenaryService ?? false));
      }

      public string GetSeasonName()
      {
         // Bannerlord year = 84 campaign days (21 per season).
         var dayOfYear = (int) (CampaignTime.Now.ToDays % 84);

         if (dayOfYear < 21) return "Spring";
         if (dayOfYear < 42) return "Summer";
         if (dayOfYear < 63) return "Autumn";

         return "Winter";
      }

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

      public ActionOutcome GrantQuestReward(Hero npcHero, NpcProfile profile, int gold, int relation)
      {
         if (npcHero == null || Hero.MainHero == null || profile == null) return null;

         var parts = new List<string>();

         if (gold > 0)
         {
            int amount = ClampInt(gold, 1, 100000);
            GiveGoldAction.ApplyBetweenCharacters(npcHero, Hero.MainHero, amount, true);
            parts.Add($"{amount} denars");
         }

         if (relation > 0)
         {
            // Verified reward: applied straight to the personal score, bypassing the RelationGate cooldown/cap
            // (the deed was confirmed by real game events). 5.1: through the single clamp path ApplyReputationDelta.
            // 5.8 / M-C3 (regard audit): it does NOT stamp LastRelationGainHour. That stamp is the CONVERSATIONAL
            // cooldown; a verified reward is a separate channel, and stamping it silently gated a warm chat's +1 to
            // 0 for 8h after a turn-in. Reward and conversation are independent, so the reward leaves the clock be.
            int before = profile.ReputationWithPlayer;
            ProfileMutator.ApplyReputationDelta(profile, relation);
            int after = profile.ReputationWithPlayer;
            if (after != before) parts.Add($"+{after - before} regard");
            ModLog.Info($"[QUEST] reward relation: {npcHero.Name} {before}→{after} (+{relation} promised)");
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
            var toRemove = new List<(ItemObject item, int count)>();
            var total = 0;
            foreach (ItemOffer offer in offered)
            {
               if (string.IsNullOrEmpty(offer.ItemId) || offer.Count <= 0) continue;
               ItemObject item = FindRosterItem(roster, offer.ItemId, out int available);

               if (item == null || available <= 0) continue;
               int take = Math.Min(offer.Count, available);
               total += item.Value * take;
               toRemove.Add((item, take));
            }

            if (total < quest.RequiredItemValue)
               return ActionOutcome.Refused(
                  $"({giverHero?.Name}: these goods are worth about {total} denars — I asked for " + $"{quest.RequiredItemValue}. Bring more.)");

            // Value met: hand the goods over, then honor the favor.
            foreach ((ItemObject item, int count) in toRemove)
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
               foreach ((ItemObject item, int count) in toRemove)
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
            if (quest is not {Type: QuestType.ProvideGoods}) return null;
            if (giver == null || Hero.MainHero == null || offered == null || offered.Count == 0) return null;

            ItemRoster roster = MobileParty.MainParty?.ItemRoster;

            if (roster == null) return null;

            string categoryKey = quest.RequiredItemCategory;
            string label = SupplyItemMatcher.Label(categoryKey);

            var toRemove = new List<(ItemObject item, int count)>();
            var count = 0;
            foreach (ItemOffer offer in offered)
            {
               if (string.IsNullOrEmpty(offer.ItemId) || offer.Count <= 0) continue;
               ItemObject item = FindRosterItem(roster, offer.ItemId, out int available);

               if (item == null || available <= 0 || !SupplyItemMatcher.Matches(item, categoryKey)) continue;
               int take = Math.Min(offer.Count, available);
               count += take;
               toRemove.Add((item, take));
            }

            if (count < quest.RequiredItemCount)
               return ActionOutcome.Refused($"({giverHero?.Name}: that is only {count} of the {quest.RequiredItemCount} {label} I asked for. Bring more.)");

            // Count met: hand over exactly the required number (never take more than asked).
            int remaining = quest.RequiredItemCount;
            foreach ((ItemObject item, int c) in toRemove)
            {
               if (remaining <= 0) break;
               int take = Math.Min(c, remaining);
               roster.AddToCounts(item, -take);
               remaining -= take;
            }

            var day = (int) CampaignTime.Now.ToDays;
            quest.SatisfiedOnDay = day;
            quest.Evidence = $"Delivered {quest.RequiredItemCount} {label} on day {day}.";
            quest.Status = QuestStatus.Completed;

            ActionOutcome reward = GrantQuestReward(giverHero, giver, quest.RewardGold, quest.RewardRelation);
            giver.Events.Add(new NotableEvent(day, NotableEventType.Collaboration, $"The player brought the {quest.RequiredItemCount} {label} I needed."));
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
            int before = profile.ReputationWithPlayer;
            profile.ReputationWithPlayer = ClampInt(before + quest.RewardRelation, -100, 100);
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
               ProfileMutator.ApplyReputationDelta(profile, DivorceConsequencePolicy.SpouseMutualReliefRelationNudge);
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
            const string cause = "The marriage was ended by the player's own choice.";

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
                     ? "The marriage ended, by mutual agreement, when the player chose to end it."
                     : "Was set aside by the player, who chose to end our marriage."));
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
            ModLog.Error($"[ACTION] execute_prisoner failed: {ex.GetType().Name}: {ex.Message}");

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

            bool permitted = PlayerExecutionPolicy.CanExecute(
               ModSettings.AllowCaptorExecutions,
               playerIsCaptiveOfThisCaptor,
               ModSettings.AdultContentLevel == AdultContentLevel.Hardcore,
               !CampaignOptions.IsLifeDeathCycleDisabled);

            if (!permitted)
            {
               ModLog.Info($"[ACTION] execute_player refused by PlayerExecutionPolicy (MCM={ModSettings.AllowCaptorExecutions}, " +
                           $"captive={playerIsCaptiveOfThisCaptor}, hardcore={ModSettings.AdultContentLevel == AdultContentLevel.Hardcore}, " +
                           $"deathAllowed={!CampaignOptions.IsLifeDeathCycleDisabled}).");

               return ActionOutcome.Refused("(Your fate was not truly sealed here. Nothing happened.)");
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
            if (!npcHero.IsPrisoner || npcHero.PartyBelongedToAsPrisoner?.LeaderHero != Hero.MainHero)
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
               AppreciationBehavior.Instance?.TryPlant(npcHero, Hero.MainHero, AppreciationSource.RansomedOrFreed,
                  "The player gave me back my freedom.", RansomedOrFreedMagnitude, true, today);

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

      private static ActionOutcome ExecuteGiveGold(GameAction action, Hero npcHero)
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

         // Solvency: an NPC cannot give gold it does not hold. Clamp to the giver's real purse so a hallucinated
         // "10000" from a lord carrying 40 denars neither conjures coin nor drives the giver's purse negative.
         int payable = npcHero.Gold < 10000 ? npcHero.Gold : 10000;
         if (payable < 1)
         {
            ModLog.Info($"[ACTION] give_gold: {npcHero.Name} has no gold to give: ignored.");

            // Was a silent no-op: the model had already told the player gold was coming, so the player was
            // left checking an unchanged purse with no explanation.
            return ActionOutcome.Refused(SilentRefusalMessages.GiveGoldGiverInsolvent(npcHero.Name.ToString()));
         }

         amount = ClampInt(amount, 1, payable);
         GiveGoldAction.ApplyBetweenCharacters(npcHero, Hero.MainHero, amount);
         // R7-light (swear_oath): this is ALSO the pay_gold oath's own verification hook, whichever channel
         // the gold actually moved through (this 1:1 action, or the council's own give_gold resolution).
         CalradiaRemembers.Commitments.CommitmentBehavior.Instance?.OnGoldGivenToPlayer(npcHero.StringId, amount);
         ModLog.Info($"[ACTION] give_gold: {npcHero.Name} → player {amount} gold");

         // The map bar does not refresh while campaign time is frozen under the chat
         // overlay — showing the balance here is the player's only live confirmation.
         return new ActionOutcome($"{npcHero.Name} gives you {amount} denars (purse: {Hero.MainHero.Gold}).", true,
            appliedGoldAmount: amount);
      }

      /// <summary>
      ///   give_item: transfers one unit of the named item from the player's party
      ///   inventory to the NPC. The item name must match a roster entry exactly
      ///   (case-insensitive). Quest items are always refused — this guard sits on both
      ///   the prompt side (picker excludes them) and here, so the bridge is the law.
      /// </summary>
      private static ActionOutcome ExecuteGiveItem(GameAction action, Hero npcHero, ActionContext context)
      {
         try
         {
            if (MobileParty.MainParty?.ItemRoster == null) return null;

            action.Parameters.TryGetValue("item", out string itemName);
            string itemId = context?.OfferedItemId; // the exact item the player picked from the bag, if any

            // Was a silent no-op that let the model replay a phantom gift: no item was named at all.
            if (string.IsNullOrWhiteSpace(itemName) && string.IsNullOrWhiteSpace(itemId))
               return ActionOutcome.Refused(SilentRefusalMessages.GiveItemNoneNamed());

            // Prefer the exact item the player picked from the bag (by id); fall back to a name match
            // for a gift offered purely in free text. The id path is immune to the LLM rewording the name.
            ItemRosterElement found = !string.IsNullOrWhiteSpace(itemId)
               ? FindRosterElementById(itemId)
               : FindItemInRoster(itemName);

            if (found.EquipmentElement.Item == null)
            {
               string label = !string.IsNullOrWhiteSpace(itemName)
                  ? itemName
                  : itemId;

               return ActionOutcome.Refused($"(You don't seem to have '{label}' to give.)");
            }

            MobileParty.MainParty.ItemRoster.AddToCounts(found.EquipmentElement, -1);
            var actualName = found.EquipmentElement.Item.Name.ToString();
            int value = found.EquipmentElement.Item.Value;

            StanceService.OnGiftReceived(npcHero); // a freely given gift builds a little trust
            ModLog.Info($"[ACTION] give_item: player gave '{actualName}' (value {value}) to {npcHero?.Name}");

            // Appreciations pillar: a freely given gift is also a remembered kindness, a channel separate
            // from the trust bump above and from the grudge-softening tail below.
            if (npcHero != null)
               TryPlantValueAppreciation(npcHero, AppreciationSource.GiftReceived, value, $"The player gave me {actualName}.");

            // Grudges pillar (Gift gesture): the same item may also soften or mend a grudge npcHero holds
            // against the player, a channel separate from the trust bump above.
            string grudgeTail = npcHero != null
               ? TryGrudgeGiftTail(npcHero, value)
               : "";

            // If the gift is something they can wear or wield, they may put it on then and there: an empty slot
            // or a real upgrade always, a lateral gift only if they are fond of you (the wife wears the gifted
            // gown; a rival keeps his own blade). The bridge is the law, so this note claims it only when the
            // equip actually happened.
            string equipTail = TryEquipGift(npcHero, found.EquipmentElement.Item);

            return new ActionOutcome($"You give {actualName} to {npcHero?.Name}.{grudgeTail}{equipTail}", false);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] give_item failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   The "gift equipment gets worn" feature: when a give_item gift is something the NPC can wear or wield,
      ///   decide (via the pure <see cref="GiftEquipPolicy" />) whether they equip it, and if so set the real
      ///   equipment slot. A civilian garment goes on their town outfit, war gear on their battle set. Returns a
      ///   short note when they equipped it (appended to the gift outcome so the model narrates the reaction),
      ///   or "" when they merely accepted it. Gender-locked items are never forced on.
      /// </summary>
      private static string TryEquipGift(Hero hero, ItemObject item)
      {
         try
         {
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

            GiftEquipDecision decision = GiftEquipPolicy.Decide(new GiftEquipFacts(
               isEquippable: true,
               slotEmpty: current.Item == null,
               giftValue: item.Value,
               currentSlotValue: current.Item?.Value ?? 0,
               relation: relation,
               isSentimental: sentimental,
               isCaptiveDressing: isCaptiveDressing));

            if (!decision.ShouldEquip) return "";

            equipment[slot] = new EquipmentElement(item);
            ModLog.Info($"[ACTION] give_item: {hero.Name} equipped '{item.Name}' ({decision.Reason}) in " +
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
            if (!MarriageGuardPolicy.BlessingAllowed(npcHero.IsAlive, npcHero.IsPrisoner, IsAtWarWithPlayer(npcHero), blesserRegard))
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

            DispatchMissionVerdict verdict = CourtActionResolver.TryDispatchMission(npcHero, missionName, about);

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
            int playerTier = Clan.PlayerClan?.Tier ?? 0;

            switch (PeaceMediationPolicy.Evaluate(true, true, regard, playerTier))
            {
               case PeaceMediationBlock.StatureTooLow:
                  return ActionOutcome.Refused($"({npcHero.Name} will not be lectured on peace by one of no standing. Raise your house before you presume to broker between realms.)");
               case PeaceMediationBlock.RegardTooLow:
                  return ActionOutcome.Refused($"({npcHero.Name} does not trust you nearly enough to heed your counsel on so grave a matter as ending a war. Earn their deep trust first.)");
            }

            MakePeaceAction.Apply(leaderFaction, target);

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
                  return ActionOutcome.Refused("(You no longer rule a kingdom, and cannot grant a crown fief.)");
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
                  return ActionOutcome.Refused("(You no longer rule a kingdom, and cannot revoke a fief in its name.)");
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
               case SwearOathResult.CannotSwearPeace:
                  return ActionOutcome.Refused($"({npcHero.Name} leads no faction of their own, and cannot swear a peace.)");
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

               default:
                  msg = $"{npcHero.Name} swears to stand at your side in battle within {CommitmentPolicy.DeadlineDays} days.";

                  break;
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

         if (!HarmPrisonerPolicy.CanHarm(playerIsCaptiveOfCaptor, _captiveSceneActive, hardcore))
         {
            ModLog.Info($"[ACTION] harm_prisoner refused by HarmPrisonerPolicy (captive={playerIsCaptiveOfCaptor}, " +
                        $"sceneActive={_captiveSceneActive}, hardcore={hardcore}).");

            return ActionOutcome.Refused("(No true harm befalls you here. Nothing happened.)");
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

         ConceptionRole role = ConceptionResolver.Resolve(
            player.IsFemale, player.IsAlive, player.Age,
            npcHero.IsFemale, npcHero.IsAlive, npcHero.Age);

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
            const int MercenaryAwardMultiplier = 50; // engine default for ApplyByJoinFactionAsMercenary

            // shouldStayInKingdomUntil = Now (no forced lock-in beyond the standard contract logic)
            ChangeKingdomAction.ApplyByJoinFactionAsMercenary(
               playerClan, kingdom, CampaignTime.Now, MercenaryAwardMultiplier);

            ModLog.Info($"[ACTION] join_as_mercenary: player clan joined {kingdom.Name} as mercenary (award multiplier {MercenaryAwardMultiplier}).");

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
            ChangeKingdomAction.ApplyByLeaveKingdomAsMercenary(playerClan);

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

            if (!VassalageOfferPolicy.CanJoin(npcIsKingdomRuler, playerAlreadyVassal, playerAtWarWithKingdom))
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

            ChangeKingdomAction.ApplyByJoinToKingdom(playerClan, kingdom);

            ModLog.Info($"[ACTION] join_as_vassal: player clan sworn as vassal of {kingdom.Name}.");

            return new ActionOutcome($"Your clan is sworn as a vassal of {kingdom.Name}.", true);
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

            if (profile != null && !StanceGatePolicy.CommandsRespectToJoinClan(StanceService.ToStance(profile).Respect, profile.ClanRelationWithPlayer ?? 0))
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

            int asking = Campaign.Current.Models.CompanionHiringPriceCalculationModel
                                 .GetCompanionHiringPrice(npcHero);
            if (asking <= 0) asking = 1;

            TryGetInt(action.Parameters, "price", out int proposed);
            if (proposed <= 0) proposed = asking;
            int price = ClampInt(proposed, (int) (asking * 0.75f), Math.Max(1, (int) (asking * 1.25f)));

            if (Hero.MainHero.Gold < price)
               return ActionOutcome.Refused($"(You cannot afford the agreed {price} denars.)");

            return DoJoin(npcHero, price,
               $"hired for {price} denars (proposed {proposed}, vanilla asking {asking})");
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

            if (npcHero.Clan == Clan.PlayerClan)
               return ActionOutcome.Refused(
                  $"({npcHero.Name} belongs to your own clan — custom forbids it.)");

            if (!MarriageProspectResolver.IsMarriageable(player, npcHero))
               return ActionOutcome.Refused(
                  $"({npcHero.Name} cannot be wed to you under the laws of Calradia.)");

            // M-B5: vanilla blocks a marriage across an active war or with a captive at its own screen; a chat
            // marriage must honour the same law (the prompt is advice, the bridge is law).
            if (!MarriageGuardPolicy.MarriageAllowed(npcHero.IsPrisoner, player.IsPrisoner, IsAtWarWithPlayer(npcHero)))
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
            bool forcedUnion = ForcedMarriagePolicy.IsForcedUnion(bestRegard);

            if (forcedUnion && !(MarriageApprovalBehavior.Instance?.IsApproved(npcHero.StringId) ?? false))
               return ActionOutcome.Refused(
                  $"({npcHero.Name} does not yet hold you dear enough to take this step, " +
                  "and no family blessing compels them to.)");

            MarriageAction.Apply(player, npcHero);

            // The political match is sealed, and the bride's unwon heart takes shape as a real, visible,
            // RESOLVABLE grievance: it colours her prompt, shows on her encyclopedia page, tarnishes her
            // kindnesses, and fades if the marriage is lived well. Planted AFTER the union so a refused plant
            // (a merciful nature's trait-raised floor may decline a faint one) never blocks the wedding.
            // KnownToPlayer: a bride wed by her family's will hides nothing about how she came to the altar.
            if (forcedUnion)
            {
               int resentment = ForcedMarriagePolicy.GrudgeMagnitude(bestRegard);
               bool planted = GrudgeBehavior.Instance?.TryPlant(npcHero, player, GrudgeSource.ForcedMarriage,
                  "Wed to them by my family's will, not my own.", resentment, true,
                  (int) CampaignTime.Now.ToDays) ?? false;
               ModLog.Info($"[ACTION] marry: a FORCED union (best regard {bestRegard} < " +
                           $"{MarriageEligibility.MarriageEligibilityThreshold}); resentment {resentment} " +
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
               marriedProfile.Romantic.Status = RomanticStatus.Committed;
               marriedProfile.Romantic.IsSecretLover = false;
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

            profile.Romantic.Status = RomanticStatus.Committed;
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

            if (profile.Romantic == null)
               return ActionOutcome.Refused($"({npcHero.Name} has no romantic connection to build upon.)");
            if (!npcHero.IsPlayerCompanion)
               return ActionOutcome.Refused($"({npcHero.Name} is not one of your own companions.)");
            if (!profile.Romantic.IsCompatibleWith(Hero.MainHero.IsFemale))
               return ActionOutcome.Refused($"(There is no such spark between you and {npcHero.Name}.)");
            if (profile.Romantic.IsConsort || profile.Romantic.IsSecretLover || npcHero == Hero.MainHero.Spouse)
               return ActionOutcome.Refused($"(You and {npcHero.Name} are already bound.)");
            if (profile.ReputationWithPlayer < 50)
               return ActionOutcome.Refused($"({npcHero.Name} is not yet close enough to you for this.)");

            profile.Romantic.Status = RomanticStatus.SecretLover;
            profile.Romantic.IsSecretLover = true;

            ModLog.Info($"[ACTION] take_as_secret_lover: {npcHero.Name} is now the player's secret lover (kept discreet, no world-event).");

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
      ///   aggrieved (see <see cref="Logic.OpenRelationshipPolicy" />). GUARD (the bridge is law): the target
      ///   must be the player's spouse, or a committed consort (<c>IsConsort</c> / <c>Status == Committed</c>);
      ///   anything less is refused, never a silent no-op. Mirrors <see cref="ExecuteTakeAsConsort" />'s
      ///   commitment path (mod-tracked only, no engine marriage call), with a hard partner-only guard.
      /// </summary>
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
         amount = Math.Min(ClampInt(amount, 1, 10000), Hero.MainHero.Gold);

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
            bool coercedPayment = silenced || owed > 0;
            if (!coercedPayment)
               TryPlantValueAppreciation(npcHero, AppreciationSource.GoldReceived, amount, "The player gave me gold.");

            // Grudges pillar (Gift gesture): a payment to the NPC may also soften or mend a grudge they
            // hold against the player, a channel separate from the relation bump above — never lets a
            // resolution failure break the gold transfer itself.
            string grudgeTail = TryGrudgeGiftTail(npcHero, amount);

            string tail = silenced
               ? " Her silence is bought, for now."
               : owed > amount
                  ? $" It is not enough: she wants {owed} denars for her silence."
                  : grudgeTail;

            return new ActionOutcome($"You give {npcHero.Name} {amount} denars (purse: {Hero.MainHero.Gold}).{tail}", false,
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
               new NemesisTurnFacts(stance.Fear, stance.Respect, grudge, honor, valor, calculating));

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
      ///   test so recruit_prisoner honours the same "held by the player" fact the rest of the mod uses.
      /// </summary>
      private static bool IsHeldByPlayer(Hero prisoner)
      {
         if (prisoner?.IsPrisoner != true) return false;

         var captor = prisoner.PartyBelongedToAsPrisoner;

         if (captor == null) return false;
         if (captor.LeaderHero == Hero.MainHero) return true;

         Clan captorClan = captor.LeaderHero?.Clan ?? captor.Settlement?.OwnerClan;

         return captorClan != null && captorClan == Clan.PlayerClan;
      }

      private static ItemRosterElement FindItemInRoster(string itemName)
      {
         if (MobileParty.MainParty?.ItemRoster == null) return default;
         foreach (ItemRosterElement el in MobileParty.MainParty.ItemRoster)
         {
            if (el.Amount <= 0) continue;
            ItemObject item = el.EquipmentElement.Item;

            if (item == null) continue;

            if (string.Equals(item.Name.ToString(), itemName, StringComparison.OrdinalIgnoreCase))
               return el;
         }

         return default;
      }

      /// <summary>Finds a roster element by the item's StringId (the bag-picked gift path).</summary>
      private static ItemRosterElement FindRosterElementById(string itemId)
      {
         if (MobileParty.MainParty?.ItemRoster == null) return default;
         foreach (ItemRosterElement el in MobileParty.MainParty.ItemRoster)
         {
            if (el.Amount <= 0) continue;
            ItemObject item = el.EquipmentElement.Item;

            if (item != null && item.StringId == itemId) return el;
         }

         return default;
      }

      /// <summary>Finds an item in the roster by its StringId, reporting how many the player holds.</summary>
      private static ItemObject FindRosterItem(ItemRoster roster, string itemId, out int available)
      {
         available = 0;
         foreach (ItemRosterElement el in roster)
         {
            ItemObject item = el.EquipmentElement.Item;

            if (item == null || item.StringId != itemId || el.Amount <= 0) continue;
            available = el.Amount;

            return item;
         }

         return null;
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
         if (!MarriageGuardPolicy.BlessingAllowed(npcHero.IsAlive, npcHero.IsPrisoner, IsAtWarWithPlayer(npcHero), blesserRegard))
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

         QuestRewardPayoutVerdict verdict = QuestRewardGatePolicy.DecideFiefPayout(
            ModSettings.AllowFiefAndMarriageQuestRewards, giverAlive, giverLeadsOwnClan, fiefStillOwned, atWar);

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

         QuestRewardPayoutVerdict verdict = QuestRewardGatePolicy.DecideMarriageRewardPayout(
            ModSettings.AllowFiefAndMarriageQuestRewards, giverAlive, hasAuthority, marriageable, atWar, playerReigns);

         if (verdict != QuestRewardPayoutVerdict.Deliver)
            return DegradeHeavyReward(npcHero, profile, $"a marriage with {quest.MarriageSpouseName ?? "the promised spouse"}", atWar);

         MarriageAction.Apply(Hero.MainHero, spouse);
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
         if (profile != null) ProfileMutator.ApplyReputationDelta(profile, compensationRelation);

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

         return StanceGatePolicy.ResolveCoercion(stance.Fear, stance.Respect, honor, valor, calculating);
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
      private static void TryPlantValueAppreciation(Hero npcHero, AppreciationSource source, int value, string cause)
      {
         try
         {
            if (npcHero == null) return;

            var today = (int) CampaignTime.Now.ToDays;
            int magnitude = AppreciationValueScaling.FromValue(value);
            AppreciationBehavior.Instance?.TryPlant(npcHero, Hero.MainHero, source, cause, magnitude, true, today);
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

         int delta = RelationGate.Resolve(
            proposed, context.Profile, context.EmittedEvent, CampaignTime.Now.ToHours, captive,
            intimacyAllowed, ModSettings.RelationWarmth);

         if (delta == 0)
         {
            ModLog.Info($"[ACTION] change_relation: proposed {proposed:+#;-#;0} gated to 0 (cooldown/clamp).");

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
         ProfileMutator.ApplyReputationDelta(context.Profile, delta);
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
      ///   give_prisoner: the player hands over the captive an outstanding deliver_prisoner bargain named.
      ///   The deed names the target, so the bridge resolves which held prisoner satisfies it (no picker),
      ///   transfers them to the lord, and settles the bargain — reward, favour, journal close — in one
      ///   turn (mirrors SettleItemBargain). A spurious emission (no bargain, or the captive not held) is
      ///   reported, never faked.
      /// </summary>
      private ActionOutcome ExecuteGivePrisoner(Hero npcHero, ActionContext context)
      {
         try
         {
            NpcProfile profile = context?.Profile;

            if (profile == null || npcHero == null) return null;

            InformalQuest quest = profile.ActiveQuests.FirstOrDefault(q =>
               q.Type == QuestType.DeliverPrisoner && q.IsOutstanding);

            // Was a silent no-op: the model had already narrated the captive as handed over while the
            // prisoner roster (and any bargain) never moved. No such bargain, so surface the refusal.
            if (quest == null) return ActionOutcome.Refused(SilentRefusalMessages.GivePrisonerNoBargain());

            var target = new PrisonerDeedTarget(quest.TargetHeroId, quest.TargetFactionId);

            CharacterObject prisonerChar = null;
            Hero prisonerHero = null;
            TroopRoster prison = MobileParty.MainParty?.PrisonRoster;
            if (prison != null)
               foreach (TroopRosterElement el in prison.GetTroopRoster())
               {
                  Hero h = el.Character?.HeroObject;

                  if (h == null || !target.Matches(h.StringId, h.MapFaction?.StringId)) continue;
                  prisonerChar = el.Character;
                  prisonerHero = h;

                  break;
               }

            if (prisonerChar == null)
               return ActionOutcome.Refused($"(You do not hold {quest.TargetHeroName ?? "the captive"} I asked for.)");

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

            TransferPrisonerAction.Apply(prisonerChar, MobileParty.MainParty.Party, dest);

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

            // Grudges pillar (Tier 2, DeliveredPrisoner): the captive ALWAYS resents being handed onward
            // (a betrayal of the custody the player held over them), heavier when handed to their own foe.
            // Guarded in its own try/catch so a plant failure never claws back the transfer that already
            // succeeded above.
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
            }
            catch (Exception ex)
            {
               ModLog.Error($"[ACTION] give_prisoner grudge plant failed: {ex.GetType().Name}: {ex.Message}");
            }

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

      #endregion
   }
}
