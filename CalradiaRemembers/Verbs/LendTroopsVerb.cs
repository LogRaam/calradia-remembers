// Code written by Gabriel Mailhot, 03/07/2026.

#region

using System;
using System.Text;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Stance;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>
    ///   lend_troops (RP with teeth, increment 1): an NPC leading their own party hands the player some
    ///   soldiers for the road. Eligibility and the count lent are entirely <see cref="CourtActionResolver.TryLendTroops" />
    ///   / <see cref="CourtActionResolver.MoveLowestTierTroops" /> (re-checked and re-computed against the
    ///   LIVE roster at execution: "the bridge is law"); this class only orchestrates.
    ///   Stance mechanical behaviour-gating (increment 2, Trust, ratified 2026-07-04): this is the NPC
    ///   exposing THEIR OWN soldiers to the player (never the reciprocal <see cref="GiveTroopsVerb" />), so
    ///   it is additionally gated on <see cref="StanceGatePolicy.WillDelegateRisk" /> reading the NPC's
    ///   live Trust — re-checked both here and at execution, the same discipline as the Fear-1 coercion gate.
    /// </summary>
    public sealed class LendTroopsVerb : IConversationVerb
    {
        public string Name => "lend_troops";

        public bool IsEligible(VerbContext ctx)
            => ctx?.Npc != null && ctx.Profile != null && CourtActionResolver.TryLendTroops(ctx.Npc, ctx.Profile, out _)
               && StanceGatePolicy.WillDelegateRisk(StanceService.ToStance(ctx.Profile).Trust, ctx.Profile.ClanRelationWithPlayer ?? 0, ctx.Profile.ReputationWithPlayer);

        public string TeachingText(VerbContext ctx)
        {
            if (ctx?.Npc == null || ctx.Profile == null) return null;
            if (!CourtActionResolver.TryLendTroops(ctx.Npc, ctx.Profile, out int count)) return null;

            var sb = new StringBuilder();
            sb.AppendLine("LEND_TROOPS, HANDING THE PLAYER SOME SOLDIERS FOR THE ROAD:");
            sb.AppendLine("You lead enough of a healthy force, and think well enough of the player, that you could");
            sb.AppendLine("part with a handful of your own rank and file to strengthen their party. You would keep");
            sb.AppendLine("the bulk of your own men, not gut your household guard.");
            sb.AppendLine("When you have genuinely resolved to do this in this conversation, and only then, emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: lend_troops");
            sb.AppendLine("[/ACTION]");
            sb.AppendLine($"The game moves {count} of your own soldiers into their ranks.");
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
                    return new ActionOutcome($"{npcHero.Name} does not yet trust you enough to risk their own men on your word. Prove yourself first.", false);

                if (!CourtActionResolver.TryLendTroops(npcHero, profile, out int count))
                    return new ActionOutcome($"({npcHero.Name} has no soldiers to spare you just now.)", false);

                MobileParty npcParty = npcHero.PartyBelongedTo;
                MobileParty playerParty = MobileParty.MainParty;

                if (npcParty == null || playerParty == null) return null;

                int moved = CourtActionResolver.MoveLowestTierTroops(npcParty, playerParty, count);

                if (moved <= 0) return new ActionOutcome($"({npcHero.Name} has no soldiers to spare you just now.)", false);

                var today = (int) CampaignTime.Now.ToDays;
                profile.Events.Add(new NotableEvent(today, NotableEventType.Collaboration,
                    $"Lent the player {moved} soldiers for the road. {CourtActionResolver.TroopsMarker}"));

                ModLog.Info($"[ACTION] lend_troops: {npcHero.Name} lent the player {moved} troops.");

                return new ActionOutcome($"{npcHero.Name} lends you {moved} soldiers.", true);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ACTION] lend_troops failed: {ex.GetType().Name}: {ex.Message}");

                return null;
            }
        }
    }
}
