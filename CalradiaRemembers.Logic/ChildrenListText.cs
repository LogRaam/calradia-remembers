// Code written by Gabriel Mailhot, 29/10/2026.
// The family line's children, said as a WHOLE list. fearente (Nexus, 29/10/2026): a brother told the player about his
// son "Lorgand", when he has a daughter. The line named the children a character has but never said there were no
// others, and when there were none it said nothing at all, a silence the model filled.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>The children part of the family line. Pure.</summary>
   public static class ChildrenListText
   {
      /// <summary>
      ///   "Children: Ira (female, age 12), Olek (male, age 9), and no other children", "Children: none"
      ///   for a grown character with none, or null for a child with none (a ten-year-old is not told so).
      /// </summary>
      /// <param name="described">Each child already described (name, sex, age or death), in the engine's order.</param>
      /// <param name="isGrown">The character is of an age to have children.</param>
      public static string Part(IReadOnlyList<string> described, bool isGrown)
      {
         List<string> children = described?.Where(d => !string.IsNullOrWhiteSpace(d)).ToList() ?? new List<string>();

         if (children.Count > 0) return $"Children: {string.Join(", ", children)}, and no other children";

         return isGrown ? "Children: none" : null;
      }
   }
}
