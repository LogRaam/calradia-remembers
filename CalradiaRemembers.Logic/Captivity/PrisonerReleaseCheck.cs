// Code written by Gabriel Mailhot, 24/11/2026.
// Whether a release the player granted in chat may be announced. whiskeypatt (Discord, 24/11/2026): he granted a
// prisoner freedom in return for something, answered Y, read that the person was free, and after the conversation they
// were still his prisoner. The bridge announced the release as soon as it had ASKED the game for it, never once
// checking that the game had done it. The announcement now follows what the game says: still held, no release.

namespace CalradiaRemembers.Logic.Captivity
{
   /// <summary>Turns the game's answer to a release into the refusal to show, or nothing when the release took.</summary>
   public static class PrisonerReleaseCheck
   {
      /// <summary>
      ///   The refusal line when the character is <paramref name="stillHeld" /> after the release was asked for, or
      ///   null when they are free and the release may be announced and its terms carried out.
      /// </summary>
      public static string RefusalWhenStillHeld(bool stillHeld, string name)
      {
         if (!stillHeld) return null;

         string who = string.IsNullOrWhiteSpace(name) ? "The prisoner" : name.Trim();

         return $"({who} could not be set free from here, and is still held. Nothing of the bargain was carried out.)";
      }
   }
}
