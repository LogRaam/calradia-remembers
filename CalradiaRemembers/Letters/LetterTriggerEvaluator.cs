// Code written by Gabriel Mailhot, 02/06/2026.
// Sprint 12b: NPC-initiated letter correspondence — trigger evaluation.

#region

using System;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Services;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Letters
{
    /// <summary>
    ///   Listens to campaign events and evaluates daily conditions to decide when
    ///   an NPC should initiate a letter to the player. Fires
    ///   <see cref="LetterGenerationService.QueueInitialLetter" /> when a trigger
    ///   condition is met, subject to per-NPC cooldowns and in-transit limits.
    ///
    ///   Sprint 12b triggers: <see cref="TournamentVictory" />, <see cref="BattleVictory" />,
    ///   <see cref="RomanticCorrespondence" />, <see cref="SpouseCorrespondence" />.
    /// </summary>
    public sealed class LetterTriggerEvaluator
    {
        // Minimum days between two letters from the same NPC (anti-spam).
        private const int DefaultCooldownDays = 15;

        // We cap how many letters queue up from a single event.
        private const int MaxLettersPerEvent = 2;

        public void Subscribe()
        {
            CampaignEvents.TournamentFinished.AddNonSerializedListener(this, OnTournamentFinished);
            // Signature: (CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            ModLog.Info("LetterTriggerEvaluator subscribed.");
        }

        // ── Daily evaluation (called from OnDailyTick) ────────────────────────

        public void EvaluateDaily(BannerlordNpcMemoryStore store, LlmServices llmServices, int today)
        {
            EvaluateRomanticCorrespondence(store, llmServices, today);
            EvaluateSpouseCorrespondence(store, llmServices, today);
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void OnTournamentFinished(CharacterObject winner,
            MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
        {
            if (winner?.HeroObject != Hero.MainHero) return;

            var behavior = NpcMemoryCampaignBehavior.Instance;
            if (behavior == null) return;

            int today = (int)CampaignTime.Now.ToDays;
            int arenaRank = behavior.GameStateBridge != null
                ? (Campaign.Current?.TournamentManager?.GetLeaderBoardRank(Hero.MainHero) ?? 0)
                : 0;

            string context = $"Player won a tournament at {town?.Name}.";
            if (arenaRank > 0) context += $" They are now ranked #{arenaRank} in the tournament leaderboard.";

            int queued = 0;
            foreach (string id in behavior.Store.ListIds())
            {
                if (queued >= MaxLettersPerEvent) break;

                NpcProfile profile = behavior.Store.Get(id);
                if (profile == null) continue;
                if (!CanSendLetter(profile, today, DefaultCooldownDays)) continue;

                Hero hero = Hero.FindFirst(h => h.StringId == id);
                if (hero == null || !hero.IsAlive) continue;

                // Friendly NPCs congratulate; rivals or hostiles might challenge.
                if (profile.ReputationWithPlayer < 10) continue;

                LetterGenerationService.QueueInitialLetter(
                    profile, hero, LetterReason.TournamentVictory, context,
                    behavior.LlmServices, behavior.Store, today);
                queued++;
            }
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent == null || !mapEvent.IsPlayerMapEvent) return;

            bool playerAttacker = mapEvent.AttackerSide?.IsMainPartyAmongParties() == true;
            bool playerDefender = mapEvent.DefenderSide?.IsMainPartyAmongParties() == true;
            if (!playerAttacker && !playerDefender) return;

            BattleSideEnum playerSide = playerAttacker ? BattleSideEnum.Attacker : BattleSideEnum.Defender;
            if (mapEvent.WinningSide != playerSide) return;

            BattleSideEnum losingSide = playerAttacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker;
            var loserParties = mapEvent.GetMapEventSide(losingSide)?.Parties;
            if (loserParties == null) return;

            // Only notable victories: at least one enemy lord was defeated.
            bool hasDefeatedLord = loserParties.Any(p =>
                p.Party?.LeaderHero != null && p.Party.LeaderHero.IsLord);
            if (!hasDefeatedLord) return;

            // Skip bandit-only battles — nobody of note would write about clearing thieves.
            bool allBandits = loserParties.All(p => p.Party?.MapFaction?.IsBanditFaction == true);
            if (allBandits) return;

            var behavior = NpcMemoryCampaignBehavior.Instance;
            if (behavior == null) return;

            int today = (int)CampaignTime.Now.ToDays;
            string defeatedNames = string.Join(", ",
                loserParties
                    .Where(p => p.Party?.LeaderHero?.IsLord == true)
                    .Select(p => p.Party.LeaderHero.Name.ToString())
                    .Take(2));
            string context = $"Player won a battle, defeating {defeatedNames}.";

            int queued = 0;
            foreach (string id in behavior.Store.ListIds())
            {
                if (queued >= 1) break; // cap at 1 per battle

                NpcProfile profile = behavior.Store.Get(id);
                if (profile == null) continue;
                if (!CanSendLetter(profile, today, DefaultCooldownDays)) continue;

                Hero hero = Hero.FindFirst(h => h.StringId == id);
                if (hero == null || !hero.IsAlive) continue;

                // Ally of the player (same faction or high personal regard) celebrates.
                bool sameOrAllied = hero.MapFaction != null
                    && hero.MapFaction == Hero.MainHero?.MapFaction;
                if (!sameOrAllied || profile.ReputationWithPlayer < 20) continue;

                LetterGenerationService.QueueInitialLetter(
                    profile, hero, LetterReason.BattleVictory, context,
                    behavior.LlmServices, behavior.Store, today);
                queued++;
            }
        }

        // ── Daily condition evaluators ─────────────────────────────────────────

        private static void EvaluateRomanticCorrespondence(
            BannerlordNpcMemoryStore store, LlmServices llmServices, int today)
        {
            foreach (string id in store.ListIds())
            {
                NpcProfile profile = store.Get(id);
                if (profile?.Romantic == null) continue;
                if (profile.Romantic.Status < RomanticStatus.Courting) continue;
                if (profile.ReputationWithPlayer < 15) continue;
                if (!CanSendLetter(profile, today, 10)) continue;
                if (DaysSinceLastEvent(profile, today) < 3) continue; // met recently

                Hero hero = Hero.FindFirst(h => h.StringId == id);
                if (hero == null || !hero.IsAlive) continue;

                string context = $"Romantic status: {profile.Romantic.Status}. " +
                                 $"Last saw the player {DaysSinceLastEvent(profile, today)} days ago.";

                LetterGenerationService.QueueInitialLetter(
                    profile, hero, LetterReason.RomanticCorrespondence, context,
                    llmServices, store, today);

                return; // one romantic letter per daily tick
            }
        }

        private static void EvaluateSpouseCorrespondence(
            BannerlordNpcMemoryStore store, LlmServices llmServices, int today)
        {
            Hero spouse = Hero.MainHero?.Spouse;
            if (spouse == null || !spouse.IsAlive) return;

            NpcProfile profile = store.Get(spouse.StringId);
            if (profile == null) return;
            if (!CanSendLetter(profile, today, 7)) return;
            if (DaysSinceLastEvent(profile, today) < 5) return;

            string context = $"Player's spouse. Has not seen the player in {DaysSinceLastEvent(profile, today)} days.";

            LetterGenerationService.QueueInitialLetter(
                profile, spouse, LetterReason.SpouseCorrespondence, context,
                llmServices, store, today);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>True when the NPC has no letter in transit and respects the cooldown.</summary>
        private static bool CanSendLetter(NpcProfile profile, int today, int cooldownDays)
        {
            if (profile.SentLetters == null || profile.SentLetters.Count == 0) return true;

            // Block if a letter is still in transit.
            bool inTransit = profile.SentLetters.Any(l => !l.IsDelivered && l.DeliveryDay > today);
            if (inTransit) return false;

            // Enforce cooldown from most recent letter sent.
            int lastSent = profile.SentLetters.Max(l => l.SentOnDay);
            return today - lastSent >= cooldownDays;
        }

        /// <summary>Days since the last notable event in this NPC's profile (proxy for last meeting).</summary>
        private static int DaysSinceLastEvent(NpcProfile profile, int today)
        {
            if (profile.Events == null || profile.Events.Count == 0) return int.MaxValue;
            return today - profile.Events.Max(e => e.gameDay);
        }
    }
}
