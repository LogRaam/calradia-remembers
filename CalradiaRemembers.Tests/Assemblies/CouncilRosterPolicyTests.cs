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
                                                     bool onMission = false,
                                                     bool alliedLeader = false,
                                                     bool parleyCounterpart = false,
                                                     bool partyMember = false)
            => new CouncilMembershipFacts(alive, prisoner, inBattle, companion, family, clanLord, inArmy, onMission, alliedLeader, parleyCounterpart, partyMember);

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

        // The WarCouncil scope, the first FACTION-CENTRIC council: an allied leader (a fellow clan's leader, or,
        // for an independent player clan, one of the player's own clan lords, see CouncilRosterResolver's
        // no-kingdom fallback) is seated. If this failed the war council would seat nobody and the player could
        // never convene it.
        [Test]
        public void GIVEN_an_allied_leader_WHEN_a_war_council_is_gathered_THEN_he_is_seated()
        {
            CouncilEligibility e = CouncilRosterPolicy.Evaluate(CouncilScope.WarCouncil, Member(alliedLeader: true));

            e.Eligible.Should().BeTrue();
            e.Reason.Should().Be(CouncilExclusionReason.None);
        }

        // The scope is still a filter here too: an ordinary clan lord (not an allied leader) must not be seated
        // at a war council just because he belongs to one of the OTHER internal rosters, or the war council would
        // seat the player's own household rather than his allies.
        [Test]
        public void GIVEN_a_clan_lord_who_is_not_an_allied_leader_WHEN_a_war_council_is_gathered_THEN_he_is_not_of_that_council()
        {
            CouncilEligibility e = CouncilRosterPolicy.Evaluate(CouncilScope.WarCouncil, Member(clanLord: true));

            e.Eligible.Should().BeFalse();
            e.Reason.Should().Be(CouncilExclusionReason.NotOfThisCouncil);
        }

        // Availability still gates first at a war council exactly as at any other: an allied leader marching
        // with an army cannot break away to debate war policy any more than a clan lord can, reusing the same
        // "absent member acts as if present" guard the other scopes already enforce.
        [Test]
        public void GIVEN_an_allied_leader_in_an_army_WHEN_a_war_council_is_gathered_THEN_he_is_excluded_as_in_army()
        {
            CouncilEligibility e = CouncilRosterPolicy.Evaluate(CouncilScope.WarCouncil, Member(alliedLeader: true, inArmy: true));

            e.Eligible.Should().BeFalse();
            e.Reason.Should().Be(CouncilExclusionReason.InArmy);
        }

        // Same gate, the imprisoned leg: a captured ally cannot sit in on war policy either.
        [Test]
        public void GIVEN_an_allied_leader_held_prisoner_WHEN_a_war_council_is_gathered_THEN_he_is_excluded_as_imprisoned()
        {
            CouncilEligibility e = CouncilRosterPolicy.Evaluate(CouncilScope.WarCouncil, Member(alliedLeader: true, prisoner: true));

            e.Eligible.Should().BeFalse();
            e.Reason.Should().Be(CouncilExclusionReason.Imprisoned);
        }

        // The Parley scope, declare_war's symmetric counterpart's own roster: the enemy leader the parley was
        // convened against is seated. If this failed a parley could never seat its one counterpart and make_peace
        // would have nobody at the table to negotiate with.
        [Test]
        public void GIVEN_the_parley_counterpart_WHEN_a_parley_is_gathered_THEN_he_is_seated()
        {
            CouncilEligibility e = CouncilRosterPolicy.Evaluate(CouncilScope.Parley, Member(parleyCounterpart: true));

            e.Eligible.Should().BeTrue();
            e.Reason.Should().Be(CouncilExclusionReason.None);
        }

        // The scope still filters: an allied leader (or any other roster's member) is not the specific enemy
        // leader this parley was convened against, so a parley must not seat him just because he belongs to
        // another internal roster.
        [Test]
        public void GIVEN_an_allied_leader_who_is_not_the_parley_counterpart_WHEN_a_parley_is_gathered_THEN_he_is_not_of_that_council()
        {
            CouncilEligibility e = CouncilRosterPolicy.Evaluate(CouncilScope.Parley, Member(alliedLeader: true));

            e.Eligible.Should().BeFalse();
            e.Reason.Should().Be(CouncilExclusionReason.NotOfThisCouncil);
        }

        // Author's decision, the relaxed availability gate: the parley counterpart is the LEADER of an enemy
        // faction, not one of the player's own people, so leading his own army (routinely true of an enemy at
        // war, and precisely why a parley is being sought) must NOT bar his seat the way it would for a member
        // of the player's own house. Only alive + not prisoner gate him.
        [Test]
        public void GIVEN_the_parley_counterpart_leading_an_army_WHEN_a_parley_is_gathered_THEN_he_is_still_seated()
        {
            CouncilEligibility e = CouncilRosterPolicy.Evaluate(CouncilScope.Parley, Member(parleyCounterpart: true, inArmy: true));

            e.Eligible.Should().BeTrue();
            e.Reason.Should().Be(CouncilExclusionReason.None);
        }

        // Same relaxation, the in-battle leg: an enemy leader pinned in a battle right now is still, abstractly,
        // reachable for the parley (the summons is abstract, mirroring every other scope's own no-distance-test
        // design), unlike a member of the player's own house who must physically break away.
        [Test]
        public void GIVEN_the_parley_counterpart_in_battle_WHEN_a_parley_is_gathered_THEN_he_is_still_seated()
        {
            CouncilEligibility e = CouncilRosterPolicy.Evaluate(CouncilScope.Parley, Member(parleyCounterpart: true, inBattle: true));

            e.Eligible.Should().BeTrue();
            e.Reason.Should().Be(CouncilExclusionReason.None);
        }

        // Same relaxation, the mission leg: CompanionMissionBehavior.IsAway is a fact about the PLAYER's own
        // companions, meaningless for an enemy leader, so it must never bar this seat either.
        [Test]
        public void GIVEN_the_parley_counterpart_flagged_on_mission_WHEN_a_parley_is_gathered_THEN_he_is_still_seated()
        {
            CouncilEligibility e = CouncilRosterPolicy.Evaluate(CouncilScope.Parley, Member(parleyCounterpart: true, onMission: true));

            e.Eligible.Should().BeTrue();
            e.Reason.Should().Be(CouncilExclusionReason.None);
        }

        // The two gates that DO still apply: a dead counterpart obviously cannot be negotiated with.
        [Test]
        public void GIVEN_a_dead_parley_counterpart_WHEN_a_parley_is_gathered_THEN_he_is_excluded_as_dead()
        {
            CouncilEligibility e = CouncilRosterPolicy.Evaluate(CouncilScope.Parley, Member(alive: false, parleyCounterpart: true));

            e.Eligible.Should().BeFalse();
            e.Reason.Should().Be(CouncilExclusionReason.Dead);
        }

        // And a captured enemy leader cannot sit as the enemy's own negotiator either (he already answers to the
        // player as a prisoner, there is no war-faction table left for him to represent).
        [Test]
        public void GIVEN_a_captured_parley_counterpart_WHEN_a_parley_is_gathered_THEN_he_is_excluded_as_imprisoned()
        {
            CouncilEligibility e = CouncilRosterPolicy.Evaluate(CouncilScope.Parley, Member(prisoner: true, parleyCounterpart: true));

            e.Eligible.Should().BeFalse();
            e.Reason.Should().Be(CouncilExclusionReason.Imprisoned);
        }

        // Player report (2026-08-25): toddlers were seating at a war / clan-lords council because the 13+ age rule
        // was only applied to the FAMILY roster. The rule now lives here, shared by every scope's candidate pool.
        // A child below the threshold is not old enough to sit; a teen at the threshold is. If this drifted, either
        // toddlers return to the table, or a 13-year-old kin who should hold a place is silently barred.
        [TestCase(2, false)]
        [TestCase(12, false)]
        [TestCase(13, true)]
        [TestCase(40, true)]
        public void GIVEN_a_heros_age_WHEN_asked_if_old_enough_to_sit_THEN_only_the_threshold_and_up_qualify(int age, bool expected)
        {
            CouncilRosterPolicy.IsOldEnoughToSit(age).Should().Be(expected);
        }

        // The threshold is exactly the seat age, so the boundary is unambiguous: one year under is out, the age
        // itself is in. Pins the boundary against an off-by-one that would seat twelve-year-olds or bar thirteens.
        [Test]
        public void GIVEN_the_seat_age_boundary_WHEN_checked_THEN_it_is_inclusive_at_MinSeatAge()
        {
            CouncilRosterPolicy.IsOldEnoughToSit(CouncilRosterPolicy.MinSeatAge).Should().BeTrue();
            CouncilRosterPolicy.IsOldEnoughToSit(CouncilRosterPolicy.MinSeatAge - 1).Should().BeFalse();
        }

        // Player wish (2026-08-25): a council of everyone riding with the player. A party member is seated at a
        // Party council; the scope is still a filter, so someone who is NOT riding along (only a party member's
        // flag is unset) is not of that council.
        [Test]
        public void GIVEN_a_party_member_WHEN_a_party_council_is_gathered_THEN_they_are_seated()
        {
            CouncilRosterPolicy.Evaluate(CouncilScope.Party, Member(partyMember: true)).Eligible.Should().BeTrue();
            CouncilRosterPolicy.Evaluate(CouncilScope.Party, Member(companion: true)).Eligible.Should().BeFalse();
        }

        // Availability still gates first at a party council: a party member pinned in a battle right now cannot
        // break off to sit, the same "absent member acts as if present" guard every other own-house scope uses.
        [Test]
        public void GIVEN_a_party_member_in_a_battle_WHEN_a_party_council_is_gathered_THEN_they_are_excluded_as_in_battle()
        {
            CouncilEligibility e = CouncilRosterPolicy.Evaluate(CouncilScope.Party, Member(partyMember: true, inBattle: true));

            e.Eligible.Should().BeFalse();
            e.Reason.Should().Be(CouncilExclusionReason.InBattle);
        }
    }
}
