// Code written by Gabriel Mailhot, 20/06/2026.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   How surely a rumour has reached an observer — derived from the awareness score. News from
    ///   near / one's own faction arrives well-known; from far, as a faint, doubtful rumour. Consumers
    ///   hedge their phrasing accordingly (distortion with distance).
    /// </summary>
    public enum RumorConfidence
    {
        Distant,    // barely reached them — uncertain, "none can vouch for it"
        Secondhand, // reached them at one remove — "word came that…"
        WellKnown   // near / own faction / major — common knowledge
    }
}
