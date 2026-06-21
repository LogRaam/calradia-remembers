// Code written by Gabriel Mailhot, 20/06/2026.
// v1.3 schemes — the foundation: one lord's plot against a rival. The lifecycle is mechanical and
// pure (advanced by SchemeAdvancer); the LLM is never called to simulate it — only when the player
// touches the affair (a rumour they hear). Every state change feeds the world-event store.

namespace CalradiaRemembers.Logic
{
    /// <summary>The flavour of a scheme — drives the rumour wording and the standing-damage effect, not the lifecycle.</summary>
    public enum SchemeKind
    {
        /// <summary>Blacken the target's name so their peers turn cold — relation damage with others.</summary>
        Slander,

        /// <summary>Undermine the target's position — a hit to their influence / standing.</summary>
        Sabotage
    }

    /// <summary>The lifecycle of a scheme. Plotted/Progressing/Discovered are active; Succeeded/Failed are terminal.</summary>
    public enum SchemeState
    {
        /// <summary>Set in motion but not yet underway.</summary>
        Plotted,

        /// <summary>Quietly gaining ground, still secret.</summary>
        Progressing,

        /// <summary>The target (and the gossip mill) have caught wind — now it races against counterplay.</summary>
        Discovered,

        /// <summary>Reached fruition — the standing-damage effect lands.</summary>
        Succeeded,

        /// <summary>Collapsed or was countered before it could land.</summary>
        Failed
    }

    /// <summary>
    ///   One lord's plot against a rival: who plots, against whom, of what kind, and how far along.
    ///   A plain state carrier — <see cref="SchemeAdvancer"/> drives it, the host persists it and
    ///   turns each state change into a rumour and (on success) a hit to the target's standing.
    /// </summary>
    public sealed class Scheme
    {
        /// <summary>Hero StringId of the plotter.</summary>
        public string PlotterId { get; set; }

        /// <summary>Hero StringId of the target.</summary>
        public string TargetId { get; set; }

        /// <summary>The plot's flavour.</summary>
        public SchemeKind Kind { get; set; }

        /// <summary>Where the plot stands in its lifecycle.</summary>
        public SchemeState State { get; set; } = SchemeState.Plotted;

        /// <summary>How close to fruition, 0..100; at 100 the scheme succeeds.</summary>
        public int Progress { get; set; }
    }
}
