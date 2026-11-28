// Code written by Gabriel Mailhot, 28/11/2026.
// Writes a conversation log (ChatLogRenderer) to the folder "CR Conversations", beside the player reports' "CR Reports".
// Gabriel (28/11/2026): "ajoute une option MCM qui permet de créer un log d'une conversation sous la forme que tu faisais
// déjà". Called off the main thread as a chat closes, so a slow disk never holds the game.

#region

using System;
using System.IO;
using CalradiaRemembers.Foundation;

#endregion

namespace CalradiaRemembers.Diagnostics
{
   /// <summary>Writes conversation logs to their folder.</summary>
   internal static class ConversationLogWriter
   {
      /// <summary>The folder conversation logs are written to, beside the log and the reports.</summary>
      internal static string Folder => Path.Combine(Path.GetDirectoryName(ModLog.FilePath) ?? "", "CR Conversations");

      /// <summary>Writes <paramref name="text" /> as <paramref name="fileName" /> in <see cref="Folder" />. Returns the path, or null on failure. Never throws.</summary>
      internal static string Write(string fileName, string text)
      {
         try
         {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, fileName);
            File.WriteAllText(path, text ?? "");
            ModLog.Info($"[CONVERSATION LOG] written: {fileName}");

            return path;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[CONVERSATION LOG] the file could not be written: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }
   }
}
