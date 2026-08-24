// Code written by Gabriel Mailhot, 24/08/2026.
// Council Tier 4 (give_hostage v2): this policy decides whether a battle breaks a hostage accord and, if so, whose
// fault it was, which in turn decides whether the hostage is imprisoned. What breaks if it is wrong: either a
// hostage is seized when no breach happened (the player jails a guest for a battle that never involved the pledger),
// or a real betrayal goes unpunished. DIRECTION is the load-bearing fact: the hostage is forfeit ONLY when the
// PLEDGER struck; a player-initiated attack is the player's own broken faith and must NOT jail the hostage.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class HostageBreachPolicyTests
    {
        // The core breach: the pledger's faction attacks, the player's defends. This is the case that forfeits the
        // hostage, so it must read as a breach.
        [Test]
        public void GIVEN_the_pledger_attacks_the_player_THEN_it_is_a_breach()
        {
            HostageBreachPolicy.IsBreach(attackerIsPledgerFaction: true, attackerIsPlayerFaction: false,
                defenderIsPledgerFaction: false, defenderIsPlayerFaction: true).Should().BeTrue();
        }

        // The reverse arrangement is still a breach (the two factions clashed); fault is decided separately.
        [Test]
        public void GIVEN_the_player_attacks_the_pledger_THEN_it_is_still_a_breach()
        {
            HostageBreachPolicy.IsBreach(attackerIsPledgerFaction: false, attackerIsPlayerFaction: true,
                defenderIsPledgerFaction: true, defenderIsPlayerFaction: false).Should().BeTrue();
        }

        // A battle that does not pit the two factions against each other (e.g. both on the same side against a third,
        // or neither present) is NOT a breach; jailing the hostage over it would be a false seizure.
        [Test]
        public void GIVEN_the_two_factions_do_not_face_each_other_THEN_it_is_not_a_breach()
        {
            HostageBreachPolicy.IsBreach(attackerIsPledgerFaction: true, attackerIsPlayerFaction: true,
                defenderIsPledgerFaction: false, defenderIsPlayerFaction: false).Should().BeFalse();
            HostageBreachPolicy.IsBreach(attackerIsPledgerFaction: false, attackerIsPlayerFaction: false,
                defenderIsPledgerFaction: false, defenderIsPlayerFaction: false).Should().BeFalse();
        }

        // Fault, the load-bearing direction: the hostage is forfeit ONLY when the pledger's side struck. If the
        // pledger is the attacker, they broke the faith the hostage stood surety for.
        [Test]
        public void GIVEN_the_pledger_is_the_attacker_THEN_the_pledger_is_at_fault()
        {
            HostageBreachPolicy.IsPledgerAtFault(attackerIsPledgerFaction: true).Should().BeTrue();
        }

        // If the PLAYER is the attacker, it is the player who broke faith, not the pledger, so the hostage must NOT
        // be imprisoned. This is the guard against the player jailing a guest for the player's own aggression.
        [Test]
        public void GIVEN_the_player_is_the_attacker_THEN_the_pledger_is_not_at_fault()
        {
            HostageBreachPolicy.IsPledgerAtFault(attackerIsPledgerFaction: false).Should().BeFalse();
        }
    }
}
