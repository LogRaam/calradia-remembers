// Code written by Gabriel Mailhot, 23/08/2026.
// Player complaint: conversations are ALWAYS crowded with ambient bystanders, every settlement audience reads
// the same regardless of who the NPC is or what is being said. This is the player's own taste dial for how
// full the room feels, applied ONLY to ambient hero witnesses the mod would otherwise auto-gather (never to
// the player's own companions, who came on purpose). Bustling is the exact shipped feel today: the mod is
// LIVE, so a player who never opens the menu must see nothing change.

namespace CalradiaRemembers.Logic
{
    /// <summary>How many ambient hero witnesses a conversation gathers. The player's own taste, not a difficulty.</summary>
    public enum WitnessDensity
    {
        /// <summary>Rooms thin out fast: private moments are common, at most one ambient witness.</summary>
        Sparse,

        /// <summary>A middle ground: a room usually holds one or two witnesses instead of the full gathered set.</summary>
        Normal,

        /// <summary>The shipped, unthinned feel: every ambient witness the resolver gathers is kept. The default.</summary>
        Bustling
    }
}
