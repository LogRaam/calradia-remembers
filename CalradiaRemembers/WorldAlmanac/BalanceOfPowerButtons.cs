// Code written by Gabriel Mailhot, 20/11/2026.
// Who holds the Balance of Power buttons another mod may claim (CrBalanceOfPower, the Bellum Civile bridge, Discord
// 20/11/2026). Kept here, beside the screen, so the screen asks it without knowing the public door: the door forwards to it.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Extension;

#endregion

namespace CalradiaRemembers.WorldAlmanac
{
   /// <summary>A Balance of Power button another mod may hold (the public BalanceOfPowerButton, same order).</summary>
   internal enum BalanceOfPowerSlot
   {
      Diplomacy,
      History,
      RumorsAndEvents
   }

   /// <summary>The mods holding Balance of Power buttons, and the click.</summary>
   internal static class BalanceOfPowerButtons
   {
      private static readonly Dictionary<BalanceOfPowerSlot, (string mod, Action open)> Held = new Dictionary<BalanceOfPowerSlot, (string, Action)>();

      /// <summary>Gives the button to the claimant when ScreenButtonClaimPolicy allows; whether it is theirs now.</summary>
      internal static bool Register(BalanceOfPowerSlot slot, string sourceMod, Action open)
      {
         Held.TryGetValue(slot, out (string mod, Action open) held);
         ScreenButtonClaim claim = ScreenButtonClaimPolicy.Decide(new ScreenButtonClaimFacts {Holder = held.mod, Claimant = sourceMod, HasScreen = open != null});
         if (claim == ScreenButtonClaim.Refused)
         {
            ModLog.Info($"[API] Balance of Power button {slot} not given to '{sourceMod}': {(string.IsNullOrWhiteSpace(held.mod) ? "no mod name or no screen" : $"'{held.mod}' holds it")}.");

            return false;
         }

         Held[slot] = (sourceMod.Trim(), open);
         ModLog.Info($"[API] Balance of Power button {slot} {(claim == ScreenButtonClaim.Taken ? "given to" : "replaced by")} '{sourceMod.Trim()}'.");

         return true;
      }

      /// <summary>Gives the button back to "Available Soon" when the holder asks.</summary>
      internal static bool Unregister(BalanceOfPowerSlot slot, string sourceMod)
      {
         if (!Held.TryGetValue(slot, out (string mod, Action open) held) || !ScreenButtonClaimPolicy.MayRelease(held.mod, sourceMod)) return false;

         Held.Remove(slot);

         return true;
      }

      /// <summary>The mod holding the button, or null.</summary>
      internal static string HolderOf(BalanceOfPowerSlot slot) => Held.TryGetValue(slot, out (string mod, Action open) held) ? held.mod : null;

      /// <summary>
      ///   The click: when a mod holds the button, closes the Balance of Power screen and queues its screen for the map's
      ///   next frame (never stacked over CR's), and answers true; false leaves the caller to show "Available Soon".
      /// </summary>
      internal static bool TryOpen(BalanceOfPowerSlot slot)
      {
         if (!Held.TryGetValue(slot, out (string mod, Action open) held) || held.open == null) return false;

         BalanceOfPowerManager.Close();
         MainThreadDispatcher.EnqueueForCampaign(() =>
         {
            try
            {
               held.open();
            }
            catch (Exception ex)
            {
               ModLog.Error($"[API] '{held.mod}' Balance of Power {slot} screen threw {ex.GetType().Name}: {ex.Message}");
            }
         });

         return true;
      }
   }
}
