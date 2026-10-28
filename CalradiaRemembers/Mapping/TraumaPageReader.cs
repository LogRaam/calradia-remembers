// Code written by Gabriel Mailhot, 20/10/2026.
// What the encyclopedia's Trauma section shows of a hero (Gabriel, 20/10/2026: at Hardcore, the aggressions a character
// lived through, never on the player's own page). The words are TraumaPagePolicy's, the records TraumaLedgerBehavior's.
// Shared by the page and cr.test trauma_page, so the test reads exactly what the player reads.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Trauma;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Reads the aggressions a hero lived through for the Trauma section.</summary>
   internal static class TraumaPageReader
   {
      /// <summary>The Trauma lines of a hero's page, as shown. Empty below Hardcore and on the player's own page.</summary>
      internal static IReadOnlyList<string> LinesFor(Hero hero)
      {
         if (hero == null || !TraumaPagePolicy.Shows(ModSettings.AdultContentLevel, hero == Hero.MainHero)) return Array.Empty<string>();

         return TraumaLedgerBehavior.Of(hero)
                                    .Select(r => TraumaPagePolicy.Line(r, hero.IsFemale, CaptorName(r.CaptorId), HeartPageReader.When))
                                    .ToList();
      }

      private static string CaptorName(string id)
         => string.IsNullOrEmpty(id) ? null : Hero.FindFirst(h => h.StringId == id)?.Name?.ToString();
   }
}
