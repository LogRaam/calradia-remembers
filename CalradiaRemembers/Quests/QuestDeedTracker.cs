// Code written by Gabriel Mailhot, 01/06/2026.
// Sprint 11: informal quests — verify deeds from real campaign events.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Quests
{
    /// <summary>
    ///   Watches the campaign for the deeds that satisfy outstanding quests and stamps
    ///   the matching quest with proof. This is what makes completion event-driven rather
    ///   than roleplay-driven: a quest is only ever marked done here, from a real battle,
    ///   capture, kill, or rescue in which the PLAYER took part. Stamping writes directly
    ///   onto the quest record (which persists with the profile), so it survives save/load
    ///   between doing the deed and reporting it — no separate, volatile journal.
    ///
    ///   Owned by <see cref="NpcMemoryCampaignBehavior" /> and subscribed at session launch,
    ///   so its listeners die with the campaign (handoff rule: no statics holding world refs).
    /// </summary>
    public sealed class QuestDeedTracker
    {
        public void Subscribe()
        {
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
            // Rescue is detected from the player's own victory (OnMapEventEnded), NOT from
            // HeroPrisonerReleased: that event carries no rescuer, so it cannot tell a
            // player rescue from an AI one — crediting it would break the player-link rule.
            ModLog.Info("QuestDeedTracker subscribed to campaign events.");
        }

        #region campaign event handlers

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent == null || !mapEvent.IsPlayerMapEvent) return;

            // Determine the player's side from the event itself — PartyBase.MainParty.Side
            // can already be detached by the time MapEventEnded fires.
            bool playerAttacker = mapEvent.AttackerSide?.IsMainPartyAmongParties() == true;
            bool playerDefender = mapEvent.DefenderSide?.IsMainPartyAmongParties() == true;
            if (!playerAttacker && !playerDefender) return;

            BattleSideEnum playerSide = playerAttacker ? BattleSideEnum.Attacker : BattleSideEnum.Defender;
            if (mapEvent.WinningSide != playerSide) return;

            BattleSideEnum losingSide = playerAttacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker;

            List<PartyBase> losers = mapEvent.GetMapEventSide(losingSide)?.Parties
                                         .Select(p => p.Party)
                                         .Where(p => p != null)
                                         .ToList()
                                     ?? new List<PartyBase>();

            var facts = new BattleFacts {
                IsHideout       = mapEvent.IsHideoutBattle,
                IsRaid          = mapEvent.IsRaid,
                IsSiege         = mapEvent.IsSiegeAssault,
                IsFieldBattle   = mapEvent.IsFieldBattle,
                LosersAreBandits = losers.Any(p => p.MapFaction?.IsBanditFaction == true),
                HasCaravanLoser  = losers.Any(p => p.MobileParty?.IsCaravan == true),
                LoserFactionIds  = new HashSet<string>(losers.Where(p => p.MapFaction != null)
                                                              .Select(p => p.MapFaction.StringId)),
                CaravanFactionIds = new HashSet<string>(losers.Where(p => p.MobileParty?.IsCaravan == true
                                                                          && p.MapFaction != null)
                                                              .Select(p => p.MapFaction.StringId)),
                DefeatedHeroIds  = new HashSet<string>(losers.Where(p => p.LeaderHero != null)
                                                             .Select(p => p.LeaderHero.StringId)),
                // Heroes the defeated parties were holding prisoner — the player just
                // freed them by winning this battle. Read before the aftermath scatters them.
                FreedPrisonerHeroIds = new HashSet<string>(losers.SelectMany(PrisonerHeroIds)),
                SettlementName   = ResolveBattleSettlementName(mapEvent)
            };

            int day = CurrentDay();
            ForEachOutstandingQuest((profile, quest) =>
            {
                string evidence = MatchBattle(quest, facts, day);
                if (evidence != null) Stamp(profile, quest, evidence, day);
            });
        }

        private void OnHeroKilled(Hero victim, Hero killer,
            KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            if (victim == null || killer != Hero.MainHero) return; // the player struck the blow

            int day = CurrentDay();
            ForEachOutstandingQuest((profile, quest) =>
            {
                if (quest.Type == QuestType.ExecuteEnemy
                    && string.Equals(quest.TargetHeroId, victim.StringId, StringComparison.Ordinal))
                    Stamp(profile, quest, $"Killed {victim.Name} on day {day}", day);
            });
        }

        private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            if (prisoner == null) return;
            bool byPlayer = capturer == PartyBase.MainParty
                            || prisoner.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero;
            if (!byPlayer) return;

            int day = CurrentDay();
            ForEachOutstandingQuest((profile, quest) =>
            {
                if (quest.Type == QuestType.CapturePrisoner
                    && string.Equals(quest.TargetHeroId, prisoner.StringId, StringComparison.Ordinal))
                    Stamp(profile, quest, $"Captured {prisoner.Name} on day {day}", day);
            });
        }

        #endregion

        #region matching

        /// <summary>
        ///   Returns an evidence string when this battle satisfies the quest, else null.
        ///   Place-bound types require the named settlement to match where the fight was.
        /// </summary>
        private static string MatchBattle(InformalQuest quest, BattleFacts f, int day)
        {
            switch (quest.Type)
            {
                case QuestType.BanditClear:
                    return f.LosersAreBandits && !f.IsHideout && SettlementMatches(quest, f)
                        ? $"Defeated the bandits near {quest.TargetSettlement} on day {day}"
                        : null;

                case QuestType.BanditHideout:
                    return f.LosersAreBandits && f.IsHideout && SettlementMatches(quest, f)
                        ? $"Cleared the hideout near {quest.TargetSettlement} on day {day}"
                        : null;

                case QuestType.AttackFaction:
                    return quest.TargetFactionId != null && f.LoserFactionIds.Contains(quest.TargetFactionId)
                        ? $"Beat the forces of {quest.TargetFactionName} in battle on day {day}"
                        : null;

                case QuestType.AttackLord:
                    return quest.TargetHeroId != null && f.DefeatedHeroIds.Contains(quest.TargetHeroId)
                        ? $"Defeated {quest.TargetHeroName} in battle on day {day}"
                        : null;

                case QuestType.AttackCaravan:
                    return quest.TargetFactionId != null && f.CaravanFactionIds.Contains(quest.TargetFactionId)
                        ? $"Destroyed a caravan of {quest.TargetFactionName} on day {day}"
                        : null;

                case QuestType.RaidVillage:
                    return f.IsRaid && SettlementMatches(quest, f)
                        ? $"Raided {quest.TargetSettlement} on day {day}"
                        : null;

                case QuestType.Siege:
                    return f.IsSiege && SettlementMatches(quest, f)
                        ? $"Stormed {quest.TargetSettlement} on day {day}"
                        : null;

                case QuestType.RescuePrisoner:
                    return quest.TargetHeroId != null && f.FreedPrisonerHeroIds.Contains(quest.TargetHeroId)
                        ? $"Freed {quest.TargetHeroName} from captivity on day {day}"
                        : null;

                default:
                    return null; // capture/execute are handled by their own hero events
            }
        }

        private static bool SettlementMatches(InformalQuest quest, BattleFacts f)
            => !string.IsNullOrWhiteSpace(quest.TargetSettlement)
               && string.Equals(quest.TargetSettlement, f.SettlementName, StringComparison.OrdinalIgnoreCase);

        #endregion

        #region helpers

        private static void ForEachOutstandingQuest(Action<NpcProfile, InformalQuest> visit)
        {
            var store = NpcMemoryCampaignBehavior.Instance?.Store;
            if (store == null) return;

            foreach (var id in store.ListIds())
            {
                NpcProfile profile = store.Get(id);
                if (profile == null || profile.ActiveQuests.Count == 0) continue;

                // Snapshot so a stamp doesn't disturb the enumeration.
                foreach (InformalQuest quest in profile.ActiveQuests.Where(q => q.IsOutstanding).ToList())
                    visit(profile, quest);
            }
        }

        private static void Stamp(NpcProfile profile, InformalQuest quest, string evidence, int day)
        {
            quest.SatisfiedOnDay = day;
            quest.Evidence = evidence;
            NpcMemoryCampaignBehavior.Instance?.Store.Set(profile);
            ModLog.Info($"[QUEST] satisfied for {profile.Name}: {evidence}");
            InformPlayer($"Task progress — {evidence}. Report back to {profile.Name}.");
        }

        /// <summary>
        ///   The settlement a battle should be matched against. A hideout battle reports
        ///   the hideout itself as <see cref="MapEvent.MapEventSettlement"/> — useless for
        ///   matching a named town — so the hideout path falls back to the nearest real
        ///   settlement. Sieges and raids carry the true target settlement and use it.
        /// </summary>
        private static string ResolveBattleSettlementName(MapEvent mapEvent)
        {
            if (mapEvent.IsHideoutBattle) return NearestSettlement()?.Name?.ToString();
            return (mapEvent.MapEventSettlement ?? NearestSettlement())?.Name?.ToString();
        }

        private static Settlement NearestSettlement()
        {
            MobileParty main = MobileParty.MainParty;
            if (main == null) return null;

            Vec2 pos = main.GetPosition2D;
            Settlement best = null;
            float bestDist = float.MaxValue;
            foreach (Settlement s in Settlement.All)
            {
                // Only real, nameable settlements — never a hideout or hidden point.
                if (!(s.IsTown || s.IsVillage || s.IsCastle)) continue;

                float d = s.GetPosition2D.Distance(pos);
                if (d < bestDist) { bestDist = d; best = s; }
            }

            return best;
        }

        /// <summary>Stable ids of every hero a party is holding prisoner.</summary>
        private static IEnumerable<string> PrisonerHeroIds(PartyBase party)
        {
            var roster = party?.PrisonRoster;
            if (roster == null) yield break;

            for (int i = 0; i < roster.Count; i++)
            {
                Hero hero = roster.GetCharacterAtIndex(i)?.HeroObject;
                if (hero != null) yield return hero.StringId;
            }
        }

        private static int CurrentDay() => (int)CampaignTime.Now.ToDays;

        private static void InformPlayer(string text)
        {
            try { MBInformationManager.AddQuickInformation(new TextObject(text)); }
            catch (Exception ex) { ModLog.Error($"[QUEST] InformPlayer failed: {ex.Message}"); }
        }

        #endregion

        /// <summary>Flattened facts about a player-won battle, computed once per event.</summary>
        private sealed class BattleFacts
        {
            public bool IsHideout;
            public bool IsRaid;
            public bool IsSiege;
            public bool IsFieldBattle;
            public bool LosersAreBandits;
            public bool HasCaravanLoser;
            public HashSet<string> LoserFactionIds = new HashSet<string>();
            public HashSet<string> CaravanFactionIds = new HashSet<string>();
            public HashSet<string> DefeatedHeroIds = new HashSet<string>();
            public HashSet<string> FreedPrisonerHeroIds = new HashSet<string>();
            public string SettlementName;
        }
    }
}
