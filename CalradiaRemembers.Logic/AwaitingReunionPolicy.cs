// Code written by Gabriel Mailhot, 05/08/2026.
// v1.34.2 companion hardening. A companion taken prisoner (or whose errand party dissolved) parks their mission
// in the AwaitingReunion phase, which resolved ONLY when they were free AND back in the player's party. A
// companion who was ransomed, escaped to a wanderer state, or whose party vanished never re-entered the main
// party on their own, so they stayed IsAway == true FOREVER (audit MAJEUR): blocking re-dispatch, expel, and
// their journal, with no timeout and no notice. This pure policy names the timeout after which the errand must
// be force-closed so the companion stops being counted abroad.

namespace CalradiaRemembers.Logic
{
    /// <summary>Pure timeout decision for a companion errand stuck in its AwaitingReunion phase.</summary>
    public static class AwaitingReunionPolicy
    {
        /// <summary>
        ///   Days a mission may sit in AwaitingReunion before it is force-closed. Long enough that a normal
        ///   escape/ransom/rescue resolves it naturally first, short enough that a stranded companion is freed
        ///   within a season rather than for the rest of the campaign.
        /// </summary>
        public const int TimeoutDays = 30;

        /// <summary>
        ///   True when an errand awaiting reunion has waited long enough that it must be closed out now, clearing
        ///   the companion's away-status regardless of whether they ever rejoined the party. Boundary is inclusive:
        ///   exactly <paramref name="timeoutDays" /> days waiting force-closes.
        /// </summary>
        public static bool ShouldForceClose(int daysAwaiting, int timeoutDays)
            => daysAwaiting >= timeoutDays;

        /// <summary>Convenience overload using the default <see cref="TimeoutDays" />.</summary>
        public static bool ShouldForceClose(int daysAwaiting)
            => ShouldForceClose(daysAwaiting, TimeoutDays);
    }
}
