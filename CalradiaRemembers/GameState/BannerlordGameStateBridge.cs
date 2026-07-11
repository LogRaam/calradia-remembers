// Code written by Gabriel Mailhot, 24/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Commitments;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Marriage;
using CalradiaRemembers.Missions;
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

      /// <summary>Appreciations pillar: the fixed formation magnitude for a RansomedOrFreed plant (free_prisoner / release_prisoner) — comparable to Tier2GrudgeRules.DeliveredPrisonerBaseMagnitude, being freed weighing roughly as much as delivering a prisoner does on the other side of the ledger.</summary>
      private const int RansomedOrFreedMagnitude = 35;

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

      // ── Sprint 17: public humiliation standing penalty ────────────────────
      private const int RenownLossPerWitness = 2;

      // Set when a harm_prisoner action wounds the player during the current captive scene, so
      // ApplyForcedSceneToll knows not to double up. Reset at each scene's start.
      private static bool _captiveHarmApplied;
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
         _sceneHarmCount = 0;
         _pendingEscape = false;
         _pendingRelease = false;
         _pendingReleaseCaptor = null;
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
            case "join_party":
               return ExecuteJoinParty(action, npcHero);
            case "join_as_mercenary":
               return ExecuteJoinAsMercenary(npcHero);
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
            case "give_item":
               return ExecuteGiveItem(action, npcHero, context);
            case "give_prisoner":
               return ExecuteGivePrisoner(npcHero, context);
            case "free_prisoner":
               return ExecuteFreePrisoner(npcHero, context);
            case "execute_prisoner":
               return ExecuteExecutePrisoner(npcHero);
            case "turn_nemesis":
               return ExecuteTurnNemesis(npcHero, context?.Profile);
            case "grant_blessing":
               return ExecuteGrantBlessing(action, npcHero);
            case "harm_prisoner":
               return ExecuteHarmPrisoner(action);
            case "impregnation_risk":
               return ExecuteImpregnationRisk(npcHero);
            case "gather_news":
               return ExecuteGatherNews(action, npcHero);
            case "reassure_companion":
               return ExecuteReassureCompanion(npcHero);
            case "recall_companion":
               return ExecuteRecallCompanion(npcHero);
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
               ModLog.Info($"[ACTION] Unknown type '{action.Type}' — ignored.");

               return null;
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

         if (settlement.IsCastle || settlement.IsFortification)
            return SceneType.Keep;

         if (settlement.IsTown || settlement.IsVillage)
            return SceneType.Settlement;

         return SceneType.Outdoor;
      }

      public DiplomaticStatus GetDiplomaticStatus(Hero npc)
      {
         if (npc == null || npc.MapFaction == null || Hero.MainHero?.MapFaction == null)
            return DiplomaticStatus.Unknown;

         // Same faction (or same clan, both treated as "allied" here)
         if (npc.MapFaction == Hero.MainHero.MapFaction)
            return DiplomaticStatus.Allied;

         if (FactionManager.IsAtWarAgainstFaction(npc.MapFaction, Hero.MainHero.MapFaction))
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
      {
         if (npc == null || Hero.MainHero == null)
            return PlayerStatusVsNpc.Unknown;

         // Player is the NPC's captive
         if (Hero.MainHero.IsPrisoner && Hero.MainHero.PartyBelongedToAsPrisoner?.LeaderHero == npc)
            return PlayerStatusVsNpc.Captive;

         // NPC is the player's captive
         if (npc.IsPrisoner && npc.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero)
            return PlayerStatusVsNpc.NpcIsCaptive;

         // Same clan
         if (npc.Clan != null && npc.Clan == Clan.PlayerClan)
            return PlayerStatusVsNpc.ClanMember;

         // Player is a vassal of the NPC's kingdom (and NPC leads it)
         if (Hero.MainHero.Clan?.Kingdom?.Leader == npc && npc != Hero.MainHero)
            return PlayerStatusVsNpc.Liege;

         // NPC is a vassal of the player's kingdom (player leads it)
         if (npc.Clan?.Kingdom?.Leader == Hero.MainHero && Hero.MainHero.Clan?.Kingdom != null)
            return PlayerStatusVsNpc.Vassal;

         return PlayerStatusVsNpc.Free;
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

               // Recognized but not yet executable — declined cleanly rather than faked.
               // The quest factory refuses to issue these, so this is a defensive backstop.
               case RewardGrant.GiveItem:
               case RewardGrant.GiveTroops:
               case RewardGrant.ReleasePrisoner:
                  ModLog.Info($"[GRANT] '{quest.Reward}' not yet supported — declined for {npcHero.Name}.");

                  return new ActionOutcome($"({npcHero.Name} cannot honor that kind of bargain yet.)", false);

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
            // Verified reward: applied straight to the personal score, bypassing the
            // RelationGate cooldown/cap — the deed was confirmed by real game events.
            int before = profile.ReputationWithPlayer;
            int after = ClampInt(before + relation, -100, 100);
            profile.ReputationWithPlayer = after;
            profile.LastRelationGainHour = CampaignTime.Now.ToHours;
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
         double chance = Nemesis.NemesisBehavior.Instance?.ActiveNemesisEscapeChance() ?? EscapeChance;
         if (!force && MBRandom.RandomFloat >= chance) return false;

         // Commit the outcome NOW (irreversible), but defer the actual campaign action to scene
         // close: freeing mid-chat could pop an encounter/menu and disrupt the open scene window.
         _pendingEscape = true;
         ModLog.Info("[ESCAPE] attempt SUCCEEDED — freeing committed (applied at scene close).");

         return true;
      }

      /// <inheritdoc />
      public bool CommitCaptorRelease(Hero captor)
      {
         try
         {
            if (captor == null) return false;

            // The prompt only PROPOSES; the bridge is the law. Re-validate the same gate the teaching used.
            CaptorReleaseVerdict verdict = ResolveCaptorReleaseVerdict(captor);
            if (!verdict.AllowsPersuasion) return false;

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

      /// <summary>
      ///   Re-validates a captor's willingness to free the player via the pure <see cref="CaptorReleasePolicy" />,
      ///   reading the captor's Mercy and the heaviest live grudge they hold against the player. Mirrors the
      ///   advice-side gate in <c>EncounterContextBuilder</c> so the prompt and the bridge cannot disagree.
      /// </summary>
      private static CaptorReleaseVerdict ResolveCaptorReleaseVerdict(Hero captor)
      {
         int mercy = TraitLevelOf(captor, DefaultTraits.Mercy);
         int grudge = HeaviestGrudgeAgainstPlayer(captor);

         return CaptorReleasePolicy.Resolve(mercy, grudge, isNemesisVendetta: false);
      }

      /// <summary>The magnitude of the heaviest LIVE grudge this hero holds against the player, or 0 when none.</summary>
      private static int HeaviestGrudgeAgainstPlayer(Hero hero)
      {
         var grudges = GrudgeBehavior.Instance?.GrudgesFor(hero?.StringId);
         if (grudges == null) return 0;

         string playerId = Hero.MainHero?.StringId;
         int max = 0;
         foreach (var g in grudges)
            if (!g.IsResolved && g.TargetId == playerId && g.Magnitude > max) max = g.Magnitude;

         return max;
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
               return new ActionOutcome(
                  $"({giverHero?.Name}: these goods are worth about {total} denars — I asked for " + $"{quest.RequiredItemValue}. Bring more.)", false);

            // Value met: hand the goods over, then honor the favor.
            foreach ((ItemObject item, int count) in toRemove)
               roster.AddToCounts(item, -count);

            ModLog.Info($"[BARGAIN] {giverHero?.Name}: accepted goods worth {total} denars " + $"(required {quest.RequiredItemValue}) for {quest.Reward}.");

            ActionOutcome grant = GrantConditionalReward(giverHero, giver, quest);

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
            string label = Items.SupplyItemMatcher.Label(categoryKey);

            var toRemove = new List<(ItemObject item, int count)>();
            var count = 0;
            foreach (ItemOffer offer in offered)
            {
               if (string.IsNullOrEmpty(offer.ItemId) || offer.Count <= 0) continue;
               ItemObject item = FindRosterItem(roster, offer.ItemId, out int available);

               if (item == null || available <= 0 || !Items.SupplyItemMatcher.Matches(item, categoryKey)) continue;
               int take = Math.Min(offer.Count, available);
               count += take;
               toRemove.Add((item, take));
            }

            if (count < quest.RequiredItemCount)
               return new ActionOutcome($"({giverHero?.Name}: that is only {count} of the {quest.RequiredItemCount} {label} I asked for. Bring more.)", false);

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
            Quests.QuestJournalBridge.CloseAsCompleted(quest, giverHero);
            Stance.StanceService.OnPromiseKept(giver);
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
      ///   Performs the actual hire — the native actions shared by the cash hire and the
      ///   in-kind bargain. The caller owns the price logic and affordability check; this
      ///   re-validates the NPC is genuinely recruitable (the bridge is the law) and then
      ///   transfers any cash, adds the companion to the clan, and moves them into the party.
      ///   A price of 0 means the payment was made some other way (goods already handed over).
      /// </summary>
      private static ActionOutcome DoJoin(Hero npcHero, int price, string logDetail)
      {
         if (!RecruitmentPricing.IsRecruitable(npcHero))
            return new ActionOutcome($"({npcHero?.Name} is not free to take service.)", false);

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
      ///   free_prisoner: a captive lord the PLAYER holds has struck a bargain for their freedom and the
      ///   player agreed — release them. The reward they promised (goodwill, a ransom, or service) rides
      ///   alongside as the LLM's own change_relation / give_gold / join_clan actions. Guarded: only a
      ///   hero the player actually holds prisoner is freed, so a stray emission can never let one escape.
      ///   Stance mechanical behaviour-gating (increment 1, Fear): re-validated here, never trusted from
      ///   the LLM's own framing of the bargain — <see cref="StanceGatePolicy.ResolveCoercion"/> reads the
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
               return new ActionOutcome($"({npcHero.Name} is not yours to set free.)", false);

            CoercionResult coercion = ResolveFreePrisonerCoercion(npcHero, context);

            if (coercion.Outcome == CoercionOutcome.RefusesFlat)
            {
               ModLog.Info($"[ACTION] free_prisoner refused: {npcHero.Name} is unafraid and unmoved by the bargain.");

               return new ActionOutcome($"{npcHero.Name} is not afraid enough to bargain, and refuses to be swayed.", false);
            }

            EndCaptivityAction.ApplyByReleasedByChoice(npcHero, Hero.MainHero);
            StanceService.OnGiftReceived(npcHero); // freedom is a gift — a little trust is earned
            ModLog.Info($"[ACTION] free_prisoner: player released {npcHero.Name} from captivity.");

            // Appreciations pillar: being handed back one's own freedom is a remembered kindness in its
            // own right, a channel separate from the trust bump above.
            var today = (int) CampaignTime.Now.ToDays;
            AppreciationBehavior.Instance?.TryPlant(npcHero, Hero.MainHero, AppreciationSource.RansomedOrFreed,
               "The player gave me back my freedom.", RansomedOrFreedMagnitude, knownToPlayer: true, today);

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
               string pronoun = npcHero.IsFemale ? "her" : "his";
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
      ///   execute_prisoner: the player, mid PLAYER-AS-CAPTOR scene (the mod's <c>ChatViewModel.ActivateCaptorSceneMode</c>),
      ///   states a real, final killing act against the prisoner they hold, and the LLM recognized it (see
      ///   the SDK's <c>PromptBuilder.AppendPlayerCaptorSceneRules</c>). Guarded exactly like free_prisoner:
      ///   the prompt is advice, the bridge is law, so only a hero the player ACTUALLY holds prisoner right
      ///   now is executed; a stray or premature emission never kills the wrong person. Runs the same real
      ///   vanilla <see cref="KillCharacterAction.ApplyByExecution"/> the old map-popup entry point used,
      ///   which raises <see cref="CampaignEvents.HeroKilledEvent"/> with detail Executed: existing
      ///   listeners fire off that same event without any special-casing here. For a tracked nemesis,
      ///   <see cref="Nemesis.NemesisResolutionBehavior"/> records the closure deed; for any other captured
      ///   lord, <see cref="Grudges.ExecutedKinGrudgeBehavior"/> plants a vendetta on their living kin,
      ///   exactly as it already does for any other execution in the game.
      /// </summary>
      private static ActionOutcome ExecuteExecutePrisoner(Hero npcHero)
      {
         try
         {
            if (npcHero == null) return null;
            if (!npcHero.IsPrisoner || npcHero.PartyBelongedToAsPrisoner?.LeaderHero != Hero.MainHero)
               return new ActionOutcome($"({npcHero.Name} is not yours to execute.)", false);

            string name = npcHero.Name?.ToString() ?? "the prisoner";
            KillCharacterAction.ApplyByExecution(npcHero, Hero.MainHero, true);
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

            Hero held = Nemesis.NemesisBehavior.Instance?.PlayerHeldNemesisPrisoner();
            if (held == null || held != npcHero)
               return new ActionOutcome($"({npcHero.Name} is not a nemesis you hold prisoner to turn.)", false);

            NpcProfile p = profile ?? Behaviors.NpcMemoryCampaignBehavior.Instance?.Store?.Get(npcHero.StringId);
            if (p == null)
               return new ActionOutcome($"({npcHero.Name} cannot be read well enough to judge whether he would turn.)", false);

            NpcStance stance = StanceService.ToStance(p);
            int honor = TraitLevelOf(npcHero, DefaultTraits.Honor);
            int valor = TraitLevelOf(npcHero, DefaultTraits.Valor);
            int calculating = TraitLevelOf(npcHero, DefaultTraits.Calculating);
            int grudge = NemesisGrudgeAgainstPlayer(npcHero);

            NemesisTurnVerdict verdict = NemesisTurnPolicy.Resolve(
               new NemesisTurnFacts(stance.Fear, stance.Respect, grudge, honor, valor, calculating));

            if (!verdict.CanTurn)
               return new ActionOutcome($"({npcHero.Name} refuses to bend: he {TurnRefusalPhrase(verdict.Refusal)}.)", false);

            // He turns. Free him, swear him into the player's clan and party, and close the vendetta.
            Clan oldClan = npcHero.Clan;
            EndCaptivityAction.ApplyByReleasedByChoice(npcHero, Hero.MainHero);
            npcHero.Clan = Clan.PlayerClan;
            if (MobileParty.MainParty != null) AddHeroToPartyAction.Apply(npcHero, MobileParty.MainParty);

            // Identity + chronicle side (marks TurnedDay, clears his grudge against the player, records the deed).
            Nemesis.NemesisResolutionBehavior.Turn(npcHero);

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

      /// <summary>The deepest grudge the nemesis holds specifically against the player (0..100), or 0 when none.</summary>
      private static int NemesisGrudgeAgainstPlayer(Hero nemesis)
      {
         try
         {
            var grudges = Grudges.GrudgeBehavior.Instance?.GrudgesFor(nemesis.StringId);
            if (grudges == null) return 0;

            var max = 0;
            foreach (var g in grudges)
               if (g != null && string.Equals(g.TargetId, Hero.MainHero?.StringId, StringComparison.Ordinal) && g.Magnitude > max)
                  max = g.Magnitude;

            return max;
         }
         catch { return 0; }
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
      ///   Stance mechanical behaviour-gating (increment 1, Fear): reads the captive's LIVE Fear/Respect
      ///   from the persisted profile and traits from the live Hero, then resolves
      ///   <see cref="StanceGatePolicy.ResolveCoercion"/>. A MISSING profile (no stance data at all —
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

      /// <summary>A hero's trait level, or 0 when the hero or the trait cannot be read (never throws).</summary>
      private static int TraitLevelOf(Hero hero, TraitObject trait)
      {
         try { return hero?.GetTraitLevel(trait) ?? 0; }
         catch { return 0; }
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
               "They cowed me into freeing them out of fear, not goodwill.", magnitude, knownToPlayer: false, today);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[STANCE] coerced-release grudge plant failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      // give_influence, lend_troops, sway_opinion, release_prisoner executors moved to
      // CalradiaRemembers.Verbs (GiveInfluenceVerb / LendTroopsVerb / SwayOpinionVerb / ReleasePrisonerVerb),
      // routed by VerbRegistry from the switch above. Reuse CourtActionResolver/CourtActionPolicy exactly
      // as before; see those classes for the eligibility gates and cooldown markers. give_troops /
      // spend_influence (04/07/2026, GiveTroopsVerb / SpendInfluenceVerb) are the reciprocal direction,
      // routed identically.

      /// <summary>
      ///   gather_news action: the player asked one of their companions to ride out and bring back word of
      ///   the realm, and the companion agreed. Dispatches the errand (the behavior re-validates every guard
      ///   — the prompt is advice, the bridge is law) and reports back to the player.
      /// </summary>
      private static ActionOutcome ExecuteGatherNews(GameAction action, Hero npcHero)
      {
         try
         {
            if (npcHero == null) return null;
            var behavior = CompanionMissionBehavior.Instance;

            if (behavior == null) return null;

            // Optional topic the LLM extracted from the player's request ("news about Sturgia" → Sturgia);
            // null means any/nearest town. The behavior resolves it to a destination.
            action.Parameters.TryGetValue("about", out string about);

            // Which errand the player asked for (news / scout / steal / trade / envoy); defaults to news.
            action.Parameters.TryGetValue("errand", out string errand);

            (bool dispatched, string message) = behavior.Dispatch(npcHero, MapErrand(errand), about);
            ModLog.Info($"[ACTION] gather_news: {npcHero.Name} → {(dispatched ? "dispatched" : "refused")}: {message}");

            return new ActionOutcome(message, dispatched);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] gather_news failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      private static ActionOutcome ExecuteGiveGold(GameAction action, Hero npcHero)
      {
         if (!TryGetInt(action.Parameters, "amount", out int amount)) return null;
         if (amount <= 0 || npcHero == null || Hero.MainHero == null) return null;

         amount = ClampInt(amount, 1, 10000);
         GiveGoldAction.ApplyBetweenCharacters(npcHero, Hero.MainHero, amount);
         ModLog.Info($"[ACTION] give_gold: {npcHero.Name} → player {amount} gold");

         // The map bar does not refresh while campaign time is frozen under the chat
         // overlay — showing the balance here is the player's only live confirmation.
         return new ActionOutcome($"{npcHero.Name} gives you {amount} denars (purse: {Hero.MainHero.Gold}).", true);
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

            if (string.IsNullOrWhiteSpace(itemName) && string.IsNullOrWhiteSpace(itemId)) return null;

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

               return new ActionOutcome($"(You don't seem to have '{label}' to give.)", false);
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
            string grudgeTail = npcHero != null ? TryGrudgeGiftTail(npcHero, value) : "";

            return new ActionOutcome($"You give {actualName} to {npcHero?.Name}.{grudgeTail}", false);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] give_item failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

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
               return new ActionOutcome("(You are already wed — the blessing cannot apply.)", false);

            if (!action.Parameters.TryGetValue("hero", out string spouseName) || string.IsNullOrWhiteSpace(spouseName)) return null;

            Hero spouse = MarriageProspectResolver.ResolveCandidate(npcHero, spouseName);

            if (spouse == null)
               return new ActionOutcome($"({npcHero.Name} names no one of their house by '{spouseName}'.)", false);

            if (!MarriageProspectResolver.HasAuthority(npcHero, spouse))
               return new ActionOutcome($"({npcHero.Name} has no authority to bless that match.)", false);

            if (!MarriageProspectResolver.IsMarriageable(Hero.MainHero, spouse))
               return new ActionOutcome($"({spouse.Name} is not free to be matched with you.)", false);

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

      private static ActionOutcome ExecuteHarmPrisoner(GameAction action)
      {
         var player = Hero.MainHero;

         if (player == null) return null;

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
               return new ActionOutcome($"({npcHero.Name} does not belong to a kingdom.)", false);

            var playerClan = Clan.PlayerClan;

            if (playerClan == null) return null;

            if (playerClan.Kingdom != null)
               return new ActionOutcome(
                  $"(Your clan is already sworn to {playerClan.Kingdom.Name}. " + "Settle that obligation first.)", false);

            // shouldStayInKingdomUntil = Now (no forced lock-in beyond the standard contract logic)
            // awardMultiplier = 1 (standard mercenary wage)
            ChangeKingdomAction.ApplyByJoinFactionAsMercenary(
               playerClan, kingdom, CampaignTime.Now, 1);

            ModLog.Info($"[ACTION] join_as_mercenary: player clan joined {kingdom.Name} as mercenary.");

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
               return new ActionOutcome($"({npcHero.Name} will not leave their house to join yours — not yet.)", false);

            if (profile != null && !StanceGatePolicy.CommandsRespectToJoinClan(StanceService.ToStance(profile).Respect, profile.ClanRelationWithPlayer ?? 0))
               return new ActionOutcome($"({npcHero.Name} does not respect you enough to throw in their lot with you. Earn their esteem first.)", false);

            Clan oldClan = npcHero.Clan;

            // Pull them out of any kingdom obligation first, then move them under the player's banner.
            if (oldClan?.Kingdom != null && oldClan.Leader == npcHero && oldClan.Kingdom != playerClan.Kingdom)
               return new ActionOutcome(
                  $"({npcHero.Name} leads a clan sworn to {oldClan.Kingdom.Name}; such a defection cannot be sealed in mere conversation.)", false);

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
               return new ActionOutcome($"({npcHero.Name} is not free to take service.)", false);

            var clan = Clan.PlayerClan;

            if (clan == null) return null;
            if (clan.Companions.Count >= clan.CompanionLimit)
               return new ActionOutcome(
                  $"(Your clan cannot support another companion — limit {clan.CompanionLimit}.)", false);

            int asking = Campaign.Current.Models.CompanionHiringPriceCalculationModel
                                 .GetCompanionHiringPrice(npcHero);
            if (asking <= 0) asking = 1;

            TryGetInt(action.Parameters, "price", out int proposed);
            if (proposed <= 0) proposed = asking;
            int price = ClampInt(proposed, (int) (asking * 0.75f), Math.Max(1, (int) (asking * 1.25f)));

            if (Hero.MainHero.Gold < price)
               return new ActionOutcome($"(You cannot afford the agreed {price} denars.)", false);

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
               return new ActionOutcome(
                  "(You are already wed — no second marriage can be sealed in law.)", false);

            if (npcHero.Clan == Clan.PlayerClan)
               return new ActionOutcome(
                  $"({npcHero.Name} belongs to your own clan — custom forbids it.)", false);

            if (!MarriageProspectResolver.IsMarriageable(player, npcHero))
               return new ActionOutcome(
                  $"({npcHero.Name} cannot be wed to you under the laws of Calradia.)", false);

            int reputation = context?.Profile?.ReputationWithPlayer ?? 0;
            int vanillaRelation = npcHero.GetRelation(player);

            if (reputation < MarriageEligibility.MarriageEligibilityThreshold &&
                vanillaRelation < MarriageEligibility.MarriageEligibilityThreshold)
               return new ActionOutcome(
                  $"({npcHero.Name} does not yet hold you dear enough to take this step.)", false);

            MarriageAction.Apply(player, npcHero);
            ModLog.Info($"[ACTION] marry: player wed {npcHero.Name}.");

            return new ActionOutcome(
               $"You and {npcHero.Name} are joined in marriage — bound together before Calradia.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] marry failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
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
            Hero player = Hero.MainHero;

            if (player == null || npcHero == null || npcHero != player.Spouse)
               return new ActionOutcome("(There is no marriage between you and this person to end.)", false);

            var demands = SpouseDivorceDemandBehavior.Instance;
            bool demanding = demands?.IsDemanding(npcHero.StringId) == true;
            bool estranging = demands?.IsEstranging(npcHero.StringId) == true;

            if (demands == null || (!demanding && !estranging))
               return new ActionOutcome($"({npcHero.Name} has not pressed you to end the marriage.)", false);

            int spouseVsPlayerBefore = npcHero.GetRelation(player);

            if (!MarriageDissolutionService.TryDissolvePlayerMarriage(npcHero, out string reason))
               return new ActionOutcome($"(The marriage could not be ended: {reason}.)", false);

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
            Hero player = Hero.MainHero;

            if (player == null || npcHero == null || npcHero != player.Spouse) return null;

            var demands = SpouseDivorceDemandBehavior.Instance;

            if (demands == null || !demands.IsDemanding(npcHero.StringId)) return null;

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
            Hero player = Hero.MainHero;

            if (player == null || npcHero == null || npcHero != player.Spouse)
               return new ActionOutcome("(There is no marriage between you and this person to end.)", false);

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
               return new ActionOutcome($"(The marriage could not be ended: {reason}.)", false);

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
      ///   pledge_against (RP with teeth, increment 2): a lord, moved by genuine and standing enmity, vows
      ///   to move against a named rival. This is a POLITICAL commitment ONLY — it launches a real scheme
      ///   (slander or sabotage) via <see cref="WorldEvents.SchemeStore" /> and records a
      ///   <see cref="TrackedCommitment" /> the world will hold the lord to; it never declares, nor implies,
      ///   war. Only taught when <see cref="CourtActionResolver.ResolvePledgeRivalName" /> names a genuine
      ///   rival, but every guard is re-checked here — the prompt is advice, the bridge is law: the target
      ///   is re-resolved by name, kin are excluded again, and a lord may hold only one pledge at a time.
      ///   Reuses an already-active scheme between the pair rather than erroring (<c>ForcePlot</c>'s own
      ///   guard), so a lord already secretly plotting can still be held to account for it.
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
               return new ActionOutcome($"({npcHero.Name} already has a pledge outstanding — one vow at a time.)", false);

            action.Parameters.TryGetValue("target", out string targetName);
            Hero target = CourtActionResolver.ResolvePledgeTarget(targetName, npcHero);

            if (target == null)
               return new ActionOutcome("(No rival by that name is known to pledge against.)", false);

            if (CourtActionResolver.IsCloseKin(npcHero, target))
               return new ActionOutcome($"({npcHero.Name} will not move against their own kin.)", false);

            // A calculating lord favours quiet sabotage; others reach for slander. Never a war action —
            // the scheme is the whole of what a pledge sets in motion.
            SchemeKind kind = npcHero.GetTraitLevel(DefaultTraits.Calculating) >= 1
               ? SchemeKind.Sabotage
               : SchemeKind.Slander;
            string schemeStatus = SchemeStore.Instance?.ForcePlot(npcHero, target, kind);

            var today = (int) CampaignTime.Now.ToDays;
            behavior.Create(npcHero.StringId, target.StringId, today);

            profile.Events.Add(new NotableEvent(today, NotableEventType.Collaboration,
               $"Pledged to the player to move against {target.Name}."));

            ModLog.Info($"[ACTION] pledge_against: {npcHero.Name} pledged to move against {target.Name} ({kind}). {schemeStatus}");

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
               return new ActionOutcome("(There is no plot of theirs you could help with just now.)", false);

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
               return new ActionOutcome("(There is no plot against them you could warn of just now.)", false);

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
               return new ActionOutcome($"({npcHero.Name} has no romantic connection to build upon.)", false);

            if (profile.Romantic.Status < RomanticStatus.Intimate)
               return new ActionOutcome($"({npcHero.Name} is not yet close enough to you for this bond.)", false);

            if (profile.Romantic.IsConsort || profile.Romantic.Status == RomanticStatus.Committed)
               return new ActionOutcome($"(You and {npcHero.Name} are already bound as consorts.)", false);

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
            Jealousy.JealousyBehavior.Instance?.OnRomanticAct(
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
               return new ActionOutcome($"({npcHero.Name} has no romantic connection to build upon.)", false);
            if (!npcHero.IsPlayerCompanion)
               return new ActionOutcome($"({npcHero.Name} is not one of your own companions.)", false);
            if (!profile.Romantic.IsCompatibleWith(Hero.MainHero.IsFemale))
               return new ActionOutcome($"(There is no such spark between you and {npcHero.Name}.)", false);
            if (profile.Romantic.IsConsort || profile.Romantic.IsSecretLover || npcHero == Hero.MainHero.Spouse)
               return new ActionOutcome($"(You and {npcHero.Name} are already bound.)", false);
            if (profile.ReputationWithPlayer < 50)
               return new ActionOutcome($"({npcHero.Name} is not yet close enough to you for this.)", false);

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

      private static ActionOutcome ExecuteTakeGold(GameAction action, Hero npcHero)
      {
         if (!TryGetInt(action.Parameters, "amount", out int amount)) return null;
         if (amount <= 0 || Hero.MainHero == null) return null;

         // Never take more than the player actually carries (bandits simply empty the purse).
         amount = Math.Min(ClampInt(amount, 1, 10000), Hero.MainHero.Gold);

         if (amount <= 0) return null;

         if (npcHero != null)
         {
            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, npcHero, amount);
            CompleteProvideGoldOnPayment(npcHero, amount); // any payment satisfies AND closes a ProvideGold quest

            // Paying a scheming bastard-mother her demand IS settling the blackmail (buys her silence).
            bool silenced = Bastards.BastardBehavior.Instance?.SettleByPayment(npcHero, amount) == true;
            ModLog.Info($"[ACTION] take_gold: player → {npcHero.Name} {amount} gold{(silenced ? " (bastard silence bought)" : "")}");

            // Appreciations pillar: a payment is also a remembered kindness, a channel separate from the
            // relation bump above and from the grudge-softening tail below (a payment can soften a wound
            // AND be remembered as a kindness — both are correct).
            TryPlantValueAppreciation(npcHero, AppreciationSource.GoldReceived, amount, "The player gave me gold.");

            // Grudges pillar (Gift gesture): a payment to the NPC may also soften or mend a grudge they
            // hold against the player, a channel separate from the relation bump above — never lets a
            // resolution failure break the gold transfer itself.
            string grudgeTail = TryGrudgeGiftTail(npcHero, amount);

            string tail = silenced
               ? " Her silence is bought — for now."
               : grudgeTail;

            return new ActionOutcome($"You give {npcHero.Name} {amount} denars (purse: {Hero.MainHero.Gold}).{tail}", false);
         }

         // No hero recipient (e.g. a synthesized bandit captor) — the coin is simply taken.
         Hero.MainHero.ChangeHeroGold(-amount);
         ModLog.Info($"[ACTION] take_gold: {amount} gold extorted (no hero recipient).");

         return new ActionOutcome($"They take {amount} denars from you (purse: {Hero.MainHero.Gold}).", false);
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
            AppreciationBehavior.Instance?.TryPlant(npcHero, Hero.MainHero, source, cause, magnitude, knownToPlayer: true, today);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[APPRECIATION] value-scaled plant failed: {ex.GetType().Name}: {ex.Message}");
         }
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
            return new ActionOutcome($"({npcHero.Name} cannot formalize the betrothal just now.)", false);

         Hero spouse = string.IsNullOrEmpty(quest.MarriageSpouseId)
            ? null
            : Hero.FindFirst(h => h.StringId == quest.MarriageSpouseId);

         if (spouse == null)
            return new ActionOutcome($"({npcHero.Name}: the match can no longer be honored.)", false);

         var day = (int) CampaignTime.Now.ToDays;
         MarriageApprovalBehavior.Instance.Grant(spouse, npcHero, day);
         ModLog.Info($"[GRANT] marriage_consent: {npcHero.Name} blesses the player's marriage to {spouse.Name} (deed done).");

         return new ActionOutcome(
            $"{npcHero.Name} gives their blessing — you have earned {spouse.Name}'s hand. Their house stands with you.", true);
      }

      /// <summary>Maps the LLM's 'errand' keyword to a mission type; anything unrecognized falls back to news.</summary>
      private static CompanionMissionType MapErrand(string errand)
      {
         switch ((errand ?? "").Trim().ToLowerInvariant())
         {
            case "scout":
            case "spy": return CompanionMissionType.Spy;
            case "steal": return CompanionMissionType.Steal;
            case "trade":
            case "barter": return CompanionMissionType.Barter;
            case "envoy": return CompanionMissionType.Envoy;
            default: return CompanionMissionType.GatherNews;
         }
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

      private static List<Kingdom> RetrieveAllKingdoms()
      {
         var kingdoms = new List<Kingdom>();
         foreach (Kingdom k in Kingdom.All)
            if (!k.IsEliminated)
               kingdoms.Add(k);

         return kingdoms;
      }

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
         Stance.StanceService.OnPromiseKept(profile);
         store.Set(profile);

         MBInformationManager.AddQuickInformation(new TextObject($"{npcHero.Name} is grateful for the support."), 0, npcHero.CharacterObject);
         ModLog.Info($"[QUEST] ProvideGold completed: {npcHero.Name} received {amountGiven} denars (quest closed).");
      }

      private static bool TryGetInt(IReadOnlyDictionary<string, string> parameters, string key, out int value)
      {
         value = 0;

         return parameters.TryGetValue(key, out string raw) && int.TryParse(raw, out value);
      }

      private ActionOutcome ExecuteChangeRelation(GameAction action, Hero npcHero, ActionContext context)
      {
         if (!TryGetInt(action.Parameters, "delta", out int proposed)) return null;
         if (npcHero == null || Hero.MainHero == null || context?.Profile == null) return null;

         bool captive = GetPlayerStatus(npcHero) == PlayerStatusVsNpc.NpcIsCaptive;
         int delta = RelationGate.Resolve(
            proposed, context.Profile, context.EmittedEvent, CampaignTime.Now.ToHours, captive,
            ModSettings.RelationWarmth);

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
         int before = context.Profile.ReputationWithPlayer;
         int after = ClampInt(before + delta, -100, 100);
         context.Profile.ReputationWithPlayer = after;
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

         return new ActionOutcome(message, delta > 0);
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

            if (quest == null) return null; // no such bargain — ignore a stray emission

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
               return new ActionOutcome($"(You do not hold {quest.TargetHeroName ?? "the captive"} I asked for.)", false);

            // Captured BEFORE the transfer, per the rule's own "prior regard" framing (mirrors
            // DivorceFacts.SpouseRegardForPlayer's low-regard-nurses-harder read).
            int captiveRegardForPlayer = Hero.MainHero != null ? prisonerHero.GetRelation(Hero.MainHero) : 0;

            // Hand the captive to the lord's party; if they ride with none, take them off the player's hands.
            PartyBase dest = npcHero.PartyBelongedTo?.Party;
            if (dest != null) TransferPrisonerAction.Apply(prisonerChar, MobileParty.MainParty.Party, dest);
            else MobileParty.MainParty.PrisonRoster.AddToCounts(prisonerChar, -1);

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
               bool toEnemy = prisonerHero.MapFaction != null && npcHero.MapFaction != null &&
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