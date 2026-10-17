// Code written by Gabriel Mailhot, 17/10/2026.
// Gabriel (17/10/2026): never "the player" in the game, the player's character by name; true of every text the game
// shows, logs aside. A stored memory, a grudge's cause, a model's summary or reply may still say "the player" (old
// saves, and prompts that call him so); each screen that shows such text passes it through here.

#region

using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Text on its way to the screen, with "the player" naming the player's character.</summary>
   public static class ShownText
   {
      /// <summary>The text with "the player" replaced by the main hero's name (unchanged with no campaign).</summary>
      public static string Of(string text) => PlayerNameText.ForDisplay(text, Hero.MainHero?.Name?.ToString());
   }
}
