// Code written by Gabriel Mailhot, 24/06/2026.

namespace CalradiaRemembers.Logic
{
   /// <summary>Which participant, if either, becomes the mother of a conception in this scene.</summary>
   public enum ConceptionRole
   {
      /// <summary>No conception is possible — a same-sex pairing, or no fertile female with a living male partner.</summary>
      None,

      /// <summary>The player is the mother; the NPC is the father.</summary>
      PlayerIsMother,

      /// <summary>The NPC is the mother; the player is the father.</summary>
      NpcIsMother
   }

   /// <summary>
   ///   Decides, from the two participants' sex / life / age, which (if either) could conceive from a
   ///   completed vaginal act. Conception needs exactly one fertile-female mother and a living male father;
   ///   a same-sex pairing, or a female outside the fertile window, yields <see cref="ConceptionRole.None" />.
   ///   Pure and engine-agnostic — the mod maps real heroes onto these primitives and performs the act.
   /// </summary>
   public static class ConceptionResolver
   {
      /// <summary>Oldest age a female can be the mother of a conception.</summary>
      public const double MaxFertileAge = 45d;

      /// <summary>Youngest age a female can be the mother of a conception.</summary>
      public const double MinFertileAge = 18d;

      /// <summary>True when this participant is a female who could carry a child: alive and within the fertile window.</summary>
      public static bool IsFertileMother(bool isFemale, bool isAlive, double age)
         => isFemale && isAlive && age is >= MinFertileAge and <= MaxFertileAge;

      /// <summary>
      ///   Resolves the mother (if any) for a player + NPC pairing: the fertile female is the mother and the
      ///   other party must be a living male father; otherwise <see cref="ConceptionRole.None" />. Two females
      ///   (no father) or two males (no mother) both resolve to None.
      /// </summary>
      public static ConceptionRole Resolve(
         bool playerIsFemale,
         bool playerIsAlive,
         double playerAge,
         bool npcIsFemale,
         bool npcIsAlive,
         double npcAge)
      {
         if (IsFertileMother(playerIsFemale, playerIsAlive, playerAge) && IsViableFather(npcIsFemale, npcIsAlive))
            return ConceptionRole.PlayerIsMother;

         if (IsFertileMother(npcIsFemale, npcIsAlive, npcAge) && IsViableFather(playerIsFemale, playerIsAlive))
            return ConceptionRole.NpcIsMother;

         return ConceptionRole.None;
      }

      #region private

      /// <summary>A living male is the only valid father.</summary>
      private static bool IsViableFather(bool isFemale, bool isAlive)
         => !isFemale && isAlive;

      #endregion
   }
}