// Code written by Gabriel Mailhot, 03/07/2026.
// Grudges pillar, pure foundation: the same five personality traits RomanticTraits already carries
// (-2..2, Bannerlord's own scale), re-declared here so the Grudges namespace has no dependency on the
// romance layer. The mod's adapter (a future GrudgeBridge) is the only place that reads a live Hero.

namespace CalradiaRemembers.Logic.Grudges
{
    /// <summary>
    ///   A hero's trait levels relevant to grudge formation, rigidity, and decay: Mercy (forgiving vs
    ///   tallying), Honor (broken word stings hardest), Valor (fearful grudges hide rather than confront),
    ///   Calculating (forms easily, never forgets), Generosity (reserved for future resolution-cost tuning).
    /// </summary>
    public sealed class TraitFacts
    {
        /// <summary>High Mercy raises the formation threshold, softens rigidity, and speeds decay.</summary>
        public int Mercy { get; set; }

        /// <summary>High Honor grudges broken word (failed quests, oaths) hardest, yet honours amends rituals.</summary>
        public int Honor { get; set; }

        /// <summary>Low Valor expresses grudges through schemes, never to the holder's own face (surfacing concern, not modelled here).</summary>
        public int Valor { get; set; }

        /// <summary>High Calculating lowers the formation threshold, raises rigidity, and never decays.</summary>
        public int Calculating { get; set; }

        /// <summary>Reserved: not yet consumed by any policy rule in this increment.</summary>
        public int Generosity { get; set; }
    }
}
