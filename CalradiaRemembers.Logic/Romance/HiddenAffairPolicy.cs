// Code written by Gabriel Mailhot, 01/11/2026.
// Hidden affairs between lords (Gabriel's "go", 01/11/2026). Until now the only affairs in Calradia touched the player, so the
// secret lever and expose_secret reached almost nobody. A married lord may now take a hidden lover: rarely, a handful in the
// realm at once, because a rare secret is worth something and a world where everyone cheats is not one Gabriel wants. It is a
// fact the game holds, not a simulation: no courtship and no scenes. It stays nothing to the player until a companion sent to
// spy on a lord brings it back, the one road he chose to begin with. Those in it know it, may keep silent or turn the question
// aside, and never lie about it. The figures are mine, to tune in play.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Companions;
using Newtonsoft.Json;

#endregion

namespace CalradiaRemembers.Logic.Romance
{
   /// <summary>One hidden affair the world holds.</summary>
   public sealed class HiddenAffair
   {
      /// <summary>The married one, who carries the lover on their profile.</summary>
      public string SubjectId { get; set; }

      /// <summary>Their lover.</summary>
      public string LoverId { get; set; }

      /// <summary>The day it began.</summary>
      public int Day { get; set; }
   }

   /// <summary>Every hidden affair standing. Saved as JSON bytes under one additive key; an old save reads an empty ledger.</summary>
   public sealed class HiddenAffairLedger
   {
      /// <summary>The affairs standing, oldest first.</summary>
      public List<HiddenAffair> Affairs { get; set; } = new List<HiddenAffair>();

      /// <summary>The affair this person is in, on either side, or null.</summary>
      public HiddenAffair Of(string heroId) => Affairs.FirstOrDefault(a => a.SubjectId == heroId || a.LoverId == heroId);

      /// <summary>The ledger as saved.</summary>
      public string Serialize() => JsonConvert.SerializeObject(this, Formatting.None);

      /// <summary>Reads a saved ledger; null, empty or broken input reads an empty ledger (an old save).</summary>
      public static HiddenAffairLedger Deserialize(string json)
      {
         if (string.IsNullOrWhiteSpace(json)) return new HiddenAffairLedger();

         try
         {
            HiddenAffairLedger ledger = JsonConvert.DeserializeObject<HiddenAffairLedger>(json);

            return ledger?.Affairs == null ? new HiddenAffairLedger() : ledger;
         }
         catch (JsonException)
         {
            return new HiddenAffairLedger();
         }
      }
   }

   /// <summary>What decides whether a lord may take a hidden lover.</summary>
   public sealed class HiddenAffairCandidate
   {
      /// <summary>An adult, wed to someone still in the world.</summary>
      public bool IsMarriedAdult { get; init; }

      /// <summary>They are the player, or wed to the player.</summary>
      public bool IsThePlayersOwn { get; init; }

      /// <summary>They are of the player's clan.</summary>
      public bool IsOfPlayerHousehold { get; init; }

      /// <summary>They already have a lover, or are in an affair on the other side.</summary>
      public bool HasLover { get; init; }

      /// <summary>They are the player's own secret lover.</summary>
      public bool IsPlayersLover { get; init; }

      /// <summary>Their Honor trait, -2 to 2.</summary>
      public int Honor { get; init; }

      /// <summary>What they feel for their spouse.</summary>
      public int FeelingForSpouse { get; init; }
   }

   /// <summary>What decides whether an affair is over.</summary>
   public sealed class HiddenAffairSpan
   {
      /// <summary>Both lovers are in the world.</summary>
      public bool BothInTheWorld { get; init; }

      /// <summary>The subject is still wed, and not to the player.</summary>
      public bool StillMarried { get; init; }

      /// <summary>The lover is still unwed.</summary>
      public bool LoverStillFree { get; init; }

      /// <summary>Days since it began.</summary>
      public int AgeDays { get; init; }
   }

   /// <summary>The names a report or a prompt is written with.</summary>
   public sealed class HiddenAffairNames
   {
      /// <summary>The married one.</summary>
      public string Subject { get; init; }

      /// <summary>Their lover.</summary>
      public string Lover { get; init; }

      /// <summary>The one betrayed.</summary>
      public string Spouse { get; init; }
   }

   /// <summary>What a companion's inquiry into a lord comes to.</summary>
   public enum AffairInquiry
   {
      /// <summary>It went too badly to say anything either way.</summary>
      NothingCertain,

      /// <summary>It went well, and there is nothing to find.</summary>
      NothingToFind,

      /// <summary>It went well, and the affair is found.</summary>
      Found
   }

   /// <summary>Decides who takes a lover, how many stand at once, when it ends and what an inquiry finds.</summary>
   public static class HiddenAffairPolicy
   {
      /// <summary>The most standing at once: a handful in the whole of Calradia.</summary>
      public const int MaxAtOnce = 5;

      /// <summary>How long one runs, about three years, so the handful turns over.</summary>
      public const int LongestDays = 252;

      /// <summary>The weekly chance of the one lord drawn, in percent.</summary>
      public const int WeeklyChancePercent = 6;

      /// <summary>Added by a dishonourable nature, and again by a cold marriage.</summary>
      public const int RestlessBonusPercent = 6;

      /// <summary>From here on Honor forbids it outright.</summary>
      public const int FaithfulHonor = 2;

      /// <summary>True when this lord could take a hidden lover at all.</summary>
      public static bool MayTake(HiddenAffairCandidate c)
         => c != null && c.IsMarriedAdult && !c.IsThePlayersOwn && !c.IsOfPlayerHousehold && !c.HasLover && !c.IsPlayersLover && c.Honor < FaithfulHonor;

      /// <summary>The weekly chance for this lord, in percent: their nature and their marriage decide it.</summary>
      public static int ChancePercent(HiddenAffairCandidate c)
      {
         if (!MayTake(c)) return 0;

         int chance = c.Honor > 0 ? WeeklyChancePercent / 2 : WeeklyChancePercent;
         if (c.Honor < 0) chance += RestlessBonusPercent;
         if (c.FeelingForSpouse < 0) chance += RestlessBonusPercent;

         return chance;
      }

      /// <summary>True when a new affair begins: there is room, and the roll (0 to 99) lands.</summary>
      public static bool Begins(int standing, int chancePercent, int roll) => standing < MaxAtOnce && roll < chancePercent;

      /// <summary>True when the affair is over: someone left the world, a marriage changed, or it ran its course.</summary>
      public static bool IsOver(HiddenAffairSpan s) => s == null || !s.BothInTheWorld || !s.StillMarried || !s.LoverStillFree || s.AgeDays >= LongestDays;

      /// <summary>What an inquiry comes to: only an errand that went well says anything, and it says what is true.</summary>
      public static AffairInquiry Inquire(MissionResult result, bool affairExists)
      {
         if (result != MissionResult.Success) return AffairInquiry.NothingCertain;

         return affairExists ? AffairInquiry.Found : AffairInquiry.NothingToFind;
      }

      /// <summary>The line the companion brings back about this lord, without its end stop: their reports are bullets.</summary>
      public static string ReportLine(AffairInquiry inquiry, string lord, HiddenAffairNames names)
      {
         switch (inquiry)
         {
            case AffairInquiry.Found: return $"{names.Subject} and {names.Lover} are lovers in secret, and {names.Spouse} does not know";
            case AffairInquiry.NothingToFind: return $"you found nothing hidden in the private life of {lord}";
            default: return $"you could learn nothing certain of the private life of {lord}";
         }
      }

      /// <summary>What one of the two lovers carries into any conversation: theirs to keep, never to lie about.</summary>
      public static string PromptBlock(string otherLover, string spouse)
         => $"YOUR HIDDEN AFFAIR:{Environment.NewLine}"
            + $"You and {otherLover} are lovers in secret, and {spouse} does not know.{Environment.NewLine}"
            + $"Never raise it. If you are asked, stay silent, turn the question aside or refuse to speak of it, but never deny it with a lie.{Environment.NewLine}{Environment.NewLine}";
   }
}
