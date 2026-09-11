// Code written by Gabriel Mailhot, 20/07/2026.
// ASSEMBLY pillar, Council module: the pure vocabulary of consent. When the player moves a resolution, each
// seated member takes a stance on it, and whether that stance can BLOCK the motion depends on the council: your
// companions and family advise but do not veto their clan head, whereas your fellow clan lords carry real
// political weight. The LLM narrates each stance in character; these types let the mod DECIDE deterministically.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>How a seated member leans on a proposed resolution.</summary>
    public enum ConsentStance
    {
        /// <summary>Backs the motion.</summary>
        Favor,

        /// <summary>Neither backs nor blocks it.</summary>
        Abstain,

        /// <summary>Speaks against the motion.</summary>
        Oppose
    }

    /// <summary>Whether a council's members can BLOCK a motion, or only advise the player who decides alone.</summary>
    public enum ConsentAuthority
    {
        /// <summary>Companions and family: they voice a stance and it carries a relational cost, but no veto.</summary>
        Advisory,

        /// <summary>Clan lords: enough opposition among them actually stops the motion.</summary>
        Binding
    }

    /// <summary>The flattened facts one member weighs on one motion.</summary>
    public readonly struct ConsentFacts
    {
        public ConsentFacts(int regardToInitiator, int stake)
        {
            RegardToInitiator = regardToInitiator;
            Stake = stake;
        }

        /// <summary>The member's personal regard for the player who convened the council.</summary>
        public int RegardToInitiator { get; }

        /// <summary>The member's stake in THIS motion: positive if it favours them, negative if it costs them,
        /// zero if it does not touch them. Computed per resolution by the mod.</summary>
        public int Stake { get; }
    }

    /// <summary>The tally of a council's stances on one motion, and whether it carries.</summary>
    public readonly struct ConsentResult
    {
        /// <summary>True when the motion may be staged as Agreed (advisory councils always carry).</summary>
        public bool Carries { get; init; }

        public int Favor { get; init; }
        public int Oppose { get; init; }
        public int Abstain { get; init; }
    }
}
