// Code written by Gabriel Mailhot, 22/06/2026.

#region

using System;
using System.Linq;
using CalradiaRemembers.GameState;
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
      /// <summary>Upper bound on an LLM-proposed item-barter value, for non-priced grants.</summary>
      private const int MaxRequiredItemValue = 100000;

      private const int MaxRewardGold = 5000;
      private const int MaxRewardRelation = 10;

      // TUNE: generous radius (campaign-map units) around the named town within which a real, still-infested
      // hideout must exist for the giver to send the player after it. No lair in range => no such task to give.
      private const float HideoutSearchRadius = 80f;

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

         return ResolveTargets(quest, proposal, giver)
            ? quest
            : null;
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
         string bearing = CalradiaRemembers.Logic.CardinalDirectionPolicy.Describe(a.x, a.y, h.x, h.y);
         quest.DirectionHint = $"{bearing} of {anchor.Name}";

         return true;
      }

      /// <summary>The nearest still-infested (uncleared) bandit hideout within <paramref name="maxDistance" />, or null.</summary>
      private static Settlement NearestActiveHideout(Vec2 from, float maxDistance)
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

      /// <summary>The nearest real settlement to <paramref name="giver" />, used as the bearing anchor when they named no resolvable town.</summary>
      private static Settlement NearestTownTo(Hero giver)
      {
         Vec2? pos = giver?.PartyBelongedTo?.GetPosition2D
                  ?? giver?.CurrentSettlement?.GetPosition2D
                  ?? giver?.HomeSettlement?.GetPosition2D;

         if (pos == null) return null;

         Settlement best = null;
         float bestDist = float.MaxValue;

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

            case QuestType.ScoutArmy:
               // Prefer a named army leader; fall back to any army of the named faction;
               // fall back to the giver's first enemy kingdom when neither is specified.
               if (!string.IsNullOrWhiteSpace(proposal.TargetHero))
                  return BindHero(quest, proposal.TargetHero, giver);
               if (!string.IsNullOrWhiteSpace(proposal.TargetFaction))
                  return BindFaction(quest, proposal.TargetFaction);

               return BindFirstEnemyFaction(quest, giver);

            default:
               return false;
         }
      }

      #endregion
   }
}