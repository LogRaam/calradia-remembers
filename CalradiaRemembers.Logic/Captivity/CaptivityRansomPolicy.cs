// Code written by Gabriel Mailhot, 01/11/2026.
// Windmolen (Nexus, 26/10/2026): "The NPC's never seem to be capable of just doing it! Give in, or just execute you or let you
// free or ransom you." At Hardcore the game's ransom offer was cancelled for every captor, under a log line that read "bandits
// do not bargain you free": a lord who would have named a price never did. This says whose offer stands.

namespace CalradiaRemembers.Logic.Captivity
{
   /// <summary>Who holds the player when the game is about to offer a ransom.</summary>
   public sealed class RansomOfferFacts
   {
      /// <summary>The content level is Hardcore, where the captive scenes need the time a captivity gives them.</summary>
      public bool IsHardcore { get; init; }

      /// <summary>The player is held for trial or serving a sentence: the law does not sell its prisoners.</summary>
      public bool HeldByTheLaw { get; init; }

      /// <summary>The captors are bandits or pirates.</summary>
      public bool CaptorIsBandit { get; init; }

      /// <summary>The captor is a nemesis, whose captivity is a hunt that has its own way out.</summary>
      public bool CaptorIsNemesis { get; init; }

      /// <summary>The player is in an ambush convoy, on the way to the lord who paid for it.</summary>
      public bool InAmbushConvoy { get; init; }
   }

   /// <summary>Whether the game's own ransom offer reaches the player.</summary>
   public static class CaptivityRansomPolicy
   {
      /// <summary>True when the offer is cancelled: the law always, and at Hardcore those who do not bargain. A lord bargains.</summary>
      public static bool Suppresses(RansomOfferFacts f)
      {
         if (f == null) return false;
         if (f.HeldByTheLaw) return true;

         return f.IsHardcore && (f.CaptorIsBandit || f.CaptorIsNemesis || f.InAmbushConvoy);
      }
   }
}
