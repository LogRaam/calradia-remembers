// Code written by Gabriel Mailhot, 05/08/2026.
// Documents non_aggression_pact's pure policy: the fixed term, the order-independent faction-pair match
// (CouncilPactBehavior.IsUnderPact's own core), and the breach-fault predicates a real MapEvent's attacker/
// defender sides feed into (CouncilPactBehavior.OnMapEventEnded). The STAKE across every test here: this pact is
// HONEST-LIMITED by design (see NonAggressionPactPolicy's own header) - it binds only the mod's own hostile-quest
// emission and a detected breach, never vanilla AI, so the fault-direction rule (a grudge ONLY when the player's
// own side attacks) must never be inverted, or the mod would plant a nonsensical grudge (an NPC resenting the
// player for the NPC'S OWN faction's attack).
#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class NonAggressionPactPolicyTests
    {
        // Author's simplification, documented: a FIXED term (mirroring HostagePolicy.TermDays' identical choice
        // for the identical COUNCIL_ACTIONS.md 20-90 day range), never a second LLM-chosen parameter.
        [Test]
        public void GIVEN_the_pact_term_WHEN_asked_THEN_it_is_a_fixed_forty_five_days()
        {
            NonAggressionPactPolicy.TermDays.Should().Be(45);
        }

        // COUNCIL_ACTIONS.md's own Kimi review names these figures directly ("grudge PactBroken, magnitude ~40"
        // and "relation -20"): pinned here so a future tuning pass cannot silently drift them without a test
        // noticing.
        [Test]
        public void GIVEN_the_breach_consequence_figures_WHEN_asked_THEN_they_match_the_doc()
        {
            NonAggressionPactPolicy.BreachGrudgeMagnitude.Should().Be(40);
            NonAggressionPactPolicy.BreachRelationHit.Should().Be(-20);
        }

        // IsMatch is the core of CouncilPactBehavior.IsUnderPact: a caller might pass the two factions in either
        // order (the player's own faction first, or the enemy's), so the match must not care which side is which.
        [Test]
        public void GIVEN_the_same_pair_in_either_order_WHEN_matched_THEN_both_orders_are_recognized()
        {
            NonAggressionPactPolicy.IsMatch(new FactionPair("kingdom_a", "clan_b"), new FactionPair("kingdom_a", "clan_b")).Should().BeTrue();
            NonAggressionPactPolicy.IsMatch(new FactionPair("kingdom_a", "clan_b"), new FactionPair("clan_b", "kingdom_a")).Should().BeTrue();
        }

        // A query naming a faction from a DIFFERENT pact entirely (or only one side matching) must never match:
        // the whole point of storing both ids is to bind THIS exact pair, not any pair sharing one member.
        [Test]
        public void GIVEN_a_mismatched_pair_WHEN_matched_THEN_it_is_refused()
        {
            NonAggressionPactPolicy.IsMatch(new FactionPair("kingdom_a", "clan_b"), new FactionPair("kingdom_a", "clan_c")).Should().BeFalse();
            NonAggressionPactPolicy.IsMatch(new FactionPair("kingdom_a", "clan_b"), new FactionPair("clan_c", "clan_d")).Should().BeFalse();
        }

        // Null/blank ids on either side (a save written before this behavior existed, or a malformed query) must
        // refuse rather than throw or false-positive on two empty strings.
        [Test]
        public void GIVEN_a_null_or_blank_id_on_either_side_WHEN_matched_THEN_it_is_refused()
        {
            NonAggressionPactPolicy.IsMatch(new FactionPair(null, "clan_b"), new FactionPair("kingdom_a", "clan_b")).Should().BeFalse();
            NonAggressionPactPolicy.IsMatch(new FactionPair("kingdom_a", ""), new FactionPair("kingdom_a", "clan_b")).Should().BeFalse();
            NonAggressionPactPolicy.IsMatch(new FactionPair("kingdom_a", "clan_b"), new FactionPair(null, "clan_b")).Should().BeFalse();
        }

        // IsBreach's own question: a real clash only counts when the two SIDES of the MapEvent map cleanly onto
        // the two pact factions, one attacking, the other defending - in EITHER arrangement (factionA attacks
        // factionB, or factionB attacks factionA).
        [Test]
        public void GIVEN_factionA_attacking_factionB_WHEN_checking_breach_THEN_it_is_a_breach()
        {
            NonAggressionPactPolicy.IsBreach(new NonAggressionPactPolicy.PactBreachFacts {AttackerIsFactionA = true, AttackerIsFactionB = false, DefenderIsFactionA = false, DefenderIsFactionB = true}).Should().BeTrue();
        }

        [Test]
        public void GIVEN_factionB_attacking_factionA_WHEN_checking_breach_THEN_it_is_still_a_breach()
        {
            NonAggressionPactPolicy.IsBreach(new NonAggressionPactPolicy.PactBreachFacts {AttackerIsFactionA = false, AttackerIsFactionB = true, DefenderIsFactionA = true, DefenderIsFactionB = false}).Should().BeTrue();
        }

        // A battle involving neither pact faction, or only ONE side of the pact against some unrelated third
        // party, must never count: the pact's own two factions must be on OPPOSITE sides of THIS SAME event.
        [Test]
        public void GIVEN_the_battle_does_not_pit_the_two_pact_factions_against_each_other_WHEN_checking_breach_THEN_it_is_not_a_breach()
        {
            // Neither faction involved at all.
            NonAggressionPactPolicy.IsBreach(new NonAggressionPactPolicy.PactBreachFacts {AttackerIsFactionA = false, AttackerIsFactionB = false, DefenderIsFactionA = false, DefenderIsFactionB = false}).Should().BeFalse();
            // FactionA attacks, but the defender is neither pact faction (a third party).
            NonAggressionPactPolicy.IsBreach(new NonAggressionPactPolicy.PactBreachFacts {AttackerIsFactionA = true, AttackerIsFactionB = false, DefenderIsFactionA = false, DefenderIsFactionB = false}).Should().BeFalse();
            // Both pact factions fight on the SAME side (e.g. an allied army) - not a breach of each other.
            NonAggressionPactPolicy.IsBreach(new NonAggressionPactPolicy.PactBreachFacts {AttackerIsFactionA = true, AttackerIsFactionB = true, DefenderIsFactionA = false, DefenderIsFactionB = false}).Should().BeFalse();
        }

        // The fault-direction rule (this class's own header, and CouncilPactBehavior's own consequence split):
        // when the PLAYER's own faction (factionA, by this codebase's {factionAId=player's} convention) is the
        // attacker, the player is at fault - the one direction a grudge can honestly be planted.
        [Test]
        public void GIVEN_factionA_is_the_attacker_WHEN_asking_who_is_at_fault_THEN_the_player_is_at_fault()
        {
            NonAggressionPactPolicy.IsPlayerFactionAtFault(attackerIsFactionA: true).Should().BeTrue();
        }

        // The reverse: when factionA (the player) is NOT the attacker, the player is not at fault (the enemy
        // faction attacked instead) - CouncilPactBehavior must never plant a grudge in this direction (see this
        // class's own header for why that would be causally backward).
        [Test]
        public void GIVEN_factionA_is_not_the_attacker_WHEN_asking_who_is_at_fault_THEN_the_player_is_not_at_fault()
        {
            NonAggressionPactPolicy.IsPlayerFactionAtFault(attackerIsFactionA: false).Should().BeFalse();
        }
    }
}
