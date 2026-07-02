// Code written by Gabriel Mailhot, 01/07/2026.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   A hero's five personality traits, already converted to the -2..2 scale, carried as plain
    ///   ints so <see cref="RomanticProfilePolicy" /> stays free of any engine dependency. The mod's
    ///   adapter (<c>RomanticProfileBuilder</c>) is the only place that reads a live <c>Hero</c>.
    /// </summary>
    public sealed class RomanticTraits
    {
        public int Calculating;
        public int Generosity;
        public int Honor;
        public int Mercy;
        public int Valor;
    }
}
