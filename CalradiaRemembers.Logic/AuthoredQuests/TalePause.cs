// Code written by Gabriel Mailhot, 25/10/2026.
// Gabriel (25/10/2026): "est-ce que ce serait possible d'avoir plus qu'une histoire en cours? Par exemple, je fais la
// quete du loup, mais je suis fait prisonnier par Rhagaea.. je devrais pouvoir vivre la nuit avec elle et reprendre
// ensuite la quete du loup lorsque je suis libere de prison." Then "oui ca me va" to: a tale a scenario opens may
// interrupt the tale under way, one level only; the interrupted tale waits with its clock stopped, and takes up again
// once the interruption is over and the player is free.

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>A tale set aside while a scenario's tale plays, and taken up again after. Pure.</summary>
   public static class TalePause
   {
      /// <summary>
      ///   Whether a tale counts as under way for one that would begin now. A summons by a scenario may set the tale under
      ///   way aside, but never a second one (one level only); anything else waits for both to be over.
      /// </summary>
      public static bool StoryUnderWay(bool runActive, bool onePaused, bool bySummons)
         => bySummons ? runActive && onePaused : runActive || onePaused;

      /// <summary>Sets the run aside on this day: its clock stops.</summary>
      public static void Pause(AuthoredQuestRun run, int today)
      {
         if (run != null) run.PausedOnDay = today;
      }

      /// <summary>The set-aside tale takes up again once nothing else plays and the player is no longer held.</summary>
      public static bool ShouldResume(bool onePaused, bool runActive, bool playerPrisoner) => onePaused && !runActive && !playerPrisoner;

      /// <summary>
      ///   Takes the run up again: the days it waited are not counted against it, so its time limit, its waits and its
      ///   band's flight start where they stood. A defeat already told in its stage may be told once more after (its key
      ///   carries the stage's first day); a small price for a clock that never ran out in a cell.
      /// </summary>
      public static void Resume(AuthoredQuestRun run, int today)
      {
         if (run?.PausedOnDay is not int from) return;
         int waited = System.Math.Max(0, today - from);
         run.StartDay += waited;
         run.StageStartDay += waited;
         run.PausedOnDay = null;
      }
   }
}
