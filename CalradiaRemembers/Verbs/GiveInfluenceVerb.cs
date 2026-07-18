// Code written by Gabriel Mailhot, 03/07/2026.

#region

using System;
using System.Text;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Stance;
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
    ///   Stance mechanical behaviour-gating (increment 2, Trust, ratified 2026-07-04): this is the NPC
    ///   exposing THEIR OWN clan's standing to the player (never the reciprocal <see cref="SpendInfluenceVerb" />),
    ///   so it is additionally gated on <see cref="StanceGatePolicy.WillDelegateRisk" /> reading the NPC's
    ///   live Trust — re-checked both here and at execution, the same discipline as the Fear-1 coercion gate.
    /// </summary>
    public sealed class GiveInfluenceVerb : IConversationVerb
    {
        public string Name => "give_influence";

        public bool IsEligible(VerbContext ctx)
            => ctx?.Npc != null && ctx.Profile != null && CourtActionResolver.TryGiveInfluence(ctx.Npc, ctx.Profile, out _)
               && StanceGatePolicy.WillDelegateRisk(StanceService.ToStance(ctx.Profile).Trust, ctx.Profile.ClanRelationWithPlayer ?? 0, ctx.Profile.ReputationWithPlayer);

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

                // Stance mechanical behaviour-gating (increment 2, Trust): re-validated here, never
                // trusted from the LLM's own framing of the offer.
                if (!StanceGatePolicy.WillDelegateRisk(StanceService.ToStance(profile).Trust, profile.ClanRelationWithPlayer ?? 0, profile.ReputationWithPlayer))
                    return new ActionOutcome($"{npcHero.Name} does not yet trust you enough to risk their own standing on your word. Prove yourself first.", false);

                if (!CourtActionResolver.TryGiveInfluence(npcHero, profile, out int amount))
                    return new ActionOutcome($"({npcHero.Name} has no influence to spare you just now.)", false);

                Clan giver = npcHero.Clan;
                ChangeClanInfluenceAction.Apply(giver, -amount);
                ChangeClanInfluenceAction.Apply(Clan.PlayerClan, amount);

                var today = (int) CampaignTime.Now.ToDays;
                profile.Events.Add(new NotableEvent(today, NotableEventType.Collaboration,
                    $"Lent the player {amount} points of influence at court. {CourtActionResolver.InfluenceMarker}"));
                CourtActionResolver.RecordCourtAction(profile, CourtActionResolver.InfluenceMarker, today); // M-B6

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
