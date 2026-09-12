// Code written by Gabriel Mailhot, 12/09/2026.
// How hurt somebody is, from the numbers the engine keeps.
//
// It exists because the first version of this lived inline in the host and two of its four bands were
// UNREACHABLE, which nothing noticed until a real character was asked a real question.
//
// The seeded run put a lord at 60% of his health and asked him how he fared. He answered "I fare well enough."
// The reason: the rule deferred entirely to Hero.IsWounded - "not wounded by the engine's own reckoning is
// WHOLE, whatever the arithmetic says" - and the engine only calls a hero wounded near the BOTTOM of the bar.
// So above that threshold everybody was Unhurt however battered, and below it the full-bar ratio was always
// tiny, so everybody was Grievous. Scratched never happened. Wounded almost never did.
//
// The deference was not wrong, it was applied to the wrong question. The engine's IsWounded answers "can this
// person still fight", which is exactly the right authority for the bottom of the scale and no authority at
// all on whether a man carries bruises worth mentioning.
//
// Pure, and in Logic, because that is what would have caught it: an inline private method in a file that needs
// a running campaign cannot be asked whether all four of its answers are possible.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>How badly hurt somebody is, on the SDK's four bands, from the engine's own numbers.</summary>
   public static class BodyHurtPolicy
   {
      /// <summary>
      ///   Above this share of their health somebody is simply well. Not 1.0: a scratch nobody would mention
      ///   is not worth a line in every conversation in the game, and this pack is carried by everyone.
      /// </summary>
      public const float WholeAbove = 0.90f;

      /// <summary>
      ///   Within the engine's own wounded range, below this share of the wound limit they are barely up.
      ///   Measured against the LIMIT rather than against full health, which is the fix: the wounded range is
      ///   the bottom slice of the bar, so grading it against the whole bar puts everybody at the bottom of it.
      /// </summary>
      public const float GrievousBelow = 0.40f;

      /// <summary>0 = unknown, 1 = scratched, 2 = wounded, 3 = grievous. Mapped to the SDK's enum by the host.</summary>
      public enum Hurt
      {
         Unknown,
         Unhurt,
         Scratched,
         Wounded,
         Grievous
      }

      /// <summary>
      ///   How hurt they are.
      /// </summary>
      /// <param name="hitPoints">Their health now.</param>
      /// <param name="maxHitPoints">Their health when whole.</param>
      /// <param name="engineCallsThemWounded">
      ///   <c>Hero.IsWounded</c>. The engine's authority on whether somebody is DOWN, which is the bottom of
      ///   this scale and nothing above it.
      /// </param>
      /// <param name="woundedLimit">
      ///   <c>Hero.WoundedHealthLimit</c>: the health at which the engine starts calling them wounded. Used to
      ///   grade WITHIN that range instead of against the whole bar.
      /// </param>
      public static Hurt Read(int hitPoints, int maxHitPoints, bool engineCallsThemWounded, int woundedLimit)
      {
         if (maxHitPoints <= 0) return Hurt.Unknown;

         if (engineCallsThemWounded)
         {
            int limit = Math.Max(1, woundedLimit);

            return hitPoints / (float) limit < GrievousBelow ? Hurt.Grievous : Hurt.Wounded;
         }

         // Still on their feet by the engine's reckoning, and still visibly knocked about. This is the band
         // that used to be unreachable, and it is the commonest one a player actually meets.
         return hitPoints / (float) maxHitPoints < WholeAbove ? Hurt.Scratched : Hurt.Unhurt;
      }
   }
}
