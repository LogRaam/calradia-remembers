// Code written by Gabriel Mailhot, 24/08/2026.
// Council Tier 4 (give_hostage v2, ratified 2026-08-24): a hostage is a pledge of the pledger's house's good
// faith. If that house BREAKS faith and takes arms against the player during the term, the hostage is forfeit and
// becomes the player's prisoner. This pure policy mirrors NonAggressionPactPolicy's own breach predicate exactly
// (recognizes, never causes: the caller reads a real MapEvent's sides into four booleans, this only combines
// them), so the direction, who struck whom, is unit-tested rather than eyeballed. Direction is load-bearing: the
// hostage is punished ONLY when the PLEDGER's side is the attacker (they betrayed the guarantee); when the PLAYER
// is the attacker it is the player who broke faith, not the pledger, so the hostage must NOT be imprisoned.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Pure breach predicates for give_hostage: did the pledger's faction and the player's clash, and whose fault was it.</summary>
    public static class HostageBreachPolicy
    {
        /// <summary>
        ///   Which of the two watched houses stands on which side of a real battle. FOUR booleans in a row
        ///   whose whole meaning is the CROSS between them: the breach is pledger-attacks-player OR
        ///   player-attacks-pledger, so a swap inside either pair quietly asks a different question.
        ///   This is the same rule <see cref="NonAggressionPactPolicy.IsBreach" /> applies to a pact; the
        ///   two are kept apart because the pillars are, not because they disagree.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class HostageBreachFacts
        {
           /// <summary>The attacking side is wholly the pledger's faction.</summary>
           public bool AttackerIsPledgerFaction { get; set; }

           /// <summary>The attacking side is wholly the player's faction.</summary>
           public bool AttackerIsPlayerFaction { get; set; }

           /// <summary>The defending side is wholly the pledger's faction.</summary>
           public bool DefenderIsPledgerFaction { get; set; }

           /// <summary>The defending side is wholly the player's faction.</summary>
           public bool DefenderIsPlayerFaction { get; set; }
        }

        /// <summary>
        ///   True when a real MapEvent's sides pit the pledger's faction against the player's (in EITHER
        ///   attacker/defender arrangement). Mirrors <see cref="NonAggressionPactPolicy.IsBreach" />: the caller
        ///   resolves each flag with a party-vs-faction scan, this only combines the four already-resolved booleans.
        /// </summary>
        public static bool IsBreach(HostageBreachFacts facts)
        {
           if (facts == null) return false;

           bool attackerIsPledgerFaction = facts.AttackerIsPledgerFaction;
           bool attackerIsPlayerFaction = facts.AttackerIsPlayerFaction;
           bool defenderIsPledgerFaction = facts.DefenderIsPledgerFaction;
           bool defenderIsPlayerFaction = facts.DefenderIsPlayerFaction;

           return attackerIsPledgerFaction && defenderIsPlayerFaction || attackerIsPlayerFaction && defenderIsPledgerFaction;
        }

        /// <summary>
        ///   True when the PLEDGER's own faction is the breach's ATTACKER: the one direction that forfeits the
        ///   hostage (the pledger broke the faith the hostage stood surety for). When the PLAYER is the attacker it
        ///   is the player who broke faith, so the hostage is NOT punished. Only meaningful when
        ///   <see cref="IsBreach" /> is already true for the same flags.
        /// </summary>
        public static bool IsPledgerAtFault(bool attackerIsPledgerFaction) => attackerIsPledgerFaction;
    }
}
