// Code written by Gabriel Mailhot, 07/10/2026.
// Small rules about a written tale under way, from fkasad's walk through the Wolf of the Wood (Nexus, 07/10/2026):
// who holds a role in it (the tale is their errand, so they hand out no other task), when leaving a scene must be
// confirmed (a stray click ended it for good), and what a companion who walked the scene remembers of it.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>Who holds a role in the tale under way. Pure.</summary>
   public static class AuthoredTaleRoles
   {
      /// <summary>True when the hero is bound to a role of a tale still under way (its giver, its rival...).</summary>
      public static bool HoldsARole(AuthoredQuestRun run, string heroId)
      {
         if (run is not {Status: AuthoredQuestStatus.Active} || string.IsNullOrEmpty(heroId) || run.BoundIds == null) return false;

         // The village is bound too, by its settlement id; only people are asked.
         return run.BoundIds.Any(kv => kv.Key != "village" && kv.Value == heroId);
      }
   }

   /// <summary>Whether ending a scene asks first. Pure.</summary>
   public static class SceneLeavePolicy
   {
      /// <summary>A scene still going asks before it is left; a scene over by itself, or an ordinary chat, does not.</summary>
      public static bool AsksFirst(bool isScene, bool sceneOver) => isScene && !sceneOver;
   }

   /// <summary>What a companion who walked a scene with the player remembers of it. Pure.</summary>
   public static class SceneCompanionMemory
   {
      /// <summary>The longest such memory, within the ledger's bound for a memory line.</summary>
      public const int MaxChars = 300;

      public static string Compose(string sceneTitle, string playerName, IReadOnlyList<string> found)
      {
         string place = string.IsNullOrWhiteSpace(sceneTitle) ? "the scene" : sceneTitle.Trim();
         string who = string.IsNullOrWhiteSpace(playerName) ? "the player" : playerName.Trim();
         string text = $"Went with {who} through {place}.";
         List<string> lines = found?.Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f.Trim()).ToList() ?? new List<string>();
         if (lines.Count > 0) text += " We found: " + string.Join(" ", lines);

         return text.Length <= MaxChars ? text : text.Substring(0, MaxChars - 3).TrimEnd() + "...";
      }
   }
}
