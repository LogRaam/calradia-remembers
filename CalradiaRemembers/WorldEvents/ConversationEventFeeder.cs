// Code written by Gabriel Mailhot, 19/06/2026.
// v1.2 Phase 3: the second world-event feeder — outcomes from CR conversations (a public affront
// before witnesses) become rumors, so "word gets around" instead of evaporating at window close.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.WorldEvents
{
    /// <summary>Records social outcomes of CR conversations into the world-event store.</summary>
    public static class ConversationEventFeeder
    {
        // A relation drop at least this steep, in front of witnesses, counts as a public affront
        // worth gossiping about. Routine −1 cooling never spreads.
        private const int AffrontThreshold = -5;

        /// <summary>
        ///   When the player affronts an NPC steeply enough in front of witnesses, record it as a
        ///   rumor (spreads by awareness, ages out, NOT chronicle material). No-op for minor turns.
        /// </summary>
        public static void RecordPublicAffront(Hero npc, ParsedResponse response, int witnessCount)
        {
            if (npc == null || response?.Actions == null || witnessCount <= 0 || WorldEventStore.Instance == null) return;

            int worst = 0;
            foreach (GameAction a in response.Actions)
                if (string.Equals(a.Type, "change_relation", StringComparison.OrdinalIgnoreCase)
                    && a.Parameters != null
                    && a.Parameters.TryGetValue("delta", out string ds)
                    && int.TryParse(ds, out int d) && d < worst)
                    worst = d;

            if (worst > AffrontThreshold) return; // not a real public affront

            Hero player = Hero.MainHero;
            Settlement place = player?.CurrentSettlement ?? npc.CurrentSettlement;
            string playerName = player?.Name?.ToString() ?? "the player";
            string near = place != null ? $" at {place.Name}" : "";

            WorldEventStore.Instance.Record(new WorldEvent
            {
                Type           = WorldEventType.Affront,
                Day            = (int) CampaignTime.Now.ToDays,
                PlaceId        = place?.StringId,
                InvolvesPlayer = false, // gossip that spreads and fades — not the heroic chronicle
                Magnitude      = Math.Min(50, 25 + witnessCount * 5),
                Summary        = $"{playerName} gave grave offence to {npc.Name} before others{near}.",
                ActorIds       = new List<string> { player?.StringId, npc.StringId }.Where(s => s != null).ToList(),
                FactionIds     = npc.MapFaction != null ? new List<string> { npc.MapFaction.StringId } : new List<string>()
            });
        }
    }
}
