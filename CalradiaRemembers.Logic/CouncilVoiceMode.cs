// Code written by Gabriel Mailhot, 21/07/2026.
// How a council speaks. The two modes trade different things, not "cheap" versus "good": one call for the
// whole table keeps the scene COHESIVE (one model sees the whole room at once, members react to each other
// inside a single completion), but the non-anchor members are voiced from little more than a name, so their
// memories and interests never reach the prompt. One call per member gives each their REAL dossier (own
// memory, quests, regard), so the roleplay is authentic, at the cost of N round-trips of latency and no
// shared view of the same instant. This value is only the PERSISTED DEFAULT (MCM) for a newly opened council;
// the in-window toggle (ChatViewModel.ExecuteToggleCouncilVoiceMode) is session state layered on top of it.

namespace CalradiaRemembers.Logic
{
    /// <summary>How a council's turns are generated: together in one call, or each seated member their own.</summary>
    public enum CouncilVoiceMode
    {
        /// <summary>One call yields the anchor's reply plus a reaction from each other member. The default.</summary>
        WholeTable,

        /// <summary>Each seated member gets their own full-profile call, in turn.</summary>
        PerMember
    }
}
