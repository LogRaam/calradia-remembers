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

      /// <summary>
      ///   Gabriel (10/10/2026): a tale whose stages are states of one conversation, each with its picture. When the state
      ///   changes, the picture to show now: the new one when it differs from the one shown; null (keep what is shown)
      ///   when the new state has none or it is the same.
      /// </summary>
      public static string ImageToShow(string shown, string next)
         => !string.IsNullOrWhiteSpace(next) && !string.Equals(shown, next, StringComparison.OrdinalIgnoreCase) ? next : null;

      /// <summary>
      ///   Gabriel (11/10/2026): a tale's picture opened beside an ordinary "A word with you" with a notable the tale only
      ///   concerned ("il ne faudrait pas que ca apparaisse lorsque je ne suis pas dans le contexte du conte"). A conversation
      ///   shows the stage's picture only when it IS the stage under way: the hero a talk_to (or return) objective names, a
      ///   local of the place a talk_to_locals objective names, or the one a choice is said to. A character the stage merely
      ///   concerns (a knower, the giver between two steps) is spoken to without it.
      /// </summary>
      public static bool ConversationIsTheStep(AuthoredQuestRun run, AuthoredQuest q, string heroId, string localOfPlaceId)
      {
         if (run is not {Status: AuthoredQuestStatus.Active} || string.IsNullOrEmpty(heroId)) return false;
         AuthoredStage stage = AuthoredQuestEngine.Current(run, q);
         if (stage == null) return false;

         foreach (AuthoredWay way in AuthoredQuestEngine.Ways(run, q))
         {
            bool step = way.Objective?.Type switch {
               "talk_to" => heroId == AuthoredQuestEngine.Bound(run, way.Objective.Hero),
               "return" => heroId == AuthoredQuestEngine.Bound(run, "{giver}"),
               "talk_to_locals" => localOfPlaceId != null && localOfPlaceId == AuthoredQuestEngine.Bound(run, way.Objective.Place),
               _ => false
            };
            if (step) return true;
         }

         return (stage.Choices ?? new List<AuthoredChoice>()).Any(c => c?.Said?.To != null && heroId == AuthoredQuestEngine.Bound(run, c.Said.To));
      }

      /// <summary>The image of the stage under way, or null when it has none.</summary>
      public static string ImageFor(AuthoredQuestRun run, AuthoredQuest q)
      {
         string image = AuthoredQuestEngine.Current(run, q)?.Image;

         return IsAcceptedName(image) ? image.Trim() : null;
      }

      /// <summary>
      ///   The picture a scene narration calls for (Gabriel, 08/10/2026): of the people it brings in, else the dangers it
      ///   springs, else the findings it reveals, the last named of the kind; null when none of them carries one.
      /// </summary>
      public static string ImageForMarkers(AuthoredQuestRun run, AuthoredQuest q, IEnumerable<string> found, IEnumerable<string> sprung, IEnumerable<string> met)
      {
         AuthoredScene scene = AuthoredQuestEngine.Current(run, q)?.Objective?.Scene;
         if (scene == null) return null;

         string Last<T>(IEnumerable<string> ids, IEnumerable<T> items, Func<T, string> id, Func<T, string> image)
            => (ids ?? Enumerable.Empty<string>()).Select(i => items.FirstOrDefault(x => id(x) == i)).Where(x => x != null)
                                                 .Select(image).LastOrDefault(IsAcceptedName);

         string pick = Last(met, scene.Encounters ?? new List<AuthoredEncounter>(), e => e.Id, e => e.Image)
                       ?? Last(sprung, scene.Hazards ?? new List<AuthoredHazard>(), h => h.Id, h => h.Image)
                       ?? Last(found, scene.Findings ?? new List<AuthoredFinding>(), f => f.Id, f => f.Image);

         return pick?.Trim();
      }

      /// <summary>What is wrong with the tale's images; <paramref name="existsInTheTalesFolder" /> answers for a file name.</summary>
      public static IReadOnlyList<string> Problems(AuthoredQuest q, Func<string, bool> existsInTheTalesFolder)
      {
         var problems = new List<string>();
         void Check(string where, string image)
         {
            if (string.IsNullOrWhiteSpace(image)) return;
            if (!IsAcceptedName(image))
               problems.Add($"quest '{q.Id}': {where} names the image '{image}', which must be a .png or .jpg file name in the tale's own folder.");
            else if (existsInTheTalesFolder == null || !existsInTheTalesFolder(image.Trim()))
               problems.Add($"quest '{q.Id}': {where} names the image '{image}', which is not in the tale's folder; it is not shown.");
         }

         foreach (AuthoredStage s in q?.Stages ?? new List<AuthoredStage>())
         {
            if (s == null) continue;
            Check($"stage '{s.Id}'", s.Image);
            AuthoredScene scene = s.Objective?.Scene;
            if (scene == null) continue;
            foreach (AuthoredFinding f in scene.Findings ?? new List<AuthoredFinding>()) Check($"stage '{s.Id}', finding '{f?.Id}'", f?.Image);
            foreach (AuthoredHazard h in scene.Hazards ?? new List<AuthoredHazard>()) Check($"stage '{s.Id}', hazard '{h?.Id}'", h?.Image);
            foreach (AuthoredEncounter e in scene.Encounters ?? new List<AuthoredEncounter>()) Check($"stage '{s.Id}', person '{e?.Id}'", e?.Image);
         }

         return problems;
      }

      /// <summary>The image's full path in the tale's folder.</summary>
      public static string PathIn(string talesFolder, string image) => Path.Combine(talesFolder ?? "", image ?? "");
   }
}
