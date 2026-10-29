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
