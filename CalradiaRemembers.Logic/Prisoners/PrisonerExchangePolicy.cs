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

namespace CalradiaRemembers.Logic.Prisoners
{
   /// <summary>What decides whether an envoy may come about a captive the player holds (PrisonerExchangePolicy.EnvoyMayCome).</summary>
   public sealed class EnvoyVisitFacts
   {
      /// <summary>The day THIS lord's envoy last came (or was sent) about the captive; null if never. Too recent, he waits.</summary>
      public int? LastFromThisLordDay { get; init; }

      /// <summary>The day ANY lord's envoy last came (or was sent) about the captive; null if never. Too recent, everyone waits.</summary>
      public int? LastAboutTheCaptiveDay { get; init; }

      /// <summary>
      ///   Envoys who reached the player about the captive in this captivity. He is still held, so each left without him:
      ///   enough of them and nobody comes again.
      /// </summary>
      public int EnvoysCameThisCaptivity { get; init; }

      /// <summary>
      ///   The day the last envoy about ANY prisoner the player holds came (or was sent); null if never. Too recent, every
      ///   envoy waits (Gabriel, 12/10/2026: several prisoners drew several streams of envoys).
      /// </summary>
      public int? LastAboutAnyCaptiveDay { get; init; }

      /// <summary>Today, in campaign days.</summary>
      public int Today { get; init; }
   }

   /// <summary>What an exchange needs to know about one captive.</summary>
   public sealed class ExchangeCaptiveFacts
   {
      /// <summary>The captive's hero id (empty for a troop, which has none).</summary>
      public string HeroId { get; set; } = "";

      /// <summary>A named hero, not a troop in a prison roster.</summary>
      public bool IsHero { get; set; }

      /// <summary>
      ///   The very captive the other side wants back. Never his own counterpart (tashmetu, 27/09/2026: an envoy offered
      ///   "another Rex Derthert" in exchange for Rex Derthert).
      /// </summary>
      public bool IsTheWantedCaptive { get; set; }

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

      /// <summary>One of the player's own people (his clan's companions or kin), held by the other side.</summary>
      public bool BelongsToPlayer { get; set; }
   }

   /// <summary>What happens to a captive once the other side has them.</summary>
   public enum ExchangeHandover
   {
      /// <summary>He belongs to the side receiving him: he is set free and goes home.</summary>
      Freed,

      /// <summary>He is not theirs: he passes into their custody, still a prisoner.</summary>
      IntoCustody
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
      ///   Whether the one spoken to is another side at all. For the player's own clan, "the captives his clan holds" are
      ///   the player's own, so an exchange would trade the player's prisoners for the player's prisoners.
      /// </summary>
      public static bool IsAnotherSide(bool npcIsOfPlayersClan) => !npcIsOfPlayersClan;

      /// <summary>
      ///   Which of the captives the other side holds it would offer against the one it wants back. One of the PLAYER's
      ///   own people first (getting his companion back is what an exchange is for, not a stranger's release), then
      ///   the exchangeable one whose worth is closest to <paramref name="wantedAnchor" />, the dearer on a tie, then
      ///   by id so the choice never flickers between two identical calls. Null when it holds nobody it could offer.
      /// </summary>
      public static ExchangeCaptiveFacts ChooseCounterpart(IEnumerable<ExchangeCaptiveFacts> theirHold, int wantedAnchor)
      {
         int wanted = Worth(wantedAnchor);

         return (theirHold ?? Enumerable.Empty<ExchangeCaptiveFacts>())
                .Where(IsExchangeable)
                .Where(c => !c.IsTheWantedCaptive)
                .OrderByDescending(c => c.BelongsToPlayer)
                .ThenBy(c => Math.Abs(Worth(c.RansomAnchor) - wanted))
                .ThenByDescending(c => Worth(c.RansomAnchor))
                .ThenBy(c => c.HeroId, StringComparer.Ordinal)
                .FirstOrDefault();
      }

      /// <summary>
      ///   An exchange is two sides getting their OWN people back, so a captive handed to the side he belongs to (his
      ///   clan or his realm) walks free. One handed to a side he does not belong to (an ally who wants him, say) stays
      ///   a prisoner, only in other hands.
      /// </summary>
      public static ExchangeHandover HandoverTo(bool captiveBelongsToReceiver)
         => captiveBelongsToReceiver ? ExchangeHandover.Freed : ExchangeHandover.IntoCustody;

      /// <summary>TUNE: days before the same lord sends another envoy about the same captive, whether the last was heard or not.</summary>
      public const int EnvoyCooldownDays = 20;

      /// <summary>
      ///   TUNE: days after any envoy about a captive before another lord's may come. fkasad (Nexus, 12/10/2026): eight
      ///   envoys about one prisoner in half an hour, each lord of the realm on his own wait. 5 in 2.7.9, 10 since Gabriel
      ///   asked for fewer envoys (12/10/2026).
      /// </summary>
      public const int CaptiveSpacingDays = 10;

      /// <summary>
      ///   TUNE: days after any envoy, whatever the prisoner, before the next may come. Gabriel (12/10/2026): "on peut
      ///   reduire la frequence des demandes de Lords pour racheter un prisonnier car il faut eviter le spamming."
      /// </summary>
      public const int AnyCaptiveSpacingDays = 3;

      /// <summary>
      ///   TUNE: envoys about one captive, in one captivity, after which nobody comes again while the player holds him.
      ///   fkasad (12/10/2026): "after a couple of discarded offers for the same guy, they should stop."
      /// </summary>
      public const int EnvoysBeforeGivingUp = 2;

      /// <summary>
      ///   Whether an envoy may come about this captive today: not after <see cref="EnvoysBeforeGivingUp" /> have come and
      ///   left without him, not within <see cref="AnyCaptiveSpacingDays" /> of any envoy at all, not within
      ///   <see cref="CaptiveSpacingDays" /> of any envoy about him, and not within
      ///   <see cref="EnvoyCooldownDays" /> of this lord's own. A refused audience counts: an envoy turned away does not
      ///   come back the next morning, nor does another lord's.
      /// </summary>
      public static bool EnvoyMayCome(EnvoyVisitFacts facts)
         => facts != null
            && facts.EnvoysCameThisCaptivity < EnvoysBeforeGivingUp
            && (facts.LastAboutAnyCaptiveDay is not int anyone || facts.Today - anyone >= AnyCaptiveSpacingDays)
            && (facts.LastAboutTheCaptiveDay is not int any || facts.Today - any >= CaptiveSpacingDays)
            && (facts.LastFromThisLordDay is not int own || facts.Today - own >= EnvoyCooldownDays);

      /// <summary>
      ///   TUNE: days a captive must have been held before anyone comes to treat for him: word of the capture reaches
      ///   his people, and their envoy rides to the player. aphareish (Nexus, 04/10/2026): envoys came seconds after the
      ///   battle.
      /// </summary>
      public const int EnvoyDelayAfterCaptureDays = 3;

      /// <summary>Whether a captive held this many days can already be treated for (<see cref="EnvoyDelayAfterCaptureDays" />).</summary>
      public static bool NewsHasReachedHisPeople(int daysHeld) => daysHeld >= EnvoyDelayAfterCaptureDays;

      private static int Worth(int anchor) => anchor > 0 ? anchor : PrisonerTradePolicy.FallbackAnchor;
   }
}
