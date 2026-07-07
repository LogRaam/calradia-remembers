// Code written by Gabriel Mailhot, 07/07/2026.

#region

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

#endregion

namespace CalradiaRemembers.Chat
{
    /// <summary>
    ///   A plain ScrollablePanel for the shared-memories list. A distinct widget tag so the prefab resolves it
    ///   from this assembly, with NONE of <see cref="ChatScrollablePanel" />'s message-list behaviour (no
    ///   ActiveInstance, no scroll-to-bottom pump): a memory panel is read top-down like a journal, not tailed
    ///   like a chat. Same technique as <see cref="ComposeScrollablePanel" />.
    /// </summary>
    public class MemoryScrollablePanel : ScrollablePanel
    {
        public MemoryScrollablePanel(UIContext context) : base(context)
        {
        }
    }
}
