// Code written by Gabriel Mailhot, 20/07/2026.
// Documents who is seated at a clan council: availability bars first, then membership in the chosen roster. This
// feeds the roster panel on the right of the council screen (who shows up, and why an absent kinsman is absent).
// Because convening your own house is abstract, there is deliberately NO distance test to document here.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class CouncilRosterPolicyTests
    {
        private static CouncilMembershipFacts Member(bool alive = true,
                                                     bool prisoner = false,
                                                     bool inBattle = false,
                                                     bool companion = false,
                                                     bool family = false,
                                                     bool clanLord = false)
            => new CouncilMembershipFacts(alive, prisoner, inBattle, companion, family, clanLord);

        // The straight case: a companion is seated at a companions' council. If this failed the right-hand roster
        // would be empty and the player would have no one to address.
        [Test]
        public void GIVEN_a_companion_WHEN_a_companions_council_is_gathered_THEN_he_is_seated()
        {
            CouncilEligibility e = CouncilRosterPolicy.Evaluate(CouncilScope.Companions, Member(companion: true));

            e.Eligible.Should().BeTrue();
            e.Reason.Should().Be(CouncilExclusionReason.None);
        }

        // The scope is a filter, not a suggestion: a companion is not a clan lord, so a lords' council must not
        // seat him. Getting this wrong would let non-nobles vote on fiefs and appointments.
        [Test]
        public void GIVEN_a_companion_WHEN_a_clan_lords_council_is_gathered_THEN_he_is_not_of_that_council()
        {
            CouncilEligibility e = CouncilRosterPolicy.Evaluate(CouncilScope.ClanLords, Member(companion: true));

            e.Eligible.Should().BeFalse();
            e.Reason.Should().Be(CouncilExclusionReason.NotOfThisCouncil);
        }

        // The overlap the model is built for: a sibling who leads his own party is BOTH family and a clan lord, so
        // he is eligible for either council. This is why membership is three independent flags, not one category.
        [Test]
        public void GIVEN_a_sibling_lord_WHEN_either_council_is_gathered_THEN_he_is_seated_at_both()
        {
            CouncilMembershipFacts siblingLord = Member(family: true, clanLord: true);

            CouncilRosterPolicy.Evaluate(CouncilScope.Family, siblingLord).Eligible.Should().BeTrue();
            CouncilRosterPolicy.Evaluate(CouncilScope.ClanLords, siblingLord).Eligible.Should().BeTrue();
        }

        // Availability gates BEFORE membership: an imprisoned brother is family, but he cannot sit in council from
        // a cell. The reason must say Imprisoned (not NotOfThisCouncil) so the roster explains the empty seat
        // truthfully.
        [Test]
        public void GIVEN_an_imprisoned_family_member_WHEN_a_family_council_is_gathered_THEN_he_is_excluded_as_imprisoned()
        {
            CouncilEligibility e = CouncilRosterPolicy.Evaluate(CouncilScope.Family, Member(family: true, prisoner: true));

            e.Eligible.Should().BeFalse();
            e.Reason.Should().Be(CouncilExclusionReason.Imprisoned);
        }

        // A kinsman pinned in a battle cannot break away to attend, even an abstract summons. The battle bar is
        // the one "occupied" state that overrides the convene-your-own-house shortcut.
        [Test]
        public void GIVEN_a_clan_lord_in_a_battle_WHEN_a_council_is_gathered_THEN_he_is_excluded_as_in_battle()
        {
            CouncilEligibility e = CouncilRosterPolicy.Evaluate(CouncilScope.ClanLords, Member(clanLord: true, inBattle: true));

            e.Eligible.Should().BeFalse();
            e.Reason.Should().Be(CouncilExclusionReason.InBattle);
        }

        // The dead do not attend. A member who died is excluded with the Dead reason, guarding against a stale
        // roster that still lists a departed kinsman as present.
        [Test]
        public void GIVEN_a_dead_member_WHEN_any_council_is_gathered_THEN_he_is_excluded_as_dead()
        {
            CouncilEligibility e = CouncilRosterPolicy.Evaluate(CouncilScope.Family, Member(alive: false, family: true));

            e.Eligible.Should().BeFalse();
            e.Reason.Should().Be(CouncilExclusionReason.Dead);
        }
    }
}
