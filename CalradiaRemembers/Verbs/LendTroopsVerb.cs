// Code written by Gabriel Mailhot, 03/07/2026.

#region

using System;
using System.Text;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Verbs;
using CalradiaRemembers.Stance;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>
    ///   lend_troops (RP with teeth, increment 1): an NPC leading their own party gives the player some of
    ///   their own soldiers to keep. This is a PERMANENT reinforcement, not a loan that returns (v1.34.1
    ///   honesty fix: the transfer is one-way via <see cref="CourtActionResolver.MoveLowestTierTroops" /> and no
    ///   return mechanic is wired). The action KEY stays lend_troops; only the
    ///   player-facing wording was made truthful. Eligibility and the count given are entirely <see cref="CourtActionResolver.TryLendTroops" />
    ///   / <see cref="CourtActionResolver.MoveLowestTierTroops" /> (re-checked and re-computed against the
    ///   LIVE roster at execution: "the bridge is law"); this class only orchestrates.
    ///   Stance mechanical behaviour-gating (increment 2, Trust, ratified 2026-07-04): this is the NPC
    ///   exposing THEIR OWN soldiers to the player (never the reciprocal <see cref="GiveTroopsVerb" />), so
    ///   it is additionally gated on <see cref="StanceGatePolicy.WillDelegateRisk" /> reading the NPC's
    ///   live Trust — re-checked both here and at execution, the same discipline as the Fear-1 coercion gate.
    /// </summary>
    public sealed class LendTroopsVerb : IConversationVerb, IStatesUnavailability<VerbContext>
    {
        public string Name => "lend_troops";

        public bool IsEligible(VerbContext ctx)
            => ctx?.Npc != null && ctx.Profile != null && CourtActionResolver.TryLendTroops(ctx.Npc, ctx.Profile, out _)
               && StanceGatePolicy.WillDelegateRisk(StanceService.ToStance(ctx.Profile).Trust, ctx.Profile.ClanRelationWithPlayer ?? 0, ctx.Profile.ReputationWithPlayer);

        /// <summary>
        ///   The closed door this verb names BEFORE the model speaks (Gabriel's ruling, 09/09/2026: the LLM must
        ///   declare already knowing the gating, rather than declare and be refuted afterwards). Until today this
        ///   verb's reason for saying no existed only inside Execute, which is to say only AFTER the NPC had
        ///   already promised soldiers.
        ///   <para>
        ///     ONE door is named, and deliberately only one: the TRUST gate, because it is the only refusal this
        ///     verb can attribute with certainty. CourtActionResolver.TryLendTroops answers a single bool over
        ///     several distinct doors (rank, the relation floor, the amount, affordability, the cooldown), so
        ///     naming a reason on its say-so would risk putting a FALSE sentence in the NPC's mouth, which is the
        ///     very incoherence this whole mechanism exists to end. Giving that resolver a named verdict, the way
        ///     SecretBondEligibility got one, is the next increment; until then it stays silent.
        ///   </para>
        ///   <para>
        ///     Silent too when the deed is not in play: this speaks only on the NEAR MISS, where the NPC plainly
        ///     COULD do it and simply will not. That is the case a player finds arbitrary, because the means are
        ///     visible and the refusal is not, and it is the case where an NPC would otherwise have promised
        ///     soldiers and left the player waiting.
        ///   </para>
        /// </summary>
        public DeedRefusal Unavailable(VerbContext ctx)
        {
            if (ctx?.Npc == null || ctx.Profile == null) return null;
            if (!CourtActionResolver.TryLendTroops(ctx.Npc, ctx.Profile, out _)) return null; // not in play, or the means are the door: say nothing

            if (StanceGatePolicy.WillDelegateRisk(StanceService.ToStance(ctx.Profile).Trust,
                                                  ctx.Profile.ClanRelationWithPlayer ?? 0,
                                                  ctx.Profile.ReputationWithPlayer))
                return null; // eligible: it is being taught in full a few paragraphs above

            return new DeedRefusal {
                Deed = "lend them soldiers from your own party",
                Reason = "you do not trust them enough yet to risk your own men on their word"
            };
        }

        public string TeachingText(VerbContext ctx)
        {
            if (ctx?.Npc == null || ctx.Profile == null) return null;
            if (!CourtActionResolver.TryLendTroops(ctx.Npc, ctx.Profile, out int count)) return null;

            var sb = new StringBuilder();
            sb.AppendLine("REINFORCING THE PLAYER'S PARTY, A PERMANENT GIFT OF SOME OF YOUR OWN SOLDIERS:");
            sb.AppendLine("You lead enough of a healthy force, and think well enough of the player, that you could");
            sb.AppendLine("part with a handful of your own rank and file to strengthen their party. You would keep");
            sb.AppendLine("the bulk of your own men, not gut your household guard. These soldiers are the player's");
            sb.AppendLine("to keep: a permanent gift, never a loan that returns. Do not promise them back.");
            sb.AppendLine("When you have genuinely resolved to do this in this conversation, and only then, emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: lend_troops");
            sb.AppendLine("[/ACTION]");
            sb.AppendLine($"The game moves {count} of your own soldiers permanently into their ranks.");
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
                    return ActionOutcome.Refused($"{npcHero.Name} does not yet trust you enough to risk their own men on your word. Prove yourself first.");

                if (!CourtActionResolver.TryLendTroops(npcHero, profile, out int count))
                    return ActionOutcome.Refused($"({npcHero.Name} has no soldiers to spare you just now.)");

                MobileParty npcParty = npcHero.PartyBelongedTo;
                MobileParty playerParty = MobileParty.MainParty;

                if (npcParty == null || playerParty == null) return null;

                int moved = CourtActionResolver.MoveLowestTierTroops(npcParty, playerParty, count);

                if (moved <= 0) return ActionOutcome.Refused($"({npcHero.Name} has no soldiers to spare you just now.)");

                var today = (int) CampaignTime.Now.ToDays;
                profile.Events.Add(new NotableEvent(today, NotableEventType.Collaboration,
                    $"Reinforced the player's party with {moved} of my own soldiers, given to keep. {CourtActionResolver.TroopsMarker}"));
                CourtActionResolver.RecordCourtAction(profile, CourtActionResolver.TroopsMarker, today); // M-B6

                ModLog.Info($"[ACTION] lend_troops: {npcHero.Name} gave the player {moved} troops to keep.");

                return new ActionOutcome($"{npcHero.Name} reinforces your party with {moved} of their soldiers.", true);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ACTION] lend_troops failed: {ex.GetType().Name}: {ex.Message}");

                return null;
            }
        }
    }
}
