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
using CalradiaRemembers.Chat;
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
      ///   cr.prompt_dump [name or hero id]: writes the EXACT system prompt this character would be sent to a file, and
      ///   reports where. Read-only and free: no model is called.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("prompt_dump", "cr")]
      public static string DumpPrompt(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         string who = args is {Count: > 0} ? string.Join(" ", args).Trim() : null;
         Hero hero = PickSubject(who, out string namesakes);

         // An envoy is no hero: dump the last prompt his audience actually sent.
         if (hero == null && who == null && ChatManager.ActiveViewModel?.IsEnvoyAudience == true && ChatViewModel.LastEnvoyPrompt != null)
         {
            string envoyPath = Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
               "Mount and Blade II Bannerlord", "Logs", "CalradiaRemembers_prompt_envoy.txt");
            File.WriteAllText(envoyPath, ChatViewModel.LastEnvoyPrompt);

            return $"cr.prompt_dump: the envoy's last prompt ({ChatViewModel.LastEnvoyPrompt.Length} chars) written to {envoyPath}";
         }

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
               $"CalradiaRemembers_prompt_{Sanitise(hero.Name?.ToString())}_{Sanitise(hero.StringId)}.txt"); // the id too: namesakes no longer overwrite each other

            // DESIGN.md (Prompt Composer): a dump says first whether a custom prompt profile composed it.
            File.WriteAllText(path, PromptProfileService.ActiveLine() + Environment.NewLine + Environment.NewLine + prompt);

            return $"cr.prompt_dump: {hero.Name} ({hero.StringId})'s prompt ({prompt.Length} chars) written to {path}{namesakes}";
         }
         catch (Exception ex)
         {
            return $"cr.prompt_dump failed: {ex.GetType().Name}: {ex.Message}";
         }
      }

      /// <summary>
      ///   cr.prompt_check: the prompt profiles found (stock, the player's Custom/Prompts, each mod's cr_prompts), the one in
      ///   use and its parents, and for each kind of character the layout it gets and what the locks changed in it.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("prompt_check", "cr")]
      public static string PromptCheck(List<string> args)
      {
         try { return PromptProfileService.Check(); }
         catch (Exception ex) { return $"cr.prompt_check failed: {ex.GetType().Name}: {ex.Message}"; }
      }

      #region private

      /// <summary>
      ///   Whoever was named (a hero id, an exact name, then part of a name), else whoever is being spoken to. When
      ///   others answer to the same name, <paramref name="namesakes" /> lists them with their ids, so the next call
      ///   can name the right one (tashmetu, 02/10/2026: namesakes were dumped in silence).
      /// </summary>
      private static Hero PickSubject(string typed, out string namesakes)
      {
         namesakes = "";
         if (string.IsNullOrWhiteSpace(typed)) return Hero.OneToOneConversationHero;

         var heroes = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(Hero.AllAliveHeroes, h => h != null));
         var pick = Logic.Diagnostics.DiagnosticSubjectPicker.Pick(
            System.Linq.Enumerable.Select(heroes, h => new Logic.Diagnostics.DiagnosticSubject {Id = h.StringId, Name = h.Name?.ToString() ?? ""}),
            typed);
         if (pick.Chosen == null) return null;

         if (pick.Others.Count > 0)
            namesakes = "\nAlso answering to that name (pass the id to pick one): "
                        + string.Join(", ", System.Linq.Enumerable.Select(pick.Others, o => $"{o.Name} ({o.Id})"));

         return System.Linq.Enumerable.First(heroes, h => h.StringId == pick.Chosen.Id);
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
