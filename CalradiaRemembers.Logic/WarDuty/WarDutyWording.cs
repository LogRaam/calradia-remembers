// Code written by Gabriel Mailhot, 15/10/2026.
// The facts the vassal war duty hands the liege's letters (the summons, the levy demand). Gating before declaration
// (Gabriel, 09/09/2026): the letter may name only what the game will carry out, so the consequence is worded HERE from
// the ladder's real next step (the levy by letter; a fief taken, or only remembered when there is no fief to take),
// and the writer is told to threaten nothing else. Pure so what a letter promises is pinned by tests.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.WarDuty
{
   /// <summary>What a summons letter states.</summary>
   public sealed class SummonsFacts
   {
      /// <summary>The realms at war with the player's, by name.</summary>
      public IReadOnlyList<string> EnemyNames { get; init; }

      /// <summary>Days the realm has been at war.</summary>
      public int DaysAtWar { get; init; }

      /// <summary>Days the player has to come.</summary>
      public int DaysToCome { get; init; }

      /// <summary>The levy that will be demanded if he does not come.</summary>
      public int LevyAmount { get; init; }
   }

   /// <summary>What a levy letter (or the in-person demand) states.</summary>
   public sealed class LevyFacts
   {
      /// <summary>What is still owed, in coin.</summary>
      public int Outstanding { get; init; }

      /// <summary>Days left to serve or pay.</summary>
      public int DaysLeft { get; init; }

      /// <summary>The player's clan holds a fief the ruler could take.</summary>
      public bool HoldsATakeableFief { get; init; }
   }

   /// <summary>Pure: the words of the war duty's letters and terms.</summary>
   public static class WarDutyWording
   {
      /// <summary>The Context of the summons letter.</summary>
      public static string SummonsContext(SummonsFacts f)
      {
         if (f == null) return "";

         return $"Your realm has been at war with {Enemies(f.EnemyNames)} for {f.DaysAtWar} days, and the player, your sworn "
                + "vassal, has not fought in it: no battle against your enemies, no siege, no day in your armies. You summon "
                + $"them to come before you within {f.DaysToCome} days and answer for it. If they do not come, you will demand "
                + $"a levy of {f.LevyAmount} denars for their absence from the front.";
      }

      /// <summary>The Context of the levy letter, sent when the summoned vassal never came.</summary>
      public static string LevyLetterContext(LevyFacts f)
      {
         if (f == null) return "";

         return $"They did not come. You demand a levy of {f.Outstanding} denars for their absence from the front, to be "
                + $"paid within {f.DaysLeft} days; troops, horses or arms to that value will serve as well, and riding to "
                + $"war against your enemies before then settles it too. {Consequence(f.HoldsATakeableFief)}";
      }

      /// <summary>What follows when nothing is given by the deadline, as the ladder will carry it out.</summary>
      public static string Consequence(bool holdsATakeableFief)
         => holdsATakeableFief
            ? "If they give nothing by then, you will take one of their fiefs."
            : "If they give nothing by then, you will remember it.";

      private static string Enemies(IReadOnlyList<string> names)
      {
         List<string> clean = (names ?? new string[0]).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).ToList();
         if (clean.Count == 0) return "your enemies";
         if (clean.Count == 1) return clean[0];

         return string.Join(", ", clean.Take(clean.Count - 1)) + " and " + clean[clean.Count - 1];
      }
   }
}
