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
                RewardGold     = Clamp(proposal.RewardGold, 0, MaxRewardGold),
                RewardRelation = Clamp(proposal.RewardRelation, 0, MaxRewardRelation),
                Status         = QuestStatus.Active
            };

            return ResolveTargets(quest, proposal, giver) ? quest : null;
        }

        #region private

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
