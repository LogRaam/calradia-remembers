// Code written by Gabriel Mailhot, 07/07/2026.
// Reproduces what pressing ESC on the campaign map does (opening the game's escape/options menu) so an in-chat
// button can reach the vanilla Options and the MCM mod options WITHOUT ending the conversation. The chat is a
// keyboard-focus layer, so it necessarily swallows the ESC key (it needs the keyboard to type); rather than
// fight the input system we invoke the very same call the map's own escape command makes:
// INavigationHandler.OpenEscapeMenu(). Fully reflective and guarded, so a future game version simply leaves the
// button inert instead of throwing.

#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CalradiaRemembers.Foundation;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   Opens the vanilla escape/options menu over the chat, exactly as ESC does on the map. The chat's focus
   ///   arbiter (<see cref="ChatManager.ArbitrateFocus" />) then yields and slides the chat aside, just as it
   ///   already does for the encyclopedia, and reclaims focus when the menu closes.
   /// </summary>
   internal static class GameMenuOpener
   {
      /// <summary>
      ///   Opens the escape/options menu on the screen the player is ACTUALLY looking at. Two different engine
      ///   mechanisms answer to ESC, and picking the wrong one opens a real menu on a screen nobody can see:
      ///   <list type="bullet">
      ///     <item>In a MISSION (a settlement scene, a conversation, a battle), ESC is handled by a mission view,
      ///       <c>MissionEscapeMenuView.OnEscape()</c>. The map still exists BEHIND the mission.</item>
      ///     <item>On the map, ESC goes through <c>INavigationHandler.OpenEscapeMenu()</c>.</item>
      ///   </list>
      ///   The mission path is tried first whenever a mission is running, because a mission always sits on top of
      ///   the map. Skipping it is exactly what broke for a player still reporting no menu in v1.30.14: his chat
      ///   was hosted by a MissionScreen (which carries no navigation handler), so we fell through to the naval
      ///   map behind it and opened the menu there, under the scene. The log dutifully said "opened", and he saw
      ///   nothing. Returns false as a graceful no-op when neither mechanism is reachable.
      /// </summary>
      public static bool TryOpenEscapeMenu(object hostScreen = null)
      {
         try
         {
            object topScreen = ReflectTopScreen();
            object mapScreen = ReflectMapScreenInstance();
            ModLog.Info($"[MENU] Options clicked. hostScreen={TypeName(hostScreen)}, topScreen={TypeName(topScreen)}, " +
                        $"MapScreen.Instance={TypeName(mapScreen)}, inMission={ReflectCurrentMission() != null}.");

            // A mission is always drawn OVER the map, so when one is running it is the only screen the player can
            // see, and the only one whose escape menu would be visible.
            if (TryOpenMissionEscapeMenu()) return true;

            Type navIface = ResolveType("TaleWorlds.CampaignSystem.INavigationHandler");
            if (navIface == null) return false;

            MethodInfo open = FindOpenEscapeMenu();
            if (open == null)
            {
               ModLog.Error("[MENU] MapNavigationExtensions.OpenEscapeMenu(INavigationHandler) not found on this game version.");

               return false;
            }

            // The screen the chat sits over comes first (naval or land, whichever is visible), so the menu opens
            // on the same screen the chat is on and the arbiter's layer-count check sees the new layer.
            foreach (object screen in DistinctNonNull(hostScreen, topScreen, mapScreen))
            {
               object handler = FindMemberImplementing(screen, navIface);
               if (handler == null) continue;

               open.Invoke(null, new[] {handler});
               ModLog.Info($"[MENU] opened the escape/options menu via {TypeName(screen)} (handler {TypeName(handler)}), the same call ESC makes.");

               return true;
            }

            ModLog.Info("[MENU] no screen with an INavigationHandler among host/top/map, and no mission escape view; cannot open the escape menu.");

            return false;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MENU] TryOpenEscapeMenu failed: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }

      /// <summary>
      ///   Whether each of the two ESC mechanisms still RESOLVES on the running game version, and which one is
      ///   live right now. Everything in this class is reflection, so a TaleWorlds rename would not throw: the
      ///   button would simply go quiet, which is precisely the failure a player spent two versions reporting.
      ///   The self-test (cr.test menu) reads this so the mod says so out loud instead.
      /// </summary>
      internal static (bool MissionMechanism, bool MapMechanism, bool MissionRunning, bool EscapeViewPresent, string Detail) Probe()
      {
         Type escapeViewType = ResolveType("TaleWorlds.MountAndBlade.View.MissionViews.MissionEscapeMenuView");
         bool mapMechanism = ResolveType("TaleWorlds.CampaignSystem.INavigationHandler") != null && FindOpenEscapeMenu() != null;
         object mission = ReflectCurrentMission();

         var escapeViewPresent = false;
         var onEscapeFound = false;

         if (mission != null && escapeViewType != null
             && mission.GetType().GetProperty("MissionBehaviors", BindingFlags.Public | BindingFlags.Instance)?
                .GetValue(mission) is IEnumerable behaviors)
            foreach (object behavior in behaviors)
            {
               if (behavior == null || !escapeViewType.IsInstanceOfType(behavior)) continue;

               escapeViewPresent = true;
               onEscapeFound = behavior.GetType().GetMethod(
                  "OnEscape", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null) != null;

               break;
            }

         string detail = mission == null
            ? $"no mission running, so the map path would be used (map mechanism {(mapMechanism ? "found" : "MISSING")})"
            : $"a mission is running, so the mission path would be used (escape view {(escapeViewPresent ? "present" : "ABSENT")}, " +
              $"OnEscape() {(onEscapeFound ? "found" : "MISSING")})";

         return (escapeViewType != null, mapMechanism, mission != null, escapeViewPresent, detail);
      }

      #region private

      /// <summary>
      ///   The mission's own escape menu, which is what ESC opens while a scene is loaded. The view is reachable
      ///   without touching a single private field: a <c>MissionView</c> IS a <c>MissionBehavior</c>, so it sits
      ///   in the public <c>Mission.MissionBehaviors</c> list, and <c>OnEscape()</c> is public on it. False (and
      ///   silent) when no mission is running, which is the ordinary map case and not a failure.
      /// </summary>
      private static bool TryOpenMissionEscapeMenu()
      {
         object mission = ReflectCurrentMission();
         if (mission == null) return false;

         Type escapeViewType = ResolveType("TaleWorlds.MountAndBlade.View.MissionViews.MissionEscapeMenuView");
         if (escapeViewType == null) return false;

         if (mission.GetType().GetProperty("MissionBehaviors", BindingFlags.Public | BindingFlags.Instance)?
                .GetValue(mission) is not IEnumerable behaviors)
            return false;

         foreach (object behavior in behaviors)
         {
            if (behavior == null || !escapeViewType.IsInstanceOfType(behavior)) continue;

            // OnEscape TOGGLES. If the menu is already up (a double click, or ESC reached the game after all),
            // calling it again would slam shut the very menu the player asked for.
            object isActive = escapeViewType
               .GetProperty("IsActive", BindingFlags.Public | BindingFlags.Instance)?.GetValue(behavior);

            if (isActive is true)
            {
               ModLog.Info($"[MENU] the mission escape menu ({TypeName(behavior)}) is already open; left it alone.");

               return true;
            }

            MethodInfo onEscape = behavior.GetType().GetMethod(
               "OnEscape", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);

            if (onEscape == null) continue;

            // The mission may legitimately refuse (a cutscene, a deployment stage): believe its answer rather
            // than reporting a menu the player cannot see, and let the caller fall through to the map path.
            var opened = onEscape.Invoke(behavior, null) as bool?;

            if (opened == false)
            {
               ModLog.Info($"[MENU] {TypeName(behavior)}.OnEscape() declined to open (the mission is refusing it right now).");

               continue;
            }

            ModLog.Info($"[MENU] opened the escape/options menu via the MISSION view {TypeName(behavior)}, the same call ESC makes in a scene.");

            return true;
         }

         ModLog.Info("[MENU] a mission is running but carries no MissionEscapeMenuView; falling back to the map's navigation handler.");

         return false;
      }

      /// <summary>The running <c>Mission.Current</c>, or null on the bare campaign map.</summary>
      private static object ReflectCurrentMission()
         => ResolveType("TaleWorlds.MountAndBlade.Mission")?
            .GetProperty("Current", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);

      /// <summary>The static <c>MapNavigationExtensions.OpenEscapeMenu(INavigationHandler)</c> method, or null on an unknown game version.</summary>
      private static MethodInfo FindOpenEscapeMenu()
      {
         Type ext = ResolveType("TaleWorlds.CampaignSystem.MapNavigationExtensions");
         if (ext == null) return null;
         foreach (MethodInfo m in ext.GetMethods(BindingFlags.Public | BindingFlags.Static))
            if (m.Name == "OpenEscapeMenu" && m.GetParameters().Length == 1)
               return m;

         return null;
      }

      private static object ReflectMapScreenInstance()
         => ResolveType("SandBox.View.Map.MapScreen")?
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);

      private static object ReflectTopScreen()
         => ResolveType("TaleWorlds.ScreenSystem.ScreenManager")?
            .GetProperty("TopScreen", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);

      private static string TypeName(object o) => o?.GetType().Name ?? "null";

      /// <summary>The given candidates in order, skipping nulls and reference-duplicates.</summary>
      private static IEnumerable<object> DistinctNonNull(params object[] candidates)
      {
         var seen = new List<object>();
         foreach (object c in candidates)
            if (c != null && !seen.Contains(c))
            {
               seen.Add(c);

               yield return c;
            }
      }

      private static Type ResolveType(string fullName)
      {
         foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
         {
            Type t = a.GetType(fullName, false);
            if (t != null) return t;
         }

         return null;
      }

      /// <summary>The first instance-field value, across the object's type hierarchy, that implements <paramref name="iface" />.</summary>
      private static object FindMemberImplementing(object obj, Type iface)
      {
         if (obj == null) return null;
         for (Type t = obj.GetType(); t != null; t = t.BaseType)
            foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
               object v = f.GetValue(obj);
               if (v != null && iface.IsInstanceOfType(v)) return v;
            }

         return null;
      }

      #endregion
   }
}
