// Code written by Gabriel Mailhot, 03/09/2026.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   A reference count of how many CR panels currently need the vanilla settlement-visual tick suppressed (the
    ///   mod wires this to a Harmony guard: while the count is above zero, SettlementVisualManager.OnTick is skipped
    ///   so its siege-bombardment SetGlobalFrame cannot crash natively under a pushed panel). Pure and engine-free so
    ///   its three load-bearing invariants are unit-tested rather than only observed in game:
    ///   <list type="bullet">
    ///     <item>it stays above zero ACROSS a panel reload's close-then-reopen gap (a tick slipping through that gap
    ///       is the repeated-cr.reload_ui crash), which the caller secures by holding an extra Enter around the gap;</item>
    ///     <item>it drops to exactly zero only when the last panel has truly released the screen;</item>
    ///     <item>it never underflows, so a defensive double Exit cannot drive it negative and wedge suppression off.</item>
    ///   </list>
    ///   The mod holds ONE shared instance because the guard patches one engine method for every panel.
    /// </summary>
    public sealed class PanelTickSuppressionCounter
    {
        private int _depth;

        /// <summary>How many holds are currently active (panels open plus any reload-gap hold).</summary>
        public int Depth => _depth;

        /// <summary>True while at least one hold is active, i.e. the settlement-visual tick must be suppressed.</summary>
        public bool IsSuppressed => _depth > 0;

        /// <summary>A panel took the screen, or a reload opened a gap-spanning hold. Raises the count by one.</summary>
        public void Enter() => _depth++;

        /// <summary>
        ///   A panel released the screen, or a reload hold ended. Lowers the count by one, but never below zero, so a
        ///   pop that reaches teardown by two paths (an explicit close and the engine's finalize) cannot underflow.
        /// </summary>
        public void Exit()
        {
            if (_depth > 0) _depth--;
        }

        /// <summary>
        ///   Clears the count to zero. Called at session launch: no panel can be open then, so any residue from a
        ///   screen the previous session tore down without balancing (a save-load, a campaign end) must not survive
        ///   and leave the siege visuals frozen forever.
        /// </summary>
        public void Reset() => _depth = 0;
    }
}
