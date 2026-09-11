// Code written by Gabriel Mailhot, 06/09/2026.
// CombatProwessPolicy decides how remarkable it is that one fighter put another down, and therefore how far word of
// it travels. Gabriel's ruling (2026-09-06): a world event is worth it only for an EXPLOIT, e.g. besting an
// opponent much stronger than yourself. Two failures matter. If the stomp rule broke, every bandit the player rides
// over would become realm gossip and the feature would read as spam. If the exploit bar drifted, the one deed
// actually worth talking about (you beat someone above your weight, or a name the realm knows) would be recorded as
// an ordinary scuffle and never circulate, which is the whole point of the feature.

#region

using CalradiaRemembers.Logic.Battles;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Battles
{
    [TestFixture]
    public sealed class CombatProwessPolicyTests
    {
        private static CombatantFacts Fighter(int weapon, int level = 10, int clanTier = 1, int riding = 0, int athletics = 0)
            => new CombatantFacts {BestWeaponSkill = weapon, Riding = riding, Athletics = athletics, Level = level, ClanTier = clanTier};

        // Weapon skill dominates, mobility counts for less, and a level still adds something: a veteran of equal
        // weapon skill must read as the harder fight, or an experienced lord would rate the same as a green one.
        [Test]
        public void GIVEN_two_fighters_alike_but_for_level_WHEN_scored_THEN_the_veteran_rates_higher()
        {
            CombatProwessPolicy.Prowess(Fighter(200, level: 30))
                .Should().BeGreaterThan(CombatProwessPolicy.Prowess(Fighter(200, level: 10)));
        }

        // Mobility is real but secondary: it must never outweigh the weapon skill the fighter actually swings, or a
        // fast, unskilled fighter would rate above a master swordsman.
        [Test]
        public void GIVEN_mobility_against_weapon_skill_WHEN_scored_THEN_the_weapon_skill_wins()
        {
            CombatProwessPolicy.Prowess(Fighter(200, level: 10))
                .Should().BeGreaterThan(CombatProwessPolicy.Prowess(Fighter(60, level: 10, riding: 200, athletics: 200)));
        }

        // A live combatant never scores zero, so the ratio comparisons below can never divide by nothing however
        // empty the facts are (an unskilled peasant is still someone).
        [Test]
        public void GIVEN_empty_or_null_facts_WHEN_scored_THEN_it_is_never_below_one()
        {
            CombatProwessPolicy.Prowess(null).Should().BeGreaterThanOrEqualTo(1);
            CombatProwessPolicy.Prowess(Fighter(0, level: 0)).Should().BeGreaterThanOrEqualTo(1);
        }

        // THE rule Gabriel asked for: beating someone well above your weight is an exploit, and only an exploit may
        // become realm gossip.
        [Test]
        public void GIVEN_a_foe_well_above_the_winner_WHEN_rated_THEN_it_is_an_exploit_that_travels()
        {
            CombatFeatTier tier = CombatProwessPolicy.Rate(Fighter(80), Fighter(250, level: 30));

            tier.Should().Be(CombatFeatTier.Exploit);
            CombatProwessPolicy.TravelsAsGossip(tier).Should().BeTrue();
        }

        // The stomp rule, mirroring the army-scale rule the mod already applies to battle letters: riding over
        // someone far beneath you is not a deed, and must leave nothing at all, or the world would gossip about
        // every bandit the player cuts down.
        [Test]
        public void GIVEN_a_foe_far_beneath_the_winner_WHEN_rated_THEN_it_is_unremarkable_and_does_not_travel()
        {
            CombatFeatTier tier = CombatProwessPolicy.Rate(Fighter(250, level: 30), Fighter(60, level: 5));

            tier.Should().Be(CombatFeatTier.Unremarkable);
            CombatProwessPolicy.TravelsAsGossip(tier).Should().BeFalse();
        }

        // Fame is its own axis: beating a name the realm knows travels on reputation whatever the skills say. This
        // is what makes "you defeated Lucon, a feat worthy of the greatest" land, and it must survive even when the
        // player is the stronger fighter, because the realm still talks when a great name falls.
        [Test]
        public void GIVEN_a_famous_foe_WHEN_rated_THEN_it_is_an_exploit_even_if_the_winner_was_stronger()
        {
            CombatProwessPolicy.Rate(Fighter(250, level: 30), Fighter(60, level: 5, clanTier: CombatProwessPolicy.FamousClanTier))
                .Should().Be(CombatFeatTier.Exploit);
        }

        // An even match is a real deed for those who saw it, but not realm gossip: this middle tier is what keeps
        // the world's talk rare enough to mean something.
        [Test]
        public void GIVEN_an_even_match_WHEN_rated_THEN_it_is_notable_but_does_not_travel()
        {
            CombatFeatTier tier = CombatProwessPolicy.Rate(Fighter(150, level: 15), Fighter(150, level: 15));

            tier.Should().Be(CombatFeatTier.Notable);
            CombatProwessPolicy.TravelsAsGossip(tier).Should().BeFalse();
        }
    }
}
