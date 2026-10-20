// Code written by Gabriel Mailhot, 20/10/2026.
// Extension Surface: another mod says who ended the player's mercenary contract. tashmetu (20/10/2026): "Mercenary
// Management's AI can dismiss the player's contract (MercenaryKingdomAiBehavior), and Calradia Remembers judges
// 'Abandoned', so every lord of that realm loses regard for the player being let go. Mercenary Management keeps why each
// contract ended [...]; only MercenaryLeft is the player's choice." The game reports a dismissal and a departure the same
// way (ChangeKingdomAction LeaveAsMercenary), so only the mod that ended the contract can say which it was. Gabriel approved
// a door on CR's side (20/10/2026).

#region

using CalradiaRemembers.WorldEvents;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers
{
   /// <summary>What another mod knows of the player's mercenary contracts.</summary>
   public static class CrMercenaries
   {
      /// <summary>
      ///   The employer, not the player, ended the player's mercenary contract with <paramref name="kingdom" />: dismissed the
      ///   company, its term ran out, the war it was hired for ended. Call it the same day the contract ends, before or after
      ///   the game reports it. The end is then never judged as the company abandoning a losing war, and the company taking
      ///   service with the realm's enemy afterwards is no betrayal. Do not call it when the player chose to leave. Since
      ///   2.7.19. Names no Calradia Remembers type.
      /// </summary>
      public static void ContractEndedByEmployer(Kingdom kingdom) => MercenaryReputationBehavior.Instance?.OnEndedByEmployer(kingdom);
   }
}
