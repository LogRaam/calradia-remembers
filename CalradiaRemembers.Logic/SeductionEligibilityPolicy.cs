// Code written by Gabriel Mailhot, 26/08/2026.
// Increment 1 of the Stolen Love pillar: the first gate before any of it can begin. A married NPC must not
// be seducible merely because the player's regard cleared some number, or every marriage in Calradia turns
// into a lock waiting for a big enough key, saying nothing about who the NPC actually IS. Regard is a HARD
// precondition here (below RegardFloor, nothing else matters); past it, a genuinely honourable heart or a
// happy, long, child-bound marriage resists for the reasons real people don't stray, while an unhappy match
// or a calculating nature cracks the door instead. Increment 2 (estrangement pacing) is meant to read the
// Difficulty this returns to pace how long an affair takes to surface. Get the weights wrong here and either
// every marriage becomes a free lock to pick, or the feature never fires for anyone whose spouse isn't
// already loathed.

namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   Everything needed to judge whether a married NPC can be seduced by the player. Each field is a fact
   ///   the engine can answer directly (regard ledgers, vanilla traits, marriage state), never a conclusion.
   /// </summary>
   public sealed class SeductionFacts
   {
      /// <summary>The couple shares at least one living child together: a bond a fling would also wound.</summary>
      public bool HasChildrenTogether { get; set; }

      /// <summary>How long the marriage has stood, in in-game days. A mildly rising bar, not a hard wall.</summary>
      public int MarriageDurationDays { get; set; }

      /// <summary>Vanilla Calculating trait level, conventionally -2..2. High = open to an advantageous betrayal.</summary>
      public int NpcCalculating { get; set; }

      /// <summary>Vanilla Honor trait level, conventionally -2..2. High Honor is the DOMINANT resistor to straying.</summary>
      public int NpcHonor { get; set; }

      /// <summary>The NPC's own relation to their SPOUSE: marriage happiness. High/positive resists; low/negative cracks the door.</summary>
      public int NpcSpouseRelation { get; set; }

      /// <summary>The mod's OWN personal regard ledger toward the player, not the vanilla clan relation.</summary>
      public int RegardWithPlayer { get; set; }
   }

   /// <summary>
   ///   How narrowly a seduction cleared its bar. A signal for later increments (Inc 2's estrangement
   ///   pacing): a barely-won affair should surface and unravel slower than a wide-open one.
   /// </summary>
   public enum SeductionDifficulty
   {
      /// <summary>Openness cleared resistance by a wide margin: little genuinely stood in the way.</summary>
      Easy,

      /// <summary>A real but not desperate gap between openness and resistance.</summary>
      Hard,

      /// <summary>Either refused outright, or scraped past the bar by the thinnest of margins.</summary>
      VeryHard
   }

   /// <summary>The verdict on one NPC: whether they can be seduced, why not when refused, and how hard it was.</summary>
   public sealed class SeductionVerdict
   {
      /// <summary>How narrowly this verdict was reached; a signal for later increments to pace the affair.</summary>
      public SeductionDifficulty Difficulty { get; set; }

      /// <summary>A short phrase naming the DOMINANT blocker when not seducible; empty when seducible.</summary>
      public string Reason { get; set; }

      /// <summary>True when the NPC can be seduced.</summary>
      public bool Seducible { get; set; }
   }

   /// <summary>
   ///   Decides whether a married NPC is seducible by the player: the first gate of the Stolen Love pillar.
   ///   Every constant here is a tuning knob, named so the balance is a one-line, test-covered decision.
   /// </summary>
   public static class SeductionEligibilityPolicy
   {
      /// <summary>Tuning: openness gained per positive point of the Calculating trait; a schemer is drawn to an advantageous betrayal.</summary>
      public const int CalculatingOpennessPerLevel = 15;

      /// <summary>Tuning: flat resistance added when the couple shares children; a bond a fling would also wound.</summary>
      public const int ChildrenResistance = 20;

      /// <summary>Tuning: marriage-duration resistance stops accruing past this many days (10 years), so an ancient marriage isn't literally unseducible by duration alone.</summary>
      public const int DurationResistanceCapDays = 3650;

      /// <summary>Tuning: resistance added per full year of marriage (capped by <see cref="DurationResistanceCapDays" />); an old bond raises the bar mildly.</summary>
      public const int DurationResistancePerYear = 4;

      /// <summary>Tuning: the passing margin (beyond <see cref="SeducibleMargin" />) at/above which the case reports as Easy.</summary>
      public const int EasyPassMargin = 40;

      /// <summary>Tuning: resistance per point of a HAPPY (positive) spouse relation.</summary>
      public const int HappySpouseResistancePerPoint = 1;

      /// <summary>Tuning: the passing margin at/above which the case reports as Hard rather than VeryHard.</summary>
      public const int HardPassMargin = 15;

      /// <summary>Tuning: resistance per positive point of Honor, the DOMINANT resistor: an honourable heart doesn't stray for any price.</summary>
      public const int HonorResistancePerLevel = 30;

      /// <summary>Tuning: the regard floor below which an NPC isn't even approachable, whatever the state of their marriage. Matches the mod's existing infidelity/deep-trust gate.</summary>
      public const int RegardFloor = 30;

      /// <summary>Tuning: openness gained per point of regard ABOVE <see cref="RegardFloor" />.</summary>
      public const int RegardOpennessPerPoint = 1;

      /// <summary>Tuning: openness must clear resistance by at least this margin to actually be seducible.</summary>
      public const int SeducibleMargin = 15;

      /// <summary>Tuning: openness gained per point of an UNHAPPY (negative) spouse relation; a wretched marriage is the biggest crack in the door.</summary>
      public const int UnhappySpouseOpennessPerPoint = 1;

      /// <summary>
      ///   The full verdict: whether <paramref name="facts" /> describes a seducible NPC, why not when refused,
      ///   and how narrowly the case was decided. The regard floor is a HARD precondition nothing below it can
      ///   override; past it, resistance (honour, marital happiness, children, duration) is weighed against
      ///   openness (excess regard, Calculating, marital unhappiness), and the NPC is seducible only once
      ///   openness clears resistance by <see cref="SeducibleMargin" />.
      /// </summary>
      public static SeductionVerdict Evaluate(SeductionFacts facts)
      {
         if (facts == null)
         {
            return new SeductionVerdict {
               Seducible = false,
               Reason = "no facts to evaluate",
               Difficulty = SeductionDifficulty.VeryHard
            };
         }

         if (facts.RegardWithPlayer < RegardFloor)
         {
            return new SeductionVerdict {
               Seducible = false,
               Reason = "does not regard you nearly enough yet",
               Difficulty = SeductionDifficulty.VeryHard
            };
         }

         int honorResistance = PositivePart(facts.NpcHonor) * HonorResistancePerLevel;
         int devotionResistance = PositivePart(facts.NpcSpouseRelation) * HappySpouseResistancePerPoint
                                   + (facts.HasChildrenTogether ? ChildrenResistance : 0)
                                   + DurationResistance(facts.MarriageDurationDays);
         int totalResistance = honorResistance + devotionResistance;

         int totalOpenness = (facts.RegardWithPlayer - RegardFloor) * RegardOpennessPerPoint
                              + PositivePart(facts.NpcCalculating) * CalculatingOpennessPerLevel
                              + NegativeMagnitude(facts.NpcSpouseRelation) * UnhappySpouseOpennessPerPoint;

         int margin = totalOpenness - totalResistance;

         if (margin < SeducibleMargin)
         {
            // Past the regard floor, only two things can still stand in the way: the NPC's own principle
            // (honour) or their bond to the spouse (happiness, children, years). Whichever weighs more is the
            // truest thing to say about why this one refused. When both are near nil, the shortfall is really
            // about the NPC not being drawn in enough yet, not about the marriage or their honour at all.
            int opennessGap = totalOpenness < SeducibleMargin ? SeducibleMargin - totalOpenness : 0;

            string reason;
            if (honorResistance >= devotionResistance && honorResistance >= opennessGap)
               reason = "too honorable to stray";
            else if (devotionResistance >= opennessGap)
               reason = "too devoted to their spouse";
            else
               reason = "isn't drawn to you strongly enough for something this reckless";

            return new SeductionVerdict {
               Seducible = false,
               Reason = reason,
               Difficulty = SeductionDifficulty.VeryHard
            };
         }

         int passMargin = margin - SeducibleMargin;
         SeductionDifficulty difficulty = passMargin >= EasyPassMargin
            ? SeductionDifficulty.Easy
            : passMargin >= HardPassMargin
               ? SeductionDifficulty.Hard
               : SeductionDifficulty.VeryHard;

         return new SeductionVerdict {
            Seducible = true,
            Reason = string.Empty,
            Difficulty = difficulty
         };
      }

      /// <summary>Convenience over <see cref="Evaluate" />: true when this NPC can be seduced at all.</summary>
      public static bool IsSeducible(SeductionFacts facts) => Evaluate(facts).Seducible;

      #region private

      private static int DurationResistance(int marriageDurationDays)
      {
         int cappedDays = marriageDurationDays > DurationResistanceCapDays ? DurationResistanceCapDays : marriageDurationDays;
         if (cappedDays < 0) cappedDays = 0;

         return cappedDays / 365 * DurationResistancePerYear;
      }

      private static int NegativeMagnitude(int value) => value < 0 ? -value : 0;

      private static int PositivePart(int value) => value > 0 ? value : 0;

      #endregion
   }
}
