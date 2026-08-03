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
                                                     bool clanLord = false,
                                                     bool inArmy = false,
                                                     bool onMission = false)
            => new CouncilMembershipFacts(alive, prisoner, inBattle, companion, family, clanLord, inArmy, onMission);

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

        // The mod author's Partie 4 rule: a council may gather members who are not currently in the player's
        // party, BUT they must be AVAILABLE, meaning not a prisoner, not engaged in an army, and not away on a
        // mission. A clan lord marching with an army cannot break away for an abstract summons any more than one
        // pinned in a battle can; seating him anyway would let him agree to deeds he cannot perform, recreating
        // the "absent member acts as if present" bug under another form.
        [Test]
        public void GIVEN_a_clan_lord_in_an_army_WHEN_a_council_is_gathered_THEN_he_is_excluded_as_in_army()
        {
            CouncilEligibility e = CouncilRosterPolicy.Evaluate(CouncilScope.ClanLords, Member(clanLord: true, inArmy: true));

            e.Eligible.Should().BeFalse();
            e.Reason.Should().Be(CouncilExclusionReason.InArmy);
        }

        // Same Partie 4 rule, the mission leg: a companion sent off on an errand (CompanionMissionBehavior.IsAway)
        // is physically elsewhere and cannot sit at the table either, even though the council itself is convened
        // abstractly. Without this gate a dispatched companion would still be seated and could pledge to tasks
        // he is, in fact, off doing something else entirely.
        [Test]
        public void GIVEN_a_companion_away_on_a_mission_WHEN_a_companions_council_is_gathered_THEN_he_is_excluded_as_on_mission()
        {
            CouncilEligibility e = CouncilRosterPolicy.Evaluate(CouncilScope.Companions, Member(companion: true, onMission: true));

            e.Eligible.Should().BeFalse();
            e.Reason.Should().Be(CouncilExclusionReason.OnMission);
        }

        // Priority is unaffected by the new gates: the dead, the imprisoned, and the battle-pinned still win over
        // InArmy/OnMission when several conditions overlap, since those two facts are only ever true together with
        // IsAlive/IsPrisoner/IsInBattle in a stale-fact edge case, never a real one, and the roster must still
        // report the most severe truth first.
        [Test]
        public void GIVEN_a_dead_member_who_is_also_flagged_in_an_army_WHEN_a_council_is_gathered_THEN_dead_still_wins()
        {
            CouncilEligibility e = CouncilRosterPolicy.Evaluate(CouncilScope.ClanLords, Member(alive: false, clanLord: true, inArmy: true));

            e.Eligible.Should().BeFalse();
            e.Reason.Should().Be(CouncilExclusionReason.Dead);
        }

        [Test]
        public void GIVEN_a_prisoner_who_is_also_flagged_away_on_a_mission_WHEN_a_council_is_gathered_THEN_imprisoned_still_wins()
        {
            CouncilEligibility e = CouncilRosterPolicy.Evaluate(CouncilScope.Companions, Member(companion: true, prisoner: true, onMission: true));

            e.Eligible.Should().BeFalse();
            e.Reason.Should().Be(CouncilExclusionReason.Imprisoned);
        }

        [Test]
        public void GIVEN_a_member_in_battle_who_is_also_flagged_in_an_army_WHEN_a_council_is_gathered_THEN_in_battle_still_wins()
        {
            CouncilEligibility e = CouncilRosterPolicy.Evaluate(CouncilScope.Family, Member(family: true, inBattle: true, inArmy: true));

            e.Eligible.Should().BeFalse();
            e.Reason.Should().Be(CouncilExclusionReason.InBattle);
        }
    }
}
