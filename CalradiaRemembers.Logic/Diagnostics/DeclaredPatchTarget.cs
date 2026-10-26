// Code written by Gabriel Mailhot, 25/10/2026.
// tashmetu (Nexus, 25/10/2026), from a 1.5.5 beta log with Harvest and Production: "[EDICT] could not attach to
// CalculateTownTax (non-fatal): ArgumentException: You can only patch implemented methods/constructors [...] So edicts
// never move taxes." The edict patches and the courier speed floor take the method as seen from the ACTIVE model's own
// type; a mod's model that inherits the method without overriding it hands back the inherited method seen from the
// subclass, and Harmony refuses to patch that. "It isn't really a 1.5.5 thing: it happens with any model that inherits
// the method without overriding it." Gabriel (25/10/2026): build it for 2.7.21. This finds the method where it is
// DECLARED, the one the active model actually runs.

#region

using System;
using System.Linq;
using System.Reflection;

#endregion

namespace CalradiaRemembers.Logic.Diagnostics
{
   /// <summary>The method a model runs under a name, taken from the type that declares it, so Harmony can patch it.</summary>
   public static class DeclaredPatchTarget
   {
      private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic
                                            | BindingFlags.Instance | BindingFlags.Static;

      /// <summary>
      ///   The method named <paramref name="name" /> that an object of <paramref name="type" /> runs: the type's own
      ///   declaration when it has one (an override), else the nearest base type's. Null when no type in the line
      ///   declares it, or when one declares several overloads of that name (the caller cannot tell which it meant).
      /// </summary>
      public static MethodInfo Find(Type type, string name)
      {
         for (Type t = type; t != null; t = t.BaseType)
         {
            MethodInfo[] declared = t.GetMethods(Declared).Where(m => m.Name == name).ToArray();
            if (declared.Length == 1) return declared[0];
            if (declared.Length > 1) return null;
         }

         return null;
      }
   }
}
