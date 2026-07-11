// Code written by Gabriel Mailhot, 11/07/2026.

#region

#endregion

namespace CalradiaRemembers.Logic.Rescue
{
    /// <summary>How the freed prisoner relates to the player, deciding the shape of their gratitude.</summary>
    public enum RescueTie
    {
        /// <summary>The freed prisoner is the one the player rescued.</summary>
        RescuedSelf,

        /// <summary>A close family member of the freed prisoner.</summary>
        Family,

        /// <summary>A friend of the freed prisoner (relation above the friend threshold).</summary>
        Friend
    }

    /// <summary>
    ///   The gratitude a single NPC feels after a player rescue: how many personal-regard points it is
    ///   worth, and whether it is significant enough to be recorded as a lasting memory (versus a fleeting
    ///   warmth that leaves no trace).
    /// </summary>
    public readonly struct RescueGratitude
    {
        public RescueGratitude(int magnitude, bool recordMemory)
        {
            Magnitude = magnitude;
            RecordMemory = recordMemory;
        }

        /// <summary>Personal-regard points granted by this gratitude.</summary>
        public int Magnitude { get; }

        /// <summary>True when the gratitude is strong enough to be recorded as a lasting memory.</summary>
        public bool RecordMemory { get; }
    }
}
