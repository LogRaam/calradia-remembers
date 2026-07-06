// Code written by Gabriel Mailhot, 05/07/2026.
// Hosts the chat's GauntletLayer as a REAL screen on the ScreenManager stack (instead of an overlay layer on
// the live map). Pushing a screen makes the engine suspend the screen below — so the heavy War Sails naval map
// stops rendering (no render-texture leak) — and hands input/focus to any screen pushed on top of us (the
// encyclopedia, the ESC menu) natively, without the overlay's focus-arbitration hack.

#region

using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.ScreenSystem;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>A minimal screen that carries the pre-built chat layer and takes focus when pushed.</summary>
   internal sealed class ChatScreen : ScreenBase
   {
      private readonly GauntletLayer _layer;

      public ChatScreen(GauntletLayer layer)
      {
         _layer = layer;
      }

      protected override void OnInitialize()
      {
         base.OnInitialize();
         if (_layer == null) return;

         AddLayer(_layer);
         _layer.IsFocusLayer = true;
         ScreenManager.TrySetFocus(_layer);
      }
   }
}
