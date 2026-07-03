// Code written by Gabriel Mailhot, 03/07/2026.

#region

using System;
using System.Text;
using CalradiaRemembers.GameState;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>
    ///   give_influence (RP with teeth, increment 1): a lord lends the player some of their own political
    ///   weight at court. Eligibility and the amount granted are entirely <see cref="CourtActionResolver.TryGiveInfluence" />
    ///   (re-checked fresh both while teaching and at execution: "the bridge is law"); this class only
    ///   orchestrates: teach it, then, on the matching [ACTION], move the influence and stamp the cooldown marker.
    /// </summary>
    public sealed class GiveInfluenceVerb : IConversationVerb
    {
        public string Name => "give_influence";

        public bool IsEligible(VerbContext ctx)
            => ctx?.Npc != null && ctx.Profile != null && CourtActionResolver.TryGiveInfluence(ctx.Npc, ctx.Profile, out _);

        public string TeachingText(VerbContext ctx)
        {
            if (ctx?.Npc == null || ctx.Profile == null) return null;
            if (!CourtActionResolver.TryGiveInfluence(ctx.Npc, ctx.Profile, out int amount)) return null;

            var sb = new StringBuilder();
            sb.AppendLine("GIVE_INFLUENCE, LENDING THE PLAYER YOUR VOICE AT COURT:");
            sb.AppendLine("Your standing runs deep enough, and your clan's influence is ample enough, that you could");
            sb.AppendLine("lend the player some of your own political weight at court, a word put in on their behalf");
            sb.AppendLine("with your peers. This is not idle: it costs your own house real standing.");
            sb.AppendLine("When you have genuinely resolved to do this in this conversation, and only then, emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: give_influence");
            sb.AppendLine("[/ACTION]");
            sb.AppendLine($"The game transfers {amount} points of influence from your clan to theirs.");
            sb.AppendLine();

            return sb.ToString();
        }

        public ActionOutcome Execute(GameAction action, VerbContext ctx)
        {
            try
            {
                Hero npcHero = ctx?.Npc;
                NpcProfile profile = ctx?.Profile;

                if (npcHero == null || profile == null) return null;
                if (!CourtActionResolver.TryGiveInfluence(npcHero, profile, out int amount))
                    return new ActionOutcome($"({npcHero.Name} has no influence to spare you just now.)", false);

                Clan giver = npcHero.Clan;
                ChangeClanInfluenceAction.Apply(giver, -amount);
                ChangeClanInfluenceAction.Apply(Clan.PlayerClan, amount);

                var today = (int) CampaignTime.Now.ToDays;
                profile.Events.Add(new NotableEvent(today, NotableEventType.Collaboration,
                    $"Lent the player {amount} points of influence at court. {CourtActionResolver.InfluenceMarker}"));

                ModLog.Info($"[ACTION] give_influence: {npcHero.Name} lent the player {amount} influence.");

                return new ActionOutcome($"{npcHero.Name} lends you {amount} influence at court.", true);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ACTION] give_influence failed: {ex.GetType().Name}: {ex.Message}");

                return null;
            }
        }
    }
}
