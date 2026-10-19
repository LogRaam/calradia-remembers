// Code written by Gabriel Mailhot, 23/08/2026.
// Adds the council button as a REAL item in the map's bottom navigation bar, so the bar lengthens to hold it and
// its end decoration is pushed along. It is added in the MapNavigationVM CONSTRUCTOR postfix, BEFORE the bar
// movie binds its item list: a runtime add after that bind is silently ignored by the bar (confirmed in game),
// so the item must exist when the list is first built to render at all. The constructor is targeted with its
// EXACT argument types so Harmony resolves it deterministically. Defensive and idempotent.

#region

using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using CalradiaRemembers.Foundation;

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

            // Compat, like the attachment and survival lines: the three of them are ONE diagnosis, and a
            // triage is only as good as its weakest channel. On Info this line vanishes for any player with
            // logging off - the Nexus default - and its absence then reads as "the constructor never ran"
            // when the truth may be "added, and taken from us afterwards". Two different causes, two
            // different fixes, told apart by whether one line survived the log settings.
            ModLog.Compat($"[COUNCIL BAR] council nav item added at construction "
                        + $"(bar now holds {__instance.NavigationItems.Count} item(s)).");
         }
         catch (Exception ex) { ModLog.Error($"[COUNCIL BAR] failed to add the council nav item: {ex.GetType().Name}: {ex.Message}"); }
      }

      // The bar we last added to, held WEAKLY so this can never keep a dead view model alive, plus how many
      // ticks ago. mysticwolf92 (2026-09-09..12) loses the button depending on load order and named Retinues
      // as the cause.
      //
      // WHAT WAS WRITTEN HERE BEFORE, AND WHY IT WAS WRONG TO WRITE IT THAT WAY. This comment used to say
      // Retinues "is not the culprit", from reflection over version 1.3.14.31, which genuinely contains none
      // of the strings MapNavigation, MapBar, NavigationItems or INavigationElement. That was a finding about
      // ONE BUILD stated as a conclusion about a MOD - and Retinues v2.0.0.12 (17/09/2026) carries an entire
      // Retinues.Editor.Integration.MapBar namespace, a MapNavigationHandlerCtorPatch on
      // SandBox.View.Map.Navigation.MapNavigationHandler, a TroopsNavigationElement of its own, and
      // EnsureInjected/IsMapBarIntegrated. It puts a button on this bar.
      //
      // It still does not patch what WE patch: MapNavigationVM does not appear in that assembly at all. The
      // two hook different types - we the view model, it the handler that feeds the view model - so this is
      // not a same-method collision. What it IS is a second mod writing to the same bar, which is exactly the
      // condition the check below exists to observe rather than to guess about.
      //
      // THE STRUCTURAL RISK IS OURS, and naming it matters more than naming a mod: our item lives only on the
      // VIEW MODEL, inserted after construction. It has no handler-level registration, so anything that
      // rebuilds the VM's list FROM the handler drops it and cannot restore it. A mod adding a handler
      // element is precisely the sort of thing that can cause such a rebuild.
      //
      // What no log could answer is WHICH of three things happens to him: the item never added, added and then
      // removed by somebody else, or present and simply not drawn. Those need completely different fixes and
      // they look identical from the outside. Looking again a moment later separates the first two, and if the
      // item IS still there his answer is the third and the search moves to the sprite and the prefab.
      private static WeakReference _watched;
      private static int _ticksSinceAdd;

      /// <summary>
      ///   Says at startup whether this patch actually ATTACHED to the constructor it names, on the channel
      ///   that stays on when logging is off.
      ///
      ///   Without it, one of the three ways a player loses the button is completely invisible. PatchAll
      ///   reports "Harmony patches applied" for the whole set, so a target that never resolved, or a bar
      ///   built from some other view model, leaves no trace at all — and silence is also what a player who
      ///   simply never opened the map produces. With this line the three cases separate cleanly in a log:
      ///
      ///     no "attached" line          the patch never applied; the search is load order and game version
      ///     attached, no "added" line   the constructor never ran; another mod supplies a different bar
      ///     added, then "GONE"          somebody rebuilt the list after us, and the line names what is left
      ///
      ///   Players report losing the button alongside Retinues (mysticwolf92, 2026-09-09..12, and others per
      ///   Gabriel on 17/09). That correlation is now well attested; the MECHANISM is not, and reflection over
      ///   Retinues v2.0.0.12 argues against the simple reading — it contains MapNavigationVM zero times and
      ///   set_NavigationItems zero times, so it never touches the list our item lives in. Which is exactly
      ///   why the answer has to come from a log rather than from another round of inference.
      /// </summary>
      internal static void ReportAttachment(Harmony harmony)
      {
         try
         {
            ConstructorInfo target = typeof(MapNavigationVM).GetConstructor(
               new[] {typeof(INavigationHandler), typeof(Func<MapBarShortcuts>)});

            if (target == null)
            {
               ModLog.Compat("[COUNCIL BAR] the map bar constructor this build expects was not found; the "
                           + "council button cannot be added. Use cr.council to convene.");

               return;
            }

            Patches info = Harmony.GetPatchInfo(target);
            bool ours = info?.Postfixes?.Any(p => p.owner == harmony.Id) == true;

            if (ours) ModLog.Compat("[COUNCIL BAR] council button patch attached to the map navigation bar.");
            else
               ModLog.Compat("[COUNCIL BAR] council button patch did NOT attach; the button will be absent. "
                           + "Use cr.council to convene.");
         }
         catch (Exception ex)
         {
            ModLog.Compat($"[COUNCIL BAR] could not verify the button patch: {ex.GetType().Name}: {ex.Message}");
         }
      }

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

            // NAME WHAT IS LEFT. "Another mod rebuilt the list" is what we already knew; WHICH mod is what a
            // player report cannot supply and this can. The surviving item ids identify the winner by its own
            // element - a Retinues TroopsNavigationElement, say - so the next report arrives as evidence
            // instead of a guess, and nobody has to reflect over another mod's assembly to start.
            string remaining = string.Join(", ", bar.NavigationItems
                                                   .Select(i => i?.ItemId)
                                                   .Where(id => !string.IsNullOrWhiteSpace(id)));

            ModLog.Compat("[COUNCIL BAR] our council item was added and is GONE from the bar "
                        + $"({bar.NavigationItems.Count} item(s) remain: {remaining}). Another mod rebuilt or "
                        + "cleared the navigation list after us. Use cr.council with no argument to convene "
                        + "meanwhile.");
         }
         catch (Exception ex)
         {
            _watched = null;
            ModLog.Error($"[COUNCIL BAR] survival check failed: {ex.GetType().Name}: {ex.Message}");
         }
      }
   }
}
