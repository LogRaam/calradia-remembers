// Code written by Gabriel Mailhot, 28/11/2026.
// cr.depth / cr.depth_clear: Stage 4 of the group-scene plan, "generated depth" (Gabriel, 28/11/2026: "go pour tout le système ...
// je testerai demain"). A depth is generated once and never regenerated, so the same character keeps one nature between
// meetings; clearing it is the one way back, by hand, for one character.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>cr.depth &lt;name&gt;: the generated depth a character holds. Read-only.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("depth", "cr")]
      public static string DepthShow(List<string> args)
      {
         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         if (behavior?.Store == null) return "cr.depth: no campaign loaded.";

         Hero hero = DepthHeroNamed(args);
         if (hero == null) return "Usage: cr.depth <name>";

         GeneratedDepth d = behavior.Store.Get(hero.StringId)?.GeneratedDepth;
         return d == null
            ? $"cr.depth ({(ModSettings.GeneratedDepth ? "ON" : "OFF")}): {hero.Name} has no generated depth."
            : $"cr.depth ({(ModSettings.GeneratedDepth ? "ON" : "OFF")}): {hero.Name}, generated day {d.GeneratedOnDay}\n  voice: {d.Voice}\n  mask: {d.Mask}\n  fear: {d.Fear}\n  limit: {d.Limit}\n  secret: {d.Secret}";
      }

      /// <summary>cr.depth_clear &lt;name&gt;: forgets a character's generated depth, so the next talk with the switch on writes a new one.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("depth_clear", "cr")]
      public static string DepthClear(List<string> args)
      {
         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         if (behavior?.Store == null) return "cr.depth_clear: no campaign loaded.";

         Hero hero = DepthHeroNamed(args);
         if (hero == null) return "Usage: cr.depth_clear <name>";

         NpcProfile profile = behavior.Store.Get(hero.StringId);
         if (profile?.GeneratedDepth == null) return $"cr.depth_clear: {hero.Name} has no generated depth.";

         profile.GeneratedDepth = null;
         behavior.Store.Set(profile);
         Chat.GeneratedDepthService.ForgetFailure(profile.Id);

         return $"cr.depth_clear: {hero.Name}'s depth is forgotten. With Deeper characters on, the next talk writes a new one. A secret already seeded in the meeting record stays there.";
      }

      private static Hero DepthHeroNamed(List<string> args)
      {
         string name = string.Join(" ", args ?? new List<string>()).Trim();
         if (name.Length == 0) return null;

         return Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && string.Equals(h.Name?.ToString(), name, StringComparison.OrdinalIgnoreCase))
                ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.Name?.ToString().IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
      }
   }
}
