// Code written by Gabriel Mailhot, 04/07/2026.
// Appreciations pillar, pure foundation: the same five personality traits Grudges.TraitFacts carries
// (-2..2, Bannerlord's own scale), re-declared here so the Appreciations namespace has no dependency on
// the Grudges namespace (the two pillars stay decoupled; the future engine-side bridge is the only place
// that reads a live Hero and feeds both). Unlike Grudges.TraitFacts, Generosity is NOT reserved here — it
// is the trait most directly tied to feeling a kindness, so AppreciationPolicy actively consumes it.

namespace CalradiaRemembers.Logic.Appreciations
{
    /// <summary>
    ///   A hero's trait levels relevant to appreciation formation and decay: Generosity and Mercy (a warm
    ///   heart remembers a favour more readily and for longer), Calculating (a cold ledger-keeper is less
    ///   moved by a mere kindness), Honor and Valor (carried for parity with Grudges.TraitFacts; not yet
    ///   consumed by any policy rule in this increment).
    /// </summary>
    public sealed class TraitFacts
    {
        /// <summary>High Generosity lowers the formation threshold and slows decay: a warm heart feels a kindness more strongly and keeps it longer.</summary>
        public int Generosity { get; set; }

        /// <summary>High Mercy lowers the formation threshold and slows decay, mirroring Generosity.</summary>
        public int Mercy { get; set; }

        /// <summary>High Calculating raises the formation threshold: a cold, tallying holder is less moved by a mere kindness.</summary>
        public int Calculating { get; set; }

        /// <summary>Reserved: not yet consumed by any policy rule in this increment.</summary>
        public int Honor { get; set; }

        /// <summary>Reserved: not yet consumed by any policy rule in this increment.</summary>
        public int Valor { get; set; }
    }
}
