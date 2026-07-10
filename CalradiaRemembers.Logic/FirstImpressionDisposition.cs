// Code written by Gabriel Mailhot, 10/07/2026.
// Player report (ProudSeal 2026-07-10): NPCs open too neutral and too reasonable; a rivalry needs the player to
// offend first. First impressions let a noble's traits and the player's standing colour the OPENING TONE of a
// meeting (arrogant, suspicious, demeaning, or warm), without touching the relation numbers (deeds still govern
// regard). This enum is the resolved opening posture; the pure FirstImpressionPolicy decides it.

namespace CalradiaRemembers.Logic
{
    /// <summary>How a barely-known NPC opens toward the player, driven by their traits and the player's standing.</summary>
    public enum FirstImpressionDisposition
    {
        /// <summary>No first-impression colour: the NPC is already known, so their existing stance governs. Inject nothing.</summary>
        None,

        /// <summary>The NPC is in the player's OWN clan: they open loyal, grateful, or a believer in the player's cause.</summary>
        Loyal,

        /// <summary>Warmly inclined to a newcomer: a fair, welcoming hearing.</summary>
        Cordial,

        /// <summary>Even and unremarkable: the ordinary default (treated as no colour, inject nothing).</summary>
        Neutral,

        /// <summary>Reserved and transactional: keeps a stranger at arm's length until they prove worth the time.</summary>
        Cool,

        /// <summary>Superior and territorial: resents a new clan crowding the faction's shared rewards.</summary>
        Arrogant,

        /// <summary>Guarded and distrustful: reads a stranger for hidden motives, slow to take their word.</summary>
        Suspicious,

        /// <summary>Contemptuous and belittling: an unproven, landless upstart not worth their respect.</summary>
        Demeaning
    }
}
