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
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Logic.Prisoners;
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
            sb.AppendLine("The player holds these hero captives (in their party, or behind bars in their own town),");
            sb.AppendLine("and you may offer to BUY one for gold,");
            sb.AppendLine("if the man or woman is worth something to you (an enemy of your realm to lock away,");
            sb.AppendLine("kin to ransom home, leverage over a rival house):");
            // The most this buyer can really pay, stated BEFORE they name a figure (tashmetu, 27/09/2026: a lord
            // offered fifteen thousand she did not have, and the game paid 5098).
            foreach (Hero captive in held.Take(PrisonerTradePolicy.CaptivesPricedFor(ModSettings.LeanPromptChosen)))
            {
                int anchor = RansomAnchor(captive);
                int most = PrisonerTradePolicy.MostABuyerCanPay(anchor, ctx.Npc.Gold, BondTo(ctx.Npc, captive));
                string means = most < PrisonerTradePolicy.MinAcceptablePayment(anchor)
                    ? "you cannot raise a fair price for them"
                    : $"the most you can pay is {most}";
                sb.AppendLine($"- {captive.Name} (a fair price would be around {anchor} denars; {means})");
            }
            sb.AppendLine("Negotiate the figure in conversation like the lord you are; pay a premium only for");
            sb.AppendLine("someone you truly want. When you and the player have AGREED on the sale, and only");
            sb.AppendLine("then, emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: buy_prisoner");
            sb.AppendLine("target: the captive's name, exactly as listed above");
            sb.AppendLine("price: the agreed denars");
            sb.AppendLine("[/ACTION]");
            sb.AppendLine("The purchase moves the coin itself. Never also emit give_gold or take_gold for it.");
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
                {
                    // WRONG WAY ROUND, and custody says so (Lordfadooboo, 21/09/2026): the player asked to BUY,
                    // the model heard "buy" and emitted the verb for the NPC buying. If the named captive is in
                    // this lord's own cage, there is exactly one trade this can be, so run it rather than refuse
                    // a sale the player already agreed and paid for in the fiction.
                    if (PrisonerTradePolicy.DirectionFor(false, CourtActionResolver.ResolveHeldPrisonerByName(targetName, npcHero) != null)
                        == PrisonerTradePolicy.TradeDirection.NpcSellsToPlayer)
                    {
                        ModLog.Info($"[ACTION] buy_prisoner named a captive {npcHero.Name} holds; running it as sell_prisoner.");

                        return new SellPrisonerVerb().Execute(action, ctx);
                    }

                    // Neither of them holds the captive named: say who does, never "you hold no such captive to sell"
                    // to a player who was buying (Lordfadooboo, 04/10/2026).
                    return ActionOutcome.Refused(SellPrisonerVerb.NotHisToSell(npcHero, targetName, verb: "buy_prisoner"));
                }

                if (IsEchoOfLastTrade(captive, npcHero)) return ActionOutcome.Refused(EchoRefusal(npcHero, captive));

                // A CAPTIVE HE ASKED FOR IS A DELIVERY, NOT A SALE (Lordfadooboo, 18/09/2026). A Khuzait lord
                // asked for a Nord lord in chains; the player brought one and typed the handover, and the model
                // reached for this verb. It paid a price, moved the captive, and left the bargain open for good:
                // "i even got paid, but the quest still remained on my board, unable to be completed."
                //
                // Settled HERE rather than by teaching the model harder, because the prompt is advice and this
                // is law, and because it was never the model's choice to make: handing a lord the very captive
                // he asked for keeps his bargain, whatever the words. The delivery pays the bargain's own reward,
                // so the player is not paid twice for one captive, once by price and once by reward.
                InformalQuest owed = DeliveryQuestMatcher.QuestSettledBy(
                    profile.ActiveQuests, new HeldCaptive {Id = captive.StringId, FactionId = captive.MapFaction?.StringId});

                IGameStateBridge bridge = NpcMemoryCampaignBehavior.Instance?.GameStateBridge;
                if (owed != null && bridge != null)
                {
                    ModLog.Info($"[ACTION] buy_prisoner: {captive.Name} settles {npcHero.Name}'s outstanding delivery bargain, so this is a delivery, not a sale.");

                    var delivery = new GameAction {
                        Type = "give_prisoner",
                        Parameters = new Dictionary<string, string> {["target"] = captive.Name?.ToString() ?? ""}
                    };

                    return bridge.ExecuteAction(delivery, npcHero, new ActionContext(profile, null));
                }

                action.Parameters.TryGetValue("price", out string priceStr);

                // Given for nothing is a gift, not a sale at the floor (3chmidt, Nexus, 05/11/2026): the interpreter may read
                // "I give him to you, I ask nothing" as this verb with price 0, and the floor would pay the player for a gift.
                if (PrisonerTradePolicy.AsksNothing(priceStr) && bridge != null)
                {
                    ModLog.Info($"[ACTION] buy_prisoner for nothing: {captive.Name} is a gift to {npcHero.Name}; running it as give_prisoner.");
                    var freeGift = new GameAction {Type = "give_prisoner", Parameters = new Dictionary<string, string> {["target"] = captive.Name?.ToString() ?? ""}};

                    return bridge.ExecuteAction(freeGift, npcHero, new ActionContext(profile, null));
                }

                // A price that is no figure pays the floor (PrisonerTradePolicy.ClampPrice). Said in the log, because a money
                // token the table missed reads "a fair sum" here (RaveN017Mod, Nexus, 05/11/2026: an agreed price lost on the way).
                if (!int.TryParse(priceStr?.Trim(), out int proposed))
                    ModLog.Info($"[EXCHANGE] buy_prisoner: the price \"{priceStr}\" is no figure; {npcHero.Name} pays the floor for {captive.Name}.");
                int anchor = RansomAnchor(captive);
                int price = PrisonerTradePolicy.ClampPrice(proposed, anchor, BondTo(npcHero, captive));

                // Given away, not sold (fkasad, 26/09/2026: "yours for 0 denars" to his own queen's envoy paid him the
                // full ceiling). Only a "gift" the player agreed skips the price; the envoy's binder sets it.
                bool gift = action.Parameters.TryGetValue("gift", out string giftStr) && string.Equals(giftStr?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

                // Bought by his OWN side (his house, his realm), the captive goes home free (fkasad, 26/09/2026: his own
                // count ransomed him, and the game then reported him "escaped", because a lord held captive by his own
                // side is let go by the engine). The ransom path, as the exchange takes, the one that keeps a player
                // companion from turning fugitive. Bought by anyone else, he changes gaolers.
                bool goesHome = GoesHomeTo(captive, npcHero);

                // Captured BEFORE the transfer (the grudge rule reads the captive's prior regard).
                int captiveRegardForPlayer = captive.GetRelation(Hero.MainHero);

                // Resolve a REAL destination prison the buyer can hold the captive in BEFORE any gold moves.
                // A buyer with nowhere to keep a prisoner is REFUSED here, never paid: the old code fell back
                // to AddToCounts(-1) on the PLAYER's roster, which deleted the captive and left them a
                // prisoner of no one (IsPrisoner with no captor party, verified state corruption).
                PartyBase dest = goesHome ? null : ResolveCaptorPrison(npcHero);
                if (!goesHome && dest == null)
                    return ActionOutcome.Refused($"({npcHero.Name} has no party or holding to keep a prisoner in.)");

                // NPC solvency: GiveGoldAction self-clamps a payment to the payer's purse, so a broke buyer
                // would otherwise "buy" the captive for its last few denars while the player was told the full
                // agreed price. Pay the TRUE amount the buyer can raise, and REFUSE below the honest floor
                // rather than hand a valuable captive over for a pittance.
                int floor = PrisonerTradePolicy.MinAcceptablePayment(anchor);
                bool limitedByPurse = !gift && npcHero.Gold < price;
                int payment = gift ? 0 : Math.Min(price, npcHero.Gold);
                if (!gift && payment < floor)
                    return ActionOutcome.Refused($"({npcHero.Name} cannot raise a fair price for {captive.Name} and will not take them for a pittance.)");

                CharacterObject captiveChar = captive.CharacterObject;

                // The gold, then the man, ATOMICALLY: the NPC pays the player the true amount, then the captive
                // changes hands into the buyer's custody (a real +1 to a real roster, never a phantom deletion).
                // If the transfer still throws, the payment is refunded and the sale refused, never a silent
                // half-trade (gold paid, no captive moved, player told nothing).
                ExchangeService.Pay(ExchangeKind.CaptiveSale, npcHero, Hero.MainHero, payment);
                try
                {
                    if (goesHome)
                    {
                        EndCaptivityAction.ApplyByRansom(captive, npcHero);
                    }
                    else
                    {
                        // Source from where the captive is ACTUALLY held (the main party OR a settlement dungeon of the
                        // player's), not a hardcoded main party: a captive left behind bars in the player's own town is
                        // still theirs to sell, and transferring from the wrong roster would move nothing.
                        PartyBase source = captive.PartyBelongedToAsPrisoner ?? MobileParty.MainParty?.Party;
                        TransferPrisonerAction.Apply(captiveChar, source, dest);
                    }
                }
                catch (Exception transferEx)
                {
                    ExchangeService.Pay(ExchangeKind.CaptiveSaleRefund, Hero.MainHero, npcHero, payment); // refund the failed purchase
                    ModLog.Error($"[ACTION] buy_prisoner transfer failed, payment refunded: {transferEx.GetType().Name}: {transferEx.Message}");

                    return ActionOutcome.Refused($"({captive.Name} could not be handed over, so nothing changed hands; {npcHero.Name}'s payment is returned.)");
                }

                var today = (int) CampaignTime.Now.ToDays;
                profile.Events.Add(new NotableEvent(today, NotableEventType.Collaboration,
                    gift
                        ? $"Received the captive {captive.Name} from the player as a gift, for nothing."
                        : $"Bought the captive {captive.Name} from the player for {payment} denars."));

                // The SOLD captive resents being traded like goods, heavier when sold to their own enemy.
                // Guarded so a plant failure never claws back the sale that already succeeded.
                // A captive who went home free has nothing to resent: he was ransomed, not sold.
                if (!goesHome)
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
                    if (toEnemy) Mapping.MisdeedLedgerBehavior.Record(captive.StringId, Logic.Trauma.MisdeedKind.HandedToEnemy); // Gabriel 23/10: a misdeed
                    if (toEnemy) Mapping.DeedRegardApplier.Apply(captive, Logic.PlayerDeed.HandedToEnemy);
                }
                catch (Exception ex)
                {
                    ModLog.Error($"[ACTION] buy_prisoner grudge plant failed: {ex.GetType().Name}: {ex.Message}");
                }

                ModLog.Info($"[ACTION] buy_prisoner: {npcHero.Name} {(gift ? "received" : "bought")} {captive.Name} from the player for {payment} denars{(goesHome ? ", and freed them" : "")}.");
                RecordTrade(captive, npcHero);

                string handed = goesHome ? $"and {captive.Name} goes home free" : $"and takes {captive.Name} into their custody";
                return new ActionOutcome(gift
                    ? $"You give {captive.Name} to {npcHero.Name} for nothing, {(goesHome ? $"and {captive.Name} goes home free" : $"into {npcHero.Name}'s custody")}."
                    : $"{npcHero.Name} pays you {payment} denars{PrisonerTradePolicy.PriceDifferenceNote(proposed, payment, limitedByPurse, npcHero.Name.ToString())} {handed}.", true);
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
        ///   Taken by his OWN side (his house, his realm), a captive goes home free (fkasad, 26/09/2026): a lord held captive
        ///   by his own side is let go by the engine and reported "escaped". One rule for a sale and a gift alike.
        /// </summary>
        internal static bool GoesHomeTo(Hero captive, Hero receiver)
            => captive != null && receiver != null
               && ((captive.Clan != null && captive.Clan == receiver.Clan) || (captive.MapFaction != null && captive.MapFaction == receiver.MapFaction));

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
        /// <summary>
        ///   Every alive hero captive the player currently holds: those travelling in the main party AND those
        ///   left behind bars in a settlement the player's clan owns (a town or castle dungeon). Player report: a
        ///   ransom-offer letter's Accept button vanished after the captured lord was moved from the party to the
        ///   player's own city prison, though the player plainly still held him; the same blind spot also kept a
        ///   dungeon captive from ever being offered for sale in conversation or triggering a ransom letter.
        ///   Deduped by StringId so a hero is counted once.
        /// </summary>
        internal static List<Hero> PlayerHeldHeroPrisoners()
        {
            var byId = new Dictionary<string, Hero>();
            var prisons = new List<PrisonHolding>
            {
                BuildHolding(MobileParty.MainParty?.PrisonRoster, isPlayerMainParty: true, ownedByPlayer: false, byId)
            };

            Clan playerClan = Clan.PlayerClan;
            if (playerClan?.Settlements != null)
                foreach (Settlement s in playerClan.Settlements)
                    prisons.Add(BuildHolding(s?.Party?.PrisonRoster, isPlayerMainParty: false,
                        ownedByPlayer: s?.OwnerClan == playerClan, byId));

            // The pure PrisonerCustodyPolicy owns the rule (which prisons count) and the dedup; the engine here
            // only reads rosters and maps the surviving ids back to their Hero.
            var result = new List<Hero>();
            foreach (string id in PrisonerCustodyPolicy.HeldByPlayer(prisons))
                if (byId.TryGetValue(id, out Hero h)) result.Add(h);

            return result;
        }

        /// <summary>Reads a prison roster into a pure <see cref="PrisonHolding" />, recording each alive hero's id -&gt; Hero for later mapping. Null-safe.</summary>
        private static PrisonHolding BuildHolding(
            TroopRoster prison, bool isPlayerMainParty, bool ownedByPlayer, Dictionary<string, Hero> byId)
        {
            var ids = new List<string>();
            if (prison != null)
                foreach (TroopRosterElement el in prison.GetTroopRoster())
                {
                    Hero h = el.Character?.HeroObject;
                    if (h is {IsAlive: true})
                    {
                        ids.Add(h.StringId);
                        byId[h.StringId] = h;
                    }
                }

            return new PrisonHolding(isPlayerMainParty, ownedByPlayer, ids);
        }

        // The last trade of each captive between the player and a character, this session: captive id -> (character id, day).
        // Not saved: a reload forgets it, and the echo it guards against happens within the same conversation.
        private static readonly Dictionary<string, (string counterpart, double day)> LastTrades = new Dictionary<string, (string, double)>();

        /// <summary>Notes that <paramref name="captive" /> just changed hands between the player and <paramref name="counterpart" />.</summary>
        internal static void RecordTrade(Hero captive, Hero counterpart)
        {
            if (captive?.StringId == null || counterpart?.StringId == null) return;

            LastTrades[captive.StringId] = (counterpart.StringId, CampaignTime.Now.ToDays);
        }

        /// <summary>The trade noted for a captive, or null; for cr.test to put back what it found.</summary>
        internal static (string counterpart, double day)? NotedTradeOf(Hero captive)
            => captive?.StringId != null && LastTrades.TryGetValue(captive.StringId, out var last) ? last : ((string, double)?) null;

        /// <summary>Puts back the trade noted for a captive (null forgets it); for cr.test.</summary>
        internal static void PutNotedTrade(Hero captive, (string counterpart, double day)? trade)
        {
            if (captive?.StringId == null) return;

            if (trade.HasValue) LastTrades[captive.StringId] = trade.Value;
            else LastTrades.Remove(captive.StringId);
        }

        /// <summary>True when trading <paramref name="captive" /> with <paramref name="counterpart" /> again echoes the trade just made.</summary>
        internal static bool IsEchoOfLastTrade(Hero captive, Hero counterpart)
        {
            if (captive?.StringId == null || counterpart?.StringId == null || !LastTrades.TryGetValue(captive.StringId, out var last)) return false;

            bool echo = PrisonerTradePolicy.IsEchoOfLastTrade(last.counterpart, last.day, counterpart.StringId, CampaignTime.Now.ToDays);
            if (echo) ModLog.Info($"[ACTION] {captive.Name} just changed hands with {counterpart.Name}; the same trade named again is refused, nothing moves.");

            return echo;
        }

        /// <summary>What the player reads when a trade just made is named again.</summary>
        internal static string EchoRefusal(Hero counterpart, Hero captive)
            => $"(You and {counterpart.Name} have just settled {captive.Name}; that bargain is done, and nothing changes hands again.)";

        /// <summary>Tolerant name match among the player's held hero captives.</summary>
        /// <summary>Whether the player themselves holds the captive this name points at; the mirror redirect in SellPrisonerVerb asks this.</summary>
        internal static bool PlayerHoldsCaptiveNamed(string name) => ResolvePlayerHeldPrisonerByName(name) != null;

        internal static Hero ResolvePlayerHeldPrisonerByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            // froggyluv 04/11/2026: "Elthild of dey Tihr" is Elthild. RaveN017Mod 05/11/2026: the exact name first, and a name
            // that could be two captives sells neither (the first in the roster used to go).
            List<Hero> held = PlayerHeldHeroPrisoners();
            int at = CaptiveNameMatch.Best(held.Select(h => h.Name?.ToString()).ToList(), name);

            return at >= 0 ? held[at] : null;
        }

        /// <summary>The game's own ransom valuation of the captive, or the policy fallback when unreadable.</summary>
        internal static int RansomAnchor(Hero captive) => CourtActionResolver.RansomAnchor(captive);

        /// <summary>
        ///   What the captive is to the one buying them, read from the game's family and relation (Thoragoros1, 03/11/2026):
        ///   it lifts the most they pay. Only for a buyer paying; the player buying meets the ordinary band.
        /// </summary>
        internal static CaptiveBond BondTo(Hero buyer, Hero captive)
        {
            if (buyer == null || captive == null) return CaptiveBond.None;

            bool sibling = (captive.Father != null && captive.Father == buyer.Father) || (captive.Mother != null && captive.Mother == buyer.Mother);

            return PrisonerTradePolicy.BondOf(new CaptiveBondFacts {
                IsSpouse = buyer.Spouse == captive,
                IsChild = captive.Father == buyer || captive.Mother == buyer,
                IsParentOrSibling = buyer.Father == captive || buyer.Mother == captive || sibling,
                Relation = Mapping.PairBondReader.Points(buyer, captive)
            });
        }
    }
}
