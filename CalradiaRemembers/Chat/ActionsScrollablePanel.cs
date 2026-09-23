// Code written by Gabriel Mailhot, 22/09/2026.

#region

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   A plain ScrollablePanel for the actions list. A distinct widget tag so the prefab resolves it from this
   ///   assembly, with none of <see cref="ChatScrollablePanel" />'s message-list behaviour: this list is read
   ///   top-down, like <see cref="MemoryScrollablePanel" />, never tailed like a chat.
   /// </summary>
   public class ActionsScrollablePanel : ScrollablePanel
   {
      public ActionsScrollablePanel(UIContext context) : base(context)
      {
      }
   }
}
