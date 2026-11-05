// Code written by Gabriel Mailhot, 04/11/2026.
// froggyluv (Nexus, 04/11/2026): "im getting alot of disgruntled Companions that are hallucinating some broken promise
// they swear happened and generally the other companions will jump in and all call me untrustworthy... these are things
// that never happened". The mood note told an unhappy companion "you may voice your grievance: what has soured you" and
// never said what had: the happiness score fell from known causes (idleness, a failed errand, a hearing refused) that
// were thrown away once counted, so the model supplied one, and a broken promise is the easiest to invent. NPCs never
// lie: this keeps the causes that lowered the score and hands the note only those (Gabriel, 04/11/2026: "oui").

#region

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

#endregion

namespace CalradiaRemembers.Logic.Companions
{
   /// <summary>One thing that lowered a companion's contentment.</summary>
   public sealed class GrievanceEntry
   {
      /// <summary>The campaign day it happened.</summary>
      public int Day { get; set; }

      /// <summary>The driver that moved the score.</summary>
      public HappinessDriver Driver { get; set; }

      /// <summary>The cause in words, said to the companion ("being refused a hearing..."); null reads the driver's own.</summary>
      public string Cause { get; set; }
   }

   /// <summary>What lowered each companion's contentment lately, by hero id. Saved under one additive key.</summary>
   public sealed class CompanionGrievanceLedger
   {
      /// <summary>How many causes are kept per companion; older ones fall away.</summary>
      public const int MaxKept = 8;

      /// <summary>The causes, oldest first, by companion id.</summary>
      public Dictionary<string, List<GrievanceEntry>> ByCompanion { get; set; } = new Dictionary<string, List<GrievanceEntry>>();

      /// <summary>Records a cause when the driver lowers contentment; anything else is ignored.</summary>
      public void Record(string companionId, GrievanceEntry entry)
      {
         if (string.IsNullOrEmpty(companionId) || entry == null || CompanionHappiness.DeltaFor(entry.Driver) >= 0) return;

         if (!ByCompanion.TryGetValue(companionId, out List<GrievanceEntry> list))
         {
            list = new List<GrievanceEntry>();
            ByCompanion[companionId] = list;
         }

         list.Add(entry);
         if (list.Count > MaxKept) list.RemoveRange(0, list.Count - MaxKept);
      }

      /// <summary>The companion's causes from the last <see cref="CompanionGrievancePolicy.WindowDays" /> days (never null).</summary>
      public IReadOnlyList<GrievanceEntry> Recent(string companionId, int today)
         => companionId != null && ByCompanion.TryGetValue(companionId, out List<GrievanceEntry> list)
            ? list.Where(e => today - e.Day <= CompanionGrievancePolicy.WindowDays).ToList()
            : new List<GrievanceEntry>();

      /// <summary>The ledger as saved.</summary>
      public string Serialize() => JsonConvert.SerializeObject(this, Formatting.None);

      /// <summary>Reads a saved ledger; null, empty or broken input reads an empty one (a save written before this).</summary>
      public static CompanionGrievanceLedger Deserialize(string json)
      {
         if (string.IsNullOrWhiteSpace(json)) return new CompanionGrievanceLedger();

         try
         {
            CompanionGrievanceLedger ledger = JsonConvert.DeserializeObject<CompanionGrievanceLedger>(json);

            return ledger?.ByCompanion == null ? new CompanionGrievanceLedger() : ledger;
         }
         catch (JsonException)
         {
            return new CompanionGrievanceLedger();
         }
      }
   }

   /// <summary>Says to an unhappy companion how they feel and, only from what was recorded, why.</summary>
   public static class CompanionGrievancePolicy
   {
      /// <summary>How far back a cause still counts, in days (the weekly drift has eased older ones).</summary>
      public const int WindowDays = 60;

      /// <summary>How many causes the note names, the heaviest first.</summary>
      public const int MaxNamed = 3;

      /// <summary>The words for a driver when its site gave none.</summary>
      public static string Phrase(HappinessDriver driver)
      {
         switch (driver)
         {
            case HappinessDriver.ErrandFailed:     return "an errand you ran for the player that went wrong";
            case HappinessDriver.Captured:         return "being taken prisoner on the player's business";
            case HappinessDriver.Idle:             return "being left idle, with nothing worth your skill to do";
            case HappinessDriver.KeptFromLovedOne: return "being kept far from the one you love";
            case HappinessDriver.Mistreated:       return "a slight from the player";
            case HappinessDriver.SharedDefeat:     return "defeats you fought in at the player's side";
            case HappinessDriver.LowMorale:        return "a sullen, hungry camp";
            default:                               return null;
         }
      }

      /// <summary>
      ///   The note for a companion's contentment, or null when they are content. Names only the recorded causes,
      ///   heaviest first; with none recorded it says the unease has no single cause.
      /// </summary>
      public static string MoodNote(int score, IReadOnlyList<GrievanceEntry> recent)
      {
         if (score >= 50) return null;

         string mood;
         if (CompanionHappiness.WantsToLeave(score))
            mood = "You are bitterly unhappy in the player's service and mean to LEAVE. You may say so plainly, or give them one last chance to make it right, but your patience is at its end.";
         else if (score < 25)
            mood = "You are disgruntled in the player's service and have begun to think of leaving. You may voice your grievance, and what it would take to keep you.";
         else
            mood = "You are restless in the player's service, your patience thinning. You may let a coolness or a small complaint show, without yet threatening to leave.";

         List<string> causes = (recent ?? new List<GrievanceEntry>())
                               .Select(e => (cause: e.Cause ?? Phrase(e.Driver), weight: -CompanionHappiness.DeltaFor(e.Driver)))
                               .Where(x => x.cause != null)
                               .GroupBy(x => x.cause)
                               .OrderByDescending(g => g.Sum(x => x.weight))
                               .Take(MaxNamed)
                               .Select(g => g.Key)
                               .ToList();

         string why = causes.Count == 0
            ? "No single wrong has soured you: it is a slow unease in their service, and if you speak of it you say so."
            : $"What has soured you, and only this: {string.Join("; ", causes)}.";

         return $"{mood} {why} Never claim a broken promise, a slight or any wrong not named here.";
      }
   }
}
