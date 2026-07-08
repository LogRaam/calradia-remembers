// Code written by Gabriel Mailhot, 07/07/2026.
// Reproduces what pressing ESC on the campaign map does (opening the game's escape/options menu) so an in-chat
// button can reach the vanilla Options and the MCM mod options WITHOUT ending the conversation. The chat is a
// keyboard-focus layer, so it necessarily swallows the ESC key (it needs the keyboard to type); rather than
// fight the input system we invoke the very same call the map's own escape command makes:
// MapScreen.Instance's INavigationHandler.OpenEscapeMenu(). Fully reflective and guarded, so a future game
// version simply leaves the button inert instead of throwing.

#region

using System;
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
      ///   Invokes <c>INavigationHandler.OpenEscapeMenu()</c> on the live map-screen handler. Returns false as a
      ///   graceful no-op when the map screen or the handler cannot be reached (e.g. a chat hosted over a mission
      ///   scene rather than the campaign map, where MapScreen.Instance is not the right target).
      /// </summary>
      public static bool TryOpenEscapeMenu()
      {
         try
         {
            Type mapScreenType = ResolveType("SandBox.View.Map.MapScreen");
            object mapScreen = mapScreenType?
               .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (mapScreen == null)
            {
               ModLog.Info("[MENU] no MapScreen.Instance to open the escape menu (chat not over the map?).");

               return false;
            }

            Type navIface = ResolveType("TaleWorlds.CampaignSystem.INavigationHandler");
            if (navIface == null) return false;

            object handler = FindMemberImplementing(mapScreen, navIface);
            if (handler == null)
            {
               ModLog.Info("[MENU] no INavigationHandler on the map screen; cannot open the escape menu.");

               return false;
            }

            // OpenEscapeMenu is a STATIC extension method on MapNavigationExtensions, taking the handler as its
            // argument (the interface itself only exposes navigation elements). This is the exact call the map's
            // own escape command makes.
            Type ext = ResolveType("TaleWorlds.CampaignSystem.MapNavigationExtensions");
            MethodInfo open = null;
            if (ext != null)
               foreach (MethodInfo m in ext.GetMethods(BindingFlags.Public | BindingFlags.Static))
                  if (m.Name == "OpenEscapeMenu" && m.GetParameters().Length == 1)
                  {
                     open = m;

                     break;
                  }

            if (open == null)
            {
               ModLog.Error("[MENU] MapNavigationExtensions.OpenEscapeMenu(INavigationHandler) not found on this game version.");

               return false;
            }

            open.Invoke(null, new[] {handler});
            ModLog.Info("[MENU] opened the escape/options menu from the chat (the same call ESC makes).");

            return true;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MENU] TryOpenEscapeMenu failed: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }

      #region private

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
