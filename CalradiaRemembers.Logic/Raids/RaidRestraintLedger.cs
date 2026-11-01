// Code written by Gabriel Mailhot, 01/11/2026.
// What each lord drew in the war he is fighting against the player (Gabriel, 01/11/2026, RaidRestraintPolicy). A draw that
// failed is kept as well: a friend draws once per war, not every day until he holds back. Saved as JSON bytes under one additive
// key; an old save reads an empty ledger and its lords draw the next day.

#region

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

#endregion

namespace CalradiaRemembers.Logic.Raids
{
   /// <summary>One lord's draw for one war.</summary>
   public sealed class RaidRestraint
   {
      /// <summary>The lord.</summary>
      public string HeroId { get; set; }

      /// <summary>The realm he was fighting for when he drew.</summary>
      public string FactionId { get; set; }

      /// <summary>True when he drew to spare the player's villages.</summary>
      public bool Spares { get; set; }

      /// <summary>The day he drew.</summary>
      public int Day { get; set; }

      /// <summary>Why he stopped sparing them, or null while the restraint stands.</summary>
      public string BrokenBecause { get; set; }

      /// <summary>True while the player's villages are safe from him.</summary>
      [JsonIgnore]
      public bool IsSparing => Spares && BrokenBecause == null;
   }

   /// <summary>Every draw of the wars under way.</summary>
   public sealed class RaidRestraintLedger
   {
      /// <summary>The draws.</summary>
      public List<RaidRestraint> Draws { get; set; } = new List<RaidRestraint>();

      /// <summary>True when this lord has already drawn for this realm's war.</summary>
      public bool HasDrawn(string heroId, string factionId) => Draws.Any(d => d.HeroId == heroId && d.FactionId == factionId);

      /// <summary>Records a draw made today.</summary>
      public RaidRestraint Add(string heroId, string factionId, bool spares, int today)
      {
         var draw = new RaidRestraint {HeroId = heroId, FactionId = factionId, Spares = spares, Day = today};
         Draws.Add(draw);

         return draw;
      }

      /// <summary>The ledger as saved.</summary>
      public string Serialize() => JsonConvert.SerializeObject(this, Formatting.None);

      /// <summary>Reads a saved ledger; null, empty or broken input reads an empty ledger (an old save).</summary>
      public static RaidRestraintLedger Deserialize(string json)
      {
         if (string.IsNullOrWhiteSpace(json)) return new RaidRestraintLedger();

         try
         {
            RaidRestraintLedger ledger = JsonConvert.DeserializeObject<RaidRestraintLedger>(json) ?? new RaidRestraintLedger();
            ledger.Draws = ledger.Draws ?? new List<RaidRestraint>();

            return ledger;
         }
         catch
         {
            return new RaidRestraintLedger();
         }
      }
   }
}
