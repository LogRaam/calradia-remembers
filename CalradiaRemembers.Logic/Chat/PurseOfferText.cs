// Code written by Gabriel Mailhot, 14/11/2026.
// Aporo (Discord, 14/11/2026): "i tried to give money, i used the option to give money from the bag, but later it asks me
// again to give money, even when i already gave it and got relation for it." The purse writes the offer into the
// conversation; the coin moves only when the character accepts and the player confirms [Y/n], so the confirmation he
// declined was the payment itself. Gabriel, same day: a short text in the purse's window saying it is words in the
// conversation, not a transaction, and a "learn more" opening an Info window on how a transaction with a character works
// in CR and how to tell a refusal from a bug. These are those texts.

#region

using System.Globalization;

#endregion

namespace CalradiaRemembers.Logic.Chat
{
    /// <summary>The words of the purse's windows: what giving coin from the bag does, and how a transaction is read.</summary>
    public static class PurseOfferText
    {
        /// <summary>The amount window's title.</summary>
        public const string AmountTitle = "Give Gold";

        /// <summary>The Info window's title.</summary>
        public const string LearnMoreTitle = "Giving to a character";

        /// <summary>
        ///   The amount window's text: the question, the coin carried, and that this writes an offer, no coin moving until the
        ///   character accepts and the player confirms.
        /// </summary>
        public static string AmountPrompt(int carried)
            => $"How many denars? (You carry {carried.ToString("N0", CultureInfo.InvariantCulture)}.)\n\n"
               + "This writes your offer into the conversation: no coin moves yet. If they accept, you are asked to confirm, "
               + "and the coin changes hands only then.";

        /// <summary>The Info window's text: how a transaction with a character works, and how to tell a refusal from a bug.</summary>
        public static string LearnMore()
            => "HOW IT WORKS\n"
               + "1. The purse (or your own words) makes the offer in the conversation. Nothing has changed hands yet, even if the "
               + "character's words already say they took it.\n"
               + "2. The character answers as themself: they may accept, refuse or ask for something else.\n"
               + "3. When they accept, a line asks you to confirm, such as \"Give Lady Arwa 1000 coins? [Y/n]\". Answering yes is the "
               + "payment. Answering no keeps your coin, and the line says it was not carried out.\n\n"
               + "Offering an item or a captive from the bag works the same way: the bag writes the offer, the character decides.\n\n"
               + "DID IT WORK?\n"
               + "- Done: a notice names what changed hands, and your purse or inventory shows it.\n"
               + "- Declined: you answered no at the confirmation. Offer again if you meant yes.\n"
               + "- Refused: a line in brackets names what the game would not allow, and why.\n"
               + "- Only words: the character spoke of it, but no confirmation came and nothing changed. Make the offer again, plainly.\n\n"
               + "LOOKS LIKE A BUG?\n"
               + "If a confirmation you accepted changed nothing, or one came for something already paid: turn on logging in the mod "
               + "options, then send the lines marked [ACTION] and [CONFIRM] from that conversation (Documents\\Mount and Blade II "
               + "Bannerlord\\Logs\\CalradiaRemembers.log) with a screenshot.";
    }
}
