// Code written by Gabriel Mailhot, 08/08/2026.
// World-conditions feeder, condition #2 (hardship). Decides, purely, whether a town is in real economic distress
// (a food shortage and/or collapsing prosperity) worth surfacing as a Hardship WorldEvent, so the common folk's
// grumbling spreads through the same rumour/chronicle pipeline as any other happening. The mod owns the weekly
// tick, the engine reads (the town's food stocks/change and prosperity, its owning kingdom) and the store dedup
// query; this class only reasons over plain facts so the tuning stays a one-line, test-covered decision.

#region

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   Pure threshold and cadence gate for the "hardship" world condition. Every constant here is a tuning knob
   ///   the mod owns: the prosperity floor below which a town counts as in decline, and a re-record cooldown so a
   ///   town that stays poor for a season does not re-announce its misery every weekly pass.
   /// </summary>
   public static class HardshipPolicy
   {
      /// <summary>
      ///   Tuning: the prosperity level below which a town counts as in economic decline. Matches vanilla's own
      ///   "mid prosperity" band boundary (<c>Town.MidProsperityThreshold</c> = 2000), so a town CR calls hard-hit
      ///   is one the base game also treats as below a healthy middle. Passed to <see cref="ShouldRecord" /> as a
      ///   parameter so the gate stays testable at any floor; this const is the value the feeder passes in.
      /// </summary>
      public const int ProsperityFloor = 2000;

      /// <summary>
      ///   Tuning: in-game days that must pass before the SAME town may have another hardship event recorded. A
      ///   town does not climb out of distress overnight, so re-announcing it weekly would be noise; roughly a
      ///   third of a Calradian year (84-day year).
      /// </summary>
      public const int RerecordCooldownDays = 30;

      /// <summary>
      ///   True when this town's distress warrants recording a fresh "hardship" event: it has a food shortage OR
      ///   its prosperity has fallen below <paramref name="prosperityFloor" />, AND the same town has not had a
      ///   hardship event too recently (<paramref name="daysSinceLastForThisSettlement" /> null meaning "never
      ///   recorded before"). Either distress signal alone qualifies: a town can be starving while still nominally
      ///   prosperous, or bleeding prosperity without an outright famine.
      /// </summary>
      public static bool ShouldRecord(bool foodShortage, int prosperity, int prosperityFloor, int? daysSinceLastForThisSettlement)
      {
         bool inDistress = foodShortage || prosperity < prosperityFloor;
         if (!inDistress) return false;
         if (daysSinceLastForThisSettlement != null && daysSinceLastForThisSettlement.Value < RerecordCooldownDays) return false;

         return true;
      }
   }
}
