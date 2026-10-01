// Code written by Gabriel Mailhot, 08/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Assemblies;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Marriage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Quests
{
   /// <summary>
   ///   Turns a <see cref="QuestProposal" /> (the LLM's words) into a concrete,
   ///   persistable <see cref="InformalQuest" /> by resolving its named target to a
   ///   real settlement, hero, or faction and computing the deadline. A proposal whose
   ///   required target cannot be resolved is rejected (returns null) rather than
   ///   persisted half-formed — the NPC simply will not have offered a real task.
   ///   Rewards are clamped here so the LLM cannot promise an unbounded payout.
   /// </summary>
   public static class QuestFactory
   {
      // TUNE: generous radius (campaign-map units) around the named town within which a real, still-infested
      // hideout must exist for the giver to send the player after it. No lair in range => no such task to give.
      internal const float HideoutSearchRadius = 80f;

      // non_aggression_pact's own gate (COUNCIL_ACTIONS.md's Partie 8, CalradiaRemembers.Logic.Assemblies.
      // NonAggressionPactPolicy's own header): the CR-generated deed types that are genuinely HOSTILE toward a
      // faction, refused outright here while that faction is a live pact partner of the player's own faction (see
      // IsBlockedByNonAggressionPact below). DeliverPrisoner is deliberately EXCLUDED: its target faction is the
      // RECIPIENT of a captive handed over as a favor, not a victim of an attack. ScoutArmy/RescuePrisoner are not
      // hostile acts either.
      private static readonly HashSet<QuestType> HostileQuestTypes = new HashSet<QuestType> {
         QuestType.AttackFaction, QuestType.AttackLord, QuestType.RaidVillage, QuestType.AttackCaravan,
         QuestType.Siege, QuestType.CapturePrisoner, QuestType.ExecuteEnemy, QuestType.DeclareWar
      };

      /// <summary>Upper bound on an LLM-proposed item-barter value, for non-priced grants.</summary>
      private const int MaxRequiredItemValue = 100000;

      private const int MaxRewardGold = 5000;
      private const int MaxRewardRelation = 10;

      /// <summary>
      ///   Builds the quest, or returns null when the proposal cannot be grounded in
      ///   the current game world (no such settlement/hero/faction for its type).
      /// </summary>
      public static InformalQuest Create(QuestProposal proposal, Hero giver, int gameDay)
      {
         if (proposal == null || giver == null) return null;

         var quest = new InformalQuest {
            Type = proposal.Type,
            Description = proposal.Description,
            IssuedOnDay = gameDay,
            DeadlineDay = proposal.DeadlineDays is > 0
               ? gameDay + proposal.DeadlineDays.Value
               : null,
            RewardGold = Clamp(proposal.RewardGold, 0, MaxRewardGold),
            RewardRelation = Clamp(proposal.RewardRelation, 0, MaxRewardRelation),
            Reward = proposal.Reward,
            RequiredItemValue = proposal.RequiredValue, // overridden by ResolveReward for priced grants
            Status = QuestStatus.Active
         };

         if (!ResolveReward(quest, proposal, giver)) return null;
         if (!ResolveTargets(quest, proposal, giver)) return null;

         // A capture asked in PEACE is a rival wanted in the giver's own hands (blackmail, vengeance, a scheme), not a
         // war errand: issued as the delivery it is, completed by handing him over, and never voided by a peace
         // (Gabriel's ruling on FloatingMammal's report, 25/09/2026; CaptureQuestPolicy).
         if (quest.Type == QuestType.CapturePrisoner)
         {
            Hero target = Hero.FindFirst(h => h.StringId == quest.TargetHeroId);
            bool atWar = giver.MapFaction != null && target?.MapFaction != null
                         && FactionManager.IsAtWarAgainstFaction(giver.MapFaction, target.MapFaction);
            quest.Type = CaptureQuestPolicy.TypeToIssue(quest.Type, atWar);
            if (quest.Type == QuestType.DeliverPrisoner)
               ModLog.Info($"[QUEST] {giver.Name} asked for {quest.TargetHeroName} captured in peace: issued as a delivery into their hands.");
         }

         // non_aggression_pact's own gate (CalradiaRemembers.Logic.Assemblies.NonAggressionPactPolicy's own
         // header): refuse a hostile CR-generated deed at the EMISSION point, never narrate it then silently
         // drop it later. Checked LAST, after every other target/reward binding has already succeeded, so the
         // refusal reason is always specifically "a pact protects them" rather than masking a different, earlier
         // failure.
         if (IsBlockedByNonAggressionPact(quest))
         {
            ModLog.Info($"[QUEST] '{quest.Type}' from {giver.Name} refused - the target faction is bound by a non-aggression pact right now.");

            return null;
         }

         return quest;
      }

      /// <summary>
      ///   True when <paramref name="quest" /> is a HOSTILE deed type (<see cref="HostileQuestTypes" />) whose
      ///   already-resolved target faction is a current non-aggression pact partner of the player's own LIVE
      ///   faction (<see cref="CalradiaRemembers.Assemblies.CouncilPactBehavior.IsUnderPact" />). Never throws: a
      ///   missing pact ledger, an unresolvable faction, or a friendly deed type simply means "not blocked".
      /// </summary>
      private static bool IsBlockedByNonAggressionPact(InformalQuest quest)
      {
         if (quest == null || !HostileQuestTypes.Contains(quest.Type)) return false;
         if (CouncilPactBehavior.Instance == null) return false;

         IFaction playerFaction = Hero.MainHero?.MapFaction;
         if (playerFaction == null) return false;

         IFaction targetFaction = ResolveHostileTargetFaction(quest);
         if (targetFaction == null) return false;

         return CouncilPactBehavior.Instance.IsUnderPact(playerFaction, targetFaction);
      }

      /// <summary>
      ///   The quest's own target faction, NORMALIZED to its top-level map faction (<see cref="IFaction.MapFaction" />
      ///   - a vassal clan named directly resolves to its OWN kingdom, exactly like a pact's own stored faction
      ///   id does), read from whichever field the deed type actually bound: <see cref="InformalQuest.TargetFactionId" />
      ///   (attack_faction/attack_caravan/raid_village/declare_war), the target hero's own live faction
      ///   (attack_lord/capture_prisoner/execute_enemy), or the named settlement's own faction (siege - the only
      ///   hostile type binding neither field, see <see cref="ResolveTargets" />'s own Siege case).
      /// </summary>
      private static IFaction ResolveHostileTargetFaction(InformalQuest quest)
      {
         if (!string.IsNullOrEmpty(quest.TargetFactionId))
         {
            IFaction raw = Kingdom.All?.FirstOrDefault(k => k.StringId == quest.TargetFactionId);
            if (raw == null) raw = Clan.All?.FirstOrDefault(c => c.StringId == quest.TargetFactionId);

            return raw?.MapFaction;
         }

         if (!string.IsNullOrEmpty(quest.TargetHeroId))
         {
            Hero targetHero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == quest.TargetHeroId);

            return targetHero?.MapFaction;
         }

         if (quest.Type == QuestType.Siege && !string.IsNullOrEmpty(quest.TargetSettlement))
            return ResolveSettlement(quest.TargetSettlement)?.MapFaction;

         return null;
      }

      /// <summary>The nearest still-infested (uncleared) bandit hideout within <paramref name="maxDistance" />, or null.</summary>
      internal static Settlement NearestActiveHideout(Vec2 from, float maxDistance)
      {
         Settlement best = null;
         float bestDist = maxDistance;

         foreach (Settlement s in Settlement.All)
         {
            if (s == null || !s.IsHideout) continue;

            try
            {
               if (s.Hideout == null || !s.Hideout.IsInfested) continue;
            }
            catch { continue; } // a build where the hideout state cannot be read: treat as ineligible

            float d = s.GetPosition2D.Distance(from);

            if (d > bestDist) continue;
            bestDist = d;
            best = s;
         }

         return best;
      }

      /// <summary>
      ///   The nearest real settlement to <paramref name="giver" />, used as the bearing anchor when they named no
      ///   resolvable town.
      /// </summary>
      internal static Settlement NearestTownTo(Hero giver)
      {
         Vec2? pos = giver?.PartyBelongedTo?.GetPosition2D ?? giver?.CurrentSettlement?.GetPosition2D ?? giver?.HomeSettlement?.GetPosition2D;

         if (pos == null) return null;

         Settlement best = null;
         var bestDist = float.MaxValue;

         foreach (Settlement s in Settlement.All)
         {
            if (s == null || !(s.IsTown || s.IsCastle || s.IsVillage)) continue;

            float d = s.GetPosition2D.Distance(pos.Value);

            if (d >= bestDist) continue;
            bestDist = d;
            best = s;
         }

         return best;
      }

      #region private

      /// <summary>
      ///   Binds a declare_war deed's target: a real faction that is neither the player's own nor one
      ///   they are already at war with — there is nothing to declare otherwise, so the deed is refused.
      /// </summary>
      private static bool BindDeclarableWarTarget(InformalQuest quest, string name)
      {
         IFaction target = ResolveFaction(name);

         if (target == null) return false;

         IFaction player = Hero.MainHero?.MapFaction;

         if (player == null || player == target) return false;
         if (FactionManager.IsAtWarAgainstFaction(player, target)) return false; // already at war — no deed

         quest.TargetFactionId = target.StringId;
         quest.TargetFactionName = target.Name?.ToString();

         return true;
      }

      private static bool BindFaction(InformalQuest quest, string name)
      {
         IFaction f = ResolveFaction(name);

         if (f == null) return false;
         quest.TargetFactionId = f.StringId;
         quest.TargetFactionName = f.Name?.ToString();

         return true;
      }

      private static void BindFactionFromSettlement(InformalQuest quest)
      {
         Settlement s = ResolveSettlement(quest.TargetSettlement);
         IFaction f = s?.MapFaction;

         if (f == null) return;
         quest.TargetFactionId = f.StringId;
         quest.TargetFactionName = f.Name?.ToString();
      }

      /// <summary>
      ///   Binds the quest to the first active kingdom at war with <paramref name="giver" />'s
      ///   faction. Used as the ScoutArmy fallback when the LLM does not name a target.
      /// </summary>
      private static bool BindFirstEnemyFaction(InformalQuest quest, Hero giver)
      {
         if (giver?.MapFaction == null) return false;

         IFaction enemy = Kingdom.All?.FirstOrDefault(k =>
            !k.IsEliminated && FactionManager.IsAtWarAgainstFaction(giver.MapFaction, k));

         if (enemy == null) return false;

         quest.TargetFactionId = enemy.StringId;
         quest.TargetFactionName = enemy.Name?.ToString();

         return true;
      }

      /// <summary>
      ///   Binds an earn_regard task (Lordfadooboo, 01/10/2026): a living adult third person, the regard they hold
      ///   for the player now as the baseline, ten more as the goal, and at least a month to win it. Refused when
      ///   that person already holds the player dear, since there would be nothing left to win.
      /// </summary>
      private static bool BindRegardToWin(InformalQuest quest, QuestProposal proposal, Hero giver)
      {
         if (!BindHero(quest, proposal.TargetHero, giver)) return false;

         Hero target = Hero.FindFirst(h => h.StringId == quest.TargetHeroId);
         if (!EarnRegardPolicy.MayBeNamed(RegardTargetFacts(target, giver))) return false;

         int baseline = RegardFor(target);
         int? goal = EarnRegardPolicy.GoalFrom(baseline);
         if (goal == null)
         {
            ModLog.Info($"[QUEST] earn_regard refused: {target.Name} already holds the player dear ({baseline}).");

            return false;
         }

         quest.RegardBaseline = baseline;
         quest.RegardGoal = goal;
         int? days = EarnRegardPolicy.DeadlineDays(quest.DeadlineDay - quest.IssuedOnDay);
         quest.DeadlineDay = days == null ? (int?) null : quest.IssuedOnDay + days.Value;

         return true;
      }

      /// <summary>The facts EarnRegardPolicy judges a named person on.</summary>
      internal static EarnRegardTargetFacts RegardTargetFacts(Hero target, Hero giver)
         => new EarnRegardTargetFacts {
            IsThePlayer = target != null && target == Hero.MainHero,
            IsTheGiver = target != null && target == giver,
            IsInTheWorld = Mapping.HeroPresence.IsInTheWorld(target),
            IsAdult = target != null && !target.IsChild
         };

      /// <summary>The person's own regard for the player, on the mod's ledger; 0 for someone the player never met.</summary>
      internal static int RegardFor(Hero target)
         => target == null ? 0 : Behaviors.NpcMemoryCampaignBehavior.Instance?.Store?.Get(target.StringId)?.ReputationWithPlayer ?? 0;

      private static bool BindHero(InformalQuest quest, string name, Hero giver)
      {
         Hero h = ResolveHero(name, giver);

         if (h == null) return false;
         quest.TargetHeroId = h.StringId;
         quest.TargetHeroName = h.Name?.ToString();

         return true;
      }

      /// <summary>
      ///   Binds a hideout deed to a specific, still-infested lair near a named town. The town (the giver's if
      ///   they named nothing resolvable) is the anchor for the bearing the player is given; the lair is the one
      ///   whose clearing satisfies the quest. Returns false when no clearable lair lies within
      ///   <see cref="HideoutSearchRadius" /> of the anchor, so the giver never offers an impossible hunt.
      /// </summary>
      private static bool BindHideout(InformalQuest quest, string townName, Hero giver)
      {
         Settlement anchor = ResolveSettlement(townName) ?? NearestTownTo(giver);

         if (anchor == null) return false;

         Settlement lair = NearestActiveHideout(anchor.GetPosition2D, HideoutSearchRadius);

         if (lair == null) return false;

         quest.TargetSettlement = anchor.Name?.ToString();
         quest.TargetHideoutId = lair.StringId;

         Vec2 a = anchor.GetPosition2D,
              h = lair.GetPosition2D;
         string bearing = CardinalDirectionPolicy.Describe(new MapPoint(a.x, a.y).StepTo(new MapPoint(h.x, h.y)));
         quest.DirectionHint = $"{bearing} of {anchor.Name}";

         return true;
      }

      private static bool BindSettlement(InformalQuest quest,
                                         string name,
                                         bool requireVillage = false,
                                         bool requireFortification = false)
      {
         Settlement s = ResolveSettlement(name);

         if (s == null) return false;
         if (requireVillage && !s.IsVillage) return false;
         if (requireFortification && !(s.IsTown || s.IsCastle || s.IsFortification)) return false;

         quest.TargetSettlement = s.Name?.ToString();

         return true;
      }

      private static int Clamp(int v, int min, int max)
         => v < min ? min : v > max ? max : v;

      private static bool NameContains(TextObject n, string q)
         => n != null && n.ToString().IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

      private static bool NameEquals(TextObject n, string q)
         => n != null && string.Equals(n.ToString(), q, StringComparison.OrdinalIgnoreCase);

      private static IFaction ResolveFaction(string name)
      {
         if (string.IsNullOrWhiteSpace(name)) return null;

         IFaction kingdom = Kingdom.All?.FirstOrDefault(k => NameEquals(k.Name, name)) ?? Kingdom.All?.FirstOrDefault(k => NameContains(k.Name, name));

         if (kingdom != null) return kingdom;

         return Clan.All?.FirstOrDefault(c => !c.IsEliminated && NameEquals(c.Name, name)) ?? Clan.All?.FirstOrDefault(c => !c.IsEliminated && NameContains(c.Name, name));
      }

      private static Hero ResolveHero(string name, Hero giver)
      {
         if (string.IsNullOrWhiteSpace(name)) return null;


         bool Eligible(Hero h)
         {
            return h != null && h != Hero.MainHero && h != giver;
         }


         return Hero.AllAliveHeroes.FirstOrDefault(h => Eligible(h) && NameEquals(h.Name, name)) ?? Hero.AllAliveHeroes.FirstOrDefault(h => Eligible(h) && NameContains(h.Name, name));
      }

      /// <summary>
      ///   Validates and grounds the bargain's reward grant, and — for item-barter deeds —
      ///   computes the game-side required value (the proportionality floor). Returns false
      ///   when the grant cannot be honored, so the NPC never promises a favor the game
      ///   would refuse (the anti-empty-promise rule applied to bargains).
      /// </summary>
      private static bool ResolveReward(InformalQuest quest, QuestProposal proposal, Hero giver)
      {
         switch (quest.Reward)
         {
            case RewardGrant.None:
               // An ordinary gold/relation quest. An item-barter deed still needs a value,
               // taken from the LLM's request and clamped.
               if (quest.Type == QuestType.DeliverItems)
                  quest.RequiredItemValue = Clamp(quest.RequiredItemValue, 1, MaxRequiredItemValue);

               return true;

            case RewardGrant.JoinParty:
               // Service paid in kind: the giver must be a genuinely hireable wanderer.
               int? required = RecruitmentPricing.GetInKindRequiredValue(giver);

               if (required == null) return false; // not recruitable — no such bargain
               if (quest.Type == QuestType.DeliverItems)
               {
                  // Goods must be worth a premium over the cash hiring price.
                  quest.RequiredItemValue = required.Value;

                  return true;
               }

               // A delivered captive, or a war the player starts, is the payment in itself.
               if (quest.Type == QuestType.DeliverPrisoner) return true;
               if (quest.Type == QuestType.DeclareWar) return true;

               return false;

            case RewardGrant.MarriageConsent:
            {
               // The blessing is conditioned on a verifiable deed. Resolve and validate the
               // intended spouse now, so the NPC never promises a hand they cannot give.
               Hero spouse = MarriageProspectResolver.ResolveCandidate(giver, proposal.MarriageSpouse);
               if (spouse == null)
               {
                  ModLog.Info($"[QUEST] marriage_consent refused — no eligible '{proposal.MarriageSpouse}' in {giver.Name}'s house.");

                  return false;
               }

               if (!MarriageProspectResolver.HasAuthority(giver, spouse) || !MarriageProspectResolver.IsMarriageable(Hero.MainHero, spouse))
               {
                  ModLog.Info($"[QUEST] marriage_consent refused — {giver.Name} cannot bless a match with {spouse.Name}.");

                  return false;
               }

               quest.MarriageSpouseId = spouse.StringId;
               quest.MarriageSpouseName = spouse.Name?.ToString();

               return true;
            }

            // COUNCIL_ACTIONS.md Partie 5 (the "Caladog" case): grant_fief/marriage_reward are gated ALL THE
            // WAY DOWN behind ModSettings.AllowFiefAndMarriageQuestRewards (default off). With the toggle off
            // this refuses exactly like the still-STUB grants below, so the existing anti-Caladog guardrail
            // (PromptBuilder.AppendQuestInstructions) is never contradicted by a bridge that would honor what
            // the prompt forbids promising.
            case RewardGrant.GrantFief:
            {
               if (!ModSettings.AllowFiefAndMarriageQuestRewards)
               {
                  ModLog.Info("[QUEST] grant_fief refused, the heavy-reward toggle is off.");

                  return false;
               }

               bool giverLeadsOwnClan = giver.Clan != null && giver.Clan.Leader == giver;
               Settlement fief = ResolveSettlement(proposal.RewardSettlement);
               bool fiefOwnedByGiverClan = fief != null && (fief.IsTown || fief.IsCastle) && fief.OwnerClan == giver.Clan;

               if (!QuestRewardGatePolicy.CanPromiseFief(new QuestRewardGatePolicy.FiefRewardFacts {ToggleOn = true, GiverLeadsOwnClan = giverLeadsOwnClan, FiefStillOwnedByGiverClan = fiefOwnedByGiverClan}))
               {
                  ModLog.Info($"[QUEST] grant_fief refused, {giver.Name} cannot truly give '{proposal.RewardSettlement}' away.");

                  return false;
               }

               quest.PromisedSettlementId = fief.StringId;
               quest.PromisedSettlementName = fief.Name?.ToString();
               // A heavy reward must always expire (Kimi review: an open-ended fief promise is a save-rot
               // time bomb): overrides whatever deadline Create() already computed from the LLM's own words.
               quest.DeadlineDay = quest.IssuedOnDay + QuestRewardGatePolicy.ClampHeavyRewardDeadlineDays(proposal.DeadlineDays);

               return true;
            }

            case RewardGrant.MarriageReward:
            {
               if (!ModSettings.AllowFiefAndMarriageQuestRewards)
               {
                  ModLog.Info("[QUEST] marriage_reward refused, the heavy-reward toggle is off.");

                  return false;
               }

               Hero spouse = MarriageProspectResolver.ResolveCandidate(giver, proposal.MarriageSpouse);
               bool hasAuthority = spouse != null && MarriageProspectResolver.HasAuthority(giver, spouse);
               bool marriageable = spouse != null && MarriageProspectResolver.IsMarriageable(Hero.MainHero, spouse);
               bool playerReigns = Hero.MainHero?.Clan?.Kingdom != null && Hero.MainHero.Clan.Kingdom.Leader == Hero.MainHero;

               if (!QuestRewardGatePolicy.CanPromiseMarriageReward(new QuestRewardGatePolicy.MarriageRewardFacts {ToggleOn = true, SpouseResolved = spouse != null, GiverHasAuthorityOverSpouse = hasAuthority, SpouseStillMarriageable = marriageable, PlayerIsReigningSovereign = playerReigns}))
               {
                  ModLog.Info($"[QUEST] marriage_reward refused, {giver.Name} cannot truly wed/give '{proposal.MarriageSpouse}'.");

                  return false;
               }

               quest.MarriageSpouseId = spouse.StringId;
               quest.MarriageSpouseName = spouse.Name?.ToString();
               quest.DeadlineDay = quest.IssuedOnDay + QuestRewardGatePolicy.ClampHeavyRewardDeadlineDays(proposal.DeadlineDays);

               return true;
            }

            // Recognized but not yet executable — refuse at issuance so no broken promise.
            case RewardGrant.GiveItem:
            case RewardGrant.GiveTroops:
            case RewardGrant.ReleasePrisoner:
               ModLog.Info($"[QUEST] reward grant '{quest.Reward}' not yet supported — bargain refused.");

               return false;

            default:
               return false;
         }
      }

      // ── Name resolution (exact match preferred, then substring) ──────────────

      private static Settlement ResolveSettlement(string name)
      {
         if (string.IsNullOrWhiteSpace(name)) return null;
         MBReadOnlyList<Settlement> all = Settlement.All;

         return all.FirstOrDefault(s => NameEquals(s.Name, name)) ?? all.FirstOrDefault(s => NameContains(s.Name, name));
      }

      /// <summary>
      ///   Fills the quest's target fields from the proposal according to what the
      ///   quest type requires. Returns false when a required target is missing.
      /// </summary>
      private static bool ResolveTargets(InformalQuest quest, QuestProposal proposal, Hero giver)
      {
         switch (proposal.Type)
         {
            case QuestType.BanditClear:
               return BindSettlement(quest, proposal.TargetSettlement);

            case QuestType.BanditHideout:
               // Bind a REAL, still-infested lair in the region so the deed is concrete and trackable, and,
               // since only the player can ever clear a hideout, unambiguously the player's own deed. If none
               // exists near the named town, the giver has no such task to give and the proposal is refused.
               return BindHideout(quest, proposal.TargetSettlement, giver);

            case QuestType.Siege:
               return BindSettlement(quest, proposal.TargetSettlement, requireFortification: true);

            case QuestType.RaidVillage:
               if (!BindSettlement(quest, proposal.TargetSettlement, true)) return false;
               // The enemy is whoever owns the village — drives the war-validity check.
               BindFactionFromSettlement(quest);

               return true;

            case QuestType.AttackFaction:
            case QuestType.AttackCaravan:
               return BindFaction(quest, proposal.TargetFaction);

            case QuestType.AttackLord:
            case QuestType.CapturePrisoner:
            case QuestType.ExecuteEnemy:
            case QuestType.DeliverLetter:
               return BindHero(quest, proposal.TargetHero, giver);

            case QuestType.RescuePrisoner:
               // Self-rescue: a captive lord asking to be broken out names no target — the target IS
               // the giver. Otherwise it is the usual third-party rescue (an ally asks for their kin).
               if (!string.IsNullOrWhiteSpace(proposal.TargetHero) || !giver.IsPrisoner) return BindHero(quest, proposal.TargetHero, giver);
               quest.TargetHeroId = giver.StringId;
               quest.TargetHeroName = giver.Name?.ToString();

               return true;

            case QuestType.ProvideGold:
               // No external target — the quest giver is the implicit recipient.
               return true;

            case QuestType.DeliverItems:
               // No external target — the player hands goods to the giver in conversation.
               // RequiredItemValue is set by ResolveReward (game-side proportionality).
               return quest.RequiredItemValue > 0;

            case QuestType.DeliverPrisoner:
               // A named enemy lord, or any lord of a named enemy faction — the captive to hand over.
               if (!string.IsNullOrWhiteSpace(proposal.TargetHero))
                  return BindHero(quest, proposal.TargetHero, giver);
               if (!string.IsNullOrWhiteSpace(proposal.TargetFaction))
                  return BindFaction(quest, proposal.TargetFaction);

               return false;

            case QuestType.DeclareWar:
               return BindDeclarableWarTarget(quest, proposal.TargetFaction);

            case QuestType.EarnRegard:
               return BindRegardToWin(quest, proposal, giver);

            case QuestType.PleadCase:
               // Lordfadooboo / fkasad (01/10/2026): before a living adult who holds a court, since only a lord or a
               // notable is ever taught to be swayed, and the sway toward the giver is the deed.
               if (!BindHero(quest, proposal.TargetHero, giver)) return false;
               Hero pleadBefore = Hero.FindFirst(h => h.StringId == quest.TargetHeroId);

               return PleadCasePolicy.MayBePleadedTo(RegardTargetFacts(pleadBefore, giver), CourtActionResolver.HoldsCourt(pleadBefore));

            case QuestType.ScoutArmy:
               // Prefer a named army leader; fall back to any army of the named faction;
               // fall back to the giver's first enemy kingdom when neither is specified.
               if (!string.IsNullOrWhiteSpace(proposal.TargetHero))
                  return BindHero(quest, proposal.TargetHero, giver);
               if (!string.IsNullOrWhiteSpace(proposal.TargetFaction))
                  return BindFaction(quest, proposal.TargetFaction);

               return BindFirstEnemyFaction(quest, giver);

            case QuestType.ProvideGoods:
               // A personal supply task: bind a recognized goods category and a sane count. An unrecognized
               // category is refused (the giver never asks for goods the game cannot verify handing over).
               // Or a good one of his towns truly lacks, read off its market (fkasad, 01/10/2026: "bring me iron
               // ore, horses, beer, hardwood, whatever the city needs").
               string category = ProvideGoodsPolicy.Normalize(proposal.Category) ?? Mapping.TownNeeds.LackedKey(giver, proposal.Category);

               if (category == null) return false;
               quest.RequiredItemCategory = category;
               quest.RequiredItemCount = ProvideGoodsPolicy.ClampCount(proposal.RequiredCount);

               return true;

            default:
               return false;
         }
      }

      #endregion
   }
}