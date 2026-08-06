// Code written by Gabriel Mailhot, 08/07/2026.
// Prisoner trade (ratified 2026-07-08), the BUY direction: the NPC purchases a captive the PLAYER holds, for
// gold. The LLM negotiates in-fiction; the bridge is law (price clamped by PrisonerTradePolicy around the
// game's own ransom valuation, custody re-verified at execution). Opens the bounty-hunting loop: capture a
// man, ride to whoever wants him, and sell.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Grudges;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>
    ///   buy_prisoner: the NPC pays the player gold for a hero captive the player's party holds. Taught
    ///   whenever the player holds at least one; the captive, the custody, and the price are all
    ///   re-validated game-side at execution.
    /// </summary>
    public sealed class BuyPrisonerVerb : IConversationVerb
    {
        public string Name => "buy_prisoner";

        public bool IsEligible(VerbContext ctx)
            => ctx?.Npc != null && PlayerHeldHeroPrisoners().Count > 0;

        public string TeachingText(VerbContext ctx)
        {
            if (ctx?.Npc == null) return null;

            List<Hero> held = PlayerHeldHeroPrisoners();
            if (held.Count == 0) return null;

            var sb = new StringBuilder();
            sb.AppendLine("BUY_PRISONER, PURCHASING A CAPTIVE THE PLAYER HOLDS:");
            sb.AppendLine("The player travels with these hero captives, and you may offer to BUY one for gold,");
            sb.AppendLine("if the man or woman is worth something to you (an enemy of your realm to lock away,");
            sb.AppendLine("kin to ransom home, leverage over a rival house):");
            foreach (Hero captive in held.Take(3))
                sb.AppendLine($"- {captive.Name} (a fair price would be around {RansomAnchor(captive)} denars)");
            sb.AppendLine("Negotiate the figure in conversation like the lord you are; pay a premium only for");
            sb.AppendLine("someone you truly want. When you and the player have AGREED on the sale, and only");
            sb.AppendLine("then, emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: buy_prisoner");
            sb.AppendLine("target: the captive's name, exactly as listed above");
            sb.AppendLine("price: the agreed denars");
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

                action.Parameters.TryGetValue("target", out string targetName);
                Hero captive = ResolvePlayerHeldPrisonerByName(targetName);

                if (captive == null)
                    return ActionOutcome.Refused("(You hold no such captive to sell.)");

                action.Parameters.TryGetValue("price", out string priceStr);
                int.TryParse(priceStr?.Trim(), out int proposed);
                int anchor = RansomAnchor(captive);
                int price = PrisonerTradePolicy.ClampPrice(proposed, anchor);

                // Captured BEFORE the transfer (the grudge rule reads the captive's prior regard).
                int captiveRegardForPlayer = captive.GetRelation(Hero.MainHero);

                // Resolve a REAL destination prison the buyer can hold the captive in BEFORE any gold moves.
                // A buyer with nowhere to keep a prisoner is REFUSED here, never paid: the old code fell back
                // to AddToCounts(-1) on the PLAYER's roster, which deleted the captive and left them a
                // prisoner of no one (IsPrisoner with no captor party, verified state corruption).
                PartyBase dest = ResolveCaptorPrison(npcHero);
                if (dest == null)
                    return ActionOutcome.Refused($"({npcHero.Name} has no party or holding to keep a prisoner in.)");

                // NPC solvency: GiveGoldAction self-clamps a payment to the payer's purse, so a broke buyer
                // would otherwise "buy" the captive for its last few denars while the player was told the full
                // agreed price. Pay the TRUE amount the buyer can raise, and REFUSE below the honest floor
                // rather than hand a valuable captive over for a pittance.
                int floor = PrisonerTradePolicy.MinAcceptablePayment(anchor);
                int payment = Math.Min(price, npcHero.Gold);
                if (payment < floor)
                    return ActionOutcome.Refused($"({npcHero.Name} cannot raise a fair price for {captive.Name} and will not take them for a pittance.)");

                CharacterObject captiveChar = captive.CharacterObject;

                // The gold, then the man, ATOMICALLY: the NPC pays the player the true amount, then the captive
                // changes hands into the buyer's custody (a real +1 to a real roster, never a phantom deletion).
                // If the transfer still throws, the payment is refunded and the sale refused, never a silent
                // half-trade (gold paid, no captive moved, player told nothing).
                GiveGoldAction.ApplyBetweenCharacters(npcHero, Hero.MainHero, payment);
                try
                {
                    TransferPrisonerAction.Apply(captiveChar, MobileParty.MainParty.Party, dest);
                }
                catch (Exception transferEx)
                {
                    GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, npcHero, payment); // refund the failed purchase
                    ModLog.Error($"[ACTION] buy_prisoner transfer failed, payment refunded: {transferEx.GetType().Name}: {transferEx.Message}");

                    return ActionOutcome.Refused($"(The handover of {captive.Name} fell through. {npcHero.Name}'s payment is returned.)");
                }

                var today = (int) CampaignTime.Now.ToDays;
                profile.Events.Add(new NotableEvent(today, NotableEventType.Collaboration,
                    $"Bought the captive {captive.Name} from the player for {payment} denars."));

                // The SOLD captive resents being traded like goods, heavier when sold to their own enemy.
                // Guarded so a plant failure never claws back the sale that already succeeded.
                try
                {
                    bool toEnemy = captive.MapFaction != null && npcHero.MapFaction != null &&
                                   FactionManager.IsAtWarAgainstFaction(captive.MapFaction, npcHero.MapFaction);
                    var facts = new DeliveredPrisonerFacts {
                        DeliveredToEnemyOfCaptive = toEnemy,
                        CaptiveRegardForPlayer = captiveRegardForPlayer
                    };
                    GrudgeProposal proposal = Tier2GrudgeRules.DeliveredPrisonerGrudge(facts);
                    if (proposal.Forms)
                        GrudgeBehavior.Instance?.TryPlant(captive, Hero.MainHero, GrudgeSource.DeliveredPrisoner,
                            toEnemy ? "The player sold me to my enemy." : "The player sold me off like chattel.",
                            proposal.Magnitude, proposal.KnownToPlayer, today);
                }
                catch (Exception ex)
                {
                    ModLog.Error($"[ACTION] buy_prisoner grudge plant failed: {ex.GetType().Name}: {ex.Message}");
                }

                ModLog.Info($"[ACTION] buy_prisoner: {npcHero.Name} bought {captive.Name} from the player for {payment} denars.");

                return new ActionOutcome($"{npcHero.Name} pays you {payment} denars and takes {captive.Name} into their custody.", true);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ACTION] buy_prisoner failed: {ex.GetType().Name}: {ex.Message}");

                // A pre-payment failure moved nothing (gold only leaves the buyer after every guard passes),
                // so surface a refusal rather than vanish as a silent null.
                return ActionOutcome.Refused("(The sale could not be completed. Nothing happened.)");
            }
        }

        /// <summary>
        ///   A prison-holding party that can actually RECEIVE a bought or handed-over captive, or null when
        ///   the receiver has nowhere to keep one. Resolved BEFORE any gold or transfer: the receiver's own
        ///   party, else the settlement they stand in, else a town or castle their clan owns. Returning null
        ///   (so the caller refuses) is what stops a receiver with no party from being paid while the captive
        ///   is deleted and left a prisoner of no one. Shared with the give_prisoner twin in the bridge.
        /// </summary>
        internal static PartyBase ResolveCaptorPrison(Hero receiver)
        {
            if (receiver == null) return null;

            PartyBase own = receiver.PartyBelongedTo?.Party;
            if (own != null) return own;

            PartyBase here = receiver.CurrentSettlement?.Party;
            if (here != null) return here;

            Clan clan = receiver.Clan;
            if (clan?.Settlements != null)
                foreach (Settlement s in clan.Settlements)
                    if (s?.Party != null && (s.IsTown || s.IsCastle))
                        return s.Party;

            return null;
        }

        // ── Shared with the teaching: the player's hero captives, and the game's own valuation ──

        /// <summary>Hero captives riding in the player's party (the sellable pool).</summary>
        internal static List<Hero> PlayerHeldHeroPrisoners()
        {
            var result = new List<Hero>();
            TroopRoster prison = MobileParty.MainParty?.PrisonRoster;
            if (prison == null) return result;

            foreach (TroopRosterElement el in prison.GetTroopRoster())
            {
                Hero h = el.Character?.HeroObject;
                if (h is {IsAlive: true}) result.Add(h);
            }

            return result;
        }

        /// <summary>Tolerant name match among the player's held hero captives.</summary>
        internal static Hero ResolvePlayerHeldPrisonerByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            string query = name.Trim();

            return PlayerHeldHeroPrisoners().FirstOrDefault(h =>
                h.Name != null && h.Name.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>The game's own ransom valuation of the captive, or the policy fallback when unreadable.</summary>
        internal static int RansomAnchor(Hero captive)
        {
            try
            {
                int v = Campaign.Current.Models.RansomValueCalculationModel.PrisonerRansomValue(captive.CharacterObject, Hero.MainHero);

                return v > 0 ? v : PrisonerTradePolicy.FallbackAnchor;
            }
            catch { return PrisonerTradePolicy.FallbackAnchor; }
        }
    }
}
