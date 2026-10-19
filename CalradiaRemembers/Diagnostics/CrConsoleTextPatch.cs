// Code written by Gabriel Mailhot, 03/10/2026.
// Shinelegacy (Nexus, 03/10/2026, on 2.6.10): accents typed into cr.memory_add / cr.memory_set came out as mojibake
// ("é" -> "Ã©"). The game's console reads what was typed as UTF-8 bytes back into Windows-1252 before it calls a
// command, so every cr.* command that takes text received it mangled (29 places join arguments into text). These
// prefixes repair the arguments once, at the engine's own dispatch, for CR's commands only: another mod's commands
// are left exactly as the engine hands them.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Diagnostics;
using HarmonyLib;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Diagnostics
{
   /// <summary>Repairs the text of a cr.* console command as the console typed it.</summary>
   [HarmonyPatch(typeof(CommandLineFunctionality), nameof(CommandLineFunctionality.CallFunction),
      new[] {typeof(string), typeof(string), typeof(bool)}, new[] {ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out})]
   public static class CrConsoleTextPatch
   {
      /// <summary>The arguments as one string, split by the engine after this.</summary>
      public static void Prefix(string concatName, ref string concatArguments)
      {
         try
         {
            if (IsOurs(concatName)) concatArguments = ConsoleTextRepair.Repair(concatArguments);
         }
         catch (Exception ex) { ModLog.Error($"[CONSOLE] argument repair failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>CR's commands live in the "cr" group.</summary>
      internal static bool IsOurs(string concatName) => concatName != null && concatName.StartsWith("cr.", StringComparison.OrdinalIgnoreCase);
   }

   /// <summary>The same repair for the overload that receives the arguments already split.</summary>
   [HarmonyPatch(typeof(CommandLineFunctionality), nameof(CommandLineFunctionality.CallFunction),
      new[] {typeof(string), typeof(List<string>), typeof(bool)}, new[] {ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out})]
   public static class CrConsoleTextListPatch
   {
      /// <summary>Each argument repaired in place.</summary>
      public static void Prefix(string concatName, List<string> argList)
      {
         try
         {
            if (!CrConsoleTextPatch.IsOurs(concatName) || argList == null) return;
            for (var i = 0; i < argList.Count; i++) argList[i] = ConsoleTextRepair.Repair(argList[i]);
         }
         catch (Exception ex) { ModLog.Error($"[CONSOLE] argument repair failed: {ex.GetType().Name}: {ex.Message}"); }
      }
   }
}
