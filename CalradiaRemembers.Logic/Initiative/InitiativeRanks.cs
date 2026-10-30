// Code written by Gabriel Mailhot, 28/10/2026.
// How each reason a lord has to seek the player out ranks with the world's initiative (Gabriel, 28/10/2026: coherent
// priorities; the war duty is one of his four that always pass). A matter that presses (a grudge, fresh news, a nemesis
// plaguing his lands, a patrol challenging the player on his realm's soil) comes before one that can wait (a kindness to
// acknowledge, an aim of his own).

namespace CalradiaRemembers.Logic.Initiative
{
   /// <summary>Who knocks and how it ranks, for a reason a system already has.</summary>
   public static class InitiativeRanks
   {
      /// <summary>The family a lord's reason belongs to: his own aim is the aims' family, every other reason the lords'.</summary>
      public static InitiativeFamily FamilyOf(InterceptionTrigger trigger)
         => trigger == InterceptionTrigger.StandingAim ? InitiativeFamily.Aims : InitiativeFamily.Lords;

      /// <summary>
      ///   How a companion's reason to ask for a word ranks: one close to leaving (a grievance, retirement), a plot
      ///   uncovered, or news of an errand presses; counsel, a rumour, gratitude, affection, ambition can wait.
      /// </summary>
      public static InitiativeRank RankOf(NpcMemoryService.Core.Models.CompanionAudienceReason reason)
      {
         switch (reason)
         {
            case NpcMemoryService.Core.Models.CompanionAudienceReason.Grievance:
            case NpcMemoryService.Core.Models.CompanionAudienceReason.Retirement:
            case NpcMemoryService.Core.Models.CompanionAudienceReason.SchemeWarning:
            case NpcMemoryService.Core.Models.CompanionAudienceReason.MissionFollowUp:
               return InitiativeRank.Pressing;
            default:
               return InitiativeRank.Ordinary;
         }
      }

      /// <summary>
      ///   The family a letter belongs to: what the player's own household writes (a spouse, a lover, the mother of their
      ///   child, a divorce, a pension for a child) is the household's; every other letter the letters'. The household had
      ///   no source at all, so its weight in the MCM and its month's fair turn did nothing (found 29/10/2026).
      /// </summary>
      public static InitiativeFamily FamilyOf(NpcMemoryService.Core.Models.LetterReason reason)
      {
         switch (reason)
         {
            case NpcMemoryService.Core.Models.LetterReason.SpouseCorrespondence:
            case NpcMemoryService.Core.Models.LetterReason.RomanticCorrespondence:
            case NpcMemoryService.Core.Models.LetterReason.BirthAnnouncement:
            case NpcMemoryService.Core.Models.LetterReason.ChildSupportRequest:
            case NpcMemoryService.Core.Models.LetterReason.SpouseDivorceDemand:
            case NpcMemoryService.Core.Models.LetterReason.SpouseDivorceEscalation:
            case NpcMemoryService.Core.Models.LetterReason.BastardMotherNote:
               return InitiativeFamily.Household;
            default:
               return InitiativeFamily.Letters;
         }
      }

      /// <summary>How a lord's reason to seek the player ranks.</summary>
      public static InitiativeRank RankOf(InterceptionTrigger trigger)
      {
         switch (trigger)
         {
            case InterceptionTrigger.WarDutySummons: return InitiativeRank.Imposed;
            case InterceptionTrigger.Grudge:
            case InterceptionTrigger.HeardOfDeed:
            case InterceptionTrigger.NemesisBounty:
            case InterceptionTrigger.TerritorialPatrol: return InitiativeRank.Pressing;
            default: return InitiativeRank.Ordinary;
         }
      }
   }
}
