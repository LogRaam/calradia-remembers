// Code written by Gabriel Mailhot, 08/08/2026.
// World-conditions feeder, condition #1 (war looming). Decides, purely, whether a pair of kingdoms NOT yet at
// war is tense enough that war seems near, so the feeder can record a WarLooming WorldEvent that spreads through
// the same rumour/chronicle pipeline as any other happening. The mod owns the weekly tick, the engine reads
// (the diplomacy war-score, the at-war stance, the peace date), and the store dedup query; this class only
// reasons over plain facts so the tuning stays a one-line, test-covered decision rather than a hunt through
// engine code. No war is ever declared here: this only OBSERVES rising tension and lets the world talk of it.

#region

#endregion

using NpcMemoryService.Core.Calendar;
namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   Pure threshold and cadence gate for the "war looming" world condition. Every constant here is a tuning
   ///   knob the mod owns (never an engine-internal cutoff): a war-score threshold above which tension counts as
   ///   an imminent war, a minimum peace age so freshly-signed peace does not instantly read as looming war, and
   ///   a re-record cooldown so the same pair does not flood the rumour network.
   /// </summary>
   public static class WarLoomingPolicy
   {
      /// <summary>
      ///   Tuning: the diplomacy war-score (from <c>DiplomacyModel.GetScoreOfDeclaringWar</c>) at or above which
      ///   one kingdom favouring war against another is treated as an imminent war. A positive, rising score means
      ///   war is favoured; this cutoff is deliberately well above zero so only a genuinely tense pair (not a pair
      ///   merely a hair from indifferent) surfaces as looming. Owned here, never an engine-internal threshold.
      /// </summary>
      public const double WarScoreThreshold = 30.0;

      /// <summary>
      ///   Tuning: the minimum in-game days a pair must have been at peace before their tension may read as looming
      ///   war. Guards the common case of two kingdoms that JUST signed peace still carrying a high war-score in the
      ///   model's eyes: without this, a fresh peace would immediately be announced as war-about-to-return. Roughly
      ///   a quarter of a Calradian year (84-day year).
      /// </summary>
      public const int MinPeaceDays = CalradianCalendar.DaysPerSeason;

      /// <summary>
      ///   Tuning: in-game days that must pass before the SAME kingdom pair may have another looming-war event
      ///   recorded. Keeps a long-standing cold rivalry from re-announcing itself every weekly pass. Mirrors the
      ///   gossip feeder's own rivalry cooldown (<c>WorldEventFeeders.RivalryCooldownDays</c>).
      /// </summary>
      public const int RerecordCooldownDays = 40;

      /// <summary>
      ///   True when this pair's standing tension warrants recording a fresh "war looming" event. False when they
      ///   are already at war (there is a real WarDeclared event for that), when their peace is too fresh
      ///   (<paramref name="peaceDays" /> below <see cref="MinPeaceDays" />), when the war-score is below
      ///   <see cref="WarScoreThreshold" />, or when the same pair got a looming event too recently
      ///   (<paramref name="daysSinceLastLoomingForThisPair" /> null meaning "never recorded before").
      /// </summary>
      public static bool ShouldRecord(double warScore, bool alreadyAtWar, int peaceDays, int? daysSinceLastLoomingForThisPair)
      {
         if (alreadyAtWar) return false;
         if (peaceDays < MinPeaceDays) return false;
         if (warScore < WarScoreThreshold) return false;
         if (daysSinceLastLoomingForThisPair != null && daysSinceLastLoomingForThisPair.Value < RerecordCooldownDays) return false;

         return true;
      }
   }
}
