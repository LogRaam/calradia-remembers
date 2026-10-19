// Code written by Gabriel Mailhot, 01/10/2026.

#region

using System;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>
    ///   share_intel (fkasad, Nexus, 01/10/2026: "Make contact with local gang leader, negotiate with him for intel"): a
    ///   town's gang leader, in his own town, agrees to tell the player what he knows of its walls, stores, people and
    ///   streets. Taught only to a gang leader standing in a town some lord has sent the player to scout, so the model
    ///   never offers a deal the game cannot record. What he tells is the town's true state; the bridge records it as
    ///   the scout_town task's evidence, for the lord who sent the player.
    /// </summary>
    public sealed class ShareIntelVerb : IConversationVerb
    {
        public string Name => "share_intel";

        public bool IsEligible(VerbContext ctx) => Here(ctx) != null;

        public string TeachingText(VerbContext ctx)
        {
            Settlement town = Here(ctx);
            if (town == null) return null;

            var sb = new StringBuilder();
            sb.AppendLine("SHARE_INTEL, TELLING WHAT YOU KNOW OF YOUR TOWN:");
            sb.AppendLine($"The player has come into {town.Name} for what you know of it: its walls and garrison, its stores, its");
            sb.AppendLine("people, its streets. Someone sent them. You may refuse, or name a price first (take it with take_gold).");
            sb.AppendLine("When you actually tell them, emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: share_intel");
            sb.AppendLine("[/ACTION]");
            sb.AppendLine("Say only what you truly know of the place; the game records the true state of the town as your word.");
            sb.AppendLine();

            return sb.ToString();
        }

        public ActionOutcome Execute(GameAction action, VerbContext ctx)
        {
            try
            {
                Settlement town = Here(ctx);
                if (town == null) return ActionOutcome.Refused("(Nobody has sent you here for what this one knows; nothing is recorded.)");

                return ctx.Bridge.SettleScoutTask(ctx.Npc, town);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ACTION] share_intel failed: {ex.GetType().Name}: {ex.Message}");

                return null;
            }
        }

        /// <summary>The town this gang leader stands in, when a scout_town task aims at it; else null.</summary>
        private static Settlement Here(VerbContext ctx)
        {
            Settlement town = Settlement.CurrentSettlement;
            if (ctx?.Npc == null || ctx.Bridge == null || town == null || !ctx.Npc.IsGangLeader) return null;
            if (ctx.Npc.CurrentSettlement != town) return null;

            return ctx.Bridge.HasScoutTask(town) ? town : null;
        }
    }
}
