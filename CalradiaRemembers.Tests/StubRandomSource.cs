// Code written by Gabriel Mailhot, 20/06/2026.

using CalradiaRemembers.Logic;

namespace CalradiaRemembers.Tests
{
    /// <summary>
    ///   A deterministic <see cref="IRandomSource"/> for arranging the captive-scene director's
    ///   random branches. Always returns the same configured value, so a roll like "Next(100) &lt; 55"
    ///   is forced one way or the other.
    /// </summary>
    internal sealed class StubRandomSource : IRandomSource
    {
        private readonly int _value;

        private StubRandomSource(int value) => _value = value;

        /// <summary>Every roll lands BELOW any threshold — the "yes / continue / together" branch.</summary>
        public static StubRandomSource AlwaysLow => new StubRandomSource(0);

        /// <summary>Every roll lands ABOVE any threshold — the "no / stop / skip" branch.</summary>
        public static StubRandomSource AlwaysHigh => new StubRandomSource(99);

        /// <summary>Every roll returns exactly <paramref name="value"/> — to land in a specific band.</summary>
        public static StubRandomSource Returning(int value) => new StubRandomSource(value);

        public int Next(int maxExclusive) => _value;
    }
}
