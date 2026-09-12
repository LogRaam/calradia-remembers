// Code written by Gabriel Mailhot, 12/09/2026.
// "What did the model actually SEE?" - a question that had no answer.
//
// It came up twice in one day. Migrating a prompt section needs a before-and-after to prove the move changed
// nothing, and there was no way to get one without writing a throwaway test each time. And when a player
// reports that a character knew something odd, or failed to know something obvious, the single most useful
// artefact is the exact text that character was handed - which nobody, including the player, could produce.
//
// So this writes it to a file beside the log. No LLM call, no tokens, nothing mutated: it builds the same
// context and the same prompt a real conversation would, and puts it on disk.

#region

using System;
using System.Collections.Generic;
using System.IO;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.prompt_dump [name] — writes the EXACT system prompt this character would be sent to a file, and
      ///   reports where. Read-only and free: no model is called.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("prompt_dump", "cr")]
      public static string DumpPrompt(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         string who = args is {Count: > 0} ? string.Join(" ", args).Trim() : null;
         Hero hero = PickSubject(who);

         if (hero == null) return "cr.prompt_dump: no such character, and nobody is being spoken to.";

         try
         {
            NpcProfile profile = behavior.Store.Get(hero.StringId) ?? HeroProfileMapper.FromHero(hero);
            EncounterContext context = EncounterContextBuilder.Build(hero, profile, behavior.GameStateBridge, null);

            string prompt = behavior.LlmServices.ChatService.PromptBuilder.BuildSystemPrompt(
               profile, new WorldState {CurrentDay = behavior.GameStateBridge.GetCurrentGameDay()}, context);

            // Beside the log, because that is the folder a player already knows how to find and send.
            string path = Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
               "Mount and Blade II Bannerlord", "Logs",
               $"CalradiaRemembers_prompt_{Sanitise(hero.Name?.ToString())}.txt");

            File.WriteAllText(path, prompt);

            return $"cr.prompt_dump: {hero.Name}'s prompt ({prompt.Length} chars) written to {path}";
         }
         catch (Exception ex)
         {
            return $"cr.prompt_dump failed: {ex.GetType().Name}: {ex.Message}";
         }
      }

      #region private

      /// <summary>
      ///   Whoever was named, else whoever is being spoken to. The same rule cr.memory_bench uses, so the two
      ///   diagnostics never disagree about who "this character" means.
      /// </summary>
      private static Hero PickSubject(string nameFragment)
      {
         if (!string.IsNullOrWhiteSpace(nameFragment))
            return System.Linq.Enumerable.FirstOrDefault(
               Hero.AllAliveHeroes,
               h => h?.Name != null
                    && h.Name.ToString().IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0);

         return Hero.OneToOneConversationHero;
      }

      /// <summary>A file name a person can find, from a name that may contain anything at all.</summary>
      private static string Sanitise(string name)
      {
         if (string.IsNullOrWhiteSpace(name)) return "unknown";

         var clean = new System.Text.StringBuilder();

         foreach (char c in name.Trim())
            clean.Append(char.IsLetterOrDigit(c) ? c : '_');

         return clean.ToString();
      }

      #endregion
   }
}
