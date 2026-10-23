// Code written by Gabriel Mailhot, 23/10/2026.
// What the encyclopedia's Misdeeds section shows on the player's page (Gabriel, 23/10/2026: the prisoners the player took
// by force are "une autre section avec des actions reprehensibles", not Heart). The words are MisdeedsPagePolicy's, the
// records TraumaLedgerBehavior's, the same ledger each victim's Trauma section reads. Shared by the page and cr.testall
// heart_intimacy, so the test reads exactly what the player reads.

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
   /// <summary>Reads what the player did of their own will for the Misdeeds section.</summary>
   internal static class MisdeedsPageReader
   {
      /// <summary>The Misdeeds lines of a hero's page, as shown. Empty below Hardcore and on any page but the player's.</summary>
      internal static IReadOnlyList<string> LinesFor(Hero hero)
      {
         if (hero == null || !MisdeedsPagePolicy.Shows(ModSettings.AdultContentLevel, hero == Hero.MainHero)) return Array.Empty<string>();

         var lines = new List<string>();
         foreach (MisdeedFacts deed in MisdeedsPagePolicy.Of(TraumaLedgerBehavior.All()))
         {
            Hero victim = Hero.FindFirst(h => h.StringId == deed.VictimId);
            if (victim != null) lines.Add(MisdeedsPagePolicy.Line(deed, victim.Name?.ToString(), victim.IsFemale, HeartPageReader.When));
         }

         return lines;
      }
   }
}
