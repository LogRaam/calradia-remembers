// Code written by Gabriel Mailhot, 05/11/2026.
// Console entry for the throwaway barter-screen spike (see BarterScreenSpike): can the game's own barter screen be the
// prisoner-sale panel RaveN017Mod asked for, opened over our chat, judged by our price, closed back onto the chat?

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Chat;
using CalradiaRemembers.UI.Spike;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
    public static partial class ModCheats
    {
        /// <summary>
        ///   cr.barter_spike [name]: opens the game's barter screen for your first held hero captive, sold to the character you
        ///   are chatting with (or the one named). Nothing changes hands: Accept writes to the log what would have, then closes.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("barter_spike", "cr")]
        public static string BarterSpike(List<string> args)
        {
            Hero buyer = ChatManager.ActiveViewModel != null ? ((IOfferHost) ChatManager.ActiveViewModel).Npc : null;
            if (args != null && args.Count > 0)
            {
                string name = string.Join(" ", args);
                buyer = Hero.AllAliveHeroes.FirstOrDefault(h => h.Name != null && h.Name.ToString().IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0);
            }

            Hero captive = Verbs.BuyPrisonerVerb.PlayerHeldHeroPrisoners().FirstOrDefault();
            if (buyer == null || captive == null) return BarterScreenSpike.Open(buyer, captive, 0, 0);

            int anchor = Verbs.BuyPrisonerVerb.RansomAnchor(captive);
            int price = Logic.Prisoners.PrisonerTradePolicy.MostABuyerCanPay(anchor, buyer.Gold, Verbs.BuyPrisonerVerb.BondTo(buyer, captive));

            return BarterScreenSpike.Open(buyer, captive, price, anchor);
        }
    }
}
