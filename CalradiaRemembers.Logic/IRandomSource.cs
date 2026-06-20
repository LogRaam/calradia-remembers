// Code written by Gabriel Mailhot, 20/06/2026.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   A source of bounded random integers. Injected (inversion of control) so randomness — the
    ///   one non-deterministic input to the captive-scene pacing — can be ARRANGED in tests, letting
    ///   each expected behaviour be isolated and asserted. The mod supplies an engine-backed
    ///   implementation; tests supply a deterministic stub.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>A value in the range [0, <paramref name="maxExclusive"/>).</summary>
        int Next(int maxExclusive);
    }
}
