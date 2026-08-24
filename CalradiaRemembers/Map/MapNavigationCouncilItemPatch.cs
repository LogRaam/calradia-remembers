// Code written by Gabriel Mailhot, 23/08/2026.
// Adds the council button as a REAL item in the map's bottom navigation bar, so the bar lengthens to hold it and
// its end decoration is pushed along. It is added in the MapNavigationVM CONSTRUCTOR postfix, BEFORE the bar
// movie binds its item list: a runtime add after that bind is silently ignored by the bar (confirmed in game),
// so the item must exist when the list is first built to render at all. The constructor is targeted with its
// EXACT argument types so Harmony resolves it deterministically. Defensive and idempotent.

#region

using System;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;

#endregion

namespace CalradiaRemembers.Map
{
   /// <summary>Adds the council nav item to <see cref="MapNavigationVM.NavigationItems" /> at construction, before the bar binds.</summary>
   [HarmonyPatch(typeof(MapNavigationVM), MethodType.Constructor, new[] {typeof(INavigationHandler), typeof(Func<MapBarShortcuts>)})]
   internal static class MapNavigationCouncilItemPatch
   {
      private static void Postfix(MapNavigationVM __instance)
      {
         try
         {
            if (__instance?.NavigationItems == null) return;
            if (__instance.NavigationItems.Any(i => i?.ItemId == "cr_council")) return; // already added

            // Between Party and Quest (Gabriel 2026-08-23): insert just BEFORE the quest item rather than
            // appending at the end of the bar. Anchored on "quest", never on a fixed index, so NavalDLC's
            // manage_fleet (present or not) cannot shift the button to the wrong slot; appended if not found.
            int questIndex = -1;

            for (int i = 0; i < __instance.NavigationItems.Count; i++)
               if (__instance.NavigationItems[i]?.ItemId == "quest") { questIndex = i; break; }

            if (questIndex >= 0) __instance.NavigationItems.Insert(questIndex, new CouncilNavigationItemVM());
            else __instance.NavigationItems.Add(new CouncilNavigationItemVM());

            CouncilBarIconSprite.EnsureRegistered(); // the glyph's runtime sprite, rebuilt if a language change dropped it

            ModLog.Info("[COUNCIL BAR] council nav item added at construction.");
         }
         catch (Exception ex) { ModLog.Error($"[COUNCIL BAR] failed to add the council nav item: {ex.GetType().Name}: {ex.Message}"); }
      }
   }
}
