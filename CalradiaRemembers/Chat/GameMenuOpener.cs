// Code written by Gabriel Mailhot, 07/07/2026.
// Reproduces what pressing ESC on the campaign map does (opening the game's escape/options menu) so an in-chat
// button can reach the vanilla Options and the MCM mod options WITHOUT ending the conversation. The chat is a
// keyboard-focus layer, so it necessarily swallows the ESC key (it needs the keyboard to type); rather than
// fight the input system we invoke the very same call the map's own escape command makes:
// INavigationHandler.OpenEscapeMenu(). Fully reflective and guarded, so a future game version simply leaves the
// button inert instead of throwing.

#region

using System;
using System.Collections.Generic;
using System.Reflection;

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
      ///   Invokes <c>INavigationHandler.OpenEscapeMenu()</c> on the screen the chat actually sits over, so the
      ///   menu opens on the VISIBLE screen and the focus arbiter can slide the chat aside to reveal it. Tries
      ///   the chat's own host screen first (the War Sails naval map, or the land map), then the current top
      ///   screen, then the land <c>MapScreen.Instance</c>. The old fixed <c>MapScreen.Instance</c> target opened
      ///   the menu on the hidden land map when the chat was over the naval map, so the log said "opened" yet no
      ///   menu appeared (player report). Returns false as a graceful no-op when no reachable screen carries a
      ///   handler.
      /// </summary>
      public static bool TryOpenEscapeMenu(object hostScreen = null)
      {
         try
         {
            Type navIface = ResolveType("TaleWorlds.CampaignSystem.INavigationHandler");
            if (navIface == null) return false;

            MethodInfo open = FindOpenEscapeMenu();
            if (open == null)
            {
               ModLog.Error("[MENU] MapNavigationExtensions.OpenEscapeMenu(INavigationHandler) not found on this game version.");

               return false;
            }

            object topScreen = ReflectTopScreen();
            object mapScreen = ReflectMapScreenInstance();
            ModLog.Info($"[MENU] Options clicked. hostScreen={TypeName(hostScreen)}, topScreen={TypeName(topScreen)}, MapScreen.Instance={TypeName(mapScreen)}.");

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

            ModLog.Info("[MENU] no screen with an INavigationHandler among host/top/map; cannot open the escape menu.");

            return false;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MENU] TryOpenEscapeMenu failed: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }

      #region private

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
