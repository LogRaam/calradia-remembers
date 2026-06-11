// Code written by Gabriel Mailhot, 17/05/2026.

#region

using System;
using System.Linq;
using CalradiaRemembers.GameState;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Mapping
{
    /// <summary>
    ///   Constructs an <see cref="EncounterContext" /> for a given NPC, using
    ///   the game state bridge for live data and the stored profile for
    ///   historical data (last-meeting computation).
    /// </summary>
    public static class EncounterContextBuilder
    {
        /// <summary>
        ///   Builds an <see cref="EncounterContext" /> for the given NPC.
        ///   When <paramref name="playerMessage" /> is provided, the resolver scans it
        ///   for hero names and injects a contextual hint so the NPC can answer
        ///   questions about third parties accurately — or ask for clarification when
        ///   a name is ambiguous.
        /// </summary>
        public static EncounterContext Build(
            Hero hero,
            NpcProfile profile,
            IGameStateBridge bridge,
            string playerMessage = null,
            System.Collections.Generic.IReadOnlyList<NpcMemoryService.Core.Models.WitnessEntry> witnesses = null,
            bool privacyRequested = false,
            bool isWitnessExchangeTurn    = false,
            bool isLastWitnessExchange    = false,
            bool isCollectiveCaptiveScene = false,
            PlayerStatusVsNpc? playerStatusOverride = null,
            CaptiveSceneIntent captiveIntent = CaptiveSceneIntent.Interrogation,
            bool isFinalSceneBeat = false) => new() {
            PlayerStatus           = playerStatusOverride ?? ResolvePlayerStatus(hero, profile, bridge),
            WarStatus              = bridge.GetDiplomaticStatus(hero),
            DaysSinceLastMeeting   = ComputeDaysSinceLastMeeting(profile, bridge),
            Scene                  = bridge.GetCurrentScene(),
            ContextualNames        = string.IsNullOrWhiteSpace(playerMessage)
                ? null
                : ContextualNameResolver.Resolve(hero, playerMessage),
            NpcCurrentActivity     = bridge.GetCurrentActivity(hero),
            PlayerSpouseName       = Hero.MainHero?.Spouse?.IsAlive == true
                ? Hero.MainHero.Spouse.Name?.ToString()
                : null,
            PlayerFatherName       = FormatParent(Hero.MainHero?.Father),
            PlayerMotherName        = FormatParent(Hero.MainHero?.Mother),
            PlayerClanTier         = Hero.MainHero?.Clan?.Tier ?? 0,
            PlayerArenaRank        = ResolveArenaRank(),
            PlayerArenaWins        = ResolveArenaWins(),
            Witnesses              = witnesses,
            PrivacyRequested       = privacyRequested,
            IsWitnessExchangeTurn    = isWitnessExchangeTurn,
            IsLastWitnessExchange    = isLastWitnessExchange,
            IsCollectiveCaptiveScene = isCollectiveCaptiveScene,
            CaptiveIntent            = captiveIntent,
            IsFinalSceneBeat         = isFinalSceneBeat
        };

        #region private

        /// <summary>
        ///   Formats a parent's display name for the prompt, marking the deceased with
        ///   "the late …" so the NPC never speaks of a dead parent as living. Null when
        ///   the parent is unknown or unnamed.
        /// </summary>
        private static string FormatParent(Hero parent)
        {
            string name = parent?.Name?.ToString();
            if (string.IsNullOrWhiteSpace(name)) return null;
            return parent.IsAlive ? name : $"the late {name}";
        }

        private static int ResolveArenaRank()
        {
            try { return Campaign.Current?.TournamentManager?.GetLeaderBoardRank(Hero.MainHero) ?? 0; }
            catch { return 0; }
        }

        private static int ResolveArenaWins()
        {
            try
            {
                var lb = Campaign.Current?.TournamentManager?.GetLeaderboard();
                if (lb == null) return 0;
                foreach (var kvp in lb)
                    if (kvp.Key == Hero.MainHero) return kvp.Value;
                return 0;
            }
            catch { return 0; }
        }

        private static int ComputeDaysSinceLastMeeting(NpcProfile profile, IGameStateBridge bridge)
        {
            if (profile == null || profile.Events == null || profile.Events.Count == 0)
                return -1;

            var latestEventDay = profile.Events.Max(e => e.gameDay);
            var currentDay = bridge.GetCurrentGameDay();

            return Math.Max(0, currentDay - latestEventDay);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        ///   If the profile has no events, treat the player as a Stranger; this
        ///   is more LLM-friendly than the bridge's default "Free", which would
        ///   imply the NPC already knows the player exists.
        /// </summary>
        private static PlayerStatusVsNpc ResolvePlayerStatus(
            Hero hero,
            NpcProfile profile,
            IGameStateBridge bridge)
        {
            PlayerStatusVsNpc fromBridge = bridge.GetPlayerStatus(hero);

            var firstMeeting = profile == null || profile.Events == null || profile.Events.Count == 0;

            if (firstMeeting && fromBridge == PlayerStatusVsNpc.Free)
                return PlayerStatusVsNpc.Stranger;

            return fromBridge;
        }

        #endregion
    }
}