// Code written by Gabriel Mailhot, 19/09/2026.
// Finds what other loaded modules have to contribute to one of the mod's narrative files.
//
// The engine half of the door tashmetu asked for (see NarrativePatchComposer for the report and the format).
// A module contributes by shipping ModuleData/cr_patch/<same filename>, and nothing else: no registration, no
// code, no reference to this assembly. A total conversion and a two-line bridge use the same path.
//
// Load order is the launcher's, which is the point: precedence is already a thing the player controls and
// understands, so inventing a second ordering here would only be a second thing to get wrong.

#region

using System;
using System.Collections.Generic;
using System.IO;
using CalradiaRemembers.Logic.Narrative;
using TaleWorlds.ModuleManager;

#endregion

namespace CalradiaRemembers.Storage
{
   /// <summary>Gathers other modules' contributions to a narrative file, in launcher load order.</summary>
   public static class NarrativePatchScanner
   {
      /// <summary>The folder a contributing module puts its patches in, inside its own ModuleData.</summary>
      public const string PatchFolder = "cr_patch";

      private const string OwnModuleId = "CalradiaRemembers";

      /// <summary>
      ///   Every loaded module's patch for <paramref name="filename" />, in load order, skipping this mod's own
      ///   module. Empty when nothing contributes, which is the case for almost every player.
      ///   <para>
      ///     Fail-soft to the letter. Enumerating modules is an engine call, and a game build that does not
      ///     expose it must cost a player their bridge mod's prose, never their session: the whole thing is
      ///     wrapped, and a failure is reported on the always-on channel because a silent one here looks
      ///     exactly like a modder having written the file in the wrong place.
      ///   </para>
      /// </summary>
      public static IReadOnlyList<NarrativePatch> PatchesFor(string filename)
      {
         var found = new List<NarrativePatch>();

         if (string.IsNullOrWhiteSpace(filename)) return found;

         try
         {
            foreach (string moduleId in LoadedModuleIds())
            {
               if (string.IsNullOrWhiteSpace(moduleId)) continue;
               if (string.Equals(moduleId, OwnModuleId, StringComparison.OrdinalIgnoreCase)) continue;

               string path = PatchPath(moduleId, filename);

               if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;

               string text = File.ReadAllText(path);

               if (string.IsNullOrWhiteSpace(text)) continue;

               found.Add(new NarrativePatch(moduleId, text));
               ModLog.Info($"  [{filename}] {moduleId} contributes {text.Length} chars via {PatchFolder}.");
            }
         }
         catch (Exception ex) { ModLog.Fault($"scanning modules for {filename} patches", ex); }

         return found;
      }

      #region private

      /// <summary>
      ///   The modules the player actually ENABLED, in the order the game loaded them. Empty rather than throwing.
      ///   <para>
      ///     Written first against ModuleHelper.GetModules(), and wrong twice over, found while checking a modder's
      ///     README against the code (tashmetu, 19/09/2026). That call walks every module FOUND on disk, enabled
      ///     or not, in the order of a dictionary. So a player who disabled a bridge mod in the launcher and left
      ///     the folder in place still had its prose read into every prompt, and with its assembly no longer
      ///     loaded, none of its {{variables}} were registered, so they reached the model as raw braces, every
      ///     turn. And the "launcher load order" the 2.5.13 notes promised was not what it read either.
      ///   </para>
      ///   The engine's own module list is the enabled set in load order. ModuleHelper.GetActiveModules() is the
      ///   fallback if that call is ever unavailable: still enabled-only, just without a guaranteed order.
      /// </summary>
      internal static IReadOnlyList<string> LoadedModuleIds()
      {
         try
         {
            string[] names = TaleWorlds.Engine.Utilities.GetModulesNames();
            var ids = new List<string>();

            if (names != null)
               foreach (string name in names)
                  if (!string.IsNullOrWhiteSpace(name))
                     ids.Add(name.Trim());

            if (ids.Count > 0) return ids;
         }
         catch (Exception ex)
         {
            ModLog.Fault("reading the enabled module list in load order", ex);
         }

         try
         {
            var ids = new List<string>();

            foreach (ModuleInfo module in ModuleHelper.GetActiveModules())
               if (module != null && !string.IsNullOrWhiteSpace(module.Id))
                  ids.Add(module.Id);

            return ids;
         }
         catch (Exception ex)
         {
            ModLog.Fault("reading the enabled module list", ex);

            return Array.Empty<string>();
         }
      }

      private static string PatchPath(string moduleId, string filename)
      {
         try
         {
            string root = ModuleHelper.GetModuleFullPath(moduleId);

            return string.IsNullOrEmpty(root)
               ? null
               : Path.Combine(root, "ModuleData", PatchFolder, filename);
         }
         catch { return null; }
      }

      #endregion
   }
}
