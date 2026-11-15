// Code written by Gabriel Mailhot, 28/08/2026.
// Extension Surface, Prompt Variables volet (increment 4): CrPrompt.RegisterVariable is the PUBLIC door a
// third-party mod uses to contribute one LIVE {{name}} prompt variable, computed per conversation, that the
// post-history prompt can reference. Replaces the only alternative that existed before this: a Harmony
// patch on the SDK's own PromptBuilder.BuildPromptVariables to splice a value in after the fact. Nothing
// here does any engine work itself; the provider re-resolves whatever engine state it needs from the
// NpcId on its own PromptVarFacts.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Extension;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers
{
   /// <summary>
   ///   The public facade a third-party mod calls to register (or unregister) one live prompt variable.
   ///   The provider is called once per prompt build with engine-agnostic <see cref="PromptVarFacts" />
   ///   (an NpcId to re-resolve its own Hero from, and the personal relation the host already tracks) and
   ///   must return the variable's current text; a built-in variable (<c>{{user}}</c>, <c>{{char}}</c>, and
   ///   so on) ALWAYS wins a name collision, so a third-party name can never shadow a core token.
   /// </summary>
   public static class CrPrompt
   {
      /// <summary>
      ///   Registers (or replaces) the provider for a <c>{{name}}</c> prompt variable. A null/blank name or
      ///   a null <paramref name="provider" /> is silently ignored. Registering the same name again replaces
      ///   the prior provider.
      /// </summary>
      public static void RegisterVariable(string name, Func<PromptVarFacts, string> provider)
      {
         if (string.IsNullOrWhiteSpace(name) || provider == null) return;

         PromptVariableRegistry.Register(name, provider);

         ModLog.Info($"[EXT PROMPT VAR] registered '{name}'");
      }

      /// <summary>Unregisters the provider for <paramref name="name" />. Returns whether one was present.</summary>
      public static bool Unregister(string name) => PromptVariableRegistry.Unregister(name);

      /// <summary>
      ///   Rereads the editable prompt files from disk (Custom first: world.txt, player_description.txt, the behaviour
      ///   guidelines, post_history_instructions.txt, heard_now.txt, council_seat.txt, setting.json, cultural_traditions.json,
      ///   jealousy_cultures.json, stations and folklore), what <c>cr.reload_prompts</c> does; the next reply is built from
      ///   them. Returns the files that failed to reload, empty when all did. Call it on the main thread, with a campaign
      ///   loaded. Since 2.8.10.
      /// </summary>
      public static IReadOnlyList<string> Reload() => Lore.PromptDataReload.ReloadPromptFiles();

      /// <summary>
      ///   Every keyed section of the character prompt, in the stock order: the keys a prompt profile's layout and its
      ///   sections/&lt;key&gt;.txt files name, with zone and lock. No campaign needed. Since 2.8.10.
      /// </summary>
      public static IReadOnlyList<CrPromptSection> Sections()
         => PromptBuilder.CharacterSections.Select(s => new CrPromptSection {Key = s.Key, Zone = s.Zone.ToString(), Lock = s.Lock.ToString(), Text = null}).ToList();

      /// <summary>
      ///   What each section of the STOCK character prompt writes for <paramref name="heroId" /> right now, in the stock
      ///   order: CR's own words, with no prompt profile's layout or rewording, built the way a conversation with them would
      ///   be (Bhelogan, 14/11/2026: "read-only stock text of a prompt section (for the profile editor)"). A section's text
      ///   follows the character and the moment (their rank, the adult content level, Lean...), so an empty Text means the
      ///   section writes nothing for them now, not that it has no words. Covers the one-to-one character prompt; the
      ///   commoner and council prompts are not keyed. Empty for an unknown hero, outside a campaign, or before the
      ///   model connection is set up. Game thread. Since 2.8.10.
      /// </summary>
      public static IReadOnlyList<CrPromptSection> StockSections(string heroId)
      {
         try
         {
            NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
            PromptBuilder builder = behavior?.LlmServices?.ChatService?.PromptBuilder as PromptBuilder;
            Hero hero = string.IsNullOrEmpty(heroId) || Campaign.Current == null ? null : Hero.FindFirst(h => h.StringId == heroId);
            if (builder == null || hero == null || behavior.Store == null || behavior.GameStateBridge == null) return Array.Empty<CrPromptSection>();

            NpcProfile profile = behavior.Store.Get(hero.StringId) ?? Mapping.HeroProfileMapper.FromHero(hero);
            EncounterContext context = Mapping.EncounterContextBuilder.Build(hero, profile, behavior.GameStateBridge, null);
            var world = new WorldState {CurrentDay = behavior.GameStateBridge.GetCurrentGameDay()};

            return builder.RenderStockSections(profile, world, context)
                          .Select(s => new CrPromptSection {Key = s.Key, Zone = s.Zone.ToString(), Lock = s.Lock.ToString(), Text = s.Text})
                          .ToList();
         }
         catch (Exception ex)
         {
            ModLog.Fault($"[EXT PROMPT] StockSections({heroId})", ex);

            return Array.Empty<CrPromptSection>();
         }
      }

      /// <summary>The stock text of one section for <paramref name="heroId" /> (see <see cref="StockSections" />); null for an unknown key or hero. Since 2.8.10.</summary>
      public static string StockSectionText(string key, string heroId)
         => StockSections(heroId).FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase))?.Text;
   }

   /// <summary>One section of the character prompt (CrPrompt.Sections, CrPrompt.StockSections). Since 2.8.10.</summary>
   public sealed class CrPromptSection
   {
      /// <summary>The key a profile's layout and its sections/&lt;key&gt;.txt name; never renamed once shipped.</summary>
      public string Key { get; set; }

      /// <summary>"Prefix" (the cached part, the same every turn of a conversation), "Marker" (the encounter itself) or "Tail" (fresh each turn).</summary>
      public string Zone { get; set; }

      /// <summary>"None" (free), "Required" (cannot be turned off) or "Sealed" (cannot be turned off, moved or reworded).</summary>
      public string Lock { get; set; }

      /// <summary>What the stock prompt writes there for the character asked about; empty when it writes nothing now; null from Sections().</summary>
      public string Text { get; set; }
   }
}
