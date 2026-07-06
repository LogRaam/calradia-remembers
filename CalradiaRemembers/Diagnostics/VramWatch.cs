// Code written by Gabriel Mailhot, 05/07/2026.
// A frame-loop VRAM heartbeat that runs INDEPENDENTLY of the chat, so the background map screen can be
// watched while NO chat is open — the control test that isolates whether the leak is our overlay keeping
// the heavy naval map rendering, or the map screen leaking on its own.

#region

using System;

#endregion

namespace CalradiaRemembers.Diagnostics
{
    /// <summary>
    ///   An opt-in VRAM heartbeat pumped from the submodule's per-frame tick (which fires even with no chat
    ///   open and the campaign paused). Off by default; toggled by <c>cr.vram_watch</c>. Throttled so it costs
    ///   nothing but one log line every <see cref="IntervalSeconds" />. Diagnostic only; never throws.
    /// </summary>
    public static class VramWatch
    {
        private const double IntervalSeconds = 10.0;

        private static DateTime _last = DateTime.MinValue;

        /// <summary>When set, <see cref="Tick" /> emits a throttled [VRAM] line every interval.</summary>
        public static bool Enabled { get; set; }

        /// <summary>Pumped every frame from the submodule. A no-op unless enabled and the interval elapsed.</summary>
        public static void Tick()
        {
            if (!Enabled) return;

            try
            {
                DateTime now = DateTime.UtcNow;
                if ((now - _last).TotalSeconds < IntervalSeconds) return;
                _last = now;
                ModLog.Info($"[VRAM] watch (no chat) | {VramProbe.Describe()}");
            }
            catch
            {
                // a diagnostic must never disturb the game
            }
        }
    }
}
