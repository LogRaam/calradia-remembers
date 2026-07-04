// Code written by Gabriel Mailhot, 04/07/2026.
// Grudges pillar, resolution half: the verdict GrudgeResolutionPolicy.Evaluate hands back for one
// offered gesture. Softened is the common case (most gestures chip away rather than close the book);
// Resolved and Rejected are the two ends of the spectrum.

namespace CalradiaRemembers.Logic.Grudges
{
    /// <summary>The verdict on one <see cref="GrudgeGesture" /> offered toward a <see cref="Grudge" />.</summary>
    public enum GrudgeResolutionOutcome
    {
        /// <summary>The grudge is fully mended: the caller should also call <see cref="GrudgePolicy.Resolve" />.</summary>
        Resolved = 0,

        /// <summary>The grudge is reduced but still held; subtract <see cref="GrudgeResolutionResult.MagnitudeReduction" /> from the stored magnitude.</summary>
        Softened = 1,

        /// <summary>The gesture does not move the holder at all.</summary>
        Rejected = 2
    }
}
