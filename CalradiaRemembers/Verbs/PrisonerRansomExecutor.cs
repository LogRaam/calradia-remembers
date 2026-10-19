// Code written by Gabriel Mailhot, 25/08/2026.
// The atomic prisoner-ransom transaction, extracted so BOTH the chat buy_prisoner verb and the new PrisonerOffer
// letter's Accept button settle a sale exactly the same way: clamp the price to the honest ransom band, refuse a
// buyer who cannot raise the floor (GiveGoldAction self-clamps to the purse, so an insolvent buyer would otherwise
// "buy" a valuable captive for a pittance), then move the gold and the captive ATOMICALLY with a refund if the
// transfer throws. BuyPrisonerVerb keeps its own copy (untouched, no spontaneous refactor); this mirrors it for the
// out-of-chat letter path, reusing the verb's internal ResolveCaptorPrison / RansomAnchor / PlayerHeldHeroPrisoners.

#region

using System;
using System.Linq;
using CalradiaRemembers.Foundation;
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
    /// <summary>Settles a prisoner ransom (a lord buys a captive the player holds) outside the chat verb path.</summary>
    internal static class PrisonerRansomExecutor
    {
        /// <summary>
        ///   Hands <paramref name="captive" /> to <paramref name="buyer" /> for gold, mirroring BuyPrisonerVerb's
        ///   atomic transaction. Returns true and a player-facing <paramref name="message" /> on a completed sale;
        ///   false with the reason when it is refused (captive no longer held, buyer has no prison, buyer cannot
        ///   raise the honest floor, or the transfer threw and the payment was refunded). <paramref name="buyerProfile" />
        ///   may be null (the buyer's memory of the trade is then simply not recorded).
        /// </summary>
        internal static bool Execute(Hero buyer, Hero captive, int offeredPrice, NpcProfile buyerProfile, out string message)
        {
            message = "";

            if (buyer == null || captive == null || Hero.MainHero == null)
            {
                message = "(The trade could not be made.)";

                return false;
            }

            try
            {
                // The captive may have escaped, died, or already been sold since the letter was written.
                if (!BuyPrisonerVerb.PlayerHeldHeroPrisoners().Contains(captive))
                {
                    message = $"(You no longer hold {captive.Name}.)";

                    return false;
                }

                int anchor = BuyPrisonerVerb.RansomAnchor(captive);
                int price = PrisonerTradePolicy.ClampPrice(offeredPrice, anchor);
                int captiveRegardForPlayer = captive.GetRelation(Hero.MainHero);

                PartyBase dest = BuyPrisonerVerb.ResolveCaptorPrison(buyer);
                if (dest == null)
                {
                    message = $"({buyer.Name} has no party or holding to keep a prisoner in.)";

                    return false;
                }

                int floor = PrisonerTradePolicy.MinAcceptablePayment(anchor);
                int payment = Math.Min(price, buyer.Gold);
                if (payment < floor)
                {
                    message = $"({buyer.Name} cannot raise a fair price for {captive.Name} and will not take them for a pittance.)";

                    return false;
                }

                CharacterObject captiveChar = captive.CharacterObject;

                GiveGoldAction.ApplyBetweenCharacters(buyer, Hero.MainHero, payment);
                try
                {
                    // Source from where the captive is ACTUALLY held (the main party OR a settlement dungeon of the
                    // player's), not a hardcoded main party: a captive left behind bars in the player's own town is
                    // still theirs to sell, and transferring from the wrong roster would move nothing.
                    PartyBase source = captive.PartyBelongedToAsPrisoner ?? MobileParty.MainParty?.Party;
                    TransferPrisonerAction.Apply(captiveChar, source, dest);
                }
                catch (Exception transferEx)
                {
                    GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, buyer, payment); // refund the failed purchase
                    ModLog.Error($"[LETTER OFFER] prisoner transfer failed, payment refunded: {transferEx.GetType().Name}: {transferEx.Message}");
                    message = $"(The handover of {captive.Name} fell through. {buyer.Name}'s payment is returned.)";

                    return false;
                }

                var today = (int) CampaignTime.Now.ToDays;
                buyerProfile?.Events.Add(new NotableEvent(today, NotableEventType.Collaboration,
                    $"Bought the captive {captive.Name} from the player for {payment} denars."));

                // A captive resents being sold ONLY when handed to their own enemy (a real betrayal). A ransom back
                // to their side or a sale to a neutral is a normal, honorable practice and plants nothing
                // (DeliveredPrisonerGrudge forms only when DeliveredToEnemyOfCaptive; player report 2026-08-26).
                try
                {
                    bool toEnemy = captive.MapFaction != null && buyer.MapFaction != null &&
                                   FactionManager.IsAtWarAgainstFaction(captive.MapFaction, buyer.MapFaction);
                    var facts = new DeliveredPrisonerFacts {
                        DeliveredToEnemyOfCaptive = toEnemy,
                        CaptiveRegardForPlayer = captiveRegardForPlayer
                    };
                    GrudgeProposal proposal = Tier2GrudgeRules.DeliveredPrisonerGrudge(facts);
                    if (proposal.Forms)
                        GrudgeBehavior.Instance?.TryPlant(captive, Hero.MainHero, GrudgeSource.DeliveredPrisoner,
                            "The player sold me to my enemy.",
                            proposal.Magnitude, proposal.KnownToPlayer, today);
                }
                catch (Exception ex)
                {
                    ModLog.Error($"[LETTER OFFER] prisoner-sale grudge plant failed: {ex.GetType().Name}: {ex.Message}");
                }

                ModLog.Info($"[LETTER OFFER] {buyer.Name} bought {captive.Name} from the player for {payment} denars (letter accept).");
                message = $"{buyer.Name} pays you {payment} denars and takes {captive.Name} into their custody.";

                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error($"[LETTER OFFER] prisoner ransom failed: {ex.GetType().Name}: {ex.Message}");
                message = "(The trade could not be completed. Nothing happened.)";

                return false;
            }
        }
    }
}
