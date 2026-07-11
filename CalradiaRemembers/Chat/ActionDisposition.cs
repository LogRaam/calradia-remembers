// Code written by Gabriel Mailhot, 11/07/2026.

namespace CalradiaRemembers.Chat
{
   /// <summary>What the action loop should do with a coin/commit action after the confirmation controller preprocesses it.</summary>
   internal enum ActionDisposition
   {
      /// <summary>Run it now through the bridge, as the loop normally would (ordinary actions and give_gold).</summary>
      ExecuteInline,

      /// <summary>It was queued for the player's in-chat confirmation; the loop must not run it.</summary>
      Queued,

      /// <summary>It is a repeat of an already-resolved action this conversation; the loop must drop it.</summary>
      Skip
   }
}
