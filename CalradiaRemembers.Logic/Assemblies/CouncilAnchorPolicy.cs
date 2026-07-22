// Code written by Gabriel Mailhot, 21/07/2026.
// ASSEMBLY pillar, Council module: at a council the PLAYER presides, so no seated member is the "host" the way
// an ordinary conversation has one. But the chat still renders one primary respondent per turn, and Gabriel's
// whole-table model needs exactly that shape: a player turn is ONE call that yields one member's spoken reply
// plus a reaction from each of the others who speaks or merely gestures. This picks which member carries that
// spoken reply.
//
// The rule is REGARD, highest first: the member who thinks best of the player is the one who answers when the
// player addresses the room, which is both how a real table behaves and the choice a player can predict. Ties
// break on the hero id so a given roster always seats the same anchor, because a council that reshuffled its
// first voice between turns would read as the table changing its mind about who leads it.

using System.Collections.Generic;

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>The one fact this policy needs about a seated member: who they are, and how they regard the player.</summary>
    public readonly struct CouncilSeatFacts
    {
        public CouncilSeatFacts(string heroId, int regard)
        {
            HeroId = heroId;
            Regard = regard;
        }

        /// <summary>Stable hero id, used only to break a tie deterministically.</summary>
        public string HeroId { get; }

        /// <summary>The mod's own personal regard ledger for this member (NpcProfile.ReputationWithPlayer).</summary>
        public int Regard { get; }
    }

    /// <summary>Chooses which seated member speaks the reply when the player addresses the whole council.</summary>
    public static class CouncilAnchorPolicy
    {
        /// <summary>
        ///   Index into <paramref name="seats" /> of the member who answers, or -1 when nobody is seated (the
        ///   caller must not open a council at all in that case).
        /// </summary>
        public static int SelectAnchor(IReadOnlyList<CouncilSeatFacts> seats)
        {
            if (seats == null || seats.Count == 0) return -1;

            var best = 0;
            for (var i = 1; i < seats.Count; i++)
            {
                if (seats[i].Regard > seats[best].Regard)
                {
                    best = i;

                    continue;
                }

                // Same regard: the lower hero id wins, so the same roster always produces the same anchor.
                if (seats[i].Regard == seats[best].Regard
                    && string.CompareOrdinal(seats[i].HeroId ?? "", seats[best].HeroId ?? "") < 0)
                    best = i;
            }

            return best;
        }
    }
}
