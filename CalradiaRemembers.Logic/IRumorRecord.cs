// Code written by Gabriel Mailhot, 20/06/2026.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   The minimum a stored world happening must expose for the pruner to reason about its lifetime:
    ///   when it occurred and whether it is the player's own deed (kept long-term) or third-party rumour
    ///   (aged out). The mod's WorldEvent implements this so the pure pruner never depends on the engine.
    /// </summary>
    public interface IRumorRecord
    {
        /// <summary>In-game day the event occurred.</summary>
        int Day { get; }

        /// <summary>True for the player's own deeds — chronicle material, kept long-term.</summary>
        bool InvolvesPlayer { get; }
    }
}
