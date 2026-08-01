// Code written by Gabriel Mailhot, 01/08/2026.
// Player report: a pregnant NPC woman has no idea she is pregnant in chat and denies it when asked,
// because her pregnancy state is never injected into the LLM prompt. This mirrors LifeStageDescriber's
// pattern (a raw fact turned into a short second-person phrase for the identity directive) but the
// stakes differ: the whole point here is the ACKNOWLEDGEMENT directive, not just flavour, since the
// entire bug is the NPC's denial. The father clause stays gated on namability so an affair or a
// faceless bandit conception never leaks the hidden father, preserving the bastard-secret pillar.

namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   Turns a mother's pregnancy facts into the second-person line the NPC identity prompt renders,
   ///   so she is AWARE of her own pregnancy and never denies it (see file header for the bug this fixes).
   /// </summary>
   public static class PregnancyNotePolicy
   {
      /// <summary>Below this elapsed fraction, the pregnancy reads as early / barely showing.</summary>
      private const double EarlyStageThreshold = 1d / 3d;

      /// <summary>At or above this elapsed fraction, the pregnancy reads as heavily along / near term.</summary>
      private const double LateStageThreshold = 2d / 3d;

      /// <summary>
      ///   Describes a pregnant mother's state for the NPC identity directive. Returns "" when
      ///   <paramref name="isPregnant" /> is false or <paramref name="totalDurationDays" /> is unknown
      ///   (&lt;= 0), so the caller omits the line entirely rather than render a nonsensical stage.
      ///   The father clause is appended ONLY when <paramref name="fatherNamable" /> is true: a father
      ///   who is not the mother's legal spouse (an affair, or a faceless bandit) stays unnamed, so the
      ///   NPC remains evasive about the father while still acknowledging the pregnancy itself.
      /// </summary>
      public static string Describe(
         bool isPregnant,
         bool fatherIsPlayer,
         string fatherName,
         bool fatherNamable,
         int daysRemaining,
         int totalDurationDays)
      {
         if (!isPregnant || totalDurationDays <= 0) return "";

         int elapsed = totalDurationDays - daysRemaining;
         double fraction = Clamp((double) elapsed / totalDurationDays, 0d, 1d);

         string stage = fraction < EarlyStageThreshold
            ? "in the early weeks of a pregnancy"
            : fraction < LateStageThreshold
               ? "visibly pregnant"
               : "heavily pregnant and near your term";

         string fatherClause = "";
         if (fatherNamable)
         {
            if (fatherIsPlayer)
               fatherClause = ", carrying the player's child";
            else if (!string.IsNullOrWhiteSpace(fatherName))
               fatherClause = $", carrying {fatherName}'s child";
         }

         return $"You are {stage}{fatherClause}. You are aware of this and never deny being with child, " +
                "though you may be private about the details.";
      }

      #region private

      private static double Clamp(double value, double min, double max)
         => value < min
            ? min
            : value > max
               ? max
               : value;

      #endregion
   }
}
