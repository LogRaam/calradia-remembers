// Code written by Gabriel Mailhot, 01/07/2026.

#region

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

#endregion

namespace CalradiaRemembers.Chat
{
    /// <summary>
    ///   A plain ScrollablePanel for the composer's multi-line input. A distinct widget tag so the prefab resolves
    ///   it from this assembly, with NONE of <see cref="ChatScrollablePanel" />'s message-list behaviour (no
    ///   ActiveInstance, no main-thread pump). It just gives an <c>EditableTextWidget</c> (HeightSizePolicy
    ///   CoverChildren, which grows as text wraps) a scrollable viewport — the technique the Editable Encyclopedia
    ///   mod uses for a real multi-line editable text box.
    /// </summary>
    public class ComposeScrollablePanel : ScrollablePanel
    {
        public ComposeScrollablePanel(UIContext context) : base(context)
        {
        }
    }
}
