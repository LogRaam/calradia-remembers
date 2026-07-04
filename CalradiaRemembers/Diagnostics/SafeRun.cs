// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;

#endregion

namespace CalradiaRemembers.Diagnostics
{
    /// <summary>
    ///   Shared guard for campaign tick handlers. Mirrors the SafeStep pattern proven in
    ///   <see cref="Chat.ChatManager" />: runs one subsystem step, swallowing and logging any
    ///   exception, so that a single failing subsystem can never starve the others sharing the
    ///   same daily/weekly tick.
    /// </summary>
    public static class SafeRun
    {
        /// <summary>
        ///   Runs <paramref name="action" />. On exception, logs "<paramref name="label" /> failed: …"
        ///   via <see cref="ModLog" /> and returns normally instead of propagating.
        /// </summary>
        public static void Step(string label, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                // Route a game/mod API mismatch to the always-on compat trace; ordinary failures stay on
                // the toggle-gated error log.
                ModLog.Fault(label, ex);
            }
        }
    }
}
