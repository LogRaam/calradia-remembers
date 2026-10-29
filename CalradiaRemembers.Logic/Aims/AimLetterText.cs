// Code written by Gabriel Mailhot, 28/10/2026.
// A lord writes to the player about an aim another mod registered for him through CrAims (tashmetu, 14/10/2026: "nobody
// takes the first step with the player"; Gabriel, 28/10/2026: "fait 2 et 1", the audience then the letter). The letter is
// written by the model from this context alone, so it carries the want and the player's part as the mod gave them, and
// how this lord puts such a thing (AimApproachWording: openly, guardedly, never a falsehood).

namespace CalradiaRemembers.Logic.Aims
{
   /// <summary>The context a lord's aim letter is written from.</summary>
   public static class AimLetterText
   {
      /// <summary>The context for the letter, or null when the aim has no want to write about.</summary>
      public static string Context(StandingAim aim, string approach)
      {
         if (aim == null || string.IsNullOrWhiteSpace(aim.Want)) return null;

         string text = $"What you want, in your own terms: {aim.Want.Trim()}";
         if (!string.IsNullOrWhiteSpace(aim.PlayerPart)) text += $" What the player could do in it: {aim.PlayerPart.Trim()}";
         if (!string.IsNullOrWhiteSpace(approach)) text += $" How you put it: {approach.Trim()}";

         return text;
      }
   }
}
