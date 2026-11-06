// Code written by Gabriel Mailhot, 08/07/2026.
// Prisoner trade (ratified 2026-07-08), the SELL direction: the NPC sells a captive HIS CLAN holds to the
// player, for the player's gold. The LLM negotiates in-fiction; the bridge is law (price clamped by
// PrisonerTradePolicy around the game's ransom valuation, custody and the player's purse re-verified at
// execution). The other half of the bounty loop: a player who WANTS a prisoner can simply buy him.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
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
    ///   sell_prisoner: the NPC hands the player a hero captive their clan holds, for the player's gold.
    ///   Taught whenever the clan holds at least one; custody, price, and the player's purse are all
    ///   re-validated game-side at execution.
    /// </summary>
    public sealed class SellPrisonerVerb : IConversationVerb
    {
        public string Name => "sell_prisoner";

        public bool IsEligible(VerbContext ctx)
            => ctx?.Npc != null && CourtActionResolver.HeldPrisonersOf(ctx.Npc).Count > 0;

        public string TeachingText(VerbContext ctx)
        {
            if (ctx?.Npc == null) return null;

            List<Hero> held = CourtActionResolver.HeldPrisonersOf(ctx.Npc);
            if (held.Count == 0) return null;

            var sb = new StringBuilder();
            sb.AppendLine("SELL_PRISONER, SELLING A CAPTIVE YOU HOLD TO THE PLAYER:");
            sb.AppendLine("Your clan holds these captives, and the player might offer to BUY one from you");
            sb.AppendLine("(for ransom, for a bounty they hunt, or for purposes of their own):");
            // The band the price is held to, stated BEFORE a figure is named (tashmetu, 27/09/2026, the buy side).
            foreach (Hero captive in held)
            {
                int anchor = BuyPrisonerVerb.RansomAnchor(captive);
                sb.AppendLine($"- {captive.Name} (a fair price would be around {anchor} denars; the price can be no less than "
                              + $"{PrisonerTradePolicy.MinAcceptablePayment(anchor)} nor more than {PrisonerTradePolicy.MaxPrice(anchor)})");
            }
            // 3chmidt (Nexus, 05/11/2026) asked to be given captives. Said before any word, only to one bound deeply enough.
            if (ctx.Profile != null && PrisonerTradePolicy.MayGiveFreely(ctx.Profile.ReputationWithPlayer))
                sb.AppendLine("You hold the player dear enough to give one of them for nothing, if you choose: then write price: 0.");
            sb.AppendLine("Sell only if it serves you; a captive of value to your realm is not parted with");
            sb.AppendLine("lightly, and never below what they are worth. When you and the player have AGREED");
            sb.AppendLine("on the sale, and only then, emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: sell_prisoner");
            sb.AppendLine("target: the captive's name, exactly as listed above");
            sb.AppendLine("price: the agreed denars");
            sb.AppendLine("[/ACTION]");
            sb.AppendLine("The sale moves the coin itself. Never also emit take_gold or give_gold for it, or the");
            sb.AppendLine("player pays twice for one captive.");
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

                action.Parameters.TryGetValue("target", out string targetName);
                Hero captive = CourtActionResolver.ResolveHeldPrisonerByName(targetName, npcHero);

                if (captive == null || !CourtActionResolver.CanRelease(captive, npcHero))
                {
                    // The mirror of the redirect in BuyPrisonerVerb: if the named captive is in the PLAYER's own
                    // hands, the only trade on the table is the NPC buying them.
                    if (captive == null && BuyPrisonerVerb.PlayerHoldsCaptiveNamed(targetName))
                    {
                        ModLog.Info($"[ACTION] sell_prisoner named a captive the player holds; running it as buy_prisoner.");

                        return new BuyPrisonerVerb().Execute(action, ctx);
                    }

                    return ActionOutcome.Refused(NotHisToSell(npcHero, targetName, captive));
                }

                if (BuyPrisonerVerb.IsEchoOfLastTrade(captive, npcHero)) return ActionOutcome.Refused(BuyPrisonerVerb.EchoRefusal(npcHero, captive));

                action.Parameters.TryGetValue("price", out string priceStr);
                int.TryParse(priceStr?.Trim(), out int proposed);
                // Given for nothing, by one bound deeply enough to the player (3chmidt, 05/11/2026); else a price is owed.
                bool given = PrisonerTradePolicy.AsksNothing(priceStr);
                if (given && !PrisonerTradePolicy.MayGiveFreely(profile.ReputationWithPlayer))
                    return ActionOutcome.Refused($"({npcHero.Name} will not part with {captive.Name} for nothing; a price must be agreed.)");
                int price = given ? 0 : PrisonerTradePolicy.ClampPrice(proposed, BuyPrisonerVerb.RansomAnchor(captive));

                if (Hero.MainHero.Gold < price)
                    return ActionOutcome.Refused($"(You cannot pay the {price} denars {npcHero.Name} asks for {captive.Name}.)");

                // Every failable guard is resolved BEFORE any gold moves (valid captive, held by the clan,
                // purse sufficient, source roster read), so the payment only ever leaves the player's purse
                // once the handover is certain to be attempted.
                CharacterObject captiveChar = captive.CharacterObject;
                PartyBase from = captive.PartyBelongedToAsPrisoner;

                // The gold, then the man, ATOMICALLY: the player pays, then the captive changes custody into
                // the player's own prison roster. If the transfer still throws, the payment is refunded and the
                // sale refused, never a silent half-trade (gold gone, no captive, player told nothing).
                if (price > 0) GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, npcHero, price);
                try
                {
                    if (from != null) TransferPrisonerAction.Apply(captiveChar, from, MobileParty.MainParty.Party);
                    else TakePrisonerAction.Apply(MobileParty.MainParty.Party, captive);
                }
                catch (Exception transferEx)
                {
                    if (price > 0) GiveGoldAction.ApplyBetweenCharacters(npcHero, Hero.MainHero, price); // refund the failed purchase
                    ModLog.Error($"[ACTION] sell_prisoner transfer failed, payment refunded: {transferEx.GetType().Name}: {transferEx.Message}");

                    return ActionOutcome.Refused($"({captive.Name} could not be delivered into your custody. Your {price} denars are returned.)");
                }

                // A purchase is a custody handoff, not a battle capture: TransferPrisonerAction raises no
                // HeroPrisonerTaken, so capture quests would never stamp. We do NOT dispatch that event by
                // hand — its other listeners (stance fear, world chronicle, companion missions, nemesis)
                // are tuned for a real capture and would misfire on a sale. Stamp the quests directly
                // instead; harmless on the TakePrisoner path, where the native hook already stamped them
                // (Stamp only ever touches outstanding quests).
                NpcMemoryCampaignBehavior.Instance?.QuestTracker?.StampCaptureQuests(captive);

                var today = (int) CampaignTime.Now.ToDays;
                profile.Events.Add(new NotableEvent(today, NotableEventType.Collaboration,
                    given ? $"Gave the captive {captive.Name} to the player, asking nothing." : $"Sold the captive {captive.Name} to the player for {price} denars."));

                ModLog.Info($"[ACTION] sell_prisoner: {npcHero.Name} {(given ? "gave" : "sold")} {captive.Name} to the player{(given ? " for nothing" : $" for {price} denars")}.");
                BuyPrisonerVerb.RecordTrade(captive, npcHero);

                if (given) return new ActionOutcome($"{npcHero.Name} gives {captive.Name} into your custody, asking nothing.", true);

                return new ActionOutcome($"You pay {price} denars{PrisonerTradePolicy.PriceDifferenceNote(proposed, price, false, "you")}, "
                                         + $"and {captive.Name} is delivered into your custody.", true);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ACTION] sell_prisoner failed: {ex.GetType().Name}: {ex.Message}");

                // A pre-payment failure moved nothing (gold only leaves the purse after every guard passes),
                // so surface a refusal rather than vanish as a silent null.
                return ActionOutcome.Refused("(The sale could not be completed. Nothing happened.)");
            }
        }

        /// <summary>
        ///   The refusal when this lord does not hold the captive he agreed to sell (Lordfadooboo, 04/10/2026: "You hold
        ///   no such captive to sell" answered a purchase). Finds the captive in whatever cell holds them, and says who
        ///   holds them and where, or that they are of the player's own house.
        /// </summary>
        internal static string NotHisToSell(Hero seller, string targetName, Hero resolved = null, string verb = "sell_prisoner")
        {
            string query = targetName?.Trim() ?? "";
            Hero captive = resolved ?? (query.Length == 0
                ? null
                : Hero.AllAliveHeroes.FirstOrDefault(h => h is {IsPrisoner: true} && h != Hero.MainHero && h.Name != null
                                                          && CaptiveNameMatch.Matches(h.Name.ToString(), query)));

            PartyBase captor = captive?.PartyBelongedToAsPrisoner;
            Hero keeper = captor?.LeaderHero ?? captor?.Settlement?.OwnerClan?.Leader;
            string holder = keeper?.Name?.ToString() ?? captor?.Settlement?.OwnerClan?.Name?.ToString();
            string place = captor?.Settlement?.Name?.ToString() ?? captor?.MobileParty?.Name?.ToString();
            bool own = captive != null && captive.Clan != null && captive.Clan == Clan.PlayerClan;

            // froggyluv (04/11/2026): "agreed to sell" read as a reversed trade when the lord was BUYING; say which verb ran.
            ModLog.Info($"[ACTION] {verb}: {seller?.Name} agreed on '{targetName}', who is not in the right hands for it: "
                        + (captive == null ? "no prisoner by that name." : $"{captive.Name} is held by {holder ?? "?"} at {place ?? "?"}{(own ? ", of the player's own house" : "")}."));

            return PrisonerTradePolicy.NotHisToSellNote(new NotHisToSellFacts {
                SellerName = seller?.Name?.ToString() ?? "He",
                CaptiveName = captive?.Name?.ToString() ?? (query.Length > 0 ? query : "that captive"),
                HolderName = captive == null ? null : holder,
                PlaceName = place,
                IsPlayersOwn = own,
                PlayerWasSelling = verb == "buy_prisoner"
            });
        }
    }
}
