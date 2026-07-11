// Code written by Gabriel Mailhot, 11/07/2026.

#region

using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   One participant of a chat exchange: the main NPC or a present witness, with the hero, stored profile and
   ///   display colour used to route the turn and render the line. Promoted from a private nested type of
   ///   ChatViewModel so the round-table controller (a separate class) can pass these across the IRoundTableHost
   ///   seam. Behaviour is unchanged: it remains a plain internal data holder.
   /// </summary>
   internal sealed class ChatSpeaker
   {
      public string Color; // null → default NPC cream
      public Hero Hero;
      public bool IsMain;
      public string Name;
      public NpcProfile Profile;
   }
}
