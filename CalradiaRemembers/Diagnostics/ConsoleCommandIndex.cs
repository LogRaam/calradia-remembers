// Code written by Gabriel Mailhot, 10/09/2026.
// The source of truth for what cr.help may claim: the commands the assembly ACTUALLY carries, read off the
// [CommandLineArgumentFunction] attributes themselves rather than from a list somebody maintained by hand.
//
// The hand-maintained list had drifted to 70 described out of 259 registered, and one of the 70 named a command
// that no longer existed. Reading the registry cannot drift, because it is the same thing the console dispatches
// on. See ConsoleHelpPolicy for the whole story.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Library;
using CalradiaRemembers.Foundation;

#endregion

namespace CalradiaRemembers.Diagnostics
{
   /// <summary>Every console command this assembly registers, by group, straight from the attributes.</summary>
   public static class ConsoleCommandIndex
   {
      /// <summary>The group every player-facing command of this mod lives under.</summary>
      public const string ModGroup = "cr";

      /// <summary>
      ///   The bare names ("testall", not "cr.testall") of every command registered under
      ///   <paramref name="group" />, sorted. Never throws: a reflection failure yields an empty list, because a
      ///   help command that crashes is worse than one that is briefly unhelpful.
      /// </summary>
      public static List<string> Registered(string group = ModGroup)
      {
         var names = new List<string>();

         try
         {
            foreach (Type type in typeof(ConsoleCommandIndex).Assembly.GetTypes())
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                                                        | BindingFlags.Static | BindingFlags.Instance
                                                        | BindingFlags.DeclaredOnly))
            {
               var attribute = method
                               .GetCustomAttributes(typeof(CommandLineFunctionality.CommandLineArgumentFunction), false)
                               .FirstOrDefault() as CommandLineFunctionality.CommandLineArgumentFunction;

               if (attribute == null) continue;
               if (!string.Equals(attribute.GroupName, group, StringComparison.OrdinalIgnoreCase)) continue;
               if (string.IsNullOrWhiteSpace(attribute.Name)) continue;

               names.Add(attribute.Name.Trim());
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"ConsoleCommandIndex.Registered failed: {ex.GetType().Name}: {ex.Message}");
         }

         return names.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.Ordinal).ToList();
      }
   }
}
