// Code written by Gabriel Mailhot, 01/11/2026.
// The calls for help in progress (DepaHN, Nexus, 27/10/2026), kept apart from the letters: the game holds the player to a
// plea by what happens in the world, so a case outlives the letter that opened it, read, unread or thrown away. Saved as JSON
// bytes under one additive key, as the war duty's ledger is; an old save reads an empty ledger.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

#endregion

namespace CalradiaRemembers.Logic.HelpPleas
{
   /// <summary>One call for help, from the day it was sealed to the day its crisis closed.</summary>
   public sealed class HelpPlea
   {
      /// <summary>Stable id, the journal entry's own once the player has promised.</summary>
      public string Id { get; set; }

      /// <summary>The hero who wrote.</summary>
      public string SenderId { get; set; }

      /// <summary>The house the crisis struck.</summary>
      public string ClanId { get; set; }

      /// <summary>The crisis, for the words.</summary>
      public HelpCrisis Crisis { get; set; }

      /// <summary>What is asked.</summary>
      public HelpAsk Ask { get; set; }

      /// <summary>The place at stake, or null for a plea that has none.</summary>
      public string PlaceId { get; set; }

      /// <summary>The place's name when the letter was sealed.</summary>
      public string PlaceName { get; set; }

      /// <summary>The sum or the count asked.</summary>
      public int Amount { get; set; }

      /// <summary>The day the letter was sealed.</summary>
      public int OpenedOnDay { get; set; }

      /// <summary>The day the letter reached the player, or null while it is on the road.</summary>
      public int? ReceivedOnDay { get; set; }

      /// <summary>The last day of the plea, set when the letter arrives.</summary>
      public int? DeadlineDay { get; set; }

      /// <summary>What the player answered.</summary>
      public HelpAnswer Answer { get; set; }

      /// <summary>What stood in their way when they answered.</summary>
      public HelpIncapacity IncapacityWhenAnswered { get; set; }

      /// <summary>How far the player was from the place when they promised, in map units.</summary>
      public float DistanceWhenPromised { get; set; }
   }

   /// <summary>A last letter waiting for its writer to be free to send it: a fallen lord is often a prisoner.</summary>
   public sealed class HelpReproach
   {
      /// <summary>The hero who will write.</summary>
      public string SenderId { get; set; }

      /// <summary>What they are told to write about.</summary>
      public string Context { get; set; }

      /// <summary>The day the place fell.</summary>
      public int OnDay { get; set; }
   }

   /// <summary>Every plea open, and what keeps the next one from coming too soon.</summary>
   public sealed class HelpPleaLedger
   {
      /// <summary>The pleas not yet closed.</summary>
      public List<HelpPlea> Open { get; set; } = new List<HelpPlea>();

      /// <summary>The day each writer last asked.</summary>
      public Dictionary<string, int> LastPleaDay { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);

      /// <summary>The crises that have had their plea, with the day, so one crisis asks once.</summary>
      public Dictionary<string, int> PleadedCrises { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);

      /// <summary>The last letters not yet sent.</summary>
      public List<HelpReproach> Reproaches { get; set; } = new List<HelpReproach>();

      /// <summary>The next number for an id.</summary>
      public int NextId { get; set; } = 1;

      /// <summary>The writer's open plea, or null.</summary>
      public HelpPlea From(string senderId) => Open.FirstOrDefault(p => string.Equals(p.SenderId, senderId, StringComparison.Ordinal));

      /// <summary>Records a new plea for a crisis.</summary>
      public HelpPlea Add(HelpPlea plea, string crisisKey, int today)
      {
         plea.Id = $"cr_help_plea_{NextId++}";
         plea.OpenedOnDay = today;
         Open.Add(plea);
         LastPleaDay[plea.SenderId] = today;
         PleadedCrises[crisisKey] = today;

         return plea;
      }

      /// <summary>The letter arrived: the player's time starts now, so the courier's road eats none of it.</summary>
      public void MarkReceived(HelpPlea plea, int today)
      {
         plea.ReceivedOnDay = today;
         plea.DeadlineDay = today + (plea.Ask == HelpAsk.Relief ? HelpPleaPolicy.DaysToReachTheSiege : HelpPleaPolicy.DaysToAnswer);
      }

      /// <summary>Forgets crises too old to come back, so the ledger does not grow with the campaign.</summary>
      public void Prune(int today)
      {
         foreach (string key in PleadedCrises.Where(c => today - c.Value > HelpPleaPolicy.DaysBetweenPleas * 2).Select(c => c.Key).ToList())
            PleadedCrises.Remove(key);
      }

      /// <summary>The ledger as saved.</summary>
      public string Serialize() => JsonConvert.SerializeObject(this, Formatting.None);

      /// <summary>Reads a saved ledger; null, empty or broken input reads an empty ledger (an old save).</summary>
      public static HelpPleaLedger Deserialize(string json)
      {
         if (string.IsNullOrWhiteSpace(json)) return new HelpPleaLedger();

         try
         {
            HelpPleaLedger ledger = JsonConvert.DeserializeObject<HelpPleaLedger>(json) ?? new HelpPleaLedger();
            ledger.Open = ledger.Open ?? new List<HelpPlea>();
            ledger.Reproaches = ledger.Reproaches ?? new List<HelpReproach>();
            ledger.LastPleaDay = new Dictionary<string, int>(ledger.LastPleaDay ?? new Dictionary<string, int>(), StringComparer.Ordinal);
            ledger.PleadedCrises = new Dictionary<string, int>(ledger.PleadedCrises ?? new Dictionary<string, int>(), StringComparer.Ordinal);

            return ledger;
         }
         catch
         {
            return new HelpPleaLedger();
         }
      }
   }
}
