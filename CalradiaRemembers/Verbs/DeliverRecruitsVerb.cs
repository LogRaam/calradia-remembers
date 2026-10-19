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

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>
    ///   deliver_recruits (Lordfadooboo, Nexus, 01/10/2026: "recruitment quests"): the player hands over the fresh recruits
    ///   of his own people a lord asked for, and they join that lord's party. Taught only while such a task from this lord
    ///   is outstanding and he leads a party to take them into. All the men asked or none
    ///   (<see cref="ReinforceGarrisonPolicy.CanHandOver" />); only men <see cref="RecruitTroopsPolicy.Counts" /> as
    ///   recruits move, lowest tier first; the bridge closes the task.
    /// </summary>
    public sealed class DeliverRecruitsVerb : IConversationVerb
    {
        public string Name => "deliver_recruits";

        public bool IsEligible(VerbContext ctx) => Pending(ctx, out _, out _);

        public string TeachingText(VerbContext ctx)
        {
            if (!Pending(ctx, out InformalQuest quest, out _)) return null;

            var sb = new StringBuilder();
            sb.AppendLine("DELIVER_RECRUITS, THE FRESH MEN YOU ASKED FOR:");
            sb.AppendLine($"You asked the player for {quest.RequiredItemCount} fresh recruits of {quest.RequiredCultureName ?? "your people"}. When they hand");
            sb.AppendLine("the men over and you take them into your ranks, emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: deliver_recruits");
            sb.AppendLine("[/ACTION]");
            sb.AppendLine("The game moves the recruits if the player has enough of them, or none; a promise to bring them later is not a hand-over.");
            sb.AppendLine();

            return sb.ToString();
        }

        public ActionOutcome Execute(GameAction action, VerbContext ctx)
        {
            try
            {
                if (!Pending(ctx, out InformalQuest quest, out MobileParty party))
                    return ActionOutcome.Refused("(No recruits were asked of you here; nothing is handed over.)");

                Func<CharacterObject, bool> counts = c => c != null && RecruitTroopsPolicy.Counts(
                    new TroopFacts {IsHero = c.IsHero, Tier = c.Tier, CultureId = c.Culture?.StringId}, quest.RequiredCultureId);

                MobileParty playerParty = MobileParty.MainParty;
                int have = playerParty?.MemberRoster?.GetTroopRoster().Where(el => counts(el.Character)).Sum(el => el.Number - el.WoundedNumber) ?? 0;

                if (!ReinforceGarrisonPolicy.CanHandOver(have, quest.RequiredItemCount))
                    return ActionOutcome.Refused($"(You have {have} fit recruits of {quest.RequiredCultureName ?? "that people"}; {ctx.Npc.Name} asked for {quest.RequiredItemCount}. Bring more.)");

                int moved = CourtActionResolver.MoveMatchingTroops(playerParty, party, quest.RequiredItemCount, counts);
                string player = Hero.MainHero?.Name?.ToString() ?? "The player";
                ModLog.Info($"[ACTION] deliver_recruits: {moved} recruits into {ctx.Npc.Name}'s party.");

                return ctx.Bridge.SettleTroopHandOver(ctx.Profile, ctx.Npc, quest, new TroopHandOver {
                    Moved = moved,
                    Evidence = $"Brought {moved} fresh recruits of {quest.RequiredCultureName ?? "his people"}",
                    Memory = $"{player} brought me {moved} fresh recruits of {quest.RequiredCultureName ?? "our people"}, as I asked.",
                    PlayerLine = $"{moved} of your recruits join {ctx.Npc.Name}'s ranks."
                });
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ACTION] deliver_recruits failed: {ex.GetType().Name}: {ex.Message}");

                return null;
            }
        }

        /// <summary>This lord's outstanding recruit task, with the party he leads to take the men into.</summary>
        private static bool Pending(VerbContext ctx, out InformalQuest quest, out MobileParty party)
        {
            quest = null;
            party = ctx?.Npc?.PartyBelongedTo;
            if (ctx?.Profile == null || party == null || party.LeaderHero != ctx.Npc) return false;

            quest = ctx.Profile.ActiveQuests.FirstOrDefault(q => q.Type == QuestType.RecruitTroops && q.IsOutstanding);

            return quest != null;
        }
    }
}
