// Code written by Gabriel Mailhot, 27/09/2026.
// The host half of the council's per-seat modder hook (tashmetu, Bellum Civile bridge, 26/09/2026: "I'll leave the
// council alone until there's a hook there"). For one seated member it builds that member's variables, the built-ins
// and every registered provider's, ON THE GAME THREAD (the council input is built before the model call's Task.Run,
// the same discipline the private word keeps since 2.5.19), and lets CouncilSeatNote expand council_seat.txt with them.

#region

using System;
using System.Collections.Generic;
using NpcMemoryService.Core.Extension;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using TaleWorlds.CampaignSystem;
using CalradiaRemembers.Foundation;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Resolves one council seat's modder note from council_seat.txt and that seat's own variables.</summary>
   internal static class CouncilSeatNoteResolver
   {
      /// <summary>
      ///   The note for the seat <paramref name="seatName" /> (the loaded council_seat.txt), or null. With no template
      ///   no provider is called at all, so a game without a contributing mod pays nothing for the hook.
      /// </summary>
      public static string Resolve(Hero hero, NpcProfile profile, string seatName)
         => Resolve(hero, profile, seatName, NarrativeLoader.CouncilSeatTemplate);

      /// <summary>The same, with the template given (cr.test all's seam).</summary>
      internal static string Resolve(Hero hero, NpcProfile profile, string seatName, string template)
      {
         if (string.IsNullOrWhiteSpace(template)) return null;

         try
         {
            Hero player = Hero.MainHero;
            var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
               ["user"] = player?.Name?.ToString() ?? "",
               ["user_gender"] = player?.IsFemale == true ? "woman" : "man",
               ["char"] = seatName ?? "",
               ["char_gender"] = hero?.IsFemale == true ? "woman" : "man"
            };

            IReadOnlyDictionary<string, string> external = PromptVariableRegistry.Compose(new PromptVarFacts {
               NpcId = profile?.Id ?? hero?.StringId ?? "",
               RelationToPlayer = profile?.ReputationWithPlayer ?? 0,
               Lean = ModSettings.LeanPrompt,
               CouncilSeat = true
            });

            // Built-ins win a name collision, as in a private word.
            foreach (KeyValuePair<string, string> kv in external)
               if (!vars.ContainsKey(kv.Key))
                  vars[kv.Key] = kv.Value ?? "";

            return CouncilSeatNote.Compose(template, vars);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COUNCIL] the seat note for {seatName} failed, the seat sits without it: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }
   }
}
