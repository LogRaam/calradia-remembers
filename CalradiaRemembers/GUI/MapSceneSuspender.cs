// Code written by Gabriel Mailhot, 05/07/2026.
// Spike: while the chat overlay is open, stop the host map screen's 3D scene (the heavy War Sails naval map)
// from rendering, so it no longer renders into an intermediate compositing target and leaks render-textures
// (the create_render_texture VRAM crash). Reflection on purpose: it DISCOVERS the real SceneLayer/SceneView
// enable API in-game (logged) instead of guessing a signature at compile time, and degrades to a logged no-op
// (never throws, never leaves the map suspended) if the API differs from every candidate.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.ScreenSystem;
using CalradiaRemembers.Foundation;

#endregion

namespace CalradiaRemembers.GUI
{
    /// <summary>
    ///   Suspends and resumes the host map screen's scene rendering around the chat's lifetime. One-shot per
    ///   chat: <see cref="Suspend" /> on open remembers exactly what it turned off; <see cref="Resume" /> on
    ///   close turns the same thing back on. A guard flag lets it be toggled off in-game (cr.mapsuspend) to
    ///   compare VRAM with and without the fix.
    /// </summary>
    public static class MapSceneSuspender
    {
        private static readonly string[] MethodCandidates = {"SetEnable", "SetEnabled", "SetActive", "SetIsActive", "SetVisible"};
        private static readonly string[] PropCandidates = {"IsEnabled", "Enabled", "IsActive", "Active", "IsVisible"};

        private static object _target; // the SceneView (or scene layer) we disabled
        private static string _method; // the method name that worked, or "prop:Name"
        private static bool _active;

        /// <summary>When false, <see cref="Suspend" /> is a no-op (the map keeps rendering). Default on = fix active.</summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>Stops the host screen's scene from rendering while the chat is open. Idempotent; never throws.</summary>
        public static void Suspend(ScreenBase host)
        {
            if (!Enabled || host == null || _active) return;

            // Only ever suspend a campaign MAP screen. If the chat is hosted over a conversation/mission scene
            // (the staged captive scene, a tavern talk), suspending that screen's SceneLayer would black out the
            // 3D characters we WANT to see — and it is not needed there (a mission already suspends the map).
            if (host.GetType().Name.IndexOf("MapScreen", StringComparison.OrdinalIgnoreCase) < 0)
            {
                ModLog.Info($"[MAPSUSPEND] host is {host.GetType().Name}, not a map screen — leaving its scene rendering.");

                return;
            }

            try
            {
                foreach (ScreenLayer layer in HostLayers(host))
                {
                    if (layer == null) continue;
                    if (layer.GetType().Name.IndexOf("SceneLayer", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    object view = GetProp(layer, "SceneView");

                    // Try the SceneView first (that is what actually renders), then the layer itself.
                    foreach (object target in new[] {view, layer})
                    {
                        if (target == null) continue;
                        if (TrySetRenderEnabled(target, false, out string how))
                        {
                            _target = target;
                            _method = how;
                            _active = true;
                            ModLog.Info($"[MAPSUSPEND] scene render suspended via {target.GetType().Name}.{how}(false).");

                            return;
                        }
                    }

                    // Nothing matched — log what IS available so the exact call can be wired next iteration.
                    LogCandidates("SceneLayer", layer);
                    LogCandidates("SceneView", view);
                }

                ModLog.Info("[MAPSUSPEND] no suspendable scene layer found on the host screen.");
            }
            catch (Exception ex)
            {
                ModLog.Error($"[MAPSUSPEND] Suspend failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>Restores the scene render turned off by <see cref="Suspend" />. Always clears state.</summary>
        public static void Resume()
        {
            if (!_active || _target == null)
            {
                _active = false;
                _target = null;

                return;
            }

            try
            {
                if (_method != null && _method.StartsWith("prop:"))
                    TrySetBoolProp(_target, _method.Substring(5), true);
                else
                    TryInvokeBool(_target, _method, true);
                ModLog.Info("[MAPSUSPEND] scene render resumed.");
            }
            catch (Exception ex)
            {
                ModLog.Error($"[MAPSUSPEND] Resume failed: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                _active = false;
                _target = null;
                _method = null;
            }
        }

        #region private

        private static IEnumerable<ScreenLayer> HostLayers(ScreenBase host)
        {
            try { return host.Layers ?? Enumerable.Empty<ScreenLayer>(); }
            catch { return Enumerable.Empty<ScreenLayer>(); }
        }

        private static bool TrySetRenderEnabled(object o, bool value, out string how)
        {
            foreach (string name in MethodCandidates)
                if (TryInvokeBool(o, name, value)) { how = name; return true; }

            foreach (string name in PropCandidates)
                if (TrySetBoolProp(o, name, value)) { how = "prop:" + name; return true; }

            how = null;

            return false;
        }

        private static object GetProp(object o, string name)
        {
            if (o == null) return null;
            PropertyInfo p = o.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);

            return p?.GetValue(o);
        }

        private static bool TryInvokeBool(object o, string method, bool arg)
        {
            if (o == null || method == null) return false;
            MethodInfo m = o.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance, null, new[] {typeof(bool)}, null);
            if (m == null) return false;
            m.Invoke(o, new object[] {arg});

            return true;
        }

        private static bool TrySetBoolProp(object o, string name, bool value)
        {
            if (o == null) return false;
            PropertyInfo p = o.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p == null || !p.CanWrite || p.PropertyType != typeof(bool)) return false;
            p.SetValue(o, value);

            return true;
        }

        /// <summary>Logs the enable/active/render-ish members on an object so the working call can be identified.</summary>
        private static void LogCandidates(string label, object o)
        {
            if (o == null)
            {
                ModLog.Info($"[MAPSUSPEND] {label}: null.");

                return;
            }

            try
            {
                var sb = new StringBuilder();
                sb.Append($"[MAPSUSPEND] {label} = {o.GetType().FullName}; members: ");
                foreach (MemberInfo mi in o.GetType().GetMembers(BindingFlags.Public | BindingFlags.Instance))
                {
                    string n = mi.Name;
                    if (n.IndexOf("Enable", StringComparison.OrdinalIgnoreCase) >= 0
                     || n.IndexOf("Active", StringComparison.OrdinalIgnoreCase) >= 0
                     || n.IndexOf("Render", StringComparison.OrdinalIgnoreCase) >= 0
                     || n.IndexOf("Visible", StringComparison.OrdinalIgnoreCase) >= 0
                     || n.IndexOf("Suspend", StringComparison.OrdinalIgnoreCase) >= 0)
                        sb.Append(n).Append(' ');
                }

                ModLog.Info(sb.ToString());
            }
            catch (Exception ex)
            {
                ModLog.Error($"[MAPSUSPEND] LogCandidates failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        #endregion
    }
}
