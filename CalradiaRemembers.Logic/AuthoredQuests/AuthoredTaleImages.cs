// Code written by Gabriel Mailhot, 08/10/2026.
// Images in written tales (Gabriel, 08/10/2026): a stage names an image file of its tale's own folder, shown beside the
// chat when that stage's scene opens. The name is checked here (a bare .png or .jpg file name, nothing that climbs out of
// the folder); whether the file is there is asked of the caller, so this stays off the disk.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>A tale's images: which one shows, and what is wrong with them. Pure.</summary>
   public static class AuthoredTaleImages
   {
      private static readonly string[] Extensions = {".png", ".jpg", ".jpeg"};

      /// <summary>A bare file name in a format the game loads: no folder, no climb out of it.</summary>
      public static bool IsAcceptedName(string image)
      {
         if (string.IsNullOrWhiteSpace(image)) return false;
         string name = image.Trim();
         if (name.Contains("/") || name.Contains("\\") || name.Contains("..")) return false;

         return Extensions.Any(e => name.EndsWith(e, StringComparison.OrdinalIgnoreCase));
      }

      /// <summary>The image of the stage under way, or null when it has none.</summary>
      public static string ImageFor(AuthoredQuestRun run, AuthoredQuest q)
      {
         string image = AuthoredQuestEngine.Current(run, q)?.Image;

         return IsAcceptedName(image) ? image.Trim() : null;
      }

      /// <summary>What is wrong with the tale's images; <paramref name="existsInTheTalesFolder" /> answers for a file name.</summary>
      public static IReadOnlyList<string> Problems(AuthoredQuest q, Func<string, bool> existsInTheTalesFolder)
      {
         var problems = new List<string>();
         foreach (AuthoredStage s in q?.Stages ?? new List<AuthoredStage>())
         {
            if (string.IsNullOrWhiteSpace(s?.Image)) continue;
            if (!IsAcceptedName(s.Image))
               problems.Add($"quest '{q.Id}': stage '{s.Id}' names the image '{s.Image}', which must be a .png or .jpg file name in the tale's own folder.");
            else if (existsInTheTalesFolder == null || !existsInTheTalesFolder(s.Image.Trim()))
               problems.Add($"quest '{q.Id}': stage '{s.Id}' names the image '{s.Image}', which is not in the tale's folder; the scene opens without it.");
         }

         return problems;
      }

      /// <summary>The image's full path in the tale's folder.</summary>
      public static string PathIn(string talesFolder, string image) => Path.Combine(talesFolder ?? "", image ?? "");
   }
}
