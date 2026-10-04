// Code written by Gabriel Mailhot, 04/10/2026.
// Where the mod's own files are. EmperorHaterius (Nexus, 04/10/2026): every file CR ships read as "not found" from
// "../../Modules\CalradiaRemembers\...", and the Dark Leather window, drawn from art on that path, came up see-through
// with no way out; the prefabs, found through the engine's module path, loaded fine. A path rebuilt from BasePath.Name
// holds only when the module sits in the game's Modules folder and the process started in its bin folder. The engine
// knows where it found the module, so ask it (ModuleRootPathTests keeps every other site from rebuilding the path).

#region

using System;
using System.IO;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;

#endregion

namespace CalradiaRemembers.Paths
{
   /// <summary>The module's own folder, as the engine found it.</summary>
   public static class ModulePaths
   {
      private const string ModuleId = "CalradiaRemembers";
      private static string _root;

      /// <summary>
      ///   The module folder: the engine's path for it when it has one, else the game's Modules folder (the only place
      ///   the old hand-built path ever worked). Read once, then cached. The submodule logs it at start ([PATHS]).
      /// </summary>
      public static string Root
      {
         get
         {
            if (_root != null) return _root;

            string engine = null;
            try { engine = ModuleHelper.GetModuleFullPath(ModuleId); }
            catch (Exception)
            {
               // the engine does not know the module (should not happen once it is loaded): fall back below
            }

            _root = !string.IsNullOrWhiteSpace(engine) && Directory.Exists(engine)
               ? Path.GetFullPath(engine)
               : Path.GetFullPath(Path.Combine(BasePath.Name, "Modules", ModuleId));

            return _root;
         }
      }
   }
}
