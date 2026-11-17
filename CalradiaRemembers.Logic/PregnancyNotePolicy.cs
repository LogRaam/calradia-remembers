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
      ///   What the mod knows about one mother's pregnancy, read off the campaign by the caller.
      ///   <para>
      ///     Named rather than positional (2026-09-10). Two of the three booleans decide whether the FATHER is
      ///     named at all, which is the bastard-secret pillar's whole hinge, and a swap between them or between
      ///     the two day counts compiled in silence: passing the days remaining where the total belongs turns a
      ///     woman near her term into one barely showing, and no test of the arithmetic would notice.
      ///   </para>
      /// </summary>
      public sealed class PregnancyFacts
      {
         /// <summary>She is carrying a child. False renders no line at all.</summary>
         public bool IsPregnant { get; set; }

         /// <summary>The player is the father, which is worth saying in his own presence.</summary>
         public bool FatherIsPlayer { get; set; }

         /// <summary>The father's name, used only when he may be named.</summary>
         public string FatherName { get; set; }

         /// <summary>
         ///   The father may be named. He is her legal spouse. An affair or a faceless bandit is NOT namable,
         ///   and she then stays evasive about who the father is while still owning the pregnancy itself.
         /// </summary>
         public bool FatherNamable { get; set; }

         /// <summary>Days still to run before the birth.</summary>
         public int DaysRemaining { get; set; }

         /// <summary>
         ///   The pregnancy model's full term in days. At or below zero the game could not resolve it, and the
         ///   line is omitted rather than guessed from a meaningless fraction.
         /// </summary>
         public int TotalDurationDays { get; set; }

         /// <summary>
         ///   Children she bore before this pregnancy. bdionysis80 (Nexus, 17/11/2026): pregnant a second time, his wife
         ///   "kept going back to the first pregnancy"; above zero the line says this one is new and the earlier is over.
         /// </summary>
         public int EarlierChildren { get; set; }
      }

      /// <summary>
      ///   Describes a pregnant mother's state for the NPC identity directive. Returns "" when
      ///   <paramref name="isPregnant" /> is false or <paramref name="totalDurationDays" /> is unknown
      ///   (&lt;= 0), so the caller omits the line entirely rather than render a nonsensical stage.
      ///   The father clause is appended ONLY when <paramref name="fatherNamable" /> is true: a father
      ///   who is not the mother's legal spouse (an affair, or a faceless bandit) stays unnamed, so the
      ///   NPC remains evasive about the father while still acknowledging the pregnancy itself.
      /// </summary>
      public static string Describe(PregnancyFacts facts)
      {
         if (facts == null) return "";

         bool isPregnant = facts.IsPregnant;
         bool fatherIsPlayer = facts.FatherIsPlayer;
         string fatherName = facts.FatherName;
         bool fatherNamable = facts.FatherNamable;
         int daysRemaining = facts.DaysRemaining;
         int totalDurationDays = facts.TotalDurationDays;

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

         string newOne = facts.EarlierChildren <= 0
            ? ""
            : facts.EarlierChildren == 1
               ? " This is a new pregnancy: the child you bore before is already born, and that pregnancy is long over."
               : " This is a new pregnancy: the children you bore before are already born, and those pregnancies are long over.";

         return $"You are {stage}{fatherClause}.{newOne} You are aware of this and never deny being with child, " +
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
