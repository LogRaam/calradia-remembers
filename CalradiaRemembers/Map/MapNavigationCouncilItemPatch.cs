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

            // Hold the bar so Tick can look again in a moment (see the field's own doc): "added" is not the
            // same claim as "still there", and until now the log only ever made the first one.
            _watched = new WeakReference(__instance);
            _ticksSinceAdd = 0;

            ModLog.Info($"[COUNCIL BAR] council nav item added at construction "
                      + $"(bar now holds {__instance.NavigationItems.Count} item(s)).");
         }
         catch (Exception ex) { ModLog.Error($"[COUNCIL BAR] failed to add the council nav item: {ex.GetType().Name}: {ex.Message}"); }
      }

      // The bar we last added to, held WEAKLY so this can never keep a dead view model alive, plus how many
      // ticks ago. mysticwolf92 (2026-09-09..12) loses the button depending on load order and named Retinues
      // as the cause; reflection over Retinues 1.3.14.31 shows it does not contain the strings MapNavigation,
      // MapBar, NavigationItems or INavigationElement at all, so it is not the culprit and guessing at the
      // next mod would be the same mistake again.
      //
      // What no log could answer is WHICH of three things happens to him: the item never added, added and then
      // removed by somebody else, or present and simply not drawn. Those need completely different fixes and
      // they look identical from the outside. Looking again a moment later separates the first two, and if the
      // item IS still there his answer is the third and the search moves to the sprite and the prefab.
      private static WeakReference _watched;
      private static int _ticksSinceAdd;

      /// <summary>
      ///   Checks ONCE, shortly after the bar was built, whether our item survived. Called from the
      ///   application tick. Silent when all is well, which is the common case by far.
      /// </summary>
      internal static void Tick()
      {
         if (_watched == null) return;

         // ~2 seconds at 60fps: long enough for every other mod's construction-time work to have run, short
         // enough that the line sits next to the add in the log rather than pages away from it.
         if (++_ticksSinceAdd < 120) return;

         try
         {
            var bar = _watched.Target as MapNavigationVM;
            _watched = null;

            if (bar?.NavigationItems == null) return; // the bar went away; nothing to report

            if (bar.NavigationItems.Any(i => i?.ItemId == "cr_council")) return; // survived, say nothing

            ModLog.Error("[COUNCIL BAR] our council item was added and is GONE from the bar "
                       + $"({bar.NavigationItems.Count} item(s) remain). Another mod rebuilt or cleared the "
                       + "navigation list after us. Use cr.council with no argument to convene meanwhile.");
         }
         catch (Exception ex)
         {
            _watched = null;
            ModLog.Error($"[COUNCIL BAR] survival check failed: {ex.GetType().Name}: {ex.Message}");
         }
      }
   }
}
