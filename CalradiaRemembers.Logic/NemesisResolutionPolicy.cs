// Code written by Gabriel Mailhot, 06/07/2026.
// Nemesis pillar increment 2 (resolution layer): the pure wording/classification for how a tracked
// nemesis's vendetta closes. Two of the three resolutions (R1 kill in battle, R2 execute) share the
// same recognition shape: the engine only needs to know whether the kill was a deliberate execution
// or an ordinary battle death, and this policy turns that single bool into a distinct flavour for the
// closure deed recorded into the world-event store. R3 (imprison + torture) has no resolution wording
// of its own: it does not end the vendetta, so it is out of scope for this policy.

#region

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>How a tracked nemesis's life ended, for wording purposes only (no state of its own).</summary>
    public enum NemesisResolutionKind
    {
        /// <summary>Cut down in the field, no ceremony.</summary>
        KilledInBattle,

        /// <summary>Taken captive, then put to death deliberately.</summary>
        Executed
    }

    /// <summary>
    ///   Pure classification and wording for a nemesis's closure deed. The engine-side behaviour
    ///   (<see cref="NemesisResolutionKind" />'s consumer, the Nemesis resolution hook) supplies the
    ///   raw facts; this class decides which flavour applies and how the deed reads, both as
    ///   third-person rumour (<see cref="ClosureSummary" />) and as first-person chronicle prose
    ///   (<see cref="ClosureChronicleLine" />).
    /// </summary>
    public static class NemesisResolutionPolicy
    {
        /// <summary>
        ///   TUNE: how far the closure deed's news carries. A bandit boss is no lord, so this sits
        ///   below a lord's execution (65 elsewhere in the world-event feeders) but is still a real feat.
        /// </summary>
        public const int ClosureMagnitude = 55;

        /// <summary>Maps the single fact the engine has (was this a deliberate execution?) to the resolution kind.</summary>
        public static NemesisResolutionKind ResolutionKindFor(bool wasExecuted)
            => wasExecuted ? NemesisResolutionKind.Executed : NemesisResolutionKind.KilledInBattle;

        /// <summary>
        ///   The third-person rumour line for the closure deed, distinct per <paramref name="kind" /> so an
        ///   execution reads as a deliberate reckoning ("head on a pike") and a battle death reads as a
        ///   cleaner, impersonal end.
        /// </summary>
        public static string ClosureSummary(string playerName, string nemesisDisplayName, NemesisResolutionKind kind)
            => kind == NemesisResolutionKind.Executed
                ? $"{playerName} took {nemesisDisplayName} captive at last and had his head struck off, ending the vendetta for good."
                : $"{playerName} cut {nemesisDisplayName} down in battle, ending the vendetta between them.";

        /// <summary>
        ///   The first-person chronicler-voice line (no date, no leading name; the chronicle prefixes it),
        ///   distinct per <paramref name="kind" /> to match <see cref="ClosureSummary" />'s flavour split.
        /// </summary>
        public static string ClosureChronicleLine(string nemesisDisplayName, NemesisResolutionKind kind)
            => kind == NemesisResolutionKind.Executed
                ? $"I finally took {nemesisDisplayName} alive and had his head struck from his shoulders, the long hunt between us ended for good"
                : $"I finally cut {nemesisDisplayName} down in battle, and the long hunt between us was ended";

        /// <summary>
        ///   TUNE: the turning of a nemesis is a rarer, more remarkable feat than cutting him down, so its
        ///   news carries a touch further than a plain closure kill.
        /// </summary>
        public const int TurnedMagnitude = 60;

        /// <summary>The third-person rumour line for a nemesis SPARED and turned to the player's service (increment 4).</summary>
        public static string TurnedSummary(string playerName, string nemesisDisplayName)
            => $"{playerName} spared {nemesisDisplayName} and bent him to their service; the hunter now rides at their side.";

        /// <summary>The first-person chronicler-voice line for a turned nemesis (no date, no leading name).</summary>
        public static string TurnedChronicleLine(string nemesisDisplayName)
            => $"I held {nemesisDisplayName} at my mercy and, rather than end him, bent him to my banner; the man who once hunted me now rides at my side";
    }
}
