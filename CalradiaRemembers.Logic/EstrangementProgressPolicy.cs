// Code written by Gabriel Mailhot, 26/08/2026.
// Stolen Love pillar, Increment 2: how fast an affair unravels a married NPC's marriage. Pure so the pacing
// (how many days from the first crack to a spouse ready to leave, scaled by how hard the seduction was) stays a
// tested, one-line decision. The behavior reads Phase each day to decide what the NPC voices and, at
// ReadyToDivorce, to hand off to Increment 3 (the actual divorce + the jilted spouse's grudge). Get the days
// wrong and either a marriage of years collapses in a week (reads as absurd), or the arc never reaches its
// payoff and the whole pillar stalls at "they seem troubled" forever.

namespace CalradiaRemembers.Logic
{
   /// <summary>Where a fraying marriage stands on its way from the first crack to a spouse ready to walk out.</summary>
   public enum EstrangementPhase
   {
      /// <summary>Not begun, or still within the opening grace before the strain shows at all.</summary>
      None,

      /// <summary>The marriage is visibly under strain: the NPC is torn, but not yet resolved to end it.</summary>
      Fraying,

      /// <summary>The rift is complete: the NPC is ready to leave their spouse (Increment 3 executes the divorce).</summary>
      ReadyToDivorce
   }

   /// <summary>
   ///   Paces a Stolen Love estrangement from its start day to the point the spouse is ready to leave, scaled by
   ///   the seduction's <see cref="SeductionDifficulty" />. Every span is a named, test-covered tuning knob.
   /// </summary>
   public static class EstrangementProgressPolicy
   {
      /// <summary>Tuning: days after the affair begins before the marriage visibly frays at all (a brief calm before the strain shows).</summary>
      public const int FrayingGraceDays = 5;

      /// <summary>Tuning: days from start to ReadyToDivorce for an EASY seduction (a marriage already all but dead unravels fast, ~one season).</summary>
      public const int DaysToReadyEasy = 21;

      /// <summary>Tuning: days from start to ReadyToDivorce for a HARD seduction (~two seasons).</summary>
      public const int DaysToReadyHard = 42;

      /// <summary>Tuning: days from start to ReadyToDivorce for a VERYHARD seduction (a hard-won heart is slow to break its vows, ~three seasons).</summary>
      public const int DaysToReadyVeryHard = 63;

      /// <summary>Days from start to ReadyToDivorce for a given seduction difficulty.</summary>
      public static int DaysToReady(SeductionDifficulty difficulty)
      {
         switch (difficulty)
         {
            case SeductionDifficulty.Easy: return DaysToReadyEasy;
            case SeductionDifficulty.Hard: return DaysToReadyHard;
            default: return DaysToReadyVeryHard;
         }
      }

      /// <summary>
      ///   The phase given how many days have elapsed since the estrangement began and how hard the seduction was.
      ///   Negative elapsed (a bad clock) reads as None. Before <see cref="FrayingGraceDays" /> the marriage has
      ///   not visibly cracked yet; from there to <see cref="DaysToReady" /> it is Fraying; at or past it the NPC
      ///   is ready to leave.
      /// </summary>
      public static EstrangementPhase Phase(int elapsedDays, SeductionDifficulty difficulty)
      {
         if (elapsedDays < FrayingGraceDays) return EstrangementPhase.None;
         if (elapsedDays < DaysToReady(difficulty)) return EstrangementPhase.Fraying;
         return EstrangementPhase.ReadyToDivorce;
      }

      /// <summary>
      ///   A 0..100 sense of how far the rift has progressed toward ReadyToDivorce, for flavour in the fraying
      ///   note. Clamped to the [0, 100] range so a stale clock cannot report a nonsense figure.
      /// </summary>
      public static int ProgressPercent(int elapsedDays, SeductionDifficulty difficulty)
      {
         int total = DaysToReady(difficulty);
         if (total <= 0) return 100;
         if (elapsedDays <= 0) return 0;
         if (elapsedDays >= total) return 100;
         return elapsedDays * 100 / total;
      }
   }
}
