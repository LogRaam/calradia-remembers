// Code written by Gabriel Mailhot, 20/06/2026.
// Extracted from the mod (Sprint 24 jealousy): the vocabulary the pure barèmes reason over — how
// serious the act was, and who has a stake — kept engine-agnostic so the scoring can be tested.

namespace CalradiaRemembers.Logic
{
    /// <summary>How serious the romantic act is — drives grudge severity and letter likelihood.</summary>
    public enum RomanticActTier
    {
        Flirt,
        Intimacy,
        Marriage
    }

    /// <summary>Who the jealous party is, and why they have a stake — sets the reaction's flavour.</summary>
    public enum JealousStake
    {
        /// <summary>The spouse of the NPC the player courted — an affront to their marriage bed.</summary>
        SpouseOfCourted,

        /// <summary>The player's own spouse/primary partner — betrayal, or competition in a sharing culture.</summary>
        PlayerPartner,

        /// <summary>A close associate of the courted NPC who could plausibly have married them.</summary>
        RivalSuitor,

        /// <summary>An NPC who carried a strong attraction to the player and was passed over.</summary>
        SpurnedAdmirer
    }
}
