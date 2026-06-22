// Code written by Gabriel Mailhot, 22/06/2026.
// The negotiation framework's "deliver a prisoner" deed: an NPC demands a captive — a named lord, or any
// lord of an enemy faction — as the price of a favor. Pure so the two load-bearing rules stay tested: WHICH
// held prisoner satisfies the demand (a wrong match would hand over, or credit, the wrong captive), and
// WHETHER the player already holds one (the host phrases it as "hand him over" vs "go capture him").

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   What captive an NPC is asking for: a specific hero (<see cref="HeroId"/>) when named, otherwise
    ///   any lord of a faction (<see cref="FactionId"/>). A named hero always takes precedence.
    /// </summary>
    public sealed class PrisonerDeedTarget
    {
        public string HeroId { get; }
        public string FactionId { get; }

        public PrisonerDeedTarget(string heroId, string factionId)
        {
            HeroId = heroId;
            FactionId = factionId;
        }

        /// <summary>A demand is meaningful only when it names a hero or a faction.</summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(HeroId) || !string.IsNullOrWhiteSpace(FactionId);

        /// <summary>
        ///   True when a prisoner satisfies this demand: the named hero when one is set, else any captive
        ///   belonging to the demanded faction. An invalid (targetless) demand matches nothing.
        /// </summary>
        public bool Matches(string prisonerHeroId, string prisonerFactionId)
        {
            if (!string.IsNullOrWhiteSpace(HeroId))
                return string.Equals(HeroId, prisonerHeroId, StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(FactionId))
                return string.Equals(FactionId, prisonerFactionId, StringComparison.OrdinalIgnoreCase);

            return false;
        }

        /// <summary>
        ///   True when the player already holds a captive that satisfies this demand — case (a),
        ///   an immediate hand-over. False means case (b): the NPC sets a capture-and-deliver quest.
        /// </summary>
        public bool AnyHeldSatisfies(IEnumerable<(string HeroId, string FactionId)> heldPrisoners)
            => heldPrisoners != null && heldPrisoners.Any(p => Matches(p.HeroId, p.FactionId));
    }
}
