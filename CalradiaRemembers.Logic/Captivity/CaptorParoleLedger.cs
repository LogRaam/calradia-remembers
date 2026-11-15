// Code written by Gabriel Mailhot, 01/11/2026.
// The oaths the player swore to win their freedom (Gabriel, 01/11/2026, "go"). The swear_oath verb is the other way round, a
// character swearing to the player, and since 22/08/2026 a character's vow is only remembered; a word the player gives is held
// to here. Saved as JSON bytes under one additive key; an old save reads an empty ledger.

#region

using System.Collections.Generic;
using Newtonsoft.Json;

#endregion

namespace CalradiaRemembers.Logic.Captivity
{
   /// <summary>One oath: not to attack this realm until this day.</summary>
   public sealed class CaptorParole
   {
      /// <summary>Stable id, the journal entry's own.</summary>
      public string Id { get; set; }

      /// <summary>The captor who took the oath.</summary>
      public string CaptorId { get; set; }

      /// <summary>The realm the player swore not to attack.</summary>
      public string FactionId { get; set; }

      /// <summary>The realm's name when the oath was sworn.</summary>
      public string FactionName { get; set; }

      /// <summary>The last day of the oath.</summary>
      public int UntilDay { get; set; }
   }

   /// <summary>An oath a captive takes to a captor's realm: the captor, the realm, its name, and the day.</summary>
   public sealed class CaptorOathTaken
   {
      /// <summary>The captor's hero id.</summary>
      public string CaptorId { get; init; }

      /// <summary>The realm sworn to.</summary>
      public string FactionId { get; init; }

      /// <summary>The realm's name, as the oath is told.</summary>
      public string FactionName { get; init; }

      /// <summary>The game day the oath is taken.</summary>
      public int Today { get; init; }
   }

   /// <summary>Every oath of parole still binding the player.</summary>
   public sealed class CaptorParoleLedger
   {
      /// <summary>The oaths in force.</summary>
      public List<CaptorParole> Oaths { get; set; } = new List<CaptorParole>();

      /// <summary>The next number for an id.</summary>
      public int NextId { get; set; } = 1;

      /// <summary>Records an oath taken today.</summary>
      public CaptorParole Add(CaptorOathTaken facts) => Add(facts.CaptorId, facts.FactionId, facts.FactionName, facts.Today);

      // The positional form behind the facts, private so no caller can pass two of its same-typed arguments the wrong
      // way round and still compile (swap-risk ratchet, 14/11/2026).
      private CaptorParole Add(string captorId, string factionId, string factionName, int today)
      {
         var oath = new CaptorParole {
            Id = $"cr_parole_{NextId++}", CaptorId = captorId, FactionId = factionId, FactionName = factionName,
            UntilDay = today + CaptorAudiencePolicy.ParoleDays
         };
         Oaths.Add(oath);

         return oath;
      }

      /// <summary>The ledger as saved.</summary>
      public string Serialize() => JsonConvert.SerializeObject(this, Formatting.None);

      /// <summary>Reads a saved ledger; null, empty or broken input reads an empty ledger (an old save).</summary>
      public static CaptorParoleLedger Deserialize(string json)
      {
         if (string.IsNullOrWhiteSpace(json)) return new CaptorParoleLedger();

         try
         {
            CaptorParoleLedger ledger = JsonConvert.DeserializeObject<CaptorParoleLedger>(json) ?? new CaptorParoleLedger();
            ledger.Oaths = ledger.Oaths ?? new List<CaptorParole>();

            return ledger;
         }
         catch
         {
            return new CaptorParoleLedger();
         }
      }
   }
}
