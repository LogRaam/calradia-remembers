// Code written by Gabriel Mailhot, 01/11/2026.
// The legends of the campaign (DepaHN, 27/10/2026; LegendPolicy). A world event ages out as rumour; a legend does not, so it keeps
// everything its telling needs here, names included, for a hero long dead or a realm long gone. Saved as JSON bytes under one
// additive key; an old save reads an empty ledger.

#region

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

#endregion

namespace CalradiaRemembers.Logic.Legends
{
   /// <summary>One deed passed into legend, as told for good.</summary>
   public sealed class Legend
   {
      /// <summary>What kind of deed it was.</summary>
      public LegendKind Kind { get; set; }

      /// <summary>The name it keeps.</summary>
      public string Name { get; set; }

      /// <summary>The day it happened.</summary>
      public int Day { get; set; }

      /// <summary>The one it honours.</summary>
      public string HeroId { get; set; }

      /// <summary>Their name when it happened.</summary>
      public string HeroName { get; set; }

      /// <summary>Their house.</summary>
      public string ClanId { get; set; }

      /// <summary>The realm it honours.</summary>
      public string RealmId { get; set; }

      /// <summary>The realm it was done against.</summary>
      public string OpposingRealmId { get; set; }

      /// <summary>That realm's name when it happened.</summary>
      public string OpposingRealmName { get; set; }

      /// <summary>The place, or null for a field no settlement names.</summary>
      public string PlaceName { get; set; }

      /// <summary>The men of the side that was outnumbered.</summary>
      public int FewMen { get; set; }

      /// <summary>The men of the other side.</summary>
      public int ManyMen { get; set; }

      /// <summary>What a last stand cost the enemy.</summary>
      public int Toll { get; set; }

      /// <summary>The heroes who fought on the honoured side.</summary>
      public List<string> BesideIds { get; set; } = new List<string>();

      /// <summary>The heroes who fought on the other.</summary>
      public List<string> AgainstIds { get; set; } = new List<string>();
   }

   /// <summary>Every legend of the campaign, for good.</summary>
   public sealed class LegendLedger
   {
      /// <summary>The legends, oldest first.</summary>
      public List<Legend> Legends { get; set; } = new List<Legend>();

      /// <summary>The day of the last one, or null before the first.</summary>
      [JsonIgnore]
      public int? LastDay => Legends.Count == 0 ? (int?) null : Legends.Max(l => l.Day);

      /// <summary>The ledger as saved.</summary>
      public string Serialize() => JsonConvert.SerializeObject(this, Formatting.None);

      /// <summary>Reads a saved ledger; null, empty or broken input reads an empty ledger (an old save).</summary>
      public static LegendLedger Deserialize(string json)
      {
         if (string.IsNullOrWhiteSpace(json)) return new LegendLedger();

         try
         {
            LegendLedger ledger = JsonConvert.DeserializeObject<LegendLedger>(json) ?? new LegendLedger();
            ledger.Legends = ledger.Legends ?? new List<Legend>();

            return ledger;
         }
         catch
         {
            return new LegendLedger();
         }
      }
   }
}
