// Code written by Gabriel Mailhot, 11/07/2026.

#region

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

#endregion

namespace CalradiaRemembers.Chat
{
    /// <summary>
    ///   A plain ScrollablePanel for the portrait panel's roleplay dossier (facts, background, backstory,
    ///   appearance, discoveries, grievances, then the portrait at the bottom). A distinct widget tag so the
    ///   prefab resolves it from this assembly, with none of <see cref="ChatScrollablePanel" />'s message-list
    ///   behaviour: the dossier is read top-down, not tailed like a chat. Mirrors <see cref="MemoryScrollablePanel" />.
    /// </summary>
    public class PortraitScrollablePanel : ScrollablePanel
    {
        public PortraitScrollablePanel(UIContext context) : base(context)
        {
        }
    }
}
