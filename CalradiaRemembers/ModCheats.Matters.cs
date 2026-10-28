// Code written by Gabriel Mailhot, 27/10/2026.
// What matters most to a character (Gabriel, 27/10/2026: "qu'est-ce qui compte le plus pour un NPC"), seen from the
// console, and learned or forgotten by hand to try the What You Know section.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>cr.matters [learn|forget] [name]: what matters most to a character, whether the player learned it, and the page line.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("matters", "cr")]
      public static string Matters(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;
         string verb = args?.FirstOrDefault()?.ToLowerInvariant();
         bool learn = verb == "learn", forget = verb == "forget";
         Hero hero = ResolveHero(learn || forget ? args.Skip(1).ToList() : args);
         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         Mapping.HeroProfileMapper.UpgradeIfNeeded(profile, hero);
         if (learn) profile.MattersMostKnown = true;
         if (forget) profile.MattersMostKnown = false;
         behavior.Store.Set(profile);

         string line = Mapping.MattersMostReader.PageLine(hero, profile) ?? "(nothing on the page yet)";

         return $"{hero.Name}: what matters most = {profile.MattersMost}; learned by the player = {profile.MattersMostKnown}. Page: {line}";
      }
   }
}
