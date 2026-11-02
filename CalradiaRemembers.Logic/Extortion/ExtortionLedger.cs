// Code written by Gabriel Mailhot, 01/11/2026.
// Every extortion the player tried (ExtortionPolicy). The Misdeeds page keeps those that paid; this also keeps those refused,
// since a refusal still spends the attempt, and the bargain a payment for lands made. Saved as JSON bytes under one additive
// key; an old save reads an empty ledger.

#region

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

#endregion

namespace CalradiaRemembers.Logic.Extortion
{
   /// <summary>One attempt on one person.</summary>
   public sealed class ExtortionRecord
   {
      /// <summary>The person pressed.</summary>
      public string VictimId { get; set; }

      /// <summary>Their house, whose lands a bargain covers.</summary>
      public string ClanId { get; set; }

      /// <summary>The hold used.</summary>
      public ExtortionLever Lever { get; set; }

      /// <summary>The day of it.</summary>
      public int Day { get; set; }

      /// <summary>What they paid; 0 when they refused.</summary>
      public int Amount { get; set; }

      /// <summary>The player raided the lands they took coin to spare.</summary>
      public bool BargainBroken { get; set; }
   }

   /// <summary>One affair the player told.</summary>
   public sealed class ExposureRecord
   {
      /// <summary>The one whose affair it was.</summary>
      public string SubjectId { get; set; }

      /// <summary>Their lover, who may be the player.</summary>
      public string LoverId { get; set; }

      /// <summary>The one the player told.</summary>
      public string ListenerId { get; set; }

      /// <summary>The day of it.</summary>
      public int Day { get; set; }
   }

   /// <summary>Every attempt of the campaign, and every secret told.</summary>
   public sealed class ExtortionLedger
   {
      /// <summary>The attempts, oldest first.</summary>
      public List<ExtortionRecord> Attempts { get; set; } = new List<ExtortionRecord>();

      /// <summary>The affairs told, oldest first.</summary>
      public List<ExposureRecord> Exposures { get; set; } = new List<ExposureRecord>();

      /// <summary>How many times this person was pressed.</summary>
      public int TimesPressed(string victimId) => Attempts.Count(a => a.VictimId == victimId);

      /// <summary>The day this person was last pressed, or null.</summary>
      public int? LastDay(string victimId)
      {
         List<ExtortionRecord> theirs = Attempts.Where(a => a.VictimId == victimId).ToList();

         return theirs.Count == 0 ? (int?) null : theirs.Max(a => a.Day);
      }

      /// <summary>The live bargain covering this house's lands, or null.</summary>
      public ExtortionRecord BargainOver(string clanId, int today)
         => Attempts.LastOrDefault(a => a.ClanId == clanId && a.Lever == ExtortionLever.Lands && a.Amount > 0 && !a.BargainBroken
                                        && today - a.Day <= ExtortionPolicy.DaysOfTheBargain);

      /// <summary>The live payment this person made for silence, or null.</summary>
      public ExtortionRecord SecretBargainWith(string victimId, int today)
         => Attempts.LastOrDefault(a => a.VictimId == victimId && a.Lever == ExtortionLever.Secret && a.Amount > 0 && !a.BargainBroken
                                        && today - a.Day <= ExtortionPolicy.DaysOfTheBargain);

      /// <summary>True when this affair was told: a secret is spent once.</summary>
      public bool IsExposed(string subjectId, string loverId) => Exposures.Any(e => e.SubjectId == subjectId && e.LoverId == loverId);

      /// <summary>The ledger as saved.</summary>
      public string Serialize() => JsonConvert.SerializeObject(this, Formatting.None);

      /// <summary>Reads a saved ledger; null, empty or broken input reads an empty ledger (an old save).</summary>
      public static ExtortionLedger Deserialize(string json)
      {
         if (string.IsNullOrWhiteSpace(json)) return new ExtortionLedger();

         try
         {
            ExtortionLedger ledger = JsonConvert.DeserializeObject<ExtortionLedger>(json) ?? new ExtortionLedger();
            ledger.Attempts = ledger.Attempts ?? new List<ExtortionRecord>();
            ledger.Exposures = ledger.Exposures ?? new List<ExposureRecord>();

            return ledger;
         }
         catch
         {
            return new ExtortionLedger();
         }
      }
   }
}
