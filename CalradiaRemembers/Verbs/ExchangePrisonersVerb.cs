// Code written by Gabriel Mailhot, 25/09/2026.
// Prisoner exchange, increment 2 (fkasad, Nexus, 25/09/2026: "I don't usually sell off captive lords... But an
// exchange..."). The player hands over a hero captive he holds and receives one the NPC's clan holds. Gabriel's rulings:
// heroes only (lords, vassals, companions; never troops). The rules are PrisonerExchangePolicy's; this is the engine
// half. The bridge is law: custody, worth, both purses and both destinations are re-verified here, and nothing moves
// until every one of them holds.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Logic.Prisoners;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>
    ///   exchange_prisoners: the player gives a hero captive he holds and receives one the NPC's clan holds, with gold
    ///   evening an uneven swap. Taught when both sides hold someone exchangeable.
    /// </summary>
    public sealed class ExchangePrisonersVerb : IConversationVerb
    {
        public string Name => "exchange_prisoners";

        public bool IsEligible(VerbContext ctx)
            => ctx?.Npc != null
               && PrisonerExchangePolicy.IsAnotherSide(ctx.Npc.Clan != null && ctx.Npc.Clan == Clan.PlayerClan)
               && TheirSide(ctx.Npc).Count > 0 && PlayerSide().Count > 0;

        public string TeachingText(VerbContext ctx)
        {
            if (ctx?.Npc == null) return null;

            List<Hero> theirs = TheirSide(ctx.Npc);
            List<Hero> ours = PlayerSide();
            if (theirs.Count == 0 || ours.Count == 0) return null;

            var sb = new StringBuilder();
            sb.AppendLine("EXCHANGE_PRISONERS, TRADING ONE CAPTIVE FOR ANOTHER:");
            sb.AppendLine("Your clan holds these captives:");
            foreach (Hero h in theirs)
                sb.AppendLine($"- {h.Name} (worth about {BuyPrisonerVerb.RansomAnchor(h)} denars{(h.Clan == Clan.PlayerClan ? ", one of the player's own people" : "")})");
            sb.AppendLine("The player holds these captives:");
            foreach (Hero h in ours)
                sb.AppendLine($"- {h.Name} (worth about {BuyPrisonerVerb.RansomAnchor(h)} denars)");
            sb.AppendLine("The two of you may agree to trade one of theirs for one of yours. Captives who go back to their");
            sb.AppendLine("own side walk free; the others change gaolers. When captives differ much in worth, the side that");
            sb.AppendLine("gets the dearer one pays the difference in gold, and the exchange settles that coin itself: never");
            sb.AppendLine("also emit give_gold or take_gold for it. Only when you have both AGREED, emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: exchange_prisoners");
            sb.AppendLine("give: the name of the captive the PLAYER hands over, exactly as listed above");
            sb.AppendLine("receive: the name of the captive YOUR clan hands over, exactly as listed above");
            sb.AppendLine("[/ACTION]");
            sb.AppendLine();

            return sb.ToString();
        }

        public ActionOutcome Execute(GameAction action, VerbContext ctx)
        {
            try
            {
                Hero npcHero = ctx?.Npc;
                NpcProfile profile = ctx?.Profile;
                if (npcHero == null || profile == null || Hero.MainHero == null) return null;

                action.Parameters.TryGetValue("give", out string giveName);
                action.Parameters.TryGetValue("receive", out string receiveName);

                Hero give = BuyPrisonerVerb.ResolvePlayerHeldPrisonerByName(giveName);
                Hero receive = CourtActionResolver.ResolveExchangeableHeldByName(receiveName, npcHero);

                // Custody decides, not the word the model used (the same lesson as PrisonerTradePolicy.DirectionFor):
                // two names given the wrong way round still name one real trade.
                if (give == null && receive == null)
                {
                    Hero swappedGive = BuyPrisonerVerb.ResolvePlayerHeldPrisonerByName(receiveName);
                    Hero swappedReceive = CourtActionResolver.ResolveExchangeableHeldByName(giveName, npcHero);
                    if (swappedGive != null && swappedReceive != null)
                    {
                        ModLog.Info("[ACTION] exchange_prisoners named the two captives the wrong way round; read by custody.");
                        give = swappedGive;
                        receive = swappedReceive;
                    }
                }

                if (give == null) return ActionOutcome.Refused("(You hold no such captive to give in exchange.)");
                if (receive == null) return ActionOutcome.Refused($"({npcHero.Name}'s clan holds no such captive to exchange.)");
                if (give == receive) return ActionOutcome.Refused("(A captive cannot be traded for themselves.)");

                if (!PrisonerExchangePolicy.IsExchangeable(Facts(give)))
                    return ActionOutcome.Refused($"({give.Name} is not someone who can be exchanged.)");
                if (!PrisonerExchangePolicy.IsExchangeable(Facts(receive)))
                    return ActionOutcome.Refused($"({receive.Name} is not someone who can be exchanged.)");

                int giveWorth = BuyPrisonerVerb.RansomAnchor(give);
                int receiveWorth = BuyPrisonerVerb.RansomAnchor(receive);
                int gold = PrisonerExchangePolicy.GoldToSettle(giveWorth, receiveWorth);

                if (gold > 0 && npcHero.Gold < gold)
                    return ActionOutcome.Refused($"({npcHero.Name} cannot pay the {gold} denars that would even this exchange.)");
                if (gold < 0 && Hero.MainHero.Gold < -gold)
                    return ActionOutcome.Refused($"(You cannot pay the {-gold} denars that would even this exchange.)");

                ExchangeHandover giveHandover = PrisonerExchangePolicy.HandoverTo(Belongs(give, npcHero.Clan, npcHero.MapFaction));
                ExchangeHandover receiveHandover = PrisonerExchangePolicy.HandoverTo(Belongs(receive, Clan.PlayerClan, Hero.MainHero.MapFaction));

                PartyBase giveDest = null;
                if (giveHandover == ExchangeHandover.IntoCustody)
                {
                    giveDest = BuyPrisonerVerb.ResolveCaptorPrison(npcHero);
                    if (giveDest == null) return ActionOutcome.Refused($"({npcHero.Name} has no party or holding to keep a prisoner in.)");
                }

                PartyBase receiveDest = MobileParty.MainParty?.Party;
                if (receiveHandover == ExchangeHandover.IntoCustody && receiveDest == null)
                    return ActionOutcome.Refused("(You have no party to keep a prisoner in.)");

                int giveRegardForPlayer = give.GetRelation(Hero.MainHero);

                // Every guard has held. The coin first, so a failed handover can refund it.
                if (gold > 0) GiveGoldAction.ApplyBetweenCharacters(npcHero, Hero.MainHero, gold);
                else if (gold < 0) GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, npcHero, -gold);

                try
                {
                    HandOver(give, giveHandover, giveDest, npcHero, backToPlayer: false);
                }
                catch (Exception ex)
                {
                    Refund(gold, npcHero);
                    ModLog.Error($"[ACTION] exchange_prisoners: handing over {give.Name} failed, nothing exchanged: {ex.GetType().Name}: {ex.Message}");

                    return ActionOutcome.Refused("(The exchange could not be carried out; no captive and no gold changed hands.)");
                }

                try
                {
                    HandOver(receive, receiveHandover, receiveDest, Hero.MainHero, backToPlayer: true);
                }
                catch (Exception ex)
                {
                    // The first captive has already gone and a release cannot be undone: refund the coin and say
                    // plainly what happened, never a silent half-trade.
                    Refund(gold, npcHero);
                    ModLog.Error($"[ACTION] exchange_prisoners: {give.Name} was handed over but {receive.Name} could not be: {ex.GetType().Name}: {ex.Message}");

                    return ActionOutcome.Refused($"({give.Name} was handed over, but {receive.Name} could not be delivered to you. Any gold for the exchange is returned.)");
                }

                var today = (int) CampaignTime.Now.ToDays;
                profile.Events.Add(new NotableEvent(today, NotableEventType.Agreement,
                    $"Exchanged the captive {receive.Name} for {give.Name}, whom {Hero.MainHero.Name} handed over."));

                if (receiveHandover == ExchangeHandover.IntoCustody)
                    NpcMemoryCampaignBehavior.Instance?.QuestTracker?.StampCaptureQuests(receive);

                if (giveHandover == ExchangeHandover.IntoCustody) PlantHandedOverGrudge(give, npcHero, giveRegardForPlayer, today);

                ModLog.Info($"[ACTION] exchange_prisoners: {give.Name} ({giveHandover}) for {receive.Name} ({receiveHandover}), gold {gold} to the player.");

                return new ActionOutcome(Describe(give, giveHandover, receive, receiveHandover, gold, npcHero), true);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ACTION] exchange_prisoners failed: {ex.GetType().Name}: {ex.Message}");

                return ActionOutcome.Refused("(The exchange could not be completed. Nothing happened.)");
            }
        }

        #region private

        /// <summary>The captives the NPC's clan could put on the table.</summary>
        private static List<Hero> TheirSide(Hero npc)
            => CourtActionResolver.ExchangeablePrisonersHeldBy(npc).Where(h => PrisonerExchangePolicy.IsExchangeable(Facts(h))).ToList();

        /// <summary>The captives the player could put on the table.</summary>
        private static List<Hero> PlayerSide()
            => BuyPrisonerVerb.PlayerHeldHeroPrisoners().Where(h => PrisonerExchangePolicy.IsExchangeable(Facts(h))).ToList();

        private static ExchangeCaptiveFacts Facts(Hero h)
            => new() {
                HeroId = h?.StringId ?? "",
                IsHero = h != null,
                IsLord = h?.IsLord == true,
                IsCompanion = h != null && (h.CompanionOf != null || h.IsWanderer),
                IsAlive = h?.IsAlive == true,
                IsChild = h?.IsChild == true,
                RansomAnchor = h == null ? 0 : BuyPrisonerVerb.RansomAnchor(h)
            };

        /// <summary>A captive belongs to a side when he is of its clan, or of its realm.</summary>
        private static bool Belongs(Hero captive, Clan clan, IFaction realm)
            => (clan != null && captive.Clan == clan) || (realm != null && captive.MapFaction == realm);

        /// <summary>Frees a captive going home, or moves him into the receiver's custody.</summary>
        private static void HandOver(Hero captive, ExchangeHandover handover, PartyBase dest, Hero receiver, bool backToPlayer)
        {
            if (handover == ExchangeHandover.Freed)
            {
                // Ransom, not "released by choice": the engine keeps a released player companion from turning fugitive
                // only on this path, and an exchange is a ransom paid in a person instead of coin.
                EndCaptivityAction.ApplyByRansom(captive, receiver);

                // One of the player's own companions or kin rejoins his party rather than wandering off.
                if (backToPlayer && captive.Clan == Clan.PlayerClan && MobileParty.MainParty != null && captive.PartyBelongedTo == null)
                    AddHeroToPartyAction.Apply(captive, MobileParty.MainParty);

                return;
            }

            PartyBase source = captive.PartyBelongedToAsPrisoner;
            if (source != null) TransferPrisonerAction.Apply(captive.CharacterObject, source, dest);
            else TakePrisonerAction.Apply(dest, captive);
        }

        private static void Refund(int gold, Hero npcHero)
        {
            if (gold > 0) GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, npcHero, gold);
            else if (gold < 0) GiveGoldAction.ApplyBetweenCharacters(npcHero, Hero.MainHero, -gold);
        }

        /// <summary>
        ///   A captive the player hands into someone else's custody (not home) resents it, as a sold one does, heavier
        ///   when handed to his own enemy. The same rule and wording buy_prisoner uses. Never undoes the exchange.
        /// </summary>
        private static void PlantHandedOverGrudge(Hero captive, Hero npcHero, int captiveRegardForPlayer, int today)
        {
            try
            {
                bool toEnemy = captive.MapFaction != null && npcHero.MapFaction != null &&
                               FactionManager.IsAtWarAgainstFaction(captive.MapFaction, npcHero.MapFaction);
                GrudgeProposal proposal = Tier2GrudgeRules.DeliveredPrisonerGrudge(new DeliveredPrisonerFacts {
                    DeliveredToEnemyOfCaptive = toEnemy,
                    CaptiveRegardForPlayer = captiveRegardForPlayer
                });

                if (proposal.Forms)
                    GrudgeBehavior.Instance?.TryPlant(captive, Hero.MainHero, GrudgeSource.DeliveredPrisoner,
                        toEnemy ? "The player traded me to my enemy." : "The player traded me off like chattel.",
                        proposal.Magnitude, proposal.KnownToPlayer, today);
                if (toEnemy) Mapping.MisdeedLedgerBehavior.Record(captive.StringId, Logic.Trauma.MisdeedKind.HandedToEnemy); // Gabriel 23/10: a misdeed
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ACTION] exchange_prisoners grudge plant failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static string Describe(Hero give, ExchangeHandover giveHandover, Hero receive, ExchangeHandover receiveHandover, int gold, Hero npcHero)
        {
            string gone = giveHandover == ExchangeHandover.Freed
                ? $"{give.Name} goes home free"
                : $"{give.Name} passes into {npcHero.Name}'s custody";
            string back = receiveHandover == ExchangeHandover.Freed
                ? $"{receive.Name} is free and returns to you"
                : $"{receive.Name} is delivered into your custody";
            string coin = gold > 0
                ? $" {npcHero.Name} adds {gold} denars to even the trade."
                : gold < 0
                    ? $" You add {-gold} denars to even the trade."
                    : "";

            return $"The exchange is made: {gone}, and {back}.{coin}";
        }

        #endregion
    }
}
