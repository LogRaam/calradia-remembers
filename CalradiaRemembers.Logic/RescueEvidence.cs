// Code written by Gabriel Mailhot, 01/10/2026.
// What a completed rescue task says happened. Before this every release read "Freed X from captivity", even when
// the player had crept into a town's dungeon and broken the man out (the game's own prison break). The giver reads
// this evidence back in his prompt, and a prison break is a deed worth naming.

namespace CalradiaRemembers.Logic
{
    /// <summary>The evidence line of a completed rescue task. Pure.</summary>
    public static class RescueEvidence
    {
        /// <summary>
        ///   "Broke X out of the prison of Y on day N" when the player broke them out of a named prison, else the
        ///   plain "Freed X from captivity on day N".
        /// </summary>
        public static string Describe(string freedName, string prisonPlace, int day)
            => string.IsNullOrWhiteSpace(prisonPlace)
                ? $"Freed {freedName} from captivity on day {day}"
                : $"Broke {freedName} out of the prison of {prisonPlace} on day {day}";
    }
}
