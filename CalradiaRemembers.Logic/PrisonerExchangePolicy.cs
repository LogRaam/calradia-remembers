// Code written by Gabriel Mailhot, 25/09/2026.
// Prisoner EXCHANGE (fkasad, Nexus, 25/09/2026: "I don't usually sell off captive lords. Keep your friends close, but
// your enemies closer. But an exchange..."). Gabriel's rulings the same day: only HEROES change hands (lords, vassals,
// companions; never troops), and the offer always comes through an envoy, never the lord in person.
//
// What is decided here, pure: who may be exchanged, what gold evens an uneven swap, and which of the captives the other
// side holds it would put on the table. The worth of a captive is the game's own ransom valuation, the SAME anchor the
// sale verbs clamp around (PrisonerTradePolicy), so a swap and a sale can never price one prisoner two ways.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>What an exchange needs to know about one captive.</summary>
   public sealed class ExchangeCaptiveFacts
   {
      /// <summary>The captive's hero id (empty for a troop, which has none).</summary>
      public string HeroId { get; set; } = "";

      /// <summary>A named hero, not a troop in a prison roster.</summary>
      public bool IsHero { get; set; }

      /// <summary>A lord or lady of a noble clan (a vassal is one).</summary>
      public bool IsLord { get; set; }

      /// <summary>A companion (a wanderer taken into a clan's service).</summary>
      public bool IsCompanion { get; set; }

      /// <summary>Alive. A dead captive is nobody's to trade.</summary>
      public bool IsAlive { get; set; } = true;

      /// <summary>A child. Never a bargaining piece.</summary>
      public bool IsChild { get; set; }

      /// <summary>The game's ransom valuation of this captive; zero or less when it could not be read.</summary>
      public int RansomAnchor { get; set; }
   }

   /// <summary>The rules of a prisoner exchange. See the file header.</summary>
   public static class PrisonerExchangePolicy
   {
      /// <summary>
      ///   TUNE: two captives whose worth differs by no more than this share of the dearer one are an even swap, and
      ///   no gold changes hands. Past it, the side that receives more pays the whole difference.
      /// </summary>
      public const double EvenWithinShare = 0.25;

      /// <summary>
      ///   Whether this captive may be exchanged at all: a living, adult hero who is a lord (vassals are lords) or a
      ///   companion. Troops never; a notable or a child never; the dead never.
      /// </summary>
      public static bool IsExchangeable(ExchangeCaptiveFacts captive)
         => captive != null && captive.IsHero && captive.IsAlive && !captive.IsChild && (captive.IsLord || captive.IsCompanion);

      /// <summary>
      ///   The gold that evens a swap, seen from the player: positive when the other side pays the player (the player
      ///   gives up the dearer captive), negative when the player pays, zero when the two are within
      ///   <see cref="EvenWithinShare" /> of each other. An unreadable worth falls back to
      ///   <see cref="PrisonerTradePolicy.FallbackAnchor" />, as the sale verbs do.
      /// </summary>
      public static int GoldToSettle(int playerGivesAnchor, int playerReceivesAnchor)
      {
         int gives = Worth(playerGivesAnchor);
         int receives = Worth(playerReceivesAnchor);
         int difference = gives - receives;

         return Math.Abs(difference) <= EvenWithinShare * Math.Max(gives, receives) ? 0 : difference;
      }

      /// <summary>
      ///   Which of the captives the other side holds it would offer against the one it wants back: the exchangeable
      ///   one whose worth is closest to <paramref name="wantedAnchor" />, the dearer on a tie, then by id so the
      ///   choice never flickers between two identical calls. Null when it holds nobody it could offer.
      /// </summary>
      public static ExchangeCaptiveFacts ChooseCounterpart(IEnumerable<ExchangeCaptiveFacts> theirHold, int wantedAnchor)
      {
         int wanted = Worth(wantedAnchor);

         return (theirHold ?? Enumerable.Empty<ExchangeCaptiveFacts>())
                .Where(IsExchangeable)
                .OrderBy(c => Math.Abs(Worth(c.RansomAnchor) - wanted))
                .ThenByDescending(c => Worth(c.RansomAnchor))
                .ThenBy(c => c.HeroId, StringComparer.Ordinal)
                .FirstOrDefault();
      }

      private static int Worth(int anchor) => anchor > 0 ? anchor : PrisonerTradePolicy.FallbackAnchor;
   }
}
