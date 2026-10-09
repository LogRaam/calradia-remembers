// Code written by Gabriel Mailhot, 03/10/2026.
// Authored quests, increment 1: whether a character who could give a quest raises it now. Gabriel's rulings
// (03/10/2026): one authored quest at a time; unique quests once per campaign; any character may offer one. Whether
// this person qualifies (a lord holding a village by a wood...) is the engine's reading of the file's "requires"; this
// decides only the rest.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>The facts an offer is judged on, past the giver qualifying.</summary>
   public sealed class AuthoredOfferFacts
   {
      public int Today { get; init; }

      /// <summary>An authored quest is already being played.</summary>
      public bool StoryUnderWay { get; init; }

      public bool Unique { get; init; } = true;

      /// <summary>This quest was already played in this campaign (completed or failed).</summary>
      public bool AlreadyPlayed { get; init; }

      /// <summary>The day a repeatable quest was last played to its end (won or lost), or null.</summary>
      public int? LastPlayedOnDay { get; init; }

      /// <summary>The file's repeat_cooldown_days: how long a repeatable quest waits after it was last played.</summary>
      public int RepeatCooldownDays { get; init; }

      /// <summary>
      ///   The giver already has a vanilla task for the player, offered or under way (fkasad, 06/10/2026: two errands from
      ///   one mouth tangle the conversation).
      /// </summary>
      public bool GiverHasVanillaQuest { get; init; }

      /// <summary>The day the player last declined this quest, or null.</summary>
      public int? DeclinedOnDay { get; init; }

      /// <summary>A roll in [0,1) and the chance it must fall under.</summary>
      public double Roll { get; init; }

      public double Chance { get; init; }
   }

   /// <summary>Decides whether a qualifying giver raises the quest now. Pure.</summary>
   public static class AuthoredQuestOfferPolicy
   {
      /// <summary>How long a declined quest waits before it may be raised again: a season.</summary>
      public const int DeclineWaitDays = NpcMemoryService.Core.Calendar.WorldCalendar.DefaultDaysPerSeason;

      /// <summary>The chance a qualifying giver raises it in a given conversation.</summary>
      public const double OfferChance = 0.35;

      public static bool ShouldOffer(AuthoredOfferFacts f) => f != null && WhyNot(f) == null && f.Roll < f.Chance;

      /// <summary>
      ///   Why the quest may not be raised now, the dice aside, in plain words for an author (cr.story_check,
      ///   cr.story_start); null when nothing stands in the way.
      /// </summary>
      public static string WhyNot(AuthoredOfferFacts f)
      {
         if (f == null) return "nothing to judge";
         if (f.StoryUnderWay) return "a tale is already under way";
         if (f.GiverHasVanillaQuest) return "they already have a task of their own for you";
         if (f.Unique && f.AlreadyPlayed) return "it was already played in this campaign";
         if (!f.Unique && f.LastPlayedOnDay is int p && f.Today - p < f.RepeatCooldownDays)
            return $"it was played on day {p}, and it waits {f.RepeatCooldownDays} days before it comes back";
         if (f.DeclinedOnDay is int d && f.Today - d < DeclineWaitDays) return $"it was declined on day {d}, and it waits a season";

         return null;
      }

      /// <summary>
      ///   Which of several qualifying quests a giver raises: each by its file's weight, out of a roll in [0,1). A
      ///   weight of zero or less is never picked; -1 when nothing can be.
      /// </summary>
      public static int PickByWeight(int[] weights, double roll)
      {
         if (weights == null) return -1;
         long total = 0;
         foreach (int w in weights) total += w > 0 ? w : 0;
         if (total == 0) return -1;

         double at = roll * total, sum = 0;
         int last = -1;
         for (var i = 0; i < weights.Length; i++)
         {
            if (weights[i] <= 0) continue;
            last = i;
            sum += weights[i];
            if (at < sum) return i;
         }

         return last;
      }
   }

   /// <summary>
   ///   Where written tales are read from (fkasad, 06/10/2026: one Custom folder to manage, and the MCM path): the shipped
   ///   folder, its old Custom folder (files there keep working), and Quests under the player's Custom folder. Each once.
   /// </summary>
   public static class AuthoredQuestFolders
   {
      public static IReadOnlyList<string> List(string shippedQuestsFolder, string playerCustomFolder)
      {
         var folders = new List<string>();
         void Add(string f)
         {
            if (string.IsNullOrWhiteSpace(f)) return;
            string norm = f.TrimEnd('\\', '/');
            foreach (string known in folders)
               if (string.Equals(known.TrimEnd('\\', '/'), norm, System.StringComparison.OrdinalIgnoreCase)) return;
            folders.Add(f);
         }

         Add(shippedQuestsFolder);
         if (!string.IsNullOrWhiteSpace(shippedQuestsFolder)) Add(System.IO.Path.Combine(shippedQuestsFolder, "Custom"));
         if (!string.IsNullOrWhiteSpace(playerCustomFolder)) Add(System.IO.Path.Combine(playerCustomFolder, "Quests"));

         return folders;
      }

      /// <summary>The folder another mod ships its tales in, inside its own ModuleData.</summary>
      public const string ModuleTalesFolder = "cr_quests";

      /// <summary>
      ///   The tales folders of the other ENABLED mods (Gabriel, 09/10/2026, for tashmetu: a mod ships its tales in its own
      ///   ModuleData/cr_quests), in the launcher's order, each mod once, CR itself left out (its own folders are listed by
      ///   <see cref="List" />). <paramref name="enabledModules" /> is the engine's enabled list with each module's root.
      /// </summary>
      public static IReadOnlyList<TaleModuleFolder> FromModules(IEnumerable<(string Id, string Root)> enabledModules, string ownModuleId)
      {
         var found = new List<TaleModuleFolder>();
         var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
         foreach ((string id, string root) in enabledModules ?? new List<(string, string)>())
         {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(root)) continue;
            if (string.Equals(id.Trim(), ownModuleId, System.StringComparison.OrdinalIgnoreCase) || !seen.Add(id.Trim())) continue;
            found.Add(new TaleModuleFolder {ModuleId = id.Trim(), Folder = System.IO.Path.Combine(root, "ModuleData", ModuleTalesFolder)});
         }

         return found;
      }
   }

   /// <summary>Another mod's tales folder.</summary>
   public sealed class TaleModuleFolder
   {
      public string ModuleId { get; init; }

      public string Folder { get; init; }

      /// <summary>The source a tale of this mod is known by: the mod's id, then its file within the folder.</summary>
      public string SourceOf(string relativeFile) => $"{ModuleId}: {relativeFile}";
   }
}
