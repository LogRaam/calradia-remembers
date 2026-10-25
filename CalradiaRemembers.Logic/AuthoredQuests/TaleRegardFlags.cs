// Code written by Gabriel Mailhot, 24/10/2026.
// Gabriel (24/10/2026), on the night Rhagaea sends for her prisoner: "Ca s'est bien passe si Rhagaea a gagne plus de Regards
// qu'elle en a perdu pendant l'histoire", and the memory stays either way, "plus neutre.. ou meme negatif si ca se passe
// mal". The game judges it, never the model: the giver's regard when the tale began against her regard as it ends. The
// ending reads it through two flags it may test like any other (an ending's memory or regard under "regard_rose"); the
// path itself never sets them.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>The flags that say how the giver's regard moved over the tale. Pure.</summary>
   public static class TaleRegardFlags
   {
      /// <summary>Set when the giver holds the player higher at the end than when the tale began.</summary>
      public const string Rose = "regard_rose";

      /// <summary>Set when the giver holds the player lower at the end than when the tale began.</summary>
      public const string Fell = "regard_fell";

      /// <summary>
      ///   Sets the one flag that says how the regard moved, from <see cref="AuthoredQuestRun.GiverRegardAtStart" /> to
      ///   <paramref name="regardNow" />; neither when it did not move. A run that never noted where it began is left as it is.
      /// </summary>
      public static void Mark(AuthoredQuestRun run, int regardNow)
      {
         if (run?.GiverRegardAtStart == null) return;

         run.Flags ??= new List<string>();
         run.Flags.Remove(Rose);
         run.Flags.Remove(Fell);
         if (regardNow > run.GiverRegardAtStart.Value) run.Flags.Add(Rose);
         else if (regardNow < run.GiverRegardAtStart.Value) run.Flags.Add(Fell);
      }
   }
}
