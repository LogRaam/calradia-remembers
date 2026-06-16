// Code written by Gabriel Mailhot, 01/06/2026.
// Sprint 11: informal quests — resolve a parsed proposal into a persisted quest.

#region

using System;
using System.Linq;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

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
                Type        = proposal.Type,
                Description = proposal.Description,
                IssuedOnDay = gameDay,
                DeadlineDay = proposal.DeadlineDays.HasValue && proposal.DeadlineDays.Value > 0
                    ? gameDay + proposal.DeadlineDays.Value
                    : (int?)null,
                RewardGold        = Clamp(proposal.RewardGold, 0, MaxRewardGold),
                RewardRelation    = Clamp(proposal.RewardRelation, 0, MaxRewardRelation),
                Reward            = proposal.Reward,
                RequiredItemValue = proposal.RequiredValue, // overridden by ResolveReward for priced grants
                Status            = QuestStatus.Active
            };

            if (!ResolveReward(quest, proposal, giver)) return null;
            return ResolveTargets(quest, proposal, giver) ? quest : null;
        }

        #region private

        /// <summary>Upper bound on an LLM-proposed item-barter value, for non-priced grants.</summary>
        private const int MaxRequiredItemValue = 100000;

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
                    // Service paid in kind: the giver must be a genuinely hireable wanderer, and
                    // the goods must be worth a premium over their cash hiring price.
                    int? required = GameState.RecruitmentPricing.GetInKindRequiredValue(giver);
                    if (required == null) return false;       // not recruitable — no such bargain
                    if (quest.Type != QuestType.DeliverItems) return false; // join is item-barter only this phase
                    quest.RequiredItemValue = required.Value;
                    return true;

                case RewardGrant.MarriageConsent:
                {
                    // The blessing is conditioned on a verifiable deed. Resolve and validate the
                    // intended spouse now, so the NPC never promises a hand they cannot give.
                    Hero spouse = Marriage.MarriageProspectResolver.ResolveCandidate(giver, proposal.MarriageSpouse);
                    if (spouse == null)
                    {
                        ModLog.Info($"[QUEST] marriage_consent refused — no eligible '{proposal.MarriageSpouse}' in {giver.Name}'s house.");
                        return false;
                    }
                    if (!Marriage.MarriageProspectResolver.HasAuthority(giver, spouse)
                        || !Marriage.MarriageProspectResolver.IsMarriageable(Hero.MainHero, spouse))
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

        /// <summary>
        ///   Fills the quest's target fields from the proposal according to what the
        ///   quest type requires. Returns false when a required target is missing.
        /// </summary>
        private static bool ResolveTargets(InformalQuest quest, QuestProposal proposal, Hero giver)
        {
            switch (proposal.Type)
            {
                case QuestType.BanditClear:
                case QuestType.BanditHideout:
                    return BindSettlement(quest, proposal.TargetSettlement, requireVillage: false);

                case QuestType.Siege:
                    return BindSettlement(quest, proposal.TargetSettlement, requireFortification: true);

                case QuestType.RaidVillage:
                    if (!BindSettlement(quest, proposal.TargetSettlement, requireVillage: true)) return false;
                    // The enemy is whoever owns the village — drives the war-validity check.
                    BindFactionFromSettlement(quest);
                    return true;

                case QuestType.AttackFaction:
                case QuestType.AttackCaravan:
                    return BindFaction(quest, proposal.TargetFaction);

                case QuestType.AttackLord:
                case QuestType.CapturePrisoner:
                case QuestType.ExecuteEnemy:
                case QuestType.RescuePrisoner:
                case QuestType.DeliverLetter:
                    return BindHero(quest, proposal.TargetHero, giver);

                case QuestType.ProvideGold:
                    // No external target — the quest giver is the implicit recipient.
                    return true;

                case QuestType.DeliverItems:
                    // No external target — the player hands goods to the giver in conversation.
                    // RequiredItemValue is set by ResolveReward (game-side proportionality).
                    return quest.RequiredItemValue > 0;

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

        private static bool BindSettlement(InformalQuest quest, string name,
            bool requireVillage = false, bool requireFortification = false)
        {
            Settlement s = ResolveSettlement(name);
            if (s == null) return false;
            if (requireVillage && !s.IsVillage) return false;
            if (requireFortification && !(s.IsTown || s.IsCastle || s.IsFortification)) return false;

            quest.TargetSettlement = s.Name?.ToString();
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

        private static bool BindFaction(InformalQuest quest, string name)
        {
            IFaction f = ResolveFaction(name);
            if (f == null) return false;
            quest.TargetFactionId = f.StringId;
            quest.TargetFactionName = f.Name?.ToString();
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

        // ── Name resolution (exact match preferred, then substring) ──────────────

        private static Settlement ResolveSettlement(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var all = Settlement.All;
            return all.FirstOrDefault(s => NameEquals(s.Name, name))
                   ?? all.FirstOrDefault(s => NameContains(s.Name, name));
        }

        private static Hero ResolveHero(string name, Hero giver)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            bool Eligible(Hero h) => h != null && h != Hero.MainHero && h != giver;

            return Hero.AllAliveHeroes.FirstOrDefault(h => Eligible(h) && NameEquals(h.Name, name))
                   ?? Hero.AllAliveHeroes.FirstOrDefault(h => Eligible(h) && NameContains(h.Name, name));
        }

        private static IFaction ResolveFaction(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            IFaction kingdom = Kingdom.All?.FirstOrDefault(k => NameEquals(k.Name, name))
                               ?? Kingdom.All?.FirstOrDefault(k => NameContains(k.Name, name));
            if (kingdom != null) return kingdom;

            return Clan.All?.FirstOrDefault(c => !c.IsEliminated && NameEquals(c.Name, name))
                   ?? Clan.All?.FirstOrDefault(c => !c.IsEliminated && NameContains(c.Name, name));
        }

        /// <summary>
        ///   Binds the quest to the first active kingdom at war with <paramref name="giver"/>'s
        ///   faction. Used as the ScoutArmy fallback when the LLM does not name a target.
        /// </summary>
        private static bool BindFirstEnemyFaction(InformalQuest quest, Hero giver)
        {
            if (giver?.MapFaction == null) return false;

            IFaction enemy = Kingdom.All?.FirstOrDefault(k =>
                !k.IsEliminated && FactionManager.IsAtWarAgainstFaction(giver.MapFaction, k));
            if (enemy == null) return false;

            quest.TargetFactionId   = enemy.StringId;
            quest.TargetFactionName = enemy.Name?.ToString();
            return true;
        }

        private static bool NameEquals(TaleWorlds.Localization.TextObject n, string q)
            => n != null && string.Equals(n.ToString(), q, StringComparison.OrdinalIgnoreCase);

        private static bool NameContains(TaleWorlds.Localization.TextObject n, string q)
            => n != null && n.ToString().IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

        private static int Clamp(int v, int min, int max)
            => v < min ? min : v > max ? max : v;

        #endregion
    }
}
