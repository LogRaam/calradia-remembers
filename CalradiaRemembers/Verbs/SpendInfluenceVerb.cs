// Code written by Gabriel Mailhot, 04/07/2026.

#region

using System;
using System.Text;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic.Appreciations;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>
    ///   spend_influence: the reciprocal of <see cref="GiveInfluenceVerb" />, 04/07/2026 — the PLAYER spends
    ///   some of their own clan's influence to back a needy lord's clan at court. The LLM plays the NPC, so
    ///   this action is emitted BY THE NPC when they ACCEPT the player's offer, never by the player.
    ///   Eligibility and the amount transferred are entirely <see cref="CourtActionResolver.TrySpendInfluence" />
    ///   (re-checked fresh both while teaching and at execution: "the bridge is law"); this class only
    ///   orchestrates: teach it, then, on the matching [ACTION], move the influence and stamp the cooldown
    ///   marker. Closes the last open Appreciations source, <see cref="AppreciationSource.TroopsOrInfluenceSpentForThem" />.
    /// </summary>
    public sealed class SpendInfluenceVerb : IConversationVerb
    {
        public string Name => "spend_influence";

        public bool IsEligible(VerbContext ctx)
            => ctx?.Npc != null && ctx.Profile != null && CourtActionResolver.TrySpendInfluence(ctx.Npc, ctx.Profile, out _);

        public string TeachingText(VerbContext ctx)
        {
            if (ctx?.Npc == null || ctx.Profile == null) return null;
            if (!CourtActionResolver.TrySpendInfluence(ctx.Npc, ctx.Profile, out int amount)) return null;

            var sb = new StringBuilder();
            sb.AppendLine("SPEND_INFLUENCE, ACCEPTING THE PLAYER'S BACKING FOR YOUR CLAN AT COURT:");
            sb.AppendLine("Your own clan's voice at court runs thin these days. If the player offers to spend some");
            sb.AppendLine("of their own standing to back your clan, and you have genuinely resolved to accept, emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: spend_influence");
            sb.AppendLine("[/ACTION]");
            sb.AppendLine($"The game transfers {amount} points of influence from the player's clan to yours. Only");
            sb.AppendLine("emit this once you have truly decided to accept in this conversation, never as an empty thanks.");
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
                if (!CourtActionResolver.TrySpendInfluence(npcHero, profile, out int amount))
                    return new ActionOutcome($"({npcHero.Name}'s clan has no need of your influence just now.)", false);

                Clan receiver = npcHero.Clan;
                if (receiver == null) return null;

                ChangeClanInfluenceAction.Apply(Clan.PlayerClan, -amount);
                ChangeClanInfluenceAction.Apply(receiver, amount);

                var today = (int) CampaignTime.Now.ToDays;
                profile.Events.Add(new NotableEvent(today, NotableEventType.Collaboration,
                    $"Accepted {amount} points of the player's influence to back their clan at court. {CourtActionResolver.SpendInfluenceMarker}"));

                AppreciationBehavior.Instance?.TryPlant(npcHero, Hero.MainHero, AppreciationSource.TroopsOrInfluenceSpentForThem,
                    $"The player spent {amount} points of their own influence to back {npcHero.Name}'s clan at court.",
                    AppreciationBackfillRules.TroopsOrInfluenceSpentForThemMagnitude, knownToPlayer: true, today);

                ModLog.Info($"[ACTION] spend_influence: {npcHero.Name} accepted {amount} influence from the player.");

                return new ActionOutcome($"{npcHero.Name}'s clan accepts {amount} of your influence at court.", true);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ACTION] spend_influence failed: {ex.GetType().Name}: {ex.Message}");

                return null;
            }
        }
    }
}
