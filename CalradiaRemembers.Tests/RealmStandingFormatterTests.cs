// Code written by Gabriel Mailhot, 11/11/2026.
// What the realms line of the world state tells every character: the fiefs each realm holds, and which realms have
// fallen. Schmidt (Discord, 11/11/2026): "everyone acts surprised when I tell them that Sturgia is no more, and their
// last fief taken." If this is wrong, a fallen realm is never named, and the model speaks of it as still standing.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class RealmStandingFormatterTests
    {
        // THE REPORT: Sturgia lost its last fief; every character must be told it has fallen, not left to imagine it stands.
        [Test]
        public void GIVEN_a_realm_that_has_fallen_WHEN_the_realms_are_told_THEN_it_is_named_as_fallen()
        {
            string line = RealmStandingFormatter.Describe(new[] {
                new RealmStanding {Name = "Vlandia", Towns = 6, Castles = 10},
                new RealmStanding {Name = "Sturgia", HasFallen = true}
            });

            line.Should().Be("Vlandia holds 6 towns and 10 castles. Fallen, holding nothing and no longer a power: Sturgia.");
        }

        // Schmidt asked that characters know the number of fiefs of each realm: a realm down to one castle is told as such.
        [Test]
        public void GIVEN_realms_standing_WHEN_told_THEN_each_counts_its_towns_and_castles()
        {
            RealmStandingFormatter.Describe(new[] {
                new RealmStanding {Name = "Battania", Towns = 1, Castles = 0},
                new RealmStanding {Name = "Sturgia", Towns = 0, Castles = 1}
            }).Should().Be("Battania holds 1 town; Sturgia holds 1 castle.");
        }

        // A realm still standing with no fief (its lords landless, not yet destroyed by the game) is not told as fallen.
        [Test]
        public void GIVEN_a_realm_with_no_fief_not_yet_destroyed_WHEN_told_THEN_it_holds_no_town_or_castle_and_is_not_fallen()
        {
            RealmStandingFormatter.Describe(new[] {new RealmStanding {Name = "Sturgia"}})
                .Should().Be("Sturgia holds no town or castle.");
        }

        // A long campaign raises and destroys many rebel realms; the line names a few and counts the rest, or it grows forever.
        [Test]
        public void GIVEN_many_fallen_realms_WHEN_told_THEN_the_first_are_named_and_the_rest_counted()
        {
            var realms = new RealmStanding[RealmStandingFormatter.MaxFallenNamed + 2];
            for (var i = 0; i < realms.Length; i++) realms[i] = new RealmStanding {Name = $"Rebels{i}", HasFallen = true};

            string line = RealmStandingFormatter.Describe(realms);

            line.Should().Contain("Rebels0").And.NotContain($"Rebels{RealmStandingFormatter.MaxFallenNamed}").And.EndWith("and 2 others.");
        }

        // No realm to tell (a bare test world) says nothing, so the prompt prints no empty line.
        [Test]
        public void GIVEN_no_realm_WHEN_told_THEN_nothing_is_said()
        {
            RealmStandingFormatter.Describe(null).Should().BeEmpty();
        }
    }
}
