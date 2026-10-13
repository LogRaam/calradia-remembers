// Code written by Gabriel Mailhot, 13/10/2026.
// The "Who is this?" button of an audience invitation (Lordfadooboo, Nexus, 13/10/2026: "an ability to open their
// encyclopedia page from that screen would be helpful"). The game's popup is modal and sits above every screen, so the
// button closes it, opens the encyclopedia on that person, and raises the very same invitation again once the player
// closes the encyclopedia. The invitation must never be lost on the way: an audience starts its cooldown when it is
// offered, so a popup that does not come back is an audience gone for days.

namespace CalradiaRemembers.Logic.Interception
{
   /// <summary>Where the detour stands, read once a frame.</summary>
   public sealed class EncyclopediaDetourFacts
   {
      /// <summary>The encyclopedia has been seen open since the button was pressed.</summary>
      public bool SeenOpen { get; init; }

      /// <summary>The encyclopedia is open this frame.</summary>
      public bool IsOpenNow { get; init; }

      /// <summary>Seconds since the button was pressed.</summary>
      public float SecondsWaited { get; init; }
   }

   /// <summary>What to do this frame.</summary>
   public enum EncyclopediaDetourStep
   {
      /// <summary>Keep waiting: the encyclopedia is opening or open.</summary>
      Wait,

      /// <summary>Raise the invitation again.</summary>
      RaiseAgain
   }

   /// <summary>When the invitation comes back. Pure.</summary>
   public static class EncyclopediaDetour
   {
      /// <summary>How long an encyclopedia that never opens is waited for before the invitation comes back anyway.</summary>
      public const float SecondsToOpen = 2f;

      /// <summary>
      ///   Back once the encyclopedia has been seen open and is closed again; back as well when it never opened at all
      ///   within <see cref="SecondsToOpen" />, so a page that fails to open never swallows the invitation.
      /// </summary>
      public static EncyclopediaDetourStep Next(EncyclopediaDetourFacts f)
      {
         if (f == null) return EncyclopediaDetourStep.RaiseAgain;
         if (f.IsOpenNow) return EncyclopediaDetourStep.Wait;
         if (f.SeenOpen) return EncyclopediaDetourStep.RaiseAgain;

         return f.SecondsWaited >= SecondsToOpen ? EncyclopediaDetourStep.RaiseAgain : EncyclopediaDetourStep.Wait;
      }
   }
}
