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
                    return new ActionOutcome("(You hold no such captive to sell.)", false);

                action.Parameters.TryGetValue("price", out string priceStr);
                int.TryParse(priceStr?.Trim(), out int proposed);
                int price = PrisonerTradePolicy.ClampPrice(proposed, RansomAnchor(captive));

                // Captured BEFORE the transfer (the grudge rule reads the captive's prior regard).
                int captiveRegardForPlayer = captive.GetRelation(Hero.MainHero);

                // The gold, then the man: the NPC pays the player, and the captive changes hands to the
                // buyer's party (or simply leaves the player's custody when the buyer rides with none).
                GiveGoldAction.ApplyBetweenCharacters(npcHero, Hero.MainHero, price);
                CharacterObject captiveChar = captive.CharacterObject;
                PartyBase dest = npcHero.PartyBelongedTo?.Party;
                if (dest != null) TransferPrisonerAction.Apply(captiveChar, MobileParty.MainParty.Party, dest);
                else MobileParty.MainParty.PrisonRoster.AddToCounts(captiveChar, -1);

                var today = (int) CampaignTime.Now.ToDays;
                profile.Events.Add(new NotableEvent(today, NotableEventType.Collaboration,
                    $"Bought the captive {captive.Name} from the player for {price} denars."));

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

                ModLog.Info($"[ACTION] buy_prisoner: {npcHero.Name} bought {captive.Name} from the player for {price} denars.");

                return new ActionOutcome($"{npcHero.Name} pays you {price} denars and takes {captive.Name} into their custody.", true);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ACTION] buy_prisoner failed: {ex.GetType().Name}: {ex.Message}");

                return null;
            }
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
