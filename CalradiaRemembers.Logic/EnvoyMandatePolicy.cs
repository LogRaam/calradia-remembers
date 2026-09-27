// Code written by Gabriel Mailhot, 25/09/2026.
// Prisoner exchange, increment 3 (fkasad, Nexus, 25/09/2026; Gabriel: always an anonymous envoy, never the lord). An
// envoy carries a mandate: one captive to bring home, a ransom ceiling, and at most one captive his lord will trade. The
// prompt tells him so; this is what holds him to it whatever the model writes. Whatever deal comes back is rebound to
// the mandate before the bridge sees it: the captive is always the one the lord wants, the price never passes the
// ceiling, an exchange exists only when the lord holds someone to give, and nothing else an envoy writes is ever run.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>What the lord authorised his envoy to settle.</summary>
   public sealed class EnvoyMandate
   {
      /// <summary>The captive the player holds and the lord wants back.</summary>
      public string WantedCaptiveName { get; set; } = "";

      /// <summary>The most the lord will pay in ransom.</summary>
      public int RansomCeiling { get; set; }

      /// <summary>The captive the lord would give in exchange, or empty when he holds nobody to offer.</summary>
      public string ExchangeCaptiveName { get; set; } = "";
   }

   /// <summary>What the turn that produced a deal said: the facts a gift and a plain acceptance are read from.</summary>
   public sealed class EnvoyTurnFacts
   {
      /// <summary>The player's own line this turn. A gift is theirs to make, in their words, never the model's.</summary>
      public string PlayerLine { get; set; }

      /// <summary>The last sum the envoy named in the parley, or null: what a plain acceptance agrees to.</summary>
      public int? LastOfferNamed { get; set; }
   }

   /// <summary>A deal rebound to the mandate, ready for the bridge.</summary>
   public sealed class EnvoyDeal
   {
      /// <summary>buy_prisoner or exchange_prisoners.</summary>
      public string Type { get; set; } = "";

      /// <summary>The parameters the verb reads.</summary>
      public Dictionary<string, string> Parameters { get; set; } = new();
   }

   /// <summary>Holds an envoy to his mandate, and names him. Pure.</summary>
   public static class EnvoyMandatePolicy
   {
      /// <summary>How the envoy is shown: never a name of his own (Gabriel: anonymous), only whose man he is.</summary>
      public static string DisplayName(string lordName)
         => string.IsNullOrWhiteSpace(lordName) ? "An envoy" : $"Envoy of {lordName.Trim()}";

      /// <summary>
      ///   The lord's regard for the player as the envoy's prompt says it ("regards the player {words}"), from the
      ///   canonical band phrase the encyclopedia shows. "neutral" alone reads badly after "regards", so it is said.
      /// </summary>
      public static string RegardWords(string bandPhrase)
         => string.IsNullOrWhiteSpace(bandPhrase) || string.Equals(bandPhrase, "neutral", StringComparison.OrdinalIgnoreCase)
            ? "without warmth or rancour"
            : $"with {bandPhrase}";

      /// <summary>
      ///   What the envoy opens with (fkasad, 26/09/2026: the one who asks makes the first offer): half his ceiling,
      ///   never below the honest floor a sale may go for, never above the ceiling itself.
      /// </summary>
      public static int OpeningOffer(int ransomCeiling, int honestFloor)
         => Math.Min(ransomCeiling, Math.Max(honestFloor, ransomCeiling / 2));

      private static readonly string[] GiftWords = {"free", "nothing", "gift", "none"};

      /// <summary>A written price that means the captive is given away: zero, or a word for nothing.</summary>
      private static bool IsGift(string written)
      {
         string w = written?.Trim().ToLowerInvariant();
         if (string.IsNullOrEmpty(w)) return false;
         if (int.TryParse(w, out int n)) return n == 0;

         foreach (string word in GiftWords)
            if (w.Contains(word)) return true;

         return false;
      }

      private static readonly string[] PlayerGiftWords =
         {"for nothing", "for free", "free", "no ransom", "without ransom", "no payment", "without payment", "gift",
          "keep your gold", "keep your coin", "keep your money", "zero"};

      /// <summary>
      ///   Whether the PLAYER's own words give the captive away: a word for nothing, or a sum of 0, and no other sum
      ///   named ("900 and he goes free" is a price). tashmetu (27/09/2026): "i will return him", said to a final offer,
      ///   was settled as a gift because the model wrote 0.
      /// </summary>
      public static bool PlayerGivesAway(string playerLine)
      {
         string line = playerLine?.ToLowerInvariant();
         if (string.IsNullOrWhiteSpace(line)) return false;
         if (SpokenFigure.LastIn(line, 10) != null) return false; // "no one" is not a price

         if (System.Text.RegularExpressions.Regex.IsMatch(line, @"(^|[^\d])0([^\d]|$)")) return true;

         foreach (string word in PlayerGiftWords)
            if (System.Text.RegularExpressions.Regex.IsMatch(line, @"\b" + System.Text.RegularExpressions.Regex.Escape(word) + @"\b"))
               return true;

         return false;
      }

      /// <summary>
      ///   The deal the bridge may run, or null when what the envoy wrote is outside his mandate (another action,
      ///   an exchange his lord cannot make). The captive is always the wanted one; a ransom is capped at the ceiling.
      ///   A gift needs the player's own words (<see cref="PlayerGivesAway" />); without a usable price, a ransom closes
      ///   at the envoy's last offer, else at the ceiling rather than dropped (the envoy said it was agreed, and the sale
      ///   verb still clamps it to the honest band).
      /// </summary>
      public static EnvoyDeal Bind(string actionType, IReadOnlyDictionary<string, string> parameters, EnvoyMandate mandate,
                                   EnvoyTurnFacts turn = null)
      {
         if (mandate == null || string.IsNullOrWhiteSpace(mandate.WantedCaptiveName) || string.IsNullOrWhiteSpace(actionType)) return null;

         string type = actionType.Trim().ToLowerInvariant();

         if (type == "buy_prisoner")
         {
            string written = null;
            parameters?.TryGetValue("price", out written);

            // Given away (fkasad, 26/09/2026: "yours for 0 denars" paid the full ceiling): a gift, and no gold moves. The
            // PLAYER must have said so (tashmetu, 27/09/2026): the model writing 0 is not the player giving him away.
            if (IsGift(written) && PlayerGivesAway(turn?.PlayerLine))
               return new EnvoyDeal {
                  Type = "buy_prisoner",
                  Parameters = new Dictionary<string, string> {["target"] = mandate.WantedCaptiveName, ["price"] = "0", ["gift"] = "true"}
               };

            int price = int.TryParse(written?.Trim(), out int p) && p > 0
               ? Math.Min(p, mandate.RansomCeiling)
               : turn?.LastOfferNamed is int last && last > 0
                  ? Math.Min(last, mandate.RansomCeiling)
                  : mandate.RansomCeiling;

            return new EnvoyDeal {
               Type = "buy_prisoner",
               Parameters = new Dictionary<string, string> {["target"] = mandate.WantedCaptiveName, ["price"] = price.ToString()}
            };
         }

         if (type == "exchange_prisoners" && !string.IsNullOrWhiteSpace(mandate.ExchangeCaptiveName))
            return new EnvoyDeal {
               Type = "exchange_prisoners",
               Parameters = new Dictionary<string, string> {["give"] = mandate.WantedCaptiveName, ["receive"] = mandate.ExchangeCaptiveName}
            };

         return null;
      }
   }
}
