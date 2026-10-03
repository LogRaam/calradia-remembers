// Code written by Gabriel Mailhot, 03/10/2026.
// The persona catalog (HeroProfileMapper) matched a trait only at exactly -1 or +1, so a hero at -2 or +2, the game's
// strongest, met no archetype and got the generic "figure of contradictions" line (found answering tashmetu, fixed at
// Gabriel's word, 03/10/2026). The archetype is now looked up by each trait's direction, and a trait at its strongest
// is said in a sentence of its own: what they do, never that they enjoy it.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>A hero's five personality trait levels, -2 to +2 as the game keeps them.</summary>
   public sealed class PersonaTraitLevels
   {
      public int Honor { get; init; }
      public int Mercy { get; init; }
      public int Valor { get; init; }
      public int Generosity { get; init; }
      public int Calculating { get; init; }
   }

   /// <summary>How strongly a persona's traits are said. Pure.</summary>
   public static class PersonaTraitIntensity
   {
      /// <summary>The level an archetype is looked up by: the trait's direction, -1, 0 or +1.</summary>
      public static int Direction(int level) => Math.Sign(level);

      /// <summary>One sentence per trait at its strongest (+-2); empty when every trait is a mere leaning.</summary>
      public static string Clause(PersonaTraitLevels t)
      {
         if (t == null) return "";

         var said = new List<string>();
         Add(said, t.Honor, "Their word is absolute: they would sooner lose everything than break it.", "Their word is worth nothing, and everyone who deals with them learns it.");
         Add(said, t.Mercy, "Their compassion is boundless: they spare even those who would not spare them.", "Their want of mercy is extreme: they spare no one who crosses them, and pity has no hold on them.");
         Add(said, t.Valor, "Their courage borders on recklessness: they seek out the hardest fight.", "Their caution is extreme: they avoid every danger they can.");
         Add(said, t.Generosity, "Their generosity is lavish, to friend and stranger alike.", "Their greed is extreme: they part with nothing they can keep.");
         Add(said, t.Calculating, "They calculate everything: nothing they do is unplanned.", "They act wholly on impulse, never on a plan.");

         return string.Join(" ", said);
      }

      private static void Add(List<string> said, int level, string high, string low)
      {
         if (level >= 2) said.Add(high);
         else if (level <= -2) said.Add(low);
      }
   }
}
