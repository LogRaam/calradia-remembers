// Code written by Gabriel Mailhot, 26/11/2026.
// Lordfadooboo (Nexus, 26/11/2026): "a character can be in the wrong, say false things about you and lose regard for you,
// but once they accept that they were wrong, you still have all those regard points lost even though the slight never
// existed. is there some way to account for a character 'giving regard back' when they find out they are wrong?" Gabriel
// (26/11/2026): yes. This keeps, for one conversation, how much regard each character has taken from the player and not
// yet given back; RelationGate returns up to that much when the character warms again. A new conversation starts empty:
// what was taken in an earlier one is the character's settled view, and only the game's own record of this talk counts.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>The regard each character took from the player in this conversation and has not given back. One per conversation.</summary>
    public sealed class RegardTakenLedger
    {
        private readonly Dictionary<string, int> _taken = new Dictionary<string, int>();

        /// <summary>The regard <paramref name="heroId" /> took in this conversation and still holds (0 or more).</summary>
        public int TakenFrom(string heroId)
            => heroId != null && _taken.TryGetValue(heroId, out int taken) ? taken : 0;

        /// <summary>
        ///   A regard change the game applied for <paramref name="heroId" />: a loss adds to what is held, a gain pays it back
        ///   first (never below zero).
        /// </summary>
        public void Note(string heroId, int appliedDelta)
        {
            if (heroId == null || appliedDelta == 0) return;

            int held = TakenFrom(heroId) - appliedDelta;
            _taken[heroId] = held > 0 ? held : 0;
        }
    }
}
