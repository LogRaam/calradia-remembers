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
    public sealed class GiveInfluenceVerb : IConversationVerb, IStatesUnavailability<VerbContext>
    {
        public string Name => "give_influence";

        public bool IsEligible(VerbContext ctx)
            => ctx?.Npc != null && ctx.Profile != null && CourtActionResolver.TryGiveInfluence(ctx.Npc, ctx.Profile, out _)
               && StanceGatePolicy.WillDelegateRisk(new StanceGatePolicy.DelegatedRiskFacts {Trust = StanceService.ToStance(ctx.Profile).Trust, Relation = ctx.Profile.ClanRelationWithPlayer ?? 0, Affection = ctx.Profile.ReputationWithPlayer});

        /// <summary>
        ///   The closed door this verb names BEFORE the model speaks (Gabriel's ruling, 09/09/2026: the LLM must
        ///   declare already knowing the gating, rather than declare and be refuted afterwards). Until today this
        ///   verb's reason for saying no existed only inside Execute, which is to say only AFTER the NPC had
        ///   already promised influence.
        ///   <para>
        ///     ONE door is named, and deliberately only one: the TRUST gate, because it is the only refusal this
        ///     verb can attribute with certainty. CourtActionResolver.TryGiveInfluence answers a single bool over
        ///     several distinct doors (rank, the relation floor, the amount, affordability, the cooldown), so
        ///     naming a reason on its say-so would risk putting a FALSE sentence in the NPC's mouth, which is the
        ///     very incoherence this whole mechanism exists to end. Giving that resolver a named verdict, the way
        ///     SecretBondEligibility got one, is the next increment; until then it stays silent.
        ///   </para>
        ///   <para>
        ///     Silent too when the deed is not in play: this speaks only on the NEAR MISS, where the NPC plainly
        ///     COULD do it and simply will not. That is the case a player finds arbitrary, because the means are
        ///     visible and the refusal is not, and it is the case where an NPC would otherwise have promised
        ///     influence and left the player waiting.
        ///   </para>
        /// </summary>
        public DeedRefusal Unavailable(VerbContext ctx)
        {
            if (ctx?.Npc == null || ctx.Profile == null) return null;
            if (!CourtActionResolver.TryGiveInfluence(ctx.Npc, ctx.Profile, out _)) return null; // not in play, or the means are the door: say nothing

            if (StanceGatePolicy.WillDelegateRisk(new StanceGatePolicy.DelegatedRiskFacts {Trust = StanceService.ToStance(ctx.Profile).Trust, Relation = ctx.Profile.ClanRelationWithPlayer ?? 0, Affection = ctx.Profile.ReputationWithPlayer}))
                return null; // eligible: it is being taught in full a few paragraphs above

            return new DeedRefusal {
                Deed = "lend them your voice at court",
                Reason = "you do not trust them enough yet to risk your own house's standing on their word"
            };
        }

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
                if (!StanceGatePolicy.WillDelegateRisk(new StanceGatePolicy.DelegatedRiskFacts {Trust = StanceService.ToStance(profile).Trust, Relation = profile.ClanRelationWithPlayer ?? 0, Affection = profile.ReputationWithPlayer}))
                    return ActionOutcome.Refused($"{npcHero.Name} does not yet trust you enough to risk their own standing on your word. Prove yourself first.");

                if (!CourtActionResolver.TryGiveInfluence(npcHero, profile, out int amount))
                    return ActionOutcome.Refused($"({npcHero.Name} has no influence to spare you just now.)");

                Clan giver = npcHero.Clan;
                ChangeClanInfluenceAction.Apply(giver, -amount);
                ChangeClanInfluenceAction.Apply(Clan.PlayerClan, amount);

                var today = (int) CampaignTime.Now.ToDays;
                profile.Events.Add(new NotableEvent(today, NotableEventType.Collaboration,
                    $"Lent the player {amount} points of influence at court."));
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
