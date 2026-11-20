// Code written by Gabriel Mailhot, 20/11/2026.
// Extension Surface: the Balance of Power screen's painted buttons. The Bellum Civile bridge (Discord, 20/11/2026) asked to
// open its own diplomacy and world-events screens from the Diplomacy and Rumors & Events buttons, which said "Available
// Soon", "without Harmony-patching your UI", the old popup staying when nothing is registered. Gabriel: "oui vas-y".
// Since 2.8.17 (CrApi.Version 5).

#region

using System;
using CalradiaRemembers.WorldAlmanac;

#endregion

namespace CalradiaRemembers
{
   /// <summary>A painted button of the Balance of Power screen that another mod may put its own screen behind. Since 2.8.17.</summary>
   public enum BalanceOfPowerButton
   {
      /// <summary>Diplomacy.</summary>
      Diplomacy,

      /// <summary>History.</summary>
      History,

      /// <summary>Rumors &amp; Events.</summary>
      RumorsAndEvents
   }

   /// <summary>The door through which another mod opens its own screen from a Balance of Power button. Since 2.8.17.</summary>
   public static class CrBalanceOfPower
   {
      /// <summary>
      ///   Puts <paramref name="open" /> behind <paramref name="button" /> for <paramref name="sourceMod" />. When the player
      ///   clicks it, CR closes the Balance of Power screen and calls <paramref name="open" /> on the game thread once the map
      ///   is back, so your screen is never stacked over CR's. The first mod to claim a button keeps it; the same mod may
      ///   claim it again to replace its screen. True when the button is yours now; false when another mod holds it, or the
      ///   name or the action is missing. A button nobody holds keeps CR's "Available Soon". Game thread.
      /// </summary>
      public static bool RegisterButton(BalanceOfPowerButton button, string sourceMod, Action open) => BalanceOfPowerButtons.Register(Slot(button), sourceMod, open);

      /// <summary>Gives <paramref name="button" /> back to "Available Soon". Only the mod holding it may. Whether it was released.</summary>
      public static bool UnregisterButton(BalanceOfPowerButton button, string sourceMod) => BalanceOfPowerButtons.Unregister(Slot(button), sourceMod);

      /// <summary>The mod holding <paramref name="button" />, or null when it still says "Available Soon".</summary>
      public static string HolderOf(BalanceOfPowerButton button) => BalanceOfPowerButtons.HolderOf(Slot(button));

      /// <summary>cr.test all seam: the button's click, as the screen makes it. Whether a mod's screen opens.</summary>
      internal static bool TryOpen(BalanceOfPowerButton button) => BalanceOfPowerButtons.TryOpen(Slot(button));

      private static BalanceOfPowerSlot Slot(BalanceOfPowerButton button)
      {
         switch (button)
         {
            case BalanceOfPowerButton.Diplomacy: return BalanceOfPowerSlot.Diplomacy;
            case BalanceOfPowerButton.History: return BalanceOfPowerSlot.History;
            default: return BalanceOfPowerSlot.RumorsAndEvents;
         }
      }
   }
}
