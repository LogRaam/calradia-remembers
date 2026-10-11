// Code written by Gabriel Mailhot, 11/10/2026.
// A tale of intimacy may end on a chance of a child (Gabriel, 10/10/2026: "Une chance de tomber enceinte"). The player
// and the partner the tale names: the woman of the two carries the child. Two women, or two men, conceive nothing, and
// the rest of the ending (memory, regard) holds all the same. The tale names its chance, or takes the game's own 15%
// (the same as Calradia Remembers' intimate scenes, PregnancyService).

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>The two who were together, for a tale's conception.</summary>
   public sealed class ConceptionFacts
   {
      public bool PlayerIsFemale { get; init; }

      public bool PartnerIsFemale { get; init; }
   }

   /// <summary>Which of the two would carry the child.</summary>
   public enum ConceptionMother
   {
      /// <summary>Two women or two men: no child.</summary>
      None,
      Partner,
      Player
   }

   /// <summary>Who would carry a child from a tale's ending, and on what chance. Pure.</summary>
   public static class TaleConceptionPolicy
   {
      /// <summary>The chance when the tale names none: the game's own, as for every intimate scene.</summary>
      public const double DefaultChance = 0.15;

      public static ConceptionMother Mother(ConceptionFacts f)
      {
         if (f == null || f.PlayerIsFemale == f.PartnerIsFemale) return ConceptionMother.None;

         return f.PartnerIsFemale ? ConceptionMother.Partner : ConceptionMother.Player;
      }

      /// <summary>A chance a tale may name: from 0 to 1.</summary>
      public static bool IsAChance(double chance) => chance >= 0 && chance <= 1;

      public static bool Conceives(double chance, double roll) => roll < chance;
   }
}
