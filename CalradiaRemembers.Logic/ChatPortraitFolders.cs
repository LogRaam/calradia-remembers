// Code written by Gabriel Mailhot, 10/11/2026.
// Where a chat portrait is looked for. Gabriel (10/11/2026), writing the Discord tutorial on portraits: ChatPortraits was the
// one folder of the player's own files that the Custom folder did not cover, so a clean reinstall (deleting the module
// folder) took the player's pictures with it, and the MCM "Custom Folder Path" set outside the module did not keep them.

#region

using System;
using System.Collections.Generic;
using System.IO;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>The folders a player's chat portraits are read from, in the order a file is looked for. Pure.</summary>
   public static class ChatPortraitFolders
   {
      /// <summary>The folder's name, under the Custom folder and under ModuleData alike.</summary>
      public const string FolderName = "ChatPortraits";

      /// <summary>
      ///   The player's Custom folder's ChatPortraits first, then the module's own ModuleData/ChatPortraits, where portraits
      ///   were always dropped and still are read. A folder named twice (or a blank one) is listed once, or not at all.
      /// </summary>
      public static IReadOnlyList<string> SearchOrder(string playerCustomFolder, string moduleDataFolder)
      {
         var folders = new List<string>();

         void Add(string baseFolder)
         {
            if (string.IsNullOrWhiteSpace(baseFolder)) return;
            string folder = Path.Combine(baseFolder, FolderName);
            foreach (string known in folders)
               if (string.Equals(known.TrimEnd('\\', '/'), folder.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)) return;
            folders.Add(folder);
         }

         Add(playerCustomFolder);
         Add(moduleDataFolder);

         return folders;
      }
   }
}
