// Code written by Gabriel Mailhot, 03/10/2026.
// Authored quests, increment 1: whether a character who could give a quest raises it now. Gabriel's rulings
// (03/10/2026): one authored quest at a time; unique quests once per campaign; any character may offer one. Whether
// this person qualifies (a lord holding a village by a wood...) is the engine's reading of the file's "requires"; this
// decides only the rest.

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>The facts an offer is judged on, past the giver qualifying.</summary>
   public sealed class AuthoredOfferFacts
   {
      public int Today { get; init; }

      /// <summary>An authored quest is already being played.</summary>
      public bool StoryUnderWay { get; init; }

      public bool Unique { get; init; } = true;

      /// <summary>This quest was already played in this campaign (completed or failed).</summary>
      public bool AlreadyPlayed { get; init; }

      /// <summary>The day the player last declined this quest, or null.</summary>
      public int? DeclinedOnDay { get; init; }

      /// <summary>A roll in [0,1) and the chance it must fall under.</summary>
      public double Roll { get; init; }

      public double Chance { get; init; }
   }

   /// <summary>Decides whether a qualifying giver raises the quest now. Pure.</summary>
   public static class AuthoredQuestOfferPolicy
   {
      /// <summary>How long a declined quest waits before it may be raised again: a season.</summary>
      public const int DeclineWaitDays = NpcMemoryService.Core.Calendar.WorldCalendar.DefaultDaysPerSeason;

      /// <summary>The chance a qualifying giver raises it in a given conversation.</summary>
      public const double OfferChance = 0.35;

      public static bool ShouldOffer(AuthoredOfferFacts f)
      {
         if (f == null || f.StoryUnderWay) return false;
         if (f.Unique && f.AlreadyPlayed) return false;
         if (f.DeclinedOnDay is int d && f.Today - d < DeclineWaitDays) return false;

         return f.Roll < f.Chance;
      }
   }
}
