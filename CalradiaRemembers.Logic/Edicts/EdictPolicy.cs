// Code written by Gabriel Mailhot, 22/10/2026.
// Who may decree an edict, when it stops holding, and how a town contests it (Gabriel, 21/10/2026: "ville et royaume.
// Les PNJ peuvent aussi. Un edit peut etre conteste."). Gating before declaration: the decree is checked whole here
// BEFORE anyone announces it, so a character never proclaims an edict the game then refuses. Decided overnight on
// Gabriel's leave; every number is a TUNE.

namespace CalradiaRemembers.Logic.Edicts
{
   /// <summary>What deciding a decree reads.</summary>
   public sealed class EdictDecreeFacts
   {
      /// <summary>One town or the whole realm.</summary>
      public EdictScope Scope { get; init; }

      /// <summary>
      ///   The one decreeing holds the right: for a town, he leads the house that holds it; for a realm, he rules it.
      ///   Without it the decree is refused, whoever asks.
      /// </summary>
      public bool HoldsTheRight { get; init; }

      /// <summary>The town is a town (a castle or a village takes no edict: the models read towns).</summary>
      public bool IsATown { get; init; }

      /// <summary>An edict of the same scope already holds there (the town's own, or the realm's).</summary>
      public bool OneAlreadyHolds { get; init; }

      /// <summary>Days since the last edict of this scope there ended; null when there was none.</summary>
      public int? DaysSinceLastEnded { get; init; }

      /// <summary>The influence of the one decreeing.</summary>
      public float Influence { get; init; }
   }

   /// <summary>What keeps an edict holding.</summary>
   public sealed class EdictStandingFacts
   {
      /// <summary>Whoever decreed it is still in the world.</summary>
      public bool EnactorPresent { get; init; }

      /// <summary>He still holds the right he decreed it by: the town's house, or the throne.</summary>
      public bool EnactorStillHoldsTheRight { get; init; }

      /// <summary>Today.</summary>
      public int Today { get; init; }

      /// <summary>The day its term runs out.</summary>
      public int EndDay { get; init; }
   }

   /// <summary>A town's mood under an edict, read each day.</summary>
   public sealed class EdictGrievanceFacts
   {
      /// <summary>The edict costs the people (a loyalty loss or heavier taxes).</summary>
      public bool BurdensThePeople { get; init; }

      /// <summary>The town's loyalty today.</summary>
      public float Loyalty { get; init; }

      /// <summary>Days of grievance so far.</summary>
      public int Grievance { get; init; }

      /// <summary>The town has already contested it openly.</summary>
      public bool AlreadyContested { get; init; }
   }

   /// <summary>What a day of grievance comes to.</summary>
   public enum EdictUnrest
   {
      /// <summary>Nothing said aloud.</summary>
      Quiet,

      /// <summary>The town contests it openly today: its notables raise it, the realm hears of it.</summary>
      Contested,

      /// <summary>The town has forced it off.</summary>
      Repealed
   }

   /// <summary>The day's reading of a town under an edict: its grievance after today, and what is said aloud.</summary>
   public sealed class EdictGrievanceStep
   {
      /// <summary>Days of grievance after today.</summary>
      public int Grievance { get; init; }

      /// <summary>What happens today.</summary>
      public EdictUnrest Unrest { get; init; }
   }

   /// <summary>The rules of a decree's life: may it be made, does it still hold, and is it contested.</summary>
   public static class EdictPolicy
   {
      /// <summary>TUNE: days a town (or realm) rests between one edict ending and the next decreed there.</summary>
      public const int RestDays = 10;

      /// <summary>TUNE: below this loyalty a burdened town counts a day of grievance; at or above, it eases by one.</summary>
      public const float RestlessBelowLoyalty = 40f;

      /// <summary>TUNE: days of grievance after which the town contests the edict openly, and after which it forces it off.</summary>
      public const int ContestedAfterDays = 10, RepealedAfterDays = 25;

      /// <summary>Why this decree cannot be made, in a sentence for the player; null when it can.</summary>
      public static string WhyNot(EdictDecreeFacts f)
      {
         if (f == null) return "There is nothing to decree.";
         if (f.Scope == EdictScope.Town && !f.IsATown) return "Only a town takes an edict.";
         if (!f.HoldsTheRight)
            return f.Scope == EdictScope.Realm ? "Only the ruler of the realm may decree for all of it." : "Only the head of the house that holds the town may decree for it.";
         if (f.OneAlreadyHolds) return f.Scope == EdictScope.Realm ? "An edict already holds across the realm." : "An edict already holds in that town.";
         if (f.DaysSinceLastEnded is int rest && rest < RestDays) return $"The last edict there ended only {rest} days ago; the people need {RestDays} days before another.";
         int cost = EdictCatalog.InfluenceCost(f.Scope);
         if (f.Influence < cost) return $"Decreeing it takes {cost} influence.";

         return null;
      }

      /// <summary>How an edict in force stands today: still Active, or how it ends.</summary>
      public static EdictStatus Standing(EdictStandingFacts f)
      {
         if (f == null) return EdictStatus.Lapsed;
         if (!f.EnactorPresent || !f.EnactorStillHoldsTheRight) return EdictStatus.Lapsed;

         return f.Today >= f.EndDay ? EdictStatus.Expired : EdictStatus.Active;
      }

      /// <summary>
      ///   One day of a town under an edict: a burdened, restless town counts a day of grievance, a content one lets it
      ///   ease; past the first mark it contests the edict openly (once), past the second it forces it off.
      /// </summary>
      public static EdictGrievanceStep Grieve(EdictGrievanceFacts f)
      {
         if (f == null) return new EdictGrievanceStep();
         int g = f.BurdensThePeople && f.Loyalty < RestlessBelowLoyalty ? f.Grievance + 1 : System.Math.Max(0, f.Grievance - 1);
         EdictUnrest unrest = g >= RepealedAfterDays ? EdictUnrest.Repealed
            : g >= ContestedAfterDays && !f.AlreadyContested ? EdictUnrest.Contested
            : EdictUnrest.Quiet;

         return new EdictGrievanceStep {Grievance = g, Unrest = unrest};
      }
   }
}
