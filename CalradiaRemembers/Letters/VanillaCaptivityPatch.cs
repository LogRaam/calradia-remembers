// Code written by Gabriel Mailhot, 14/06/2026.

#region

using System;
using System.Collections.Generic;
using HarmonyLib;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>
   ///   Suppresses the two vanilla events that cut a bandit captivity short before a
   ///   Calradia Remembers captive scene gets a chance to fire:
   ///   <list type="bullet">
   ///     <item>
   ///       the captor's ransom offer ("he proposes to free you in return for N gold") —
   ///       cancelled entirely; and
   ///     </item>
   ///     <item>
   ///       the spontaneous escape ("you find a chance and escape from your captors") —
   ///       re-rolled, so it still happens but far less often.
   ///     </item>
   ///   </list>
   ///   The native <c>PlayerCaptivityCampaignBehavior.CheckCaptivityChange</c> tick never applies
   ///   <c>EndCaptivityAction</c> itself — it only <see cref="GameMenu.SwitchToMenu" />es to the
   ///   relevant menu, and the captivity is ended inside that menu. So skipping the switch keeps
   ///   the player captive cleanly, with no native state half-applied. Gated to Hardcore — the
   ///   content level where CR captive scenes are active — and applies to ALL captors (bandit and
   ///   lord alike); lower content levels keep their full vanilla behavior.
   /// </summary>
   [HarmonyPatch(typeof(GameMenu), nameof(GameMenu.SwitchToMenu))]
   public static class VanillaCaptivityPatch
   {
      // Fraction of vanilla escape rolls to cancel. Each cancelled attempt simply defers to the
      // next captivity check, which re-rolls — escape stays possible, just rarer (~1 in 3 here).
      private const float EscapeSuppressChance = 0.70f;

      // The "dragged about as a prisoner, you escape" menus (wilderness + held in a settlement).
      private static readonly HashSet<string> EscapeMenus = new(StringComparer.Ordinal) {
         "menu_captivity_end_wilderness_escape",
         "menu_captivity_end_prison_escape"
      };

      // The "he proposes to free you for N gold" ransom-offer menus.
      private static readonly HashSet<string> RansomMenus = new(StringComparer.Ordinal) {
         "menu_captivity_end_propose_ransom_in_prison",
         "menu_captivity_end_propose_ransom_wilderness"
      };

      /// <summary>
      ///   Returns false (skips the native switch) to cancel a ransom offer outright, or to
      ///   cancel a given escape attempt on a successful re-roll. Everything else passes through.
      /// </summary>
      public static bool Prefix(string menuId)
      {
         if (string.IsNullOrEmpty(menuId)) return true;
         if (!EscapeMenus.Contains(menuId) && !RansomMenus.Contains(menuId)) return true;

         // Only extend captivity where CR captive scenes can actually use the extra time.
         if (ModSettings.AdultContentLevel != AdultContentLevel.Hardcore) return true;

         if (RansomMenus.Contains(menuId))
         {
            ModLog.Info($"[CAPTIVITY] vanilla ransom offer '{menuId}' suppressed — bandits do not bargain you free.");

            return false;
         }

         // A nemesis is too careful to let the player slip away by luck: fully suppress the vanilla drip, so
         // the only luck exit is the decaying scene-escape (you cannot simply wait a nemesis out).
         if (Nemesis.NemesisBehavior.Instance?.PlayerHeldByNemesis() == true)
         {
            ModLog.Info("[CAPTIVITY] vanilla escape suppressed — a nemesis does not let you slip away by chance.");

            return false;
         }

         if (!(MBRandom.RandomFloat < EscapeSuppressChance)) return true; // the escape stands this time
         ModLog.Info($"[CAPTIVITY] vanilla escape '{menuId}' re-rolled away — the player stays captive.");

         return false;
      }
   }
}