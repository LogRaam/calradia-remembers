// Code written by Gabriel Mailhot, 27/10/2026.
// What the encyclopedia's What You Know section reads of the game (Gabriel, 27/10/2026: "il faudra penser a les indiquer
// dans des sections de l'encyclopedia"): on a character's page, never the player's, what the player has learned of who
// they are. Each increment of the pillar adds its own line here.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Reads the What You Know lines of a hero's page.</summary>
   internal static class KnownPageReader
   {
      /// <summary>The What You Know lines of a hero's page, as shown (cr.test matters_most reads this seam). Empty when none.</summary>
      internal static IReadOnlyList<string> LinesFor(Hero hero)
      {
         if (hero == null || hero == Hero.MainHero) return Array.Empty<string>();
         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(hero.StringId);
         if (profile == null) return Array.Empty<string>();

         var lines = new List<string>();
         string mattersMost = MattersMostReader.PageLine(hero, profile);
         if (mattersMost != null) lines.Add(mattersMost);
         lines.AddRange(NatureReader.PageLines(hero, profile));

         return lines;
      }
   }
}
