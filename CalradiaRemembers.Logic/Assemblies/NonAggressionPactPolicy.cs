// Code written by Gabriel Mailhot, 05/08/2026.
// ASSEMBLY pillar, Council module, Partie 8 (COUNCIL_ACTIONS.md's Kimi review, "non_aggression_pact"): the
// Parley's fifth concession, rounding out the toolkit alongside make_peace, tribute, release_prisoner and
// give_hostage. HONEST-LIMITED BY DESIGN (Kimi's own warning, read first): vanilla Bannerlord has NO inter-clan
// non-aggression mechanic, and the vanilla AI (kingdom-level war councils, lords acting on their own initiative)
// will NOT respect one - a clan at war through its own kingdom attacks regardless of what its own leader agreed
// to at this table. What this pact CAN mechanically guarantee, and ONLY this:
//   (a) the mod's OWN quest-emission path refuses to create a hostile quest (attack_faction/attack_lord/
//       raid_village/attack_caravan/siege/capture_prisoner/execute_enemy/declare_war) whose target faction is a
//       current pact partner (CalradiaRemembers.Quests.QuestFactory.Create, gated at the emission point);
//   (b) a breach is DETECTED the moment a real MapEvent pits a party of one pact faction against a party of the
//       other (CalradiaRemembers.Assemblies.CouncilPactBehavior.OnMapEventEnded, mirroring
//       CalradiaRemembers.Commitments.CommitmentBehavior.OnMapEventEnded's own "recognizes, never causes" pattern),
//       and sanctioned (a grudge when the PLAYER's own side is the attacker, a relation hit either way, and a
//       WorldEvent note naming the true attacker regardless of which side's memory records it).
// What it CANNOT guarantee: a kingdom's own war council declaring war regardless, an ally's own raid the player
// never ordered, or any vanilla AI behaviour whatsoever. The teaching text (SDK PromptBuilder) must therefore
// scope the promise EXACTLY ("our companies will not attack each other for a set time"), never "there will be
// peace" - the false-promise class this whole codebase exists to close.
//
// Author's design calls (adopting COUNCIL_ACTIONS.md's own recipe):
// - A FIXED 45-day term (TermDays), the midpoint of the doc's own 20-90 day range, mirroring HostagePolicy.TermDays'
//   own identical choice for the exact same range: the LLM proposes nothing beyond WHO offers the pact (there is
//   no second field to ground alongside a duration, exactly like give_influence/expel_from_clan carry no
//   TargetHint at all, see ChatViewModel.CouncilResolutions' own default-branch doc).
// - Deliberately does NOT require the two factions to NOT already be at war (unlike COUNCIL_ACTIONS.md's own
//   "pas en guerre ouverte, sinon c'est make_peace" phrasing), mirroring CouncilTributeBehavior's own documented
//   choice for tribute: a pact may be struck in the SAME parley turn as make_peace (the peace ends the war; the
//   pact adds a time-boxed guarantee on top), or on its own while the standing war continues (the player wants a
//   narrower, cheaper promise than a full peace). SettleNonAggressionPact therefore checks only that the player
//   leads a faction, the target resolves to a real faction, and the two are not the SAME faction - never war
//   status.
namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Pure policy for non_aggression_pact: the fixed term and the faction-pair/breach-fault predicates.</summary>
    public static class NonAggressionPactPolicy
    {
        /// <summary>
        ///   A pact always runs a FIXED term of 45 days (COUNCIL_ACTIONS.md's own 20-90 day range, midpoint
        ///   chosen, mirroring HostagePolicy.TermDays' identical call for the identical range): the only variable
        ///   the LLM proposes is WHO offers the pact (the seated enemy leader), never a second duration figure.
        /// </summary>
        public const int TermDays = 45;

        /// <summary>
        ///   COUNCIL_ACTIONS.md's own Kimi review names this figure directly ("grudge PactBroken, magnitude
        ///   ~40"): pinned here, read by CouncilPactBehavior when the PLAYER's own side is the breach's attacker
        ///   (see this class's own header for why only that direction ever plants a grudge).
        /// </summary>
        public const int BreachGrudgeMagnitude = 40;

        /// <summary>
        ///   COUNCIL_ACTIONS.md's own Kimi review names this figure directly ("relation -20"): applied to BOTH
        ///   sides of the breach regardless of fault (a shattered pact strains the relationship either way, see
        ///   this class's own header), unlike the grudge, which is fault-directional.
        /// </summary>
        public const int BreachRelationHit = -20;

        /// <summary>
        ///   True when the queried faction pair (in EITHER order) matches a stored pact's own pair: the core of
        ///   <c>CouncilPactBehavior.IsUnderPact</c>, kept pure and order-independent here so a caller need not
        ///   worry which side it happens to pass first (mirrors <c>ParleyFactionLookup</c>'s own null-safety
        ///   discipline, just for a pair instead of a single id).
        ///   <para>
        ///     Takes two <see cref="FactionPair" /> values rather than four bare ids (2026-09-10). The
        ///     order-independence this method's own doc used to PROMISE the caller in prose is now the pair
        ///     type's behaviour, which is where a promise like that belongs. The two arguments may sit side
        ///     by side unnamed precisely because matching a pair against a pair is symmetric.
        ///   </para>
        /// </summary>
        public static bool IsMatch(FactionPair pact, FactionPair query)
            => pact.Matches(query);

        /// <summary>
        ///   Which of the pact's two factions stands on which side of a real battle. Each flag is resolved by
        ///   the caller with a party-vs-faction scan and never re-derived here; this predicate only asks
        ///   whether the two are on OPPOSITE sides, in either arrangement.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class PactBreachFacts
        {
           /// <summary>The attacking side is wholly the pact's first faction.</summary>
           public bool AttackerIsFactionA { get; set; }

           /// <summary>The attacking side is wholly the pact's second faction.</summary>
           public bool AttackerIsFactionB { get; set; }

           /// <summary>The defending side is wholly the pact's first faction.</summary>
           public bool DefenderIsFactionA { get; set; }

           /// <summary>The defending side is wholly the pact's second faction.</summary>
           public bool DefenderIsFactionB { get; set; }
        }

        /// <summary>
        ///   True when the attacker/defender sides read off a real MapEvent actually pit the pact's two factions
        ///   against one another (one side wholly attributable to factionA, the other to factionB, in EITHER
        ///   attacker/defender arrangement): CouncilPactBehavior's own breach test. Each side flag is read by the
        ///   caller via a party-vs-faction scan (mirrors CommitmentBehavior's own SideHasHeroOrFaction), never
        ///   re-derived here - this predicate only combines the four already-resolved booleans.
        /// </summary>
        public static bool IsBreach(PactBreachFacts facts)
        {
           if (facts == null) return false;

           bool attackerIsFactionA = facts.AttackerIsFactionA;
           bool attackerIsFactionB = facts.AttackerIsFactionB;
           bool defenderIsFactionA = facts.DefenderIsFactionA;
           bool defenderIsFactionB = facts.DefenderIsFactionB;

           return attackerIsFactionA && defenderIsFactionB || attackerIsFactionB && defenderIsFactionA;
        }

        /// <summary>
        ///   True when the PLAYER's own side (factionA, by this codebase's own {factionAId=player's} convention,
        ///   see CouncilPact's own header) is the breach's ATTACKER: the one direction a grudge can honestly be
        ///   planted (an NPC resenting the player for the player's own faction attacking them makes causal sense;
        ///   the reverse, an NPC resenting the player for THEIR OWN faction's attack, would not - see this class's
        ///   own header). Only meaningful when <see cref="IsBreach" /> is already true for the same four flags.
        /// </summary>
        public static bool IsPlayerFactionAtFault(bool attackerIsFactionA) => attackerIsFactionA;
    }
}
