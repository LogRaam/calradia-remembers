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
        public static EncounterContext Build(
            Hero hero,
            NpcProfile profile,
            IGameStateBridge bridge) => new() {
            PlayerStatus = ResolvePlayerStatus(hero, profile, bridge),
            WarStatus = bridge.GetDiplomaticStatus(hero),
            DaysSinceLastMeeting = ComputeDaysSinceLastMeeting(profile, bridge),
            Scene = bridge.GetCurrentScene()
        };

        #region private

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