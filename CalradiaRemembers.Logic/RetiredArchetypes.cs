// Code written by Gabriel Mailhot, 02/10/2026.
// tashmetu (Nexus, 02/10/2026): a companion's sister came out "very cruel, beyond what her traits suggest". Some
// persona archetypes described ordinary -1 trait combinations as an appetite for suffering ("Sadistic", "takes pleasure
// in it", "Predator", "Butcher"). They were reworded; a profile stores its archetype, so the names retired here let a
// save made before the rewording re-derive its persona on the next load.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>Persona archetypes no longer handed out, recognised in a stored profile. Pure.</summary>
   public static class RetiredArchetypes
   {
      /// <summary>The opening words of each retired archetype, as stored in a profile's Trait.</summary>
      public static readonly IReadOnlyList<string> Names = new[] {
         "Sadistic.", "The Savage.", "The Disciplined Predator.", "The Reckless Butcher.", "The Calculated Savage."
      };

      /// <summary>True when a stored persona opens with a retired archetype and should be derived again.</summary>
      public static bool IsRetired(string trait)
         => !string.IsNullOrEmpty(trait) && Names.Any(trait.StartsWith);
   }
}
