// Code written by Gabriel Mailhot, 09/10/2026.
// Who travelling with the player may ask for a private word, and who asks next. HakiTakiUmba (Nexus, 09/10/2026): the
// tavern companions asked for a word now and then, the player's own family travelling in the party (siblings, a
// spouse) never did, because only a hired companion was ever considered. Family now asks on the same found-topics.
// Gabriel (09/10/2026): the spouse already has her own channels (letters when apart), so balance it: the one who had
// the last audience lets another speak next when anyone else has something to say, so neither a wife with a long
// shared past nor any one companion takes every audience.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>What is known of one hero travelling with the player, for the private-word roster.</summary>
    public sealed class TravellerFacts
    {
        public bool IsPlayer { get; init; }

        /// <summary>A hired companion (the game's own companion flag).</summary>
        public bool IsHiredCompanion { get; init; }

        /// <summary>Of the player's own clan: their family, and a hired companion too.</summary>
        public bool IsOfPlayersClan { get; init; }

        /// <summary>Riding in the player's own party right now.</summary>
        public bool InPlayersParty { get; init; }
    }

    /// <summary>One traveller with something to say, and how strong it is.</summary>
    public sealed class AudienceCandidate
    {
        public string HeroId { get; init; }

        public int Strength { get; init; }
    }

    /// <summary>The private-word roster and its turn-taking. Pure.</summary>
    public static class TravellingAudiencePolicy
    {
        /// <summary>A hired companion, or one of the player's own family, riding in the player's party.</summary>
        public static bool MayAskForAWord(TravellerFacts f)
            => f != null && !f.IsPlayer && f.InPlayersParty && (f.IsHiredCompanion || f.IsOfPlayersClan);

        /// <summary>Family, not hired help: the audience then speaks as kin.</summary>
        public static bool IsKin(TravellerFacts f) => f != null && !f.IsHiredCompanion && f.IsOfPlayersClan;

        /// <summary>
        ///   Who asks next: the strongest matter, but the one who had the last audience waits while anyone else has
        ///   something to say. On equal strength the first listed wins (the caller lists in a stable order). Null when
        ///   nobody has anything.
        /// </summary>
        public static AudienceCandidate Choose(IReadOnlyList<AudienceCandidate> candidates, string lastAudienceHeroId)
        {
            AudienceCandidate best = null, bestOther = null;
            foreach (AudienceCandidate c in candidates ?? new List<AudienceCandidate>())
            {
                if (c == null) continue;
                if (best == null || c.Strength > best.Strength) best = c;
                if (c.HeroId != lastAudienceHeroId && (bestOther == null || c.Strength > bestOther.Strength)) bestOther = c;
            }

            return bestOther ?? best;
        }
    }
}
