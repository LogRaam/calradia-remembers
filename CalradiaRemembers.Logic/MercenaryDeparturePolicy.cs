// Code written by Gabriel Mailhot, 26/09/2026.
// A mercenary contract ends: how does the realm left behind take it? fkasad (Nexus, 25/09/2026), ratified by Gabriel:
// "a mercenary breaking their contract should entail a penalty, depending on the circumstances".
//
//   - The employer at peace: no fault. That is how a mercenary works.
//   - At war and winning: no fault, rather thanks. The company is not needed any more.
//   - At war and losing: the company abandoned them, saving its coffers. A penalty.
//   - Gone straight to the enemy: a traitor as well as a coward. A heavy penalty. It is a two-step event: the departure
//     is judged when it happens, and upgraded if the player joins the enemy soon after.
//   - Of the employer's own culture: the penalty doubles. "That's on you, a mercenary shouldn't leave family."
//   - And the reverse: coming to the aid of a realm of one's own people, in a war it is losing, is remembered well.
//
// Pure: the verdict, its weight on the lords' regard, and the words the world and the lords remember it in.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>How a realm takes the end of the player's mercenary contract.</summary>
   public enum MercenaryDepartureVerdict
   {
      /// <summary>The realm was at peace: the contract simply ended.</summary>
      AtPeace,

      /// <summary>The realm was at war and winning: the company is let go with thanks.</summary>
      ReleasedInVictory,

      /// <summary>The realm was at war and losing: the company abandoned it.</summary>
      Abandoned,

      /// <summary>The company left and took service with the realm's enemy.</summary>
      Betrayal
   }

   /// <summary>The realm's situation when the contract ended.</summary>
   public sealed class MercenaryDepartureFacts
   {
      /// <summary>The realm is at war with anyone.</summary>
      public bool EmployerAtWar { get; set; }

      /// <summary>The realm's own strength.</summary>
      public float EmployerStrength { get; set; }

      /// <summary>The combined strength of every realm it is at war with.</summary>
      public float EnemiesStrength { get; set; }
   }

   /// <summary>The rules of a mercenary's departure. Pure.</summary>
   public static class MercenaryDeparturePolicy
   {
      /// <summary>TUNE: below this share of its enemies' strength, a realm at war is losing.</summary>
      public const double LosingStrengthRatio = 0.9;

      /// <summary>TUNE: days after leaving within which joining the former employer's enemy is a betrayal.</summary>
      public const int BetrayalWindowDays = 30;

      // The four below are SHIFTS on the regard ledger, not thresholds: nothing is compared against them.

      /// <summary>TUNE: the regard the realm's lords gain for a company let go in victory.</summary>
      public const int ReleasedGratitude = 3;

      /// <summary>TUNE: the regard they lose for a company that abandoned a losing war.</summary>
      public const int AbandonedPenalty = -8;

      /// <summary>TUNE: the regard they lose for a company that went over to the enemy.</summary>
      public const int BetrayalPenalty = -20;

      /// <summary>TUNE: the regard a realm's lords gain for a company of their own people come to their aid in a losing war.</summary>
      public const int AidOfKinGratitude = 5;

      /// <summary>True when a realm at war is losing: its strength under <see cref="LosingStrengthRatio" /> of its enemies'.</summary>
      public static bool IsLosing(float ownStrength, float enemiesStrength)
         => enemiesStrength > 0f && ownStrength < enemiesStrength * LosingStrengthRatio;

      /// <summary>The verdict on a departure, as it happens (a betrayal is judged later, on the next contract).</summary>
      public static MercenaryDepartureVerdict Judge(MercenaryDepartureFacts facts)
      {
         if (facts == null || !facts.EmployerAtWar) return MercenaryDepartureVerdict.AtPeace;

         return IsLosing(facts.EmployerStrength, facts.EnemiesStrength)
            ? MercenaryDepartureVerdict.Abandoned
            : MercenaryDepartureVerdict.ReleasedInVictory;
      }

      /// <summary>
      ///   True when joining a new realm turns a recent departure into a betrayal: the new realm is at war with the
      ///   former employer, within <see cref="BetrayalWindowDays" /> of leaving it.
      /// </summary>
      public static bool IsBetrayal(int departedDay, int today, bool newRealmAtWarWithFormer)
         => newRealmAtWarWithFormer && today - departedDay >= 0 && today - departedDay <= BetrayalWindowDays;

      /// <summary>What the verdict costs (or earns) with the realm's lords; a penalty doubles for a company of their own people.</summary>
      public static int RegardShift(MercenaryDepartureVerdict verdict, bool sameCulture)
      {
         int shift = verdict switch
         {
            MercenaryDepartureVerdict.ReleasedInVictory => ReleasedGratitude,
            MercenaryDepartureVerdict.Abandoned => AbandonedPenalty,
            MercenaryDepartureVerdict.Betrayal => BetrayalPenalty,
            _ => 0
         };

         return shift < 0 && sameCulture ? shift * 2 : shift;
      }

      /// <summary>The additional regard lost when a departure already judged is upgraded to a betrayal.</summary>
      public static int BetrayalUpgradeShift(MercenaryDepartureVerdict judgedAtDeparture, bool sameCulture)
         => RegardShift(MercenaryDepartureVerdict.Betrayal, sameCulture) - RegardShift(judgedAtDeparture, sameCulture);

      /// <summary>True when a company of the realm's own people comes to its aid in a war it is losing.</summary>
      public static bool IsAidOfKin(bool sameCulture, bool realmLosing) => sameCulture && realmLosing;

      /// <summary>How far word of it carries.</summary>
      public static int Magnitude(MercenaryDepartureVerdict verdict) => verdict switch
      {
         MercenaryDepartureVerdict.ReleasedInVictory => 35,
         MercenaryDepartureVerdict.Abandoned => 50,
         MercenaryDepartureVerdict.Betrayal => 65,
         _ => 30
      };

      /// <summary>The news line, as characters hear it.</summary>
      public static string Summary(MercenaryDepartureVerdict verdict, string player, string realm, string enemy = null)
         => verdict switch
         {
            MercenaryDepartureVerdict.ReleasedInVictory => $"{player}'s company left {realm}'s service with the war well in hand, and with thanks.",
            MercenaryDepartureVerdict.Abandoned => $"{player} abandoned {realm} in the middle of a losing war, taking the company and the pay.",
            MercenaryDepartureVerdict.Betrayal => $"{player} left {realm}'s service in the middle of the war and sold the company to its enemy{(string.IsNullOrWhiteSpace(enemy) ? "" : ", " + enemy)}.",
            _ => $"{player}'s mercenary contract with {realm} came to an end."
         };

      /// <summary>The chronicler's line.</summary>
      public static string ChronicleLine(MercenaryDepartureVerdict verdict, string player, string realm, string enemy = null)
         => verdict switch
         {
            MercenaryDepartureVerdict.ReleasedInVictory => $"{player}'s hired service to {realm} ended with the war well in hand",
            MercenaryDepartureVerdict.Abandoned => $"{player} left {realm} to a losing war, and kept the pay",
            MercenaryDepartureVerdict.Betrayal => $"{player} turned from {realm} to its enemy{(string.IsNullOrWhiteSpace(enemy) ? "" : " " + enemy)} in the middle of the war",
            _ => $"{player}'s hired service to {realm} ran its course and ended"
         };

      /// <summary>What a lord of the realm remembers of it, in the first person; null when there is nothing to hold.</summary>
      public static string LordMemory(MercenaryDepartureVerdict verdict, string player, string enemy = null)
         => verdict switch
         {
            MercenaryDepartureVerdict.ReleasedInVictory => $"{player}'s company served us through the war and left with our thanks once it was well in hand.",
            MercenaryDepartureVerdict.Abandoned => $"{player} took the company and the pay and left us while the war was going against us.",
            MercenaryDepartureVerdict.Betrayal => $"{player} left our service in the middle of the war and went over to the enemy{(string.IsNullOrWhiteSpace(enemy) ? "" : ", " + enemy)}.",
            _ => null
         };

      /// <summary>
      ///   Whether one of the GAME's companies leaving a realm is news (Zivkyn, Nexus: "why does nobody resent the
      ///   Skolderbrotva?"). Only abandonment and betrayal: companies change contracts often, and a peaceful end or a
      ///   departure with the war won is not something the realm's lords talk about.
      /// </summary>
      public static bool CompanyDepartureIsNews(MercenaryDepartureVerdict verdict)
         => verdict == MercenaryDepartureVerdict.Abandoned || verdict == MercenaryDepartureVerdict.Betrayal;

      /// <summary>The news line about one of the game's companies, as characters hear it.</summary>
      public static string CompanySummary(MercenaryDepartureVerdict verdict, string company, string realm, string enemy = null)
      {
         string who = CompanyName(company);

         return verdict switch
         {
            MercenaryDepartureVerdict.Abandoned => $"{who} abandoned {realm} in the middle of a losing war, and kept the pay.",
            MercenaryDepartureVerdict.Betrayal => $"{who} left {realm}'s service in the middle of the war and went over to its enemy{(string.IsNullOrWhiteSpace(enemy) ? "" : ", " + enemy)}.",
            MercenaryDepartureVerdict.ReleasedInVictory => $"{who} left {realm}'s service with the war well in hand.",
            _ => $"{who}'s contract with {realm} came to an end."
         };
      }

      /// <summary>The chronicler's line about one of the game's companies.</summary>
      public static string CompanyChronicleLine(MercenaryDepartureVerdict verdict, string company, string realm, string enemy = null)
      {
         string who = CompanyName(company);

         return verdict == MercenaryDepartureVerdict.Betrayal
            ? $"{who} turned from {realm} to its enemy{(string.IsNullOrWhiteSpace(enemy) ? "" : " " + enemy)} in the middle of the war"
            : $"{who} left {realm} to a losing war, and kept the pay";
      }

      /// <summary>"Skolderbrotva" reads "The Skolderbrotva"; a name that already opens with its article keeps it.</summary>
      private static string CompanyName(string company)
      {
         string name = string.IsNullOrWhiteSpace(company) ? "a mercenary company" : company.Trim();

         return name.StartsWith("the ", System.StringComparison.OrdinalIgnoreCase) || name.StartsWith("a ", System.StringComparison.OrdinalIgnoreCase)
            ? char.ToUpperInvariant(name[0]) + name.Substring(1)
            : "The " + name;
      }

      /// <summary>What the lords of a realm remember of a company of their own people come to their aid.</summary>
      public static string AidOfKinMemory(string player)
         => $"{player}, one of our own people, brought the company to our aid while the war was going against us.";
   }
}
