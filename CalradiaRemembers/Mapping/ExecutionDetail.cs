// Code written by Gabriel Mailhot, 23/10/2026.
// Whether a death was an execution. The game marks a lord put to death from the prisoners screen "Executed", but one put
// to death in the popup right after a battle "ExecutionAfterMapEvent" (HeroExecutionSceneNotificationData, 1.4.8). Every
// listener compared against "Executed" alone, so a lord executed after the battle drew no vendetta from his kin, no
// chronicle line, and no Nemesis resolution as an execution (found 23/10/2026 while gathering the player's misdeeds).

#region

using TaleWorlds.CampaignSystem.Actions;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Reads the game's kill detail as an execution or not.</summary>
   internal static class ExecutionDetail
   {
      /// <summary>True for an execution, from the prisoners screen or right after a battle.</summary>
      internal static bool IsExecution(KillCharacterAction.KillCharacterActionDetail detail)
         => detail == KillCharacterAction.KillCharacterActionDetail.Executed || detail == KillCharacterAction.KillCharacterActionDetail.ExecutionAfterMapEvent;
   }
}
