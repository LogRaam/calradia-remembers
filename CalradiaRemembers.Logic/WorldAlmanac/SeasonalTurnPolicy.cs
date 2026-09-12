// Code written by Gabriel Mailhot, 12/09/2026.
// The seasonal historian, and it is Gabriel's design rather than the obvious one.
//
// The question that produced it: a lord knows how many men he has NOW; asked how many he had last year, he
// will invent. Building a Historian to make him right would be expensive and WRONG, and the wrong matters
// more. Nobody remembers a number. A real captain says "I lost half my men before Pravend", not "487". A
// historian that sampled a time series would give a character a memory BETTER than a human's, which breaks the
// illusion from the other side - and it would fight the mod's own architecture, since LlmMemoryCompressor
// already folds old events into a summary and precise figures are the first thing compression destroys.
//
// GABRIEL'S THREE RULINGS, and each one shaped a specific decision here:
//
//   1. COMPUTE IT; DO NOT HAVE THE LLM WRITE IT. "Résumé comme l'expliquerait un chroniqueur" invites a
//      background model call every season, forever, for entries only some of which are ever asked about - plus
//      a failure mode where a failed call is a hole in history, plus the reply-language problem we had just
//      finished fixing for memories. But "Vlandia grew rich" IS a delta description. So this file is pure
//      arithmetic and fixed phrasing: free, deterministic, testable, language-neutral. The chronicler stays
//      alive because it is a CHARACTER who tells it, not a background job.
//
//   2. ONE HISTORIAN FOR THE WORLD, not one per character. This produces world facts; who has heard them is
//      the rumour pipeline's business, and realm_news then decides how much of it each person carries.
//
//   3. IT MUST NOT BECOME A FOURTH MEMORY STORE. The output is a WorldEvent like any other, so the ageing,
//      the awareness and the confidence rating all already exist. Parallel ledgers have cost this project
//      before (the two regard ledgers that diverge).
//
// And the gift Gabriel spotted that makes the whole thing affordable: SEASONS make dating cheap. What made
// "date every fact" ruinous was the unit - 365 days. Four entries a year is nothing, and the prisoner question
// closes by itself, since a captive simply stops receiving new ones.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.WorldAlmanac
{
   /// <summary>
   ///   The few numbers worth keeping from one season to the next. Deliberately small: this is persisted, and
   ///   a snapshot that grew to hold everything would become the time series the design refuses to build.
   /// </summary>
   public sealed class KingdomSeasonSnapshot
   {
      /// <summary>The realm this describes.</summary>
      public string KingdomId { get; set; } = "";

      /// <summary>Its name at the time, for the line a chronicler would write.</summary>
      public string KingdomName { get; set; } = "";

      /// <summary>Fortified holdings - towns and castles. A village changing hands is not history.</summary>
      public int Holdings { get; set; }

      /// <summary>What the realm is worth, on the almanac's own reckoning.</summary>
      public long Wealth { get; set; }

      /// <summary>What it can put in the field.</summary>
      public double FieldStrength { get; set; }

      /// <summary>How its towns are doing.</summary>
      public double Prosperity { get; set; }
   }

   /// <summary>
   ///   Turns one season's change in a realm's standing into the sentence a chronicler would set down. Pure,
   ///   so the whole historian can be argued with without a campaign running.
   /// </summary>
   public static class SeasonalTurnPolicy
   {
      /// <summary>
      ///   How much a quantity must move before it is worth a line. High on purpose: a chronicle that records
      ///   every ripple is a chronicle nobody reads, and this is spoken aloud by characters who should sound
      ///   like they are remarking on something rather than reciting a ledger.
      /// </summary>
      public const double NotableShare = 0.25;

      /// <summary>Prosperity moves more slowly than coin, so it earns a lower bar.</summary>
      public const double NotableProsperityShare = 0.15;

      /// <summary>
      ///   What a chronicler would say of this realm's season, or null when nothing happened worth saying.
      /// </summary>
      /// <param name="before">Its standing at the last turn of the season.</param>
      /// <param name="now">Its standing now.</param>
      /// <param name="seasonName">The season just ended, as the calendar names it.</param>
      public static string Describe(KingdomSeasonSnapshot before, KingdomSeasonSnapshot now, string seasonName)
      {
         if (before == null || now == null) return null;

         List<string> clauses = Clauses(before, now);
         if (clauses.Count == 0) return null;

         string realm = string.IsNullOrWhiteSpace(now.KingdomName) ? "The realm" : now.KingdomName.Trim();
         string when = string.IsNullOrWhiteSpace(seasonName) ? "this past season" : $"this past {seasonName.Trim().ToLowerInvariant()}";

         return $"{realm} {Sentence(clauses)} {when}.";
      }

      /// <summary>Whether this season left anything worth recording for this realm.</summary>
      public static bool IsNotable(KingdomSeasonSnapshot before, KingdomSeasonSnapshot now)
         => Clauses(before, now).Count > 0;

      #region private

      /// <summary>
      ///   Territory first, because a town changing hands is the thing everyone actually talks about, then
      ///   coin, then swords, then how the common people are faring.
      /// </summary>
      private static List<string> Clauses(KingdomSeasonSnapshot before, KingdomSeasonSnapshot now)
      {
         var clauses = new List<string>();

         int holdings = now.Holdings - before.Holdings;
         if (holdings > 0) clauses.Add(holdings == 1 ? "took a stronghold" : $"took {holdings} strongholds");
         if (holdings < 0) clauses.Add(-holdings == 1 ? "lost a stronghold" : $"lost {-holdings} strongholds");

         AddIf(clauses, Moved(before.Wealth, now.Wealth, NotableShare), "grew richer", "saw its coffers thin");
         AddIf(clauses, Moved(before.FieldStrength, now.FieldStrength, NotableShare),
               "put more men under arms", "saw its armies thin");
         AddIf(clauses, Moved(before.Prosperity, now.Prosperity, NotableProsperityShare),
               "saw its towns prosper", "saw its towns suffer");

         return clauses;
      }

      private static void AddIf(List<string> clauses, int direction, string rose, string fell)
      {
         if (direction > 0) clauses.Add(rose);
         if (direction < 0) clauses.Add(fell);
      }

      /// <summary>
      ///   +1, -1 or 0, by SHARE rather than by amount: a thousand denars is a fortune to one realm and a
      ///   rounding error to another, and a chronicle that only ever remarked on the largest kingdom would be
      ///   a chronicle about one kingdom.
      /// </summary>
      private static int Moved(double before, double now, double share)
      {
         if (before <= 0.0) return now > 0.0 ? 0 : 0; // nothing to compare against: say nothing rather than guess

         double change = (now - before) / before;

         if (change >= share) return 1;
         if (change <= -share) return -1;

         return 0;
      }

      private static string Sentence(List<string> clauses)
      {
         if (clauses.Count == 1) return clauses[0];

         return string.Join(", ", clauses.GetRange(0, clauses.Count - 1)) + " and " + clauses[clauses.Count - 1];
      }

      #endregion
   }
}
