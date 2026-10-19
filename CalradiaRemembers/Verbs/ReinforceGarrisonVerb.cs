// Code written by Gabriel Mailhot, 01/10/2026.

#region

using System;
using System.Linq;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>
    ///   reinforce_garrison (fkasad, Nexus, 01/10/2026: "Works for garrison reinforcement too"): the player hands over
    ///   the men a governor or lord asked for, and they go straight onto the walls of the place named in the task.
    ///   Taught only while such a task from this character is outstanding and the player stands in that very place,
    ///   so the model never offers a hand-over the game cannot make. All the men asked, or nothing
    ///   (<see cref="ReinforceGarrisonPolicy.CanHandOver" />); the bridge then completes the task and pays.
    /// </summary>
    public sealed class ReinforceGarrisonVerb : IConversationVerb
    {
        public string Name => "reinforce_garrison";

        public bool IsEligible(VerbContext ctx) => Pending(ctx, out _, out _);

        public string TeachingText(VerbContext ctx)
        {
            if (!Pending(ctx, out InformalQuest quest, out Settlement place)) return null;

            var sb = new StringBuilder();
            sb.AppendLine("REINFORCE_GARRISON, THE MEN YOU ASKED FOR YOUR WALLS:");
            sb.AppendLine($"You asked the player for {quest.RequiredItemCount} soldiers for the garrison of {place.Name}, and they");
            sb.AppendLine("stand in it now. When the player hands the men over and you accept them, emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: reinforce_garrison");
            sb.AppendLine("[/ACTION]");
            sb.AppendLine($"The game moves {quest.RequiredItemCount} of the player's own healthy soldiers onto your walls, or none");
            sb.AppendLine("if they have fewer; a promise to bring them later is not a hand-over.");
            sb.AppendLine();

            return sb.ToString();
        }

        public ActionOutcome Execute(GameAction action, VerbContext ctx)
        {
            try
            {
                if (!Pending(ctx, out InformalQuest quest, out Settlement place))
                    return ActionOutcome.Refused("(No garrison task of theirs is waiting here; nothing is handed over.)");

                MobileParty playerParty = MobileParty.MainParty;
                int healthy = playerParty?.MemberRoster?.GetTroopRoster()
                                         .Where(el => el.Character is {IsHero: false})
                                         .Sum(el => el.Number - el.WoundedNumber) ?? 0;

                if (!ReinforceGarrisonPolicy.CanHandOver(healthy, quest.RequiredItemCount))
                    return ActionOutcome.Refused($"(You have {healthy} fit soldiers; {ctx.Npc.Name} asked for {quest.RequiredItemCount}. Bring more men.)");

                int moved = CourtActionResolver.MoveLowestTierTroops(playerParty, place.Town.GarrisonParty, quest.RequiredItemCount);
                ModLog.Info($"[ACTION] reinforce_garrison: {moved} of the player's soldiers onto the walls of {place.Name} for {ctx.Npc.Name}.");

                string player = Hero.MainHero?.Name?.ToString() ?? "The player";

                return ctx.Bridge.SettleTroopHandOver(ctx.Profile, ctx.Npc, quest, new TroopHandOver {
                    Moved = moved,
                    Evidence = $"Brought {moved} soldiers for the garrison of {place.Name}",
                    Memory = $"{player} brought {moved} soldiers for the walls of {place.Name}, as I asked.",
                    PlayerLine = $"{moved} of your soldiers join the garrison of {place.Name}."
                });
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ACTION] reinforce_garrison failed: {ex.GetType().Name}: {ex.Message}");

                return null;
            }
        }

        /// <summary>This character's outstanding garrison task for the place the player stands in, with that place.</summary>
        private static bool Pending(VerbContext ctx, out InformalQuest quest, out Settlement place)
        {
            quest = null;
            place = Settlement.CurrentSettlement;
            if (ctx?.Npc == null || ctx.Profile == null || place?.Town?.GarrisonParty == null) return false;

            string here = place.Name?.ToString();
            quest = ctx.Profile.ActiveQuests.FirstOrDefault(q => q.Type == QuestType.ReinforceGarrison && q.IsOutstanding
                                                                 && string.Equals(q.TargetSettlement, here, StringComparison.OrdinalIgnoreCase));

            return quest != null;
        }
    }
}
