// Code written by Gabriel Mailhot, 28/09/2026.
// Who deals with the player IN PERSON. Gabriel (28/09/2026), early in a campaign, landless and unknown: the Empress
// Rhagaea wrote to him in her own hand, and had ridden out herself to find him. "Je pense pas que l'impératrice prendrait
// le temps de m'écrire en personne." His ruling: a lord writes in person to equals and superiors, never down, except
// for love letters; a greater lord has the letter written for them, under a title with no name (a vassal only where
// that is more logical). The same rule keeps a greater lord from riding out in person to intercept the player.
//
// Rank is read from the game itself: a realm's ruler, and each clan's tier. What stays personal whatever the rank: a
// romance, a spouse or the family, a matter too private for a clerk (a threat, blackmail, a bribe), and a character who
// has come to hold the player dear (Regard.ConfidingTier), for whom friendship outranks station.

#region

using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Standing;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>What decides whether a character deals with the player in person.</summary>
   public sealed class CorrespondenceRankFacts
   {
      /// <summary>The character's standing: their clan's tier, and whether they rule.</summary>
      public Stature Sender { get; set; }

      /// <summary>The player's standing.</summary>
      public Stature Player { get; set; }

      /// <summary>A romance, a spouse, or the family: never left to a clerk.</summary>
      public bool PersonalBond { get; set; }

      /// <summary>The character's regard for the player, on the mod's ledger.</summary>
      public int Regard { get; set; }

      /// <summary>What the letter is about; null for a meeting on the road.</summary>
      public LetterReason? Reason { get; set; }
   }

   /// <summary>The rule of rank in correspondence and meetings. Pure.</summary>
   public static class CorrespondenceRankPolicy
   {
      /// <summary>TUNE: a clan this many tiers above the player's deals with the player through someone else.</summary>
      public const int TierGapForAnIntermediary = 2;

      /// <summary>TUNE: the clan tier from which a ruler deals with the player in person (a great vassal).</summary>
      public const int TierARulerAddressesInPerson = Stature.GreatHouseTier;

      /// <summary>Whether the character writes to (or rides out to meet) the player in person, rather than through someone.</summary>
      public static bool DealsInPerson(CorrespondenceRankFacts f)
      {
         if (f == null) return true;
         if (f.PersonalBond || f.Regard >= Regard.ConfidingTier || f.Player.IsRuler) return true;
         if (f.Reason.HasValue && IsPrivateMatter(f.Reason.Value)) return true;
         if (f.Sender.IsRuler) return f.Player.ClanTier >= TierARulerAddressesInPerson;

         return f.Sender.TiersAbove(f.Player) < TierGapForAnIntermediary;
      }

      /// <summary>Letters never handed to a clerk: love, marriage and family, and what is too private to dictate.</summary>
      public static bool IsPrivateMatter(LetterReason reason)
      {
         switch (reason)
         {
            case LetterReason.RomanticCorrespondence:
            case LetterReason.SpouseCorrespondence:
            case LetterReason.AwaitingReply:
            case LetterReason.MarriageProposal:
            case LetterReason.BirthAnnouncement:
            case LetterReason.ChildSupportRequest:
            case LetterReason.BastardMotherNote:
            case LetterReason.JealousThreat:
            case LetterReason.SpouseDivorceDemand:
            case LetterReason.SpouseDivorceEscalation:
            case LetterReason.Blackmail:
            case LetterReason.CorruptionAttempt:
               return true;
            default:
               return false;
         }
      }

      /// <summary>
      ///   The title a letter written on the character's behalf is signed with, with no invented name: "the chancellery
      ///   of Her Majesty Rhagaea" for a ruler, "the steward of Lord Lucon" otherwise.
      /// </summary>
      public static string ScribeTitle(string senderName, bool senderIsRuler, bool senderIsFemale)
      {
         string name = string.IsNullOrWhiteSpace(senderName) ? "their lord" : senderName.Trim();

         return senderIsRuler
            ? $"the chancellery of {(senderIsFemale ? "Her" : "His")} Majesty {name}"
            : $"the steward of {(senderIsFemale ? "Lady" : "Lord")} {name}";
      }
   }
}
