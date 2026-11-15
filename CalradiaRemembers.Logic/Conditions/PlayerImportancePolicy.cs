// Code written by Gabriel Mailhot, 14/11/2026.
// Whether the player carries enough political weight that "word of someone important may have reached even the wilds", so
// a bandit captor "might suspect a valuable, ransomable noble without knowing who" (EncounterContext.PlayerLooksImportant):
// a house of landed nobility, or real sway at court. Decided inside EncounterContextBuilder until the glue-decisions map (14/11/2026) listed
// its influence mark as a bare number in the glue; moved here unchanged.

namespace CalradiaRemembers.Logic.Conditions
{
   /// <summary>Whether the player looks important to a captor. Pure.</summary>
   public static class PlayerImportancePolicy
   {
      /// <summary>The clan influence that reads as real sway, whatever the clan's tier.</summary>
      public const float RealSwayInfluence = 150f;

      /// <summary>A house of landed nobility (Stature.LandedNobleTier) or a clan with real sway.</summary>
      public static bool LooksImportant(Stature playerStature, float clanInfluence)
         => playerStature.IsLandedNoble || clanInfluence >= RealSwayInfluence;
   }
}
